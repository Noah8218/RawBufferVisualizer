using Microsoft.VisualStudio.Extensibility.UI;

namespace RawBufferVisualizer.VisualStudio.Extensibility
{
    internal sealed class VisualizerLaunchStatusControl : RemoteUserControl
    {
        public VisualizerLaunchStatusControl(VisualizerLaunchStatus status)
            : base(status)
        {
        }
    }
}
