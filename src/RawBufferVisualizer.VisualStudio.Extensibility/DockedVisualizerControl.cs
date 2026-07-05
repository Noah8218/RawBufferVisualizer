using Microsoft.VisualStudio.Extensibility.UI;

namespace RawBufferVisualizer.VisualStudio.Extensibility
{
    internal sealed class DockedVisualizerControl : RemoteUserControl
    {
        public DockedVisualizerControl()
            : base(DockedVisualizerSession.Shared)
        {
        }
    }
}
