using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.DebuggerVisualizers;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;
using RawBufferVisualizer.Sdk;
using RawBufferVisualizer.VisualStudio.ObjectSource;

namespace RawBufferVisualizer.VisualStudio.Extensibility
{
    [VisualStudioContribution]
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
                new VisualizerTargetType(DisplayName, typeof(ConcurrentDictionary<,>)),
                new VisualizerTargetType(DisplayName, typeof(ArrayList)),
                new VisualizerTargetType(DisplayName, typeof(Hashtable)),
                new VisualizerTargetType(DisplayName, typeof(object[])),
                new VisualizerTargetType(DisplayName, "System.Collections.Generic.List`1, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                new VisualizerTargetType(DisplayName, "System.Collections.Generic.Dictionary`2, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                new VisualizerTargetType(DisplayName, "System.Collections.Concurrent.ConcurrentDictionary`2, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                new VisualizerTargetType(DisplayName, "System.Collections.ArrayList, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                new VisualizerTargetType(DisplayName, "System.Collections.Hashtable, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                new VisualizerTargetType(DisplayName, "System.Object[], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                new VisualizerTargetType(DisplayName, "System.Collections.Generic.List`1, System.Private.CoreLib"),
                new VisualizerTargetType(DisplayName, "System.Collections.Generic.Dictionary`2, System.Private.CoreLib"),
                new VisualizerTargetType(DisplayName, "System.Collections.Concurrent.ConcurrentDictionary`2, System.Collections.Concurrent"),
                new VisualizerTargetType(DisplayName, "System.Object[], System.Private.CoreLib"),
                new VisualizerTargetType(DisplayName, typeof(RawBufferSnapshot[])),
                new VisualizerTargetType(DisplayName, typeof(RawBufferView[])),
                new VisualizerTargetType(DisplayName, "System.Drawing.Bitmap[], System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"),
                new VisualizerTargetType(DisplayName, "System.Drawing.Bitmap[], System.Drawing.Common, Version=4.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51"),
                new VisualizerTargetType(DisplayName, "System.Drawing.Bitmap[], System.Drawing.Common, Version=5.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51"),
                new VisualizerTargetType(DisplayName, "System.Drawing.Bitmap[], System.Drawing.Common, Version=6.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51"),
                new VisualizerTargetType(DisplayName, "System.Drawing.Bitmap[], System.Drawing.Common, Version=7.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51"),
                new VisualizerTargetType(DisplayName, "System.Drawing.Bitmap[], System.Drawing.Common, Version=8.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51"),
                new VisualizerTargetType(DisplayName, "System.Drawing.Bitmap[], System.Drawing.Common, Version=9.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51"),
                new VisualizerTargetType(DisplayName, "System.Drawing.Bitmap[], System.Drawing.Common, Version=10.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51"),
                new VisualizerTargetType(DisplayName, "OpenCvSharp.Mat[], OpenCvSharp, Version=1.0.0.0, Culture=neutral, PublicKeyToken=6adad1e807fea099"),
                new VisualizerTargetType(DisplayName, "OpenCvSharp.Mat[], OpenCvSharp, Version=4.0.0.0, Culture=neutral, PublicKeyToken=6adad1e807fea099"),
                new VisualizerTargetType(DisplayName, "Emgu.CV.Mat[], Emgu.CV.World, Version=3.4.3.3016, Culture=neutral, PublicKeyToken=7281126722ab4438"),
                new VisualizerTargetType(DisplayName, "Emgu.CV.Mat[], Emgu.CV.World.NetStandard, Version=1.0.0.0, Culture=neutral, PublicKeyToken=7281126722ab4438"),
                new VisualizerTargetType(DisplayName, "Emgu.CV.Mat[], Emgu.CV.Platform.NetStandard, Version=4.5.5.4823, Culture=neutral, PublicKeyToken=7281126722ab4438"),
                new VisualizerTargetType(DisplayName, "Emgu.CV.Mat[], Emgu.CV, Version=4.8.1.5350, Culture=neutral, PublicKeyToken=7281126722ab4438"),
                new VisualizerTargetType(DisplayName, "Emgu.CV.Mat[], Emgu.CV, Version=4.13.0.5924, Culture=neutral, PublicKeyToken=7281126722ab4438")
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
