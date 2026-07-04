using System;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.Sdk;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    public sealed class VisualizerSnapshotTransfer
    {
        public RawImageDescriptor Descriptor { get; set; } = new RawImageDescriptor();
        public byte[] Buffer { get; set; } = Array.Empty<byte>();
        public string SourceType { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        public RawBufferSnapshot ToSnapshot()
        {
            if (Descriptor == null)
            {
                throw new InvalidOperationException("Descriptor is required.");
            }

            if (Buffer == null)
            {
                throw new InvalidOperationException("Buffer is required.");
            }

            return RawBufferSnapshot.FromByteArray(Buffer, Descriptor);
        }
    }
}
