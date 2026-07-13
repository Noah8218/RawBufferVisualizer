using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.DebuggerVisualizers;
using RawBufferVisualizer.Sdk;
using RawBufferVisualizer.VisualStudio.ObjectSource;

// Visual Studio 2022 rejects open generic visualizer targets and can disable expression evaluation.
[assembly: DebuggerVisualizer(
    typeof(RawBufferVisualizer.VisualStudio.Classic.ImageCollectionClassicDebuggerVisualizer),
    typeof(ImageCollectionVisualizerObjectSource),
    Target = typeof(List<object>),
    Description = "Raw Buffer Visualizer")]
[assembly: DebuggerVisualizer(
    typeof(RawBufferVisualizer.VisualStudio.Classic.ImageCollectionClassicDebuggerVisualizer),
    typeof(ImageCollectionVisualizerObjectSource),
    Target = typeof(Dictionary<string, object>),
    Description = "Raw Buffer Visualizer")]
[assembly: DebuggerVisualizer(
    typeof(RawBufferVisualizer.VisualStudio.Classic.ImageCollectionClassicDebuggerVisualizer),
    typeof(ImageCollectionVisualizerObjectSource),
    Target = typeof(object[]),
    Description = "Raw Buffer Visualizer")]
namespace RawBufferVisualizer.VisualStudio.Classic
{
    public sealed class ImageCollectionClassicDebuggerVisualizer : DialogDebuggerVisualizer
    {
        public ImageCollectionClassicDebuggerVisualizer()
            : base(FormatterPolicy.NewtonsoftJson)
        {
        }

        protected override void Show(
            IDialogVisualizerService windowService,
            IVisualizerObjectProvider objectProvider)
        {
            var visualStudioProcessId = Process.GetCurrentProcess().Id;
            var sourceType = "Image collection";

            try
            {
                var objectProvider2 = objectProvider as IVisualizerObjectProvider2
                    ?? throw new NotSupportedException("The Visual Studio object provider does not support JSON data.");
                var summary = objectProvider2.GetDeserializableObject()
                    .ToObject<VisualizerCollectionSummary>()
                    ?? throw new InvalidDataException("The debugger visualizer returned no collection summary.");

                sourceType = string.IsNullOrWhiteSpace(summary.SourceType) ? sourceType : summary.SourceType;
                var results = VisualizerSnapshotStore.WriteCollection(
                    summary,
                    index => objectProvider2.TransferDeserializableObject(
                            new VisualizerCollectionItemRequest
                            {
                                Operation = VisualizerCollectionOperation.Metadata,
                                Index = index
                            })
                        .ToObject<VisualizerCollectionItemMetadata>()
                        ?? throw new InvalidDataException("The debugger visualizer returned invalid collection metadata."),
                    (index, request) => objectProvider2.TransferDeserializableObject(
                            new VisualizerCollectionItemRequest
                            {
                                Operation = VisualizerCollectionOperation.Chunk,
                                Index = index,
                                Offset = request.Offset,
                                Count = request.Count
                            })
                        .ToObject<VisualizerSnapshotChunk>()
                        ?? throw new InvalidDataException("The debugger visualizer returned an invalid collection chunk."));

                foreach (var result in results)
                {
                    if (result.IsError)
                    {
                        VisualizerHandoffInbox.WriteErrorRequest(
                            visualStudioProcessId,
                            result.DisplayName,
                            result.SourceType,
                            result.ErrorMessage);
                        continue;
                    }

                    try
                    {
                        VisualizerHandoffInbox.WriteSnapshotRequest(
                            visualStudioProcessId,
                            result.MetadataPath,
                            result.DisplayName,
                            result.SourceType);
                    }
                    catch (Exception ex)
                    {
                        VisualStudioTempStore.TryDeleteSnapshotDirectoryForMetadata(result.MetadataPath);
                        VisualizerHandoffInbox.WriteErrorRequest(
                            visualStudioProcessId,
                            result.DisplayName,
                            result.SourceType,
                            ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                VisualizerHandoffInbox.WriteErrorRequest(
                    visualStudioProcessId,
                    "Image collection",
                    sourceType,
                    ex.Message);
            }

            RawBufferClassicDebuggerVisualizer.WakeDockedToolWindow(visualStudioProcessId);
        }
    }
}
