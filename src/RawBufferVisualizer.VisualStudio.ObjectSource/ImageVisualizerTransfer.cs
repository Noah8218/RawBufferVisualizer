using System;
using System.IO;
using Microsoft.VisualStudio.DebuggerVisualizers;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    internal sealed class ImageVisualizerTransfer
    {
        private readonly Func<VisualizerSnapshotMetadata> _createMetadata;
        private readonly Func<VisualizerSnapshotChunkRequest, byte[]?, VisualizerSnapshotChunk> _createChunk;
        private readonly Func<VisualizerSnapshotChunkRequest, VisualizerSnapshotTransfer> _createPreview;

        internal ImageVisualizerTransfer(
            Func<VisualizerSnapshotMetadata> createMetadata,
            Func<VisualizerSnapshotChunkRequest, byte[]?, VisualizerSnapshotChunk> createChunk,
            Func<VisualizerSnapshotChunkRequest, VisualizerSnapshotTransfer> createPreview)
        {
            _createMetadata = createMetadata;
            _createChunk = createChunk;
            _createPreview = createPreview;
        }

        internal VisualizerSnapshotMetadata CreateMetadata() => _createMetadata();

        internal VisualizerSnapshotChunk CreateChunk(VisualizerSnapshotChunkRequest request, byte[]? reusableBuffer)
            => _createChunk(request, reusableBuffer);

        internal VisualizerSnapshotTransfer CreatePreview(VisualizerSnapshotChunkRequest request) => _createPreview(request);

        internal void WriteMetadata(object target, Stream outgoingData)
        {
            VisualizerObjectSource.SerializeAsJson(outgoingData, VisualizerChunkedTransfer.AttachExpressionIdentity(CreateMetadata(), target));
        }

        internal static VisualizerSnapshotChunkRequest ReadRequest(Stream incomingData)
        {
            return VisualizerObjectSource.DeserializeFromJson<VisualizerSnapshotChunkRequest>(incomingData)
                ?? throw new InvalidDataException("Chunk request is required.");
        }

        internal void WriteData(VisualizerSnapshotChunkRequest request, Stream outgoingData)
        {
            if (request.Operation == VisualizerSnapshotOperation.Preview)
            {
                VisualizerObjectSource.SerializeAsJson(outgoingData, CreatePreview(request));
                return;
            }

            VisualizerChunkedTransfer.SerializeChunkAsJson(outgoingData, CreateChunk(request, null));
        }
    }
}
