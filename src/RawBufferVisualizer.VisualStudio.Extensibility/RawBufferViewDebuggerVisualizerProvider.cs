using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.DebuggerVisualizers;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;
using RawBufferVisualizer.Sdk;
using RawBufferVisualizer.VisualStudio.ObjectSource;

namespace RawBufferVisualizer.VisualStudio.Extensibility
{
    internal sealed class RawBufferViewDebuggerVisualizerProvider : DebuggerVisualizerProvider
    {
        public RawBufferViewDebuggerVisualizerProvider(
            RawBufferVisualizerExtension extension,
            VisualStudioExtensibility extensibility)
            : base(extension, extensibility)
        {
        }

        public override DebuggerVisualizerProviderConfiguration DebuggerVisualizerProviderConfiguration =>
            new("%RawBufferVisualizer.DebuggerVisualizer.DisplayName%", typeof(RawBufferView))
            {
                Style = VisualizerStyle.ToolWindow,
                VisualizerObjectSourceType = new(typeof(RawBufferViewVisualizerObjectSource))
            };

        public override Task<IRemoteUserControl> CreateVisualizerAsync(
            VisualizerTarget visualizerTarget,
            CancellationToken cancellationToken)
        {
            return DebuggerVisualizerLaunch.CreateControlAsync(visualizerTarget, cancellationToken);
        }
    }
}
