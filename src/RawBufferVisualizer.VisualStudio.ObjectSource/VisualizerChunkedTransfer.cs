using System;
using System.Runtime.CompilerServices;
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
        public bool SupportsDirectMemory { get; set; }
        public int ProcessId { get; set; }
        public long BufferAddress { get; set; }
        public string SourcePointerLabel { get; set; } = string.Empty;
        public long SourcePointerAddress { get; set; }
        public int ExpressionIdentityHash { get; set; }
        public string ExpressionSourceType { get; set; } = string.Empty;
    }

    public sealed class VisualizerSnapshotChunkRequest
    {
        public VisualizerSnapshotOperation Operation { get; set; }
        public long Offset { get; set; }
        public int Count { get; set; }
        public int MaximumWidth { get; set; }
        public int MaximumHeight { get; set; }
    }

    public enum VisualizerSnapshotOperation
    {
        Chunk = 0,
        Preview = 1
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

        public static VisualizerSnapshotMetadata AttachExpressionIdentity(
            VisualizerSnapshotMetadata metadata,
            object target)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            metadata.ExpressionIdentityHash = RuntimeHelpers.GetHashCode(target);
            return metadata;
        }

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
            return CreateMetadataCore(
                descriptor,
                bufferLength,
                sourceType,
                displayName,
                false,
                0,
                0,
                0,
                null);
        }

        public static VisualizerSnapshotMetadata CreatePointerMetadata(
            RawImageDescriptor descriptor,
            long bufferLength,
            IntPtr bufferAddress,
            string sourceType,
            string? displayName = null)
        {
            return CreatePointerMetadata(
                descriptor,
                bufferLength,
                bufferAddress,
                sourceType,
                displayName,
                bufferAddress,
                "Ptr");
        }

        public static VisualizerSnapshotMetadata CreatePointerMetadata(
            RawImageDescriptor descriptor,
            long bufferLength,
            IntPtr bufferAddress,
            string sourceType,
            string? displayName,
            IntPtr sourcePointerAddress,
            string? sourcePointerLabel)
        {
            if (bufferAddress == IntPtr.Zero)
            {
                throw new ArgumentException("Buffer address is required.", nameof(bufferAddress));
            }

            return CreateMetadataCore(
                descriptor,
                bufferLength,
                sourceType,
                displayName,
                true,
                System.Diagnostics.Process.GetCurrentProcess().Id,
                bufferAddress.ToInt64(),
                sourcePointerAddress.ToInt64(),
                sourcePointerLabel);
        }

        public static VisualizerSnapshotMetadata CreateCapturedPointerMetadata(
            RawImageDescriptor descriptor,
            long bufferLength,
            IntPtr bufferAddress,
            string sourceType,
            string? displayName,
            IntPtr sourcePointerAddress,
            string? sourcePointerLabel)
        {
            if (bufferAddress == IntPtr.Zero)
            {
                throw new ArgumentException("Buffer address is required.", nameof(bufferAddress));
            }

            return CreateMetadataCore(
                descriptor,
                bufferLength,
                sourceType,
                displayName,
                false,
                System.Diagnostics.Process.GetCurrentProcess().Id,
                bufferAddress.ToInt64(),
                sourcePointerAddress.ToInt64(),
                sourcePointerLabel);
        }

        private static VisualizerSnapshotMetadata CreateMetadataCore(
            RawImageDescriptor descriptor,
            long bufferLength,
            string sourceType,
            string? displayName,
            bool supportsDirectMemory,
            int processId,
            long bufferAddress,
            long sourcePointerAddress,
            string? sourcePointerLabel)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            var diagnostics = RawBufferDiagnostics.AnalyzeLength(bufferLength, descriptor);
            for (var i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity == RawDiagnosticSeverity.Error)
                {
                    throw new ArgumentException(diagnostics[i].Message, nameof(descriptor));
                }
            }

            return new VisualizerSnapshotMetadata
            {
                Descriptor = descriptor.Clone(),
                BufferLength = bufferLength,
                ChunkSize = DefaultChunkSize,
                SourceType = sourceType ?? string.Empty,
                DisplayName = displayName ?? string.Empty,
                SupportsDirectMemory = supportsDirectMemory,
                ProcessId = processId,
                BufferAddress = bufferAddress,
                SourcePointerAddress = sourcePointerAddress,
                SourcePointerLabel = sourcePointerAddress == 0
                    ? string.Empty
                    : string.IsNullOrWhiteSpace(sourcePointerLabel) ? "Ptr" : sourcePointerLabel!.Trim()
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

        public static VisualizerSnapshotTransfer CreatePreview(
            VisualizerSnapshotTransfer transfer,
            VisualizerSnapshotChunkRequest request)
        {
            if (transfer == null)
            {
                throw new ArgumentNullException(nameof(transfer));
            }

            return CreatePreview(
                transfer.Buffer,
                transfer.Descriptor,
                transfer.SourceType,
                transfer.DisplayName,
                request);
        }

        public static VisualizerSnapshotTransfer CreatePreview(
            byte[] buffer,
            RawImageDescriptor descriptor,
            string sourceType,
            string? displayName,
            VisualizerSnapshotChunkRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.Operation != VisualizerSnapshotOperation.Preview)
            {
                throw new InvalidOperationException("A preview request is required.");
            }

            return VisualizerSampledPreview.Create(
                buffer,
                descriptor,
                sourceType,
                displayName,
                request.MaximumWidth,
                request.MaximumHeight);
        }
    }
}
