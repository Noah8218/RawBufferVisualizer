using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.DebuggerVisualizers;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;
using RawBufferVisualizer.VisualStudio.ObjectSource;

namespace RawBufferVisualizer.VisualStudio.Extensibility
{
    [VisualStudioContribution]
    internal sealed class EmguCvMatDebuggerVisualizerProvider : DebuggerVisualizerProvider
    {
        private const string DisplayName = "%RawBufferVisualizer.DebuggerVisualizer.DisplayName%";

        public EmguCvMatDebuggerVisualizerProvider(
            RawBufferVisualizerExtension extension,
            VisualStudioExtensibility extensibility)
            : base(extension, extensibility)
        {
        }

        public override DebuggerVisualizerProviderConfiguration DebuggerVisualizerProviderConfiguration =>
            new(new[]
            {
                new VisualizerTargetType(DisplayName, "Emgu.CV.Mat, Emgu.CV"),
                new VisualizerTargetType(DisplayName, "Emgu.CV.Mat, Emgu.CV.World"),
                new VisualizerTargetType(DisplayName, "Emgu.CV.Mat, Emgu.CV.Platform.NetStandard")
            })
            {
                Style = VisualizerStyle.ToolWindow,
                VisualizerObjectSourceType = new(typeof(EmguCvMatVisualizerObjectSource))
            };

        public override Task<IRemoteUserControl> CreateVisualizerAsync(
            VisualizerTarget visualizerTarget,
            CancellationToken cancellationToken)
        {
            return DebuggerVisualizerLaunch.CreateControlAsync(visualizerTarget, cancellationToken);
        }
    }
}
