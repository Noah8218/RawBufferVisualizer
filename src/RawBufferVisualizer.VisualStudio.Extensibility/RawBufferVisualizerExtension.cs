using Microsoft.VisualStudio.Extensibility;

namespace RawBufferVisualizer.VisualStudio.Extensibility
{
    [VisualStudioContribution]
    internal sealed class RawBufferVisualizerExtension : Extension
    {
        public override ExtensionConfiguration ExtensionConfiguration => new()
        {
            Metadata = new(
                id: "RawBufferVisualizer.34f8ad30-2f11-4c37-a9d4-00f3a8c1d29f",
                version: this.ExtensionAssemblyVersion,
                publisherName: "Noah Choi",
                displayName: "Raw Buffer Visualizer",
                description: "Image Watch-style C# debugging with Bitmap/Mat inspection, automatic camera-frame discovery, and raw-buffer diagnosis."),
        };
    }
}
