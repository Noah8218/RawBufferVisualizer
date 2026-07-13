using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.DebuggerVisualizers;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;
using RawBufferVisualizer.VisualStudio.ObjectSource;

namespace RawBufferVisualizer.VisualStudio.Extensibility
{
    internal sealed class ImageCollectionDebuggerVisualizerProvider : DebuggerVisualizerProvider
    {
        private const string DisplayName = "%RawBufferVisualizer.DebuggerVisualizer.DisplayName%";

        public ImageCollectionDebuggerVisualizerProvider(
            RawBufferVisualizerExtension extension,
            VisualStudioExtensibility extensibility)
            : base(extension, extensibility)
        {
        }

        public override DebuggerVisualizerProviderConfiguration DebuggerVisualizerProviderConfiguration =>
            new(new[]
            {
                new VisualizerTargetType(DisplayName, typeof(List<>)),
                new VisualizerTargetType(DisplayName, typeof(Dictionary<,>)),
                new VisualizerTargetType(DisplayName, typeof(ArrayList)),
                new VisualizerTargetType(DisplayName, typeof(Hashtable)),
                new VisualizerTargetType(DisplayName, typeof(object[])),
                new VisualizerTargetType(DisplayName, "System.Collections.Generic.List`1, System.Private.CoreLib"),
                new VisualizerTargetType(DisplayName, "System.Collections.Generic.Dictionary`2, System.Private.CoreLib"),
                new VisualizerTargetType(DisplayName, "System.Object[], System.Private.CoreLib"),
                new VisualizerTargetType(DisplayName, "RawBufferVisualizer.Sdk.RawBufferSnapshot[], RawBufferVisualizer.Sdk"),
                new VisualizerTargetType(DisplayName, "RawBufferVisualizer.Sdk.RawBufferView[], RawBufferVisualizer.Sdk"),
                new VisualizerTargetType(DisplayName, "System.Drawing.Bitmap[], System.Drawing"),
                new VisualizerTargetType(DisplayName, "System.Drawing.Bitmap[], System.Drawing.Common"),
                new VisualizerTargetType(DisplayName, "OpenCvSharp.Mat[], OpenCvSharp"),
                new VisualizerTargetType(DisplayName, "Emgu.CV.Mat[], Emgu.CV"),
                new VisualizerTargetType(DisplayName, "Emgu.CV.Mat[], Emgu.CV.World"),
                new VisualizerTargetType(DisplayName, "Emgu.CV.Mat[], Emgu.CV.World.NetStandard"),
                new VisualizerTargetType(DisplayName, "Emgu.CV.Mat[], Emgu.CV.Platform.NetStandard")
            })
            {
                Style = VisualizerStyle.ToolWindow,
                VisualizerObjectSourceType = new(typeof(ImageCollectionVisualizerObjectSource))
            };

        public override Task<IRemoteUserControl> CreateVisualizerAsync(
            VisualizerTarget visualizerTarget,
            CancellationToken cancellationToken)
        {
            return DebuggerVisualizerLaunch.CreateCollectionControlAsync(visualizerTarget, cancellationToken);
        }
    }
}
