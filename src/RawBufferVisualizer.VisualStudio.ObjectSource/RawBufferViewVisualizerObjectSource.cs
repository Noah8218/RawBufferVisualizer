using System;
using System.IO;
using Microsoft.VisualStudio.DebuggerVisualizers;
using RawBufferVisualizer.Sdk;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    public sealed class RawBufferViewVisualizerObjectSource : VisualizerObjectSource
    {
        public override void GetData(object target, Stream outgoingData)
        {
            RawBufferViewVisualizerTransfer.CreateImageTransfer(GetView(target)).WriteMetadata(target, outgoingData);
        }

        public override void TransferData(object target, Stream incomingData, Stream outgoingData)
        {
            var view = GetView(target);
            var request = ImageVisualizerTransfer.ReadRequest(incomingData);
            RawBufferViewVisualizerTransfer.CreateImageTransfer(view).WriteData(request, outgoingData);
        }

        private static RawBufferView GetView(object target)
        {
            return target as RawBufferView ?? throw new NotSupportedException("Only RawBufferView is supported.");
        }
    }

    public static class RawBufferViewVisualizerTransfer
    {
        internal static ImageVisualizerTransfer CreateImageTransfer(RawBufferView view, string? displayName = null)
        {
            return new ImageVisualizerTransfer(
                () =>
                {
                    var metadata = CreateMetadata(view);
                    if (displayName != null) metadata.DisplayName = displayName;
                    return metadata;
                },
                (request, _) => CreateChunk(view, request),
                request => CreatePreview(view, request));
        }

        public static VisualizerSnapshotMetadata CreateMetadata(RawBufferView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            return VisualizerChunkedTransfer.CreatePointerMetadata(
                view.ToDescriptor(),
                view.GetBufferLength(),
                view.Buffer,
                typeof(RawBufferView).FullName ?? nameof(RawBufferView),
                view.Name,
                view.Buffer,
                "Buffer");
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
                new CurrentProcessMemoryReader(view.Buffer, totalLength)
                    .CopyTo(request.Offset, chunk, 0, length);
            }

            return new VisualizerSnapshotChunk
            {
                Offset = request.Offset,
                Buffer = chunk,
                TotalLength = totalLength,
                IsLastChunk = request.Offset + length >= totalLength
            };
        }

        public static VisualizerSnapshotTransfer CreatePreview(
            RawBufferView view,
            VisualizerSnapshotChunkRequest request)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            return VisualizerSampledPreview.Create(
                view.Buffer,
                view.GetBufferLength(),
                view.ToDescriptor(),
                typeof(RawBufferView).FullName ?? nameof(RawBufferView),
                null,
                request.MaximumWidth,
                request.MaximumHeight);
        }

    }
}
