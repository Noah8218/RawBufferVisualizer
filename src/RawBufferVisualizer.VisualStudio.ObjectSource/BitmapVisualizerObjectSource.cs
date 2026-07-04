using System;
using System.Drawing;
using System.IO;
using Microsoft.VisualStudio.DebuggerVisualizers;
using RawBufferVisualizer.BitmapAdapter;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    public sealed class BitmapVisualizerObjectSource : VisualizerObjectSource
    {
        private Bitmap? _cachedBitmap;
        private VisualizerSnapshotTransfer? _cachedTransfer;

        public override void GetData(object target, Stream outgoingData)
        {
            SerializeAsJson(outgoingData, VisualizerChunkedTransfer.CreateMetadata(GetTransfer(target)));
        }

        public override void TransferData(object target, Stream incomingData, Stream outgoingData)
        {
            var request = DeserializeFromJson<VisualizerSnapshotChunkRequest>(incomingData);
            if (request == null)
            {
                throw new InvalidDataException("Chunk request is required.");
            }

            SerializeAsJson(outgoingData, VisualizerChunkedTransfer.CreateChunk(GetTransfer(target), request));
        }

        private VisualizerSnapshotTransfer GetTransfer(object target)
        {
            if (!(target is Bitmap bitmap))
            {
                throw new NotSupportedException("Only System.Drawing.Bitmap is supported.");
            }

            if (!ReferenceEquals(bitmap, _cachedBitmap))
            {
                _cachedBitmap = bitmap;
                _cachedTransfer = BitmapVisualizerTransfer.CreateTransfer(bitmap);
            }

            return _cachedTransfer ?? throw new InvalidOperationException("Bitmap transfer was not created.");
        }
    }

    public static class BitmapVisualizerTransfer
    {
        public static VisualizerSnapshotTransfer CreateTransfer(Bitmap bitmap, string? displayName = null)
        {
            if (bitmap == null)
            {
                throw new ArgumentNullException(nameof(bitmap));
            }

            var snapshot = BitmapSnapshot.FromBitmap(bitmap);
            return RawBufferSnapshotObjectSource.CreateTransfer(
                snapshot,
                typeof(Bitmap).FullName ?? "System.Drawing.Bitmap",
                displayName);
        }
    }
}
