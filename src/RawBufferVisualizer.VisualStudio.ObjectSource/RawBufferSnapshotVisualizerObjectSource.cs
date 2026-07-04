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

            SerializeAsJson(outgoingData, RawBufferSnapshotObjectSource.CreateTransfer(snapshot));
        }
    }
}
