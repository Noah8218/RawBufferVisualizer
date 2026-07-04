using System;
using System.Drawing;
using System.IO;
using Microsoft.VisualStudio.DebuggerVisualizers;
using RawBufferVisualizer.BitmapAdapter;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    public sealed class BitmapVisualizerObjectSource : VisualizerObjectSource
    {
        public override void GetData(object target, Stream outgoingData)
        {
            if (!(target is Bitmap bitmap))
            {
                throw new NotSupportedException("Only System.Drawing.Bitmap is supported.");
            }

            SerializeAsJson(outgoingData, BitmapVisualizerTransfer.CreateTransfer(bitmap));
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
