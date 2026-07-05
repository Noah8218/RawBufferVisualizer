using System;
using System.IO;
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
    [InstalledProductRegistration("Raw Buffer Visualizer", "Docked OpenGL raw buffer viewer", "1.0")]
    [ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideBindingPath]
    [ProvideToolWindow(typeof(RawBufferToolWindow))]
    [Guid(PackageGuidString)]
    public sealed class RawBufferVisualizerPackage : AsyncPackage
    {
        public const string PackageGuidString = "c15cc508-0fef-49bb-9478-4d2fdf9f87d2";

        private FileSystemWatcher? _watcher;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            StartInboxWatcher();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _watcher?.Dispose();
                _watcher = null;
            }

            base.Dispose(disposing);
        }

        private void StartInboxWatcher()
        {
            Directory.CreateDirectory(VisualizerHandoffInbox.InboxDirectory);
            _watcher = new FileSystemWatcher(VisualizerHandoffInbox.InboxDirectory, "*.rbuf-handoff")
            {
                EnableRaisingEvents = true,
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
            };
            _watcher.Created += OnHandoffCreated;
        }

        private void OnHandoffCreated(object sender, FileSystemEventArgs e)
        {
            _ = JoinableTaskFactory.RunAsync(async delegate
            {
                await OpenHandoffAsync(e.FullPath, DisposalToken);
            });
        }

        private async Task OpenHandoffAsync(string requestPath, CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            var window = await ShowToolWindowAsync(typeof(RawBufferToolWindow), 0, true, cancellationToken);
            if (window == null || window.Frame == null)
            {
                throw new InvalidOperationException("Raw Buffer Visualizer tool window could not be created.");
            }

            ErrorHandler.ThrowOnFailure(((IVsWindowFrame)window.Frame).Show());
            ((RawBufferToolWindow)window).OpenHandoffRequest(requestPath);
        }
    }
}
