using Microsoft.Extensions.DependencyInjection;
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
                version: ExtensionAssemblyVersion,
                publisherName: "Noah8218",
                displayName: "Raw Buffer Visualizer",
                description: "Image Watch debugger visualizer for raw buffer snapshots"),
        };

        protected override void InitializeServices(IServiceCollection serviceCollection)
        {
            base.InitializeServices(serviceCollection);
        }
    }
}
