using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using RawBufferVisualizer.VisualStudio;
using Task = System.Threading.Tasks.Task;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration("Raw Buffer Visualizer", "Docked raw buffer image inspector", "1.0")]
    [ProvideBindingPath]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideToolWindow(
        typeof(RawBufferToolWindow),
        Style = VsDockStyle.Tabbed,
        Window = "3ae79031-e1bc-11d0-8f78-00a0c9110057",
        Orientation = ToolWindowOrientation.Right,
        Width = 1000,
        Height = 700)]
    [Guid(PackageGuidString)]
    public sealed class RawBufferVisualizerPackage : AsyncPackage
    {
        public const string PackageGuidString = "c15cc508-0fef-49bb-9478-4d2fdf9f87d2";
        public const string CommandSetGuidString = "8e7bc2db-12a4-4f45-8f5a-38c1846a0f26";
        public const int ShowToolWindowCommandId = 0x0100;

        private static readonly Guid CommandSetGuid = new Guid(CommandSetGuidString);
        private static readonly TimeSpan InboxPollMinInterval = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan InboxPollMaxInterval = TimeSpan.FromSeconds(10);

        private readonly object _requestGate = new object();
        private readonly HashSet<string> _queuedRequests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private FileSystemWatcher? _watcher;
        private Timer? _inboxPollTimer;
        private TimeSpan _inboxPollInterval = InboxPollMinInterval;
        private int _inboxPollActive;

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

            ScheduleNextInboxPoll(ScanInbox());
            WriteAutomationLog("InitializeAsync end");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inboxPollTimer?.Dispose();
                _inboxPollTimer = null;
                _watcher?.Dispose();
                _watcher = null;
            }

            base.Dispose(disposing);
        }

        private void StartInboxWatcher()
        {
            Directory.CreateDirectory(VisualizerHandoffInbox.InboxDirectory);
            WriteAutomationLog("StartInboxWatcher " + VisualizerHandoffInbox.InboxDirectory);
            _watcher = new FileSystemWatcher(VisualizerHandoffInbox.InboxDirectory, "*.rbuf-handoff")
            {
                EnableRaisingEvents = true,
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };
            _watcher.Created += OnHandoffCreated;
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
            WriteAutomationLog("Command registered");
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
                catch (Exception ex)
                {
                    WriteAutomationLog("Command error " + ex);
                }
            });
        }

        private void OnHandoffCreated(object sender, FileSystemEventArgs e)
        {
            WriteAutomationLog("Created " + e.FullPath);
            _inboxPollInterval = InboxPollMinInterval;
            QueueOpenHandoff(e.FullPath);
            ScheduleInboxPoll(InboxPollMinInterval);
        }

        private void QueueOpenHandoff(string requestPath)
        {
            lock (_requestGate)
            {
                if (!_queuedRequests.Add(Path.GetFullPath(requestPath)))
                {
                    WriteAutomationLog("Already queued " + requestPath);
                    return;
                }
            }

            WriteAutomationLog("Queue " + requestPath);
            _ = JoinableTaskFactory.RunAsync(async delegate
            {
                try
                {
                    WriteAutomationLog("Open start " + requestPath);
                    await OpenHandoffAsync(requestPath, DisposalToken);
                    WriteAutomationLog("Open end " + requestPath);
                }
                catch (Exception ex)
                {
                    WriteAutomationLog("Open error " + ex);
                }
                finally
                {
                    lock (_requestGate)
                    {
                        _queuedRequests.Remove(Path.GetFullPath(requestPath));
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
                var requestPaths = Directory.GetFiles(VisualizerHandoffInbox.InboxDirectory, "*.rbuf-handoff")
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
            var metricsPath = Environment.GetEnvironmentVariable("RAWBUFFERVISUALIZER_DOCKED_PERF_JSON");
            try
            {
                var logPath = string.IsNullOrWhiteSpace(metricsPath)
                    ? Path.Combine(VisualStudioTempStore.RootDirectory, "package.log")
                    : Path.ChangeExtension(metricsPath, ".package.log");
                var logDirectory = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrWhiteSpace(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                if (File.Exists(logPath) && new FileInfo(logPath).Length > 1024 * 1024)
                {
                    File.Delete(logPath);
                }

                File.AppendAllText(
                    logPath,
                    DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) + " " + message + Environment.NewLine);
            }
            catch
            {
                // Diagnostics must not affect Visual Studio package load.
            }
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

        private async Task OpenHandoffAsync(string requestPath, CancellationToken cancellationToken)
        {
            var window = await ShowRawBufferToolWindowAsync(cancellationToken);
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            window.OpenHandoffRequest(requestPath);
        }
    }
}
