using System;
using RawBufferVisualizer.Sdk;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    public static class RawBufferSnapshotObjectSource
    {
        public static VisualizerSnapshotTransfer CreateTransfer(RawBufferSnapshot snapshot, string? displayName = null)
        {
            return CreateTransfer(snapshot, typeof(RawBufferSnapshot).FullName ?? nameof(RawBufferSnapshot), displayName);
        }

        public static VisualizerSnapshotTransfer CreateTransfer(RawBufferSnapshot snapshot, string sourceType, string? displayName = null)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (string.IsNullOrWhiteSpace(sourceType))
            {
                throw new ArgumentException("Source type is required.", nameof(sourceType));
            }

            return new VisualizerSnapshotTransfer
            {
                Descriptor = snapshot.Descriptor.Clone(),
                Buffer = (byte[])snapshot.Buffer.Clone(),
                SourceType = sourceType,
                DisplayName = displayName ?? string.Empty
            };
        }
    }
}
