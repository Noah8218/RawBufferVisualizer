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
        }

        public void OpenHandoffRequest(string requestPath)
        {
            _control.OpenHandoffRequest(requestPath);
        }
    }
}
