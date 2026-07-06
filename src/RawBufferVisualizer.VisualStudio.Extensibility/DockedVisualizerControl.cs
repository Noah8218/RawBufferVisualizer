using Microsoft.VisualStudio.Extensibility.UI;

namespace RawBufferVisualizer.VisualStudio.Extensibility
{
    internal sealed class DockedVisualizerControl : RemoteUserControl
    {
        public DockedVisualizerControl(DockedVisualizerSession session)
            : base(session)
        {
        }
    }
}
