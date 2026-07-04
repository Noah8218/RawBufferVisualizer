using System;
using RawBufferVisualizer.Core;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    public sealed class VisualizerSnapshotMetadata
    {
        public RawImageDescriptor Descriptor { get; set; } = new RawImageDescriptor();
        public long BufferLength { get; set; }
        public int ChunkSize { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public sealed class VisualizerSnapshotChunkRequest
    {
        public long Offset { get; set; }
        public int Count { get; set; }
    }

    public sealed class VisualizerSnapshotChunk
    {
        public long Offset { get; set; }
        public byte[] Buffer { get; set; } = Array.Empty<byte>();
        public long TotalLength { get; set; }
        public bool IsLastChunk { get; set; }
    }

    public static class VisualizerChunkedTransfer
    {
        public const int DefaultChunkSize = 4 * 1024 * 1024;

        public static VisualizerSnapshotMetadata CreateMetadata(VisualizerSnapshotTransfer transfer)
        {
            if (transfer == null)
            {
                throw new ArgumentNullException(nameof(transfer));
            }

            return CreateMetadata(
                transfer.Descriptor,
                transfer.Buffer == null ? 0 : transfer.Buffer.LongLength,
                transfer.SourceType,
                transfer.DisplayName);
        }

        public static VisualizerSnapshotMetadata CreateMetadata(
            RawImageDescriptor descriptor,
            long bufferLength,
            string sourceType,
            string? displayName = null)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            if (bufferLength < descriptor.GetRequiredByteCount())
            {
                throw new ArgumentException("Buffer is smaller than descriptor requires.", nameof(bufferLength));
            }

            return new VisualizerSnapshotMetadata
            {
                Descriptor = descriptor.Clone(),
                BufferLength = bufferLength,
                ChunkSize = DefaultChunkSize,
                SourceType = sourceType ?? string.Empty,
                DisplayName = displayName ?? string.Empty
            };
        }

        public static VisualizerSnapshotChunk CreateChunk(VisualizerSnapshotTransfer transfer, VisualizerSnapshotChunkRequest request)
        {
            if (transfer == null)
            {
                throw new ArgumentNullException(nameof(transfer));
            }

            return CreateChunk(transfer.Buffer, request);
        }

        public static VisualizerSnapshotChunk CreateChunk(byte[] buffer, VisualizerSnapshotChunkRequest request)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Offset < 0 || request.Offset > buffer.LongLength)
            {
                throw new ArgumentOutOfRangeException(nameof(request.Offset));
            }

            if (request.Count <= 0 || request.Count > DefaultChunkSize)
            {
                throw new ArgumentOutOfRangeException(nameof(request.Count));
            }

            var remaining = buffer.LongLength - request.Offset;
            var length = (int)Math.Min(request.Count, remaining);
            var chunk = new byte[length];
            Buffer.BlockCopy(buffer, checked((int)request.Offset), chunk, 0, length);

            return new VisualizerSnapshotChunk
            {
                Offset = request.Offset,
                Buffer = chunk,
                TotalLength = buffer.LongLength,
                IsLastChunk = request.Offset + length >= buffer.LongLength
            };
        }
    }
}
