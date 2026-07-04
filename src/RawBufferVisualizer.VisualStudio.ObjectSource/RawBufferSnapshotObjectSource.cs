using System;
using RawBufferVisualizer.Sdk;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    public static class RawBufferSnapshotObjectSource
    {
        public static VisualizerSnapshotTransfer CreateTransfer(RawBufferSnapshot snapshot, string? displayName = null)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return new VisualizerSnapshotTransfer
            {
                Descriptor = snapshot.Descriptor.Clone(),
                Buffer = (byte[])snapshot.Buffer.Clone(),
                SourceType = typeof(RawBufferSnapshot).FullName ?? nameof(RawBufferSnapshot),
                DisplayName = displayName ?? string.Empty
            };
        }
    }
}
