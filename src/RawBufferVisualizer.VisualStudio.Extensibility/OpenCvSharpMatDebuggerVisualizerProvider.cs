using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.DebuggerVisualizers;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;
using RawBufferVisualizer.VisualStudio.ObjectSource;

namespace RawBufferVisualizer.VisualStudio.Extensibility
{
    [VisualStudioContribution]
    internal sealed class OpenCvSharpMatDebuggerVisualizerProvider : DebuggerVisualizerProvider
    {
        private const string DisplayName = "%RawBufferVisualizer.DebuggerVisualizer.DisplayName%";

        public OpenCvSharpMatDebuggerVisualizerProvider(
            RawBufferVisualizerExtension extension,
            VisualStudioExtensibility extensibility)
            : base(extension, extensibility)
        {
        }

        public override DebuggerVisualizerProviderConfiguration DebuggerVisualizerProviderConfiguration =>
            new(new[]
            {
                new VisualizerTargetType(DisplayName, "OpenCvSharp.Mat, OpenCvSharp, Version=1.0.0.0, Culture=neutral, PublicKeyToken=6adad1e807fea099"),
                new VisualizerTargetType(DisplayName, "OpenCvSharp.Mat, OpenCvSharp, Version=4.0.0.0, Culture=neutral, PublicKeyToken=6adad1e807fea099"),
                new VisualizerTargetType(DisplayName, "OpenCvSharp.Mat, OpenCvSharp")
            })
            {
                Style = VisualizerStyle.ToolWindow,
                VisualizerObjectSourceType = new(typeof(OpenCvSharpMatVisualizerObjectSource))
            };

        public override Task<IRemoteUserControl> CreateVisualizerAsync(
            VisualizerTarget visualizerTarget,
            CancellationToken cancellationToken)
        {
            return DebuggerVisualizerLaunch.CreateControlAsync(visualizerTarget, cancellationToken);
        }
    }
}
