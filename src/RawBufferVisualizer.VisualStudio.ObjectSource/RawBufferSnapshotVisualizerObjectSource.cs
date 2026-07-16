using System;
using System.IO;
using Microsoft.VisualStudio.DebuggerVisualizers;
using RawBufferVisualizer.Sdk;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    public sealed class RawBufferSnapshotVisualizerObjectSource : VisualizerObjectSource
    {
        public override void GetData(object target, Stream outgoingData)
        {
            if (!(target is RawBufferSnapshot snapshot))
            {
                throw new NotSupportedException("Only RawBufferSnapshot is supported.");
            }

            SerializeAsJson(
                outgoingData,
                VisualizerChunkedTransfer.CreateMetadata(
                    snapshot.Descriptor,
                    snapshot.Buffer.LongLength,
                    typeof(RawBufferSnapshot).FullName ?? nameof(RawBufferSnapshot)));
        }

        public override void TransferData(object target, Stream incomingData, Stream outgoingData)
        {
            if (!(target is RawBufferSnapshot snapshot))
            {
                throw new NotSupportedException("Only RawBufferSnapshot is supported.");
            }

            var request = DeserializeFromJson<VisualizerSnapshotChunkRequest>(incomingData);
            if (request == null)
            {
                throw new InvalidDataException("Chunk request is required.");
            }

            if (request.Operation == VisualizerSnapshotOperation.Preview)
            {
                SerializeAsJson(
                    outgoingData,
                    VisualizerChunkedTransfer.CreatePreview(
                        snapshot.Buffer,
                        snapshot.Descriptor,
                        typeof(RawBufferSnapshot).FullName ?? nameof(RawBufferSnapshot),
                        null,
                        request));
                return;
            }

            SerializeAsJson(outgoingData, VisualizerChunkedTransfer.CreateChunk(snapshot.Buffer, request));
        }
    }
}
