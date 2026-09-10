using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    [Guid(WindowGuidString)]
    public sealed class RawBufferToolWindow : ToolWindowPane
    {
        public const string WindowGuidString = "a329e331-089a-4186-8fd7-57a241fd1917";

        private readonly RawBufferToolWindowControl _control;

        public RawBufferToolWindow()
            : base(null)
        {
            Caption = "Raw Buffer Visualizer";
            _control = new RawBufferToolWindowControl();
            Content = _control;

            var dte = Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(EnvDTE.DTE)) as EnvDTE80.DTE2;
            if (dte != null)
            {
                _control.SetDte(dte);
            }
        }

        public void OpenHandoffRequest(string requestPath)
        {
            _control.OpenHandoffRequest(requestPath);
        }

        public bool OpenClaimedHandoffRequest(string requestPath, string processingPath)
        {
            return _control.OpenClaimedHandoffRequest(requestPath, processingPath);
        }

        public void ScanLocals()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _control.ScanLocals();
        }

        public void ScheduleAutomaticScan()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _control.ScheduleAutomaticScan();
        }

        public bool IsAutoInspectEnabled
        {
            get { return _control.IsAutoInspectEnabled; }
        }

        public int InvalidateLiveSources()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return _control.InvalidateLiveSources();
        }

        public void EndAutomaticInspectionSession()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _control.EndAutomaticInspectionSession();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
#pragma warning disable VSTHRD010
                _control.Dispose();
#pragma warning restore VSTHRD010
            }

            base.Dispose(disposing);
        }
    }
}
