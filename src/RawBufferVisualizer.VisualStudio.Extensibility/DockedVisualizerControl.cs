using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility.DebuggerVisualizers;
using Microsoft.VisualStudio.Extensibility.UI;

namespace RawBufferVisualizer.VisualStudio.Extensibility
{
    internal sealed class DockedVisualizerControl : RemoteUserControl
    {
        private VisualizerTarget? _visualizerTargetToClose;

        public DockedVisualizerControl(DockedVisualizerSession session, VisualizerTarget? visualizerTargetToClose)
            : base(session)
        {
            _visualizerTargetToClose = visualizerTargetToClose;
        }

        public override Task ControlLoadedAsync(CancellationToken cancellationToken)
        {
            var visualizerTarget = _visualizerTargetToClose;
            _visualizerTargetToClose = null;
            visualizerTarget?.Dispose();
            return Task.CompletedTask;
        }
    }
}
