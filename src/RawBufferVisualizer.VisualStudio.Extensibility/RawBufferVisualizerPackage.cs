using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using RawBufferVisualizer.VisualStudio;
using Task = System.Threading.Tasks.Task;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("Raw Buffer Visualizer", "Docked raw buffer image inspector", "1.0.53")]
    [ProvideBindingPath]
    [ProvideMenuResource("Menus.ctmenu", 2)]
    [ProvideToolWindow(
        typeof(RawBufferToolWindow),
        Style = VsDockStyle.Tabbed,
        Window = "3ae79031-e1bc-11d0-8f78-00a0c9110057",
        Orientation = ToolWindowOrientation.Right,
        Width = 1000,
        Height = 700)]
    [Guid(PackageGuidString)]
    public sealed class RawBufferVisualizerPackage : AsyncPackage, IDebugEventCallback2
    {
        // 1.0.47 and 1.0.48 used c15cc508-0fef-49bb-9478-4d2fdf9f87d2.
        // Keep this recovery GUID distinct so Visual Studio profiles that cached
        // the failed package identity can load the corrected package after update.
        public const string PackageGuidString = "1977574b-f107-465f-bfd1-5fc022907039";
        public const string CommandSetGuidString = "8e7bc2db-12a4-4f45-8f5a-38c1846a0f26";
        public const int ShowToolWindowCommandId = 0x0100;
        public const int ScanLocalsCommandId = 0x0101;

        private static readonly Guid CommandSetGuid = new Guid(CommandSetGuidString);
        private static readonly Guid RawBufferToolWindowGuid = new Guid(RawBufferToolWindow.WindowGuidString);
        private static readonly TimeSpan InboxPollMinInterval = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan InboxPollMaxInterval = TimeSpan.FromSeconds(10);

        private readonly object _requestGate = new object();
        private readonly HashSet<string> _queuedRequests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly string _inboxDirectory = VisualizerHandoffInbox.GetInboxDirectory(Process.GetCurrentProcess().Id);
        private FileSystemWatcher? _watcher;
        private Timer? _inboxPollTimer;
        private TimeSpan _inboxPollInterval = InboxPollMinInterval;
        private int _inboxPollActive;
        private EnvDTE.DebuggerEvents? _debuggerEvents;
        private IVsDebugger? _vsDebugger;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            WriteAutomationLog("InitializeAsync start");
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            try
            {
                StartInboxWatcher();
            }
            catch (Exception ex)
            {
                WriteAutomationLog("StartInboxWatcher error " + ex);
            }

            try
            {
                await RegisterCommandsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                WriteAutomationLog("RegisterCommands error " + ex);
            }

            try
            {
                var dte = await GetServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;
                if (dte != null)
                {
                    _debuggerEvents = dte.Events.DebuggerEvents;
                    _debuggerEvents.OnEnterBreakMode += OnEnterBreakMode;
                    _debuggerEvents.OnEnterRunMode += OnEnterRunMode;
                    WriteAutomationLog("DebuggerEvents subscribed");
                }
            }
            catch (Exception ex)
            {
                WriteAutomationLog("DebuggerEvents subscribe error " + ex);
            }

            try
            {
                _vsDebugger = await GetServiceAsync(typeof(SVsShellDebugger)) as IVsDebugger;
                if (_vsDebugger != null)
                {
                    ErrorHandler.ThrowOnFailure(_vsDebugger.AdviseDebugEventCallback(this));
                    WriteAutomationLog("Native debugger callback subscribed");
                }
            }
            catch (Exception ex)
            {
                WriteAutomationLog("Native debugger callback subscribe error " + ex);
            }

            ScheduleNextInboxPoll(ScanInbox());
            WriteAutomationLog("InitializeAsync end");
        }

        protected override void Dispose(bool disposing)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (disposing)
            {
                if (_debuggerEvents != null)
                {
                    try
                    {
                        _debuggerEvents.OnEnterBreakMode -= OnEnterBreakMode;
                        _debuggerEvents.OnEnterRunMode -= OnEnterRunMode;
                    }
                    catch (Exception ex)
                    {
                        WriteAutomationLog("DebuggerEvents unsubscribe error " + ex);
                    }

                    _debuggerEvents = null;
                }

                if (_vsDebugger != null)
                {
                    try
                    {
                        _vsDebugger.UnadviseDebugEventCallback(this);
                    }
                    catch (Exception ex)
                    {
                        WriteAutomationLog("Native debugger callback unsubscribe error " + ex);
                    }

                    _vsDebugger = null;
                }

                VisualStudioDebugFrameContext.SetCurrentThread(null);
                _inboxPollTimer?.Dispose();
                _inboxPollTimer = null;
                _watcher?.Dispose();
                _watcher = null;
            }

            base.Dispose(disposing);
        }

        private void StartInboxWatcher()
        {
            Directory.CreateDirectory(_inboxDirectory);
            WriteAutomationLog("StartInboxWatcher " + _inboxDirectory);
            _watcher = new FileSystemWatcher(_inboxDirectory, "*.rbuf-handoff")
            {
                EnableRaisingEvents = false,
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };
            _watcher.Created += OnHandoffCreated;
            _watcher.Renamed += OnHandoffRenamed;
            _watcher.EnableRaisingEvents = true;
            _inboxPollTimer = new Timer(_ => PollInbox(), null, InboxPollMinInterval, Timeout.InfiniteTimeSpan);
        }

        private async Task RegisterCommandsAsync(CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            var commandService = await GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService == null)
            {
                WriteAutomationLog("Command service unavailable");
                return;
            }

            var commandId = new CommandID(CommandSetGuid, ShowToolWindowCommandId);
            commandService.AddCommand(new OleMenuCommand(ExecuteShowToolWindowCommand, commandId));

            var scanCommandId = new CommandID(CommandSetGuid, ScanLocalsCommandId);
            commandService.AddCommand(new OleMenuCommand(ExecuteScanLocalsCommand, scanCommandId));
            WriteAutomationLog("Command registered");
        }

        private void ExecuteScanLocalsCommand(object sender, EventArgs e)
        {
            _ = JoinableTaskFactory.RunAsync(async delegate
            {
                try
                {
                    WriteAutomationLog("ScanLocals command invoked");
                    var window = await ShowRawBufferToolWindowAsync(DisposalToken);
                    await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);
                    window.ScanLocals();
                }
                catch (OperationCanceledException) when (DisposalToken.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    await ReportCommandFailureAsync("scan the current debugger frame", ex);
                }
            });
        }

        private void OnEnterBreakMode(EnvDTE.dbgEventReason reason, ref EnvDTE.dbgExecutionAction executionAction)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                WriteAutomationLog("Break mode entered");
                var window = FindToolWindow(typeof(RawBufferToolWindow), 0, false) as RawBufferToolWindow;
                if (window == null)
                {
                    WriteAutomationLog("Break mode scan skipped because the Tool Window has not been opened.");
                    return;
                }

                if (!window.IsAutoInspectEnabled)
                {
                    WriteAutomationLog("Break mode scan skipped because Auto Inspect is paused.");
                    return;
                }

                window.ScheduleAutomaticScan();
                WriteAutomationLog("Break mode scan scheduled");
            }
            catch (Exception ex)
            {
                WriteAutomationLog("Break mode scan error " + ex);
            }
        }

        private void OnEnterRunMode(EnvDTE.dbgEventReason reason)
        {
            VisualStudioDebugFrameContext.SetCurrentThread(null);
        }

        public int Event(
            IDebugEngine2 engine,
            IDebugProcess2 process,
            IDebugProgram2 program,
            IDebugThread2 thread,
            IDebugEvent2 debugEvent,
            ref Guid eventInterfaceGuid,
            uint attributes)
        {
            if (thread != null)
            {
                VisualStudioDebugFrameContext.SetCurrentThread(thread);
            }

            return VSConstants.S_OK;
        }

        private void ExecuteShowToolWindowCommand(object sender, EventArgs e)
        {
            _ = JoinableTaskFactory.RunAsync(async delegate
            {
                try
                {
                    WriteAutomationLog("Command invoked");
                    await ShowRawBufferToolWindowAsync(DisposalToken);
                    ScheduleNextInboxPoll(ScanInbox());
                }
                catch (OperationCanceledException) when (DisposalToken.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    await ReportCommandFailureAsync("open the docked window", ex);
                }
            });
        }

        private async Task ReportCommandFailureAsync(string action, Exception exception)
        {
            WriteAutomationLog("Command error while attempting to " + action + ": " + exception);
            await JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);
            VsShellUtilities.ShowMessageBox(
                this,
                "Raw Buffer Visualizer could not "
                + action
                + ".\n\n"
                + exception.Message
                + "\n\nDiagnostic log:\n"
                + Path.Combine(VisualStudioTempStore.RootDirectory, "package.log"),
                "Raw Buffer Visualizer",
                OLEMSGICON.OLEMSGICON_CRITICAL,
                OLEMSGBUTTON.OLEMSGBUTTON_OK,
                OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        private void OnHandoffCreated(object sender, FileSystemEventArgs e)
        {
            WriteAutomationLog("Created " + e.FullPath);
            _inboxPollInterval = InboxPollMinInterval;
            QueueOpenHandoff(e.FullPath);
            ScheduleInboxPoll(InboxPollMinInterval);
        }

        private void OnHandoffRenamed(object sender, RenamedEventArgs e)
        {
            if (!e.FullPath.EndsWith(
                    ".rbuf-handoff",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            WriteAutomationLog("Published " + e.FullPath);
            _inboxPollInterval = InboxPollMinInterval;
            QueueOpenHandoff(e.FullPath);
            ScheduleInboxPoll(InboxPollMinInterval);
        }

        private void QueueOpenHandoff(string requestPath)
        {
            string processingPath;
            try
            {
                if (!VisualizerHandoffInbox.TryClaimRequest(requestPath, out processingPath))
                {
                    WriteAutomationLog("Claim skipped " + requestPath);
                    return;
                }
            }
            catch (Exception ex)
            {
                WriteAutomationLog("Claim error " + requestPath + " " + ex);
                return;
            }

            var fullRequestPath = Path.GetFullPath(requestPath);
            lock (_requestGate)
            {
                if (!_queuedRequests.Add(fullRequestPath))
                {
                    WriteAutomationLog("Already queued " + requestPath);
                    TryRejectHandoffOrLog(
                        fullRequestPath,
                        processingPath,
                        "The handoff request was already queued.");
                    return;
                }
            }

            WriteAutomationLog("Queue " + requestPath + " as " + processingPath);
            _ = JoinableTaskFactory.RunAsync(async delegate
            {
                try
                {
                    WriteAutomationLog("Open start " + requestPath);
                    await OpenHandoffAsync(
                        fullRequestPath,
                        processingPath,
                        DisposalToken);
                    WriteAutomationLog("Open end " + requestPath);
                }
                catch (Exception ex)
                {
                    WriteAutomationLog("Open error " + ex);
                    TryRejectHandoffOrLog(
                        fullRequestPath,
                        processingPath,
                        ex.ToString());
                }
                finally
                {
                    lock (_requestGate)
                    {
                        _queuedRequests.Remove(fullRequestPath);
                    }
                }
            });
        }

        private void PollInbox()
        {
            if (Interlocked.Exchange(ref _inboxPollActive, 1) == 1)
            {
                return;
            }

            var foundCount = 0;
            try
            {
                foundCount = ScanInbox();
            }
            finally
            {
                Interlocked.Exchange(ref _inboxPollActive, 0);
                ScheduleNextInboxPoll(foundCount);
            }
        }

        private int ScanInbox()
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddMinutes(-10);
                var requestPaths = Directory.GetFiles(_inboxDirectory, "*.rbuf-handoff")
                             .Where(path => File.GetLastWriteTimeUtc(path) >= cutoff)
                             .OrderBy(File.GetLastWriteTimeUtc)
                             .ToList();
                if (requestPaths.Count > 0)
                {
                    WriteAutomationLog("Scan found " + requestPaths.Count.ToString(CultureInfo.InvariantCulture));
                }

                foreach (var requestPath in requestPaths)
                {
                    QueueOpenHandoff(requestPath);
                }

                return requestPaths.Count;
            }
            catch (Exception ex)
            {
                WriteAutomationLog("Scan error " + ex);
                return 0;
            }
        }

        private void ScheduleNextInboxPoll(int foundCount)
        {
            if (foundCount > 0)
            {
                _inboxPollInterval = InboxPollMinInterval;
            }
            else
            {
                var nextMilliseconds = Math.Min(
                    InboxPollMaxInterval.TotalMilliseconds,
                    Math.Max(InboxPollMinInterval.TotalMilliseconds, _inboxPollInterval.TotalMilliseconds * 2));
                _inboxPollInterval = TimeSpan.FromMilliseconds(nextMilliseconds);
            }

            ScheduleInboxPoll(_inboxPollInterval);
        }

        private void ScheduleInboxPoll(TimeSpan dueTime)
        {
            try
            {
                _inboxPollTimer?.Change(dueTime, Timeout.InfiniteTimeSpan);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private static void WriteAutomationLog(string message)
        {
            RawBufferVisualizerPackageLog.Write(message);
        }

        private async Task<RawBufferToolWindow> ShowRawBufferToolWindowAsync(CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            var window = await ShowToolWindowAsync(typeof(RawBufferToolWindow), 0, true, cancellationToken);
            if (window == null || window.Frame == null)
            {
                throw new InvalidOperationException("Raw Buffer Visualizer tool window could not be created.");
            }

            var frame = (IVsWindowFrame)window.Frame;
            var dockResult = frame.SetProperty((int)__VSFPROPID.VSFPROPID_FrameMode, (int)VSFRAMEMODE.VSFM_Dock);
            if (ErrorHandler.Failed(dockResult))
            {
                WriteAutomationLog("Dock request failed " + dockResult.ToString(CultureInfo.InvariantCulture));
            }

            ErrorHandler.ThrowOnFailure(frame.Show());
            return (RawBufferToolWindow)window;
        }

        private async Task OpenHandoffAsync(
            string requestPath,
            string processingPath,
            CancellationToken cancellationToken)
        {
            var window = await ShowRawBufferToolWindowAsync(cancellationToken);
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            if (window.OpenClaimedHandoffRequest(requestPath, processingPath))
            {
                ScheduleDebuggerVisualizerHostCleanup();
            }
            else
            {
                WriteAutomationLog("Open rejected " + requestPath);
                var state = VisualizerHandoffInbox.GetRequestState(requestPath);
                if (state != VisualizerHandoffRequestState.Rejected
                    && state != VisualizerHandoffRequestState.Acknowledged
                    && state != VisualizerHandoffRequestState.Conflicted)
                {
                    TryRejectHandoffOrLog(
                        requestPath,
                        processingPath,
                        "The docked window could not open the handoff.");
                }
            }
        }

        private static void TryRejectHandoffOrLog(
            string requestPath,
            string processingPath,
            string reason)
        {
            if (VisualizerHandoffInbox.TryRejectRequest(
                    requestPath,
                    processingPath,
                    reason))
            {
                return;
            }

            VisualizerHandoffRequestState state;
            try
            {
                state = VisualizerHandoffInbox.GetRequestState(requestPath);
            }
            catch (Exception ex)
            {
                WriteAutomationLog(
                    "Rejection marker state read failed "
                    + requestPath
                    + " "
                    + ex);
                return;
            }

            if (state != VisualizerHandoffRequestState.Rejected
                && state != VisualizerHandoffRequestState.Acknowledged)
            {
                WriteAutomationLog(
                    "Rejection marker publish failed "
                    + requestPath
                    + " state "
                    + state);
            }
        }

        private void ScheduleDebuggerVisualizerHostCleanup()
        {
            _ = JoinableTaskFactory.RunAsync(async delegate
            {
                try
                {
                    for (var attempt = 0; attempt < 8; attempt++)
                    {
                        await Task.Delay(attempt == 0 ? 100 : 250, DisposalToken);
                        await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);
                        if (await CloseDuplicateDebuggerVisualizerHostsAsync())
                        {
                            return;
                        }
                    }
                }
                catch (OperationCanceledException) when (DisposalToken.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    WriteAutomationLog("Visualizer host cleanup error " + ex);
                }
            });
        }

        private async Task<bool> CloseDuplicateDebuggerVisualizerHostsAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(DisposalToken);
            var uiShell = await GetServiceAsync(typeof(SVsUIShell)) as IVsUIShell;
            if (uiShell == null || ErrorHandler.Failed(uiShell.GetToolWindowEnum(out var frameEnumerator)))
            {
                return false;
            }

            var frames = new IVsWindowFrame[1];
            var closed = false;
            while (frameEnumerator.Next(1, frames, out var fetched) == VSConstants.S_OK && fetched == 1)
            {
                var frame = frames[0];
                if (frame == null
                    || ErrorHandler.Failed(frame.GetProperty((int)__VSFPROPID.VSFPROPID_Caption, out var captionValue))
                    || !string.Equals(captionValue as string, "Raw Buffer Visualizer", StringComparison.Ordinal))
                {
                    continue;
                }

                var persistenceGuid = Guid.Empty;
                frame.GetGuidProperty((int)__VSFPROPID.VSFPROPID_GuidPersistenceSlot, out persistenceGuid);
                if (persistenceGuid == RawBufferToolWindowGuid)
                {
                    continue;
                }

                var closeResult = frame.CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_NoSave);
                WriteAutomationLog(
                    "Closed duplicate debugger visualizer host "
                    + persistenceGuid.ToString("D", CultureInfo.InvariantCulture)
                    + " result "
                    + closeResult.ToString(CultureInfo.InvariantCulture));
                closed |= ErrorHandler.Succeeded(closeResult);
            }

            return closed;
        }
    }
}
