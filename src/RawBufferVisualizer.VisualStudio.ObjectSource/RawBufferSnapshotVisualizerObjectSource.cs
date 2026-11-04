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
            VisualizerChunkedTransfer.CreateSnapshotTransfer(GetSnapshot(target)).WriteMetadata(target, outgoingData);
        }

        public override void TransferData(object target, Stream incomingData, Stream outgoingData)
        {
            var snapshot = GetSnapshot(target);
            var request = ImageVisualizerTransfer.ReadRequest(incomingData);
            VisualizerChunkedTransfer.CreateSnapshotTransfer(snapshot).WriteData(request, outgoingData);
        }

        private static RawBufferSnapshot GetSnapshot(object target)
        {
            return target as RawBufferSnapshot ?? throw new NotSupportedException("Only RawBufferSnapshot is supported.");
        }
    }
}
