using System.Diagnostics;
using RawBufferVisualizer.VisualStudio.Classic;
using RawBufferVisualizer.VisualStudio.ObjectSource;

// These self-targeted attributes register both Classic UI IDs with the VSIX install path.
// Actual image/collection candidates remain owned by ImageVisualizerResultProvider.
// Removing them or the DotnetCustomVisualizer asset leaves ObjectSource probing the VS global directory.
[assembly: DebuggerVisualizer(
    typeof(RawBufferClassicDebuggerVisualizer),
    typeof(RawBufferSnapshotVisualizerObjectSource),
    Target = typeof(RawBufferClassicDebuggerVisualizer),
    Description = "Raw Buffer Visualizer")]
[assembly: DebuggerVisualizer(
    typeof(ImageCollectionClassicDebuggerVisualizer),
    typeof(ImageCollectionVisualizerObjectSource),
    Target = typeof(ImageCollectionClassicDebuggerVisualizer),
    Description = "Raw Buffer Visualizer")]
