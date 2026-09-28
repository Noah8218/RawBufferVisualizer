using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.DebuggerVisualizers;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.Sdk;
using RawBufferVisualizer.VisualStudio.ObjectSource;

#if !DYNAMIC_VISUALIZER_REGISTRATION
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
#endif

namespace RawBufferVisualizer.VisualStudio.Classic
{
    public sealed class ImageCollectionClassicDebuggerVisualizer : DialogDebuggerVisualizer
    {
        private readonly Func<int, TypeMappingSnapshot> _getMappings;
        private readonly Action<int> _wakeDockedToolWindow;

        public ImageCollectionClassicDebuggerVisualizer()
            : this(GetMappings, RawBufferClassicDebuggerVisualizer.WakeDockedToolWindow)
        {
        }

        internal ImageCollectionClassicDebuggerVisualizer(Func<int, TypeMappingSnapshot> getMappings, Action<int> wakeDockedToolWindow)
            : base(FormatterPolicy.NewtonsoftJson)
        {
            _getMappings = getMappings ?? throw new ArgumentNullException(nameof(getMappings));
            _wakeDockedToolWindow = wakeDockedToolWindow ?? throw new ArgumentNullException(nameof(wakeDockedToolWindow));
        }

        protected override void Show(
            IDialogVisualizerService windowService,
            IVisualizerObjectProvider objectProvider)
        {
            var visualStudioProcessId = Process.GetCurrentProcess().Id;
            var sourceType = "Image collection";
            var requestPaths = new List<string>();

            try
            {
                var objectProvider2 = objectProvider as IVisualizerObjectProvider2
                    ?? throw new NotSupportedException("The Visual Studio object provider does not support JSON data.");
                var mappings = _getMappings(visualStudioProcessId);
                var summary = objectProvider2.TransferDeserializableObject(
                        new VisualizerCollectionItemRequest { Operation = VisualizerCollectionOperation.ConfigureMappings, Mappings = mappings })
                    .ToObject<VisualizerCollectionSummary>()
                    ?? throw new InvalidDataException("The debugger visualizer returned no collection summary.");

                sourceType = string.IsNullOrWhiteSpace(summary.SourceType) ? sourceType : summary.SourceType;
                if (summary.TotalCount <= 0) throw new InvalidOperationException("Collection contains no items.");
                for (var index = 0; index < summary.ItemCount; index++)
                {
                    VisualizerCollectionItemMetadata? item = null;
                    string? metadataPath = null;
                    var displayName = "[" + index + "]";
                    try
                    {
                        item = objectProvider2.TransferDeserializableObject(new VisualizerCollectionItemRequest
                        {
                            Operation = VisualizerCollectionOperation.Metadata,
                            Index = index
                        }).ToObject<VisualizerCollectionItemMetadata>();
                        if (item != null && !string.IsNullOrWhiteSpace(item.DisplayName)) displayName = item.DisplayName;
                        var metadata = item?.Metadata ?? throw new InvalidOperationException(
                            string.IsNullOrWhiteSpace(item?.Error) ? "Collection item returned no metadata." : item!.Error);
                        var handoffId = Guid.NewGuid().ToString("N");

                        if (metadata.BufferLength >= 64L * 1024 * 1024)
                        {
                            try
                            {
                                var preview = objectProvider2.TransferDeserializableObject(new VisualizerCollectionItemRequest
                                {
                                    Operation = VisualizerCollectionOperation.Preview,
                                    Index = index,
                                    MaximumWidth = VisualizerSampledPreview.DefaultMaximumDimension,
                                    MaximumHeight = VisualizerSampledPreview.DefaultMaximumDimension
                                }).ToObject<VisualizerSnapshotTransfer>();
                                if (preview?.Descriptor != null && preview.Buffer != null
                                    && preview.Descriptor.Width > 0 && preview.Descriptor.Height > 0
                                    && preview.Buffer.LongLength >= preview.Descriptor.GetRequiredByteCount())
                                {
                                    var directory = VisualStudioTempStore.CreateSnapshotDirectory();
                                    try
                                    {
                                        var previewPath = Path.Combine(directory, "preview.rbuf.json");
                                        File.WriteAllBytes(RawBufferSnapshot.SaveMetadata(previewPath, preview.Descriptor), preview.Buffer);
                                        requestPaths.Add(VisualizerHandoffInbox.WriteSnapshotRequest(
                                            visualStudioProcessId, previewPath, displayName,
                                            metadata.SourceType + " (sampled preview of " + metadata.Descriptor.Width + "x" + metadata.Descriptor.Height + ")",
                                            handoffId, true, metadata.ProcessId, metadata.BufferAddress,
                                            metadata.SourcePointerAddress, metadata.SourcePointerLabel,
                                            summary.ExpressionIdentityHash, summary.SourceType));
                                    }
                                    catch
                                    {
                                        VisualStudioTempStore.TryDeleteDirectory(directory);
                                        throw;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Trace.WriteLine("Raw Buffer Visualizer collection preview unavailable: " + ex.Message);
                            }
                        }

                        if (metadata.BufferLength >= 8L * 1024 * 1024 && metadata.SupportsDirectMemory
                            && metadata.ProcessId > 0 && metadata.BufferAddress != 0)
                        {
                            requestPaths.Add(VisualizerHandoffInbox.WriteLiveMemoryRequest(
                                visualStudioProcessId, metadata.ProcessId, metadata.BufferAddress,
                                metadata.BufferLength, metadata.Descriptor, displayName, metadata.SourceType, handoffId,
                                metadata.SourcePointerAddress, metadata.SourcePointerLabel,
                                summary.ExpressionIdentityHash, summary.SourceType));
                        }
                        else
                        {
                            metadataPath = VisualizerSnapshotStore.WriteSnapshot(metadata, request =>
                                objectProvider2.TransferDeserializableObject(new VisualizerCollectionItemRequest
                                {
                                    Operation = VisualizerCollectionOperation.Chunk,
                                    Index = index,
                                    Offset = request.Offset,
                                    Count = request.Count
                                }).ToObject<VisualizerSnapshotChunk>()
                                ?? throw new InvalidDataException("The debugger visualizer returned an invalid collection chunk."));
                            requestPaths.Add(VisualizerHandoffInbox.WriteSnapshotRequest(
                                visualStudioProcessId, metadataPath, displayName, metadata.SourceType, handoffId,
                                false, metadata.ProcessId, metadata.BufferAddress,
                                metadata.SourcePointerAddress, metadata.SourcePointerLabel,
                                summary.ExpressionIdentityHash, summary.SourceType));
                        }
                    }
                    catch (Exception ex)
                    {
                        if (metadataPath != null) VisualStudioTempStore.TryDeleteSnapshotDirectoryForMetadata(metadataPath);
                        requestPaths.Add(VisualizerHandoffInbox.WriteErrorRequest(
                            visualStudioProcessId, displayName,
                            item == null || string.IsNullOrWhiteSpace(item.ItemTypeName) ? summary.SourceType : item.ItemTypeName,
                            ex.Message, ex.GetType().FullName, ex.ToString(),
                            memberInventory: item?.MemberInventory,
                            itemAssemblyName: item?.ItemAssemblyName,
                            debuggeeProcessId: item?.DebuggeeProcessId ?? 0,
                            expressionIdentityHash: summary.ExpressionIdentityHash,
                            expressionSourceType: summary.SourceType));
                    }
                }

                if (summary.TotalCount > summary.ItemCount)
                {
                    requestPaths.Add(VisualizerHandoffInbox.WriteErrorRequest(
                        visualStudioProcessId, "Image collection", summary.SourceType,
                        "Collection contains " + summary.TotalCount + " items; only the first " + summary.ItemCount + " are shown."));
                }
            }
            catch (Exception ex)
            {
                requestPaths.Add(VisualizerHandoffInbox.WriteErrorRequest(
                    visualStudioProcessId,
                    "Image collection",
                    sourceType,
                    ex.Message,
                    ex.GetType().FullName,
                    ex.ToString()));
            }

            RawBufferClassicDebuggerVisualizer.ScheduleTerminalCleanup(requestPaths);
            _wakeDockedToolWindow(visualStudioProcessId);
        }

        private static TypeMappingSnapshot GetMappings(int visualStudioProcessId)
        {
            var dte = VisualStudioInstance.GetDte(visualStudioProcessId)
                ?? throw new InvalidOperationException("The hosting Visual Studio solution context is unavailable.");
            return TypeMappingStore.ForSolution(VisualStudioInstance.GetSolutionPath(dte)).ExportEffectiveMappings();
        }
    }
}
