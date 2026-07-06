using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.DebuggerVisualizers;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;
using RawBufferVisualizer.VisualStudio.ObjectSource;

namespace RawBufferVisualizer.VisualStudio.Extensibility
{
    [VisualStudioContribution]
    internal sealed class ImagePtrDebuggerVisualizerProvider : DebuggerVisualizerProvider
    {
        private const string DisplayName = "%RawBufferVisualizer.DebuggerVisualizer.DisplayName%";

        public ImagePtrDebuggerVisualizerProvider(
            RawBufferVisualizerExtension extension,
            VisualStudioExtensibility extensibility)
            : base(extension, extensibility)
        {
        }

        public override DebuggerVisualizerProviderConfiguration DebuggerVisualizerProviderConfiguration =>
            new(new[]
            {
                new VisualizerTargetType(DisplayName, "ImagePtr"),
                new VisualizerTargetType(DisplayName, "ImageModel.ImagePtr"),
                new VisualizerTargetType(DisplayName, "ImageModels.ImagePtr")
            })
            {
                Style = VisualizerStyle.ToolWindow,
                VisualizerObjectSourceType = new(typeof(ImagePtrVisualizerObjectSource))
            };

        public override Task<IRemoteUserControl> CreateVisualizerAsync(
            VisualizerTarget visualizerTarget,
            CancellationToken cancellationToken)
        {
            return DebuggerVisualizerLaunch.CreateControlAsync(visualizerTarget, cancellationToken);
        }
    }
}
