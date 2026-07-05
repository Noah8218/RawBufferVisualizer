using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.DebuggerVisualizers;
using RawBufferVisualizer.Sdk;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    public sealed class RawBufferViewVisualizerObjectSource : VisualizerObjectSource
    {
        public override void GetData(object target, Stream outgoingData)
        {
            if (!(target is RawBufferView view))
            {
                throw new NotSupportedException("Only RawBufferView is supported.");
            }

            SerializeAsJson(outgoingData, RawBufferViewVisualizerTransfer.CreateMetadata(view));
        }

        public override void TransferData(object target, Stream incomingData, Stream outgoingData)
        {
            if (!(target is RawBufferView view))
            {
                throw new NotSupportedException("Only RawBufferView is supported.");
            }

            var request = DeserializeFromJson<VisualizerSnapshotChunkRequest>(incomingData);
            if (request == null)
            {
                throw new InvalidDataException("Chunk request is required.");
            }

            SerializeAsJson(outgoingData, RawBufferViewVisualizerTransfer.CreateChunk(view, request));
        }
    }

    public static class RawBufferViewVisualizerTransfer
    {
        public static VisualizerSnapshotMetadata CreateMetadata(RawBufferView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            return VisualizerChunkedTransfer.CreateMetadata(
                view.ToDescriptor(),
                view.GetBufferLength(),
                typeof(RawBufferView).FullName ?? nameof(RawBufferView),
                view.Name);
        }

        public static VisualizerSnapshotChunk CreateChunk(RawBufferView view, VisualizerSnapshotChunkRequest request)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (view.Buffer == IntPtr.Zero)
            {
                throw new InvalidOperationException("RawBufferView buffer pointer is empty.");
            }

            var totalLength = view.GetBufferLength();
            if (request.Offset < 0 || request.Offset > totalLength)
            {
                throw new ArgumentOutOfRangeException(nameof(request.Offset));
            }

            if (request.Count <= 0 || request.Count > VisualizerChunkedTransfer.DefaultChunkSize)
            {
                throw new ArgumentOutOfRangeException(nameof(request.Count));
            }

            var remaining = totalLength - request.Offset;
            var length = (int)Math.Min(request.Count, remaining);
            var chunk = new byte[length];
            if (length > 0)
            {
                Marshal.Copy(Add(view.Buffer, request.Offset), chunk, 0, length);
            }

            return new VisualizerSnapshotChunk
            {
                Offset = request.Offset,
                Buffer = chunk,
                TotalLength = totalLength,
                IsLastChunk = request.Offset + length >= totalLength
            };
        }

        private static IntPtr Add(IntPtr pointer, long offset)
        {
            return new IntPtr(checked(pointer.ToInt64() + offset));
        }
    }
}
