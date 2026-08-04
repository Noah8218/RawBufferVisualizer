using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.DebuggerVisualizers;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;
using RawBufferVisualizer.VisualStudio.ObjectSource;

namespace RawBufferVisualizer.VisualStudio.Extensibility
{
    [VisualStudioContribution]
    internal sealed class BaslerPylonGrabResultDebuggerVisualizerProvider : DebuggerVisualizerProvider
    {
        private const string DisplayName = "%RawBufferVisualizer.DebuggerVisualizer.DisplayName%";

        public BaslerPylonGrabResultDebuggerVisualizerProvider(
            RawBufferVisualizerExtension extension,
            VisualStudioExtensibility extensibility)
            : base(extension, extensibility)
        {
        }

        public override DebuggerVisualizerProviderConfiguration DebuggerVisualizerProviderConfiguration =>
            new(new[]
            {
                new VisualizerTargetType(
                    DisplayName,
                    "Basler.Pylon.IGrabResult, Basler.Pylon, Version=1.2.0.0, Culture=neutral, PublicKeyToken=e389355f398382ab"),
                new VisualizerTargetType(
                    DisplayName,
                    "Basler.Pylon.GrabResult, Basler.Pylon, Version=1.2.0.0, Culture=neutral, PublicKeyToken=e389355f398382ab")
            })
            {
                Style = VisualizerStyle.ToolWindow,
                VisualizerObjectSourceType = new(typeof(BaslerPylonGrabResultVisualizerObjectSource))
            };

        public override Task<IRemoteUserControl> CreateVisualizerAsync(
            VisualizerTarget visualizerTarget,
            CancellationToken cancellationToken)
        {
            return DebuggerVisualizerLaunch.CreateControlAsync(visualizerTarget, cancellationToken);
        }
    }
}
