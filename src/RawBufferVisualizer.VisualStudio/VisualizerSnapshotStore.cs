using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using RawBufferVisualizer.Sdk;
using RawBufferVisualizer.VisualStudio.ObjectSource;

namespace RawBufferVisualizer.VisualStudio
{
    public static class VisualizerSnapshotStore
    {
        public static IReadOnlyList<VisualizerStoredSnapshot> WriteCollection(
            VisualizerCollectionSummary summary,
            Func<int, VisualizerCollectionItemMetadata> requestMetadata,
            Func<int, VisualizerSnapshotChunkRequest, VisualizerSnapshotChunk> requestChunk)
        {
            if (summary == null)
            {
                throw new ArgumentNullException(nameof(summary));
            }

            if (requestMetadata == null)
            {
                throw new ArgumentNullException(nameof(requestMetadata));
            }

            if (requestChunk == null)
            {
                throw new ArgumentNullException(nameof(requestChunk));
            }

            if (summary.TotalCount <= 0)
            {
                throw new InvalidOperationException("Collection contains no items.");
            }

            var results = new List<VisualizerStoredSnapshot>();
            for (var index = 0; index < summary.ItemCount; index++)
            {
                var displayName = "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                VisualizerCollectionItemMetadata? item = null;
                try
                {
                    item = requestMetadata(index);
                    if (item != null && !string.IsNullOrWhiteSpace(item.DisplayName))
                    {
                        displayName = item.DisplayName;
                    }

                    if (item == null || item.Metadata == null)
                    {
                        throw new InvalidOperationException(
                            item == null || string.IsNullOrWhiteSpace(item.Error)
                                ? "Collection item returned no metadata."
                                : item.Error);
                    }

                    var metadataPath = WriteSnapshot(
                        item.Metadata,
                        request => requestChunk(index, request));
                    results.Add(VisualizerStoredSnapshot.CreateSuccess(
                        displayName,
                        item.Metadata.SourceType,
                        metadataPath,
                        item.Metadata.ProcessId,
                        item.Metadata.BufferAddress,
                        item.Metadata.SourcePointerAddress,
                        item.Metadata.SourcePointerLabel));
                }
                catch (Exception ex)
                {
                    results.Add(VisualizerStoredSnapshot.CreateError(
                        displayName,
                        item == null || string.IsNullOrWhiteSpace(item.ItemTypeName) ? summary.SourceType : item.ItemTypeName,
                        ex.Message,
                        item == null ? null : item.MemberInventory,
                        item == null ? string.Empty : item.ItemAssemblyName,
                        item == null ? 0 : item.DebuggeeProcessId));
                }
            }

            if (summary.TotalCount > summary.ItemCount)
            {
                results.Add(VisualizerStoredSnapshot.CreateError(
                    "Image collection",
                    summary.SourceType,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Collection contains {0:N0} items; only the first {1:N0} are shown.",
                        summary.TotalCount,
                        summary.ItemCount)));
            }

            return results;
        }

        public static string WriteSnapshot(
            VisualizerSnapshotMetadata metadata,
            Func<VisualizerSnapshotChunkRequest, VisualizerSnapshotChunk> requestChunk)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            if (requestChunk == null)
            {
                throw new ArgumentNullException(nameof(requestChunk));
            }

            if (metadata.ChunkSize <= 0)
            {
                throw new InvalidOperationException("Chunk size must be greater than zero.");
            }

            var snapshotDirectory = VisualStudioTempStore.CreateSnapshotDirectory();
            try
            {
                EnsureTempDiskSpace(snapshotDirectory, metadata.BufferLength);
                var metadataPath = Path.Combine(snapshotDirectory, "snapshot_" + DateTime.UtcNow.Ticks + ".rbuf.json");
                var rawPath = RawBufferSnapshot.SaveMetadata(metadataPath, metadata.Descriptor);

                using (var stream = File.Create(rawPath))
                {
                    long offset = 0;
                    while (offset < metadata.BufferLength)
                    {
                        var count = (int)Math.Min(metadata.ChunkSize, metadata.BufferLength - offset);
                        var chunk = requestChunk(new VisualizerSnapshotChunkRequest
                        {
                            Offset = offset,
                            Count = count
                        });

                        ValidateChunk(metadata, chunk, offset, count);
                        stream.Write(chunk.Buffer, 0, chunk.Buffer.Length);
                        offset += chunk.Buffer.Length;
                    }
                }

                return metadataPath;
            }
            catch
            {
                VisualStudioTempStore.TryDeleteDirectory(snapshotDirectory);
                throw;
            }
        }

        private static void ValidateChunk(
            VisualizerSnapshotMetadata metadata,
            VisualizerSnapshotChunk chunk,
            long offset,
            int count)
        {
            if (chunk == null || chunk.Buffer == null || chunk.Buffer.Length == 0)
            {
                throw new InvalidOperationException("Visualizer object source returned an empty chunk.");
            }

            if (chunk.Offset != offset)
            {
                throw new InvalidOperationException("Visualizer object source returned a chunk at the wrong offset.");
            }

            if (chunk.TotalLength != metadata.BufferLength)
            {
                throw new InvalidOperationException("Visualizer object source returned a chunk for the wrong buffer length.");
            }

            if (chunk.Buffer.Length > count || offset + chunk.Buffer.Length > metadata.BufferLength)
            {
                throw new InvalidOperationException("Visualizer object source returned an invalid chunk length.");
            }
        }

        private static void EnsureTempDiskSpace(string snapshotDirectory, long bufferLength)
        {
            if (bufferLength <= 0)
            {
                return;
            }

            var root = Path.GetPathRoot(Path.GetFullPath(snapshotDirectory));
            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            var drive = new DriveInfo(root);
            const long safetyMargin = 512L * 1024L * 1024L;
            var required = bufferLength + safetyMargin;
            if (drive.AvailableFreeSpace < required)
            {
                throw new IOException(string.Format(
                    CultureInfo.InvariantCulture,
                    "Not enough temporary disk space for this image. Required about {0:N0} bytes, available {1:N0} bytes.",
                    required,
                    drive.AvailableFreeSpace));
            }
        }
    }

    public sealed class VisualizerStoredSnapshot
    {
        private VisualizerStoredSnapshot(
            string displayName,
            string sourceType,
            string metadataPath,
            string errorMessage,
            List<VisualizerMemberInventoryItem>? memberInventory,
            string itemAssemblyName,
            int debuggeeProcessId,
            long debuggeeBufferAddress,
            long sourcePointerAddress,
            string sourcePointerLabel)
        {
            DisplayName = displayName ?? string.Empty;
            SourceType = sourceType ?? string.Empty;
            MetadataPath = metadataPath ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
            MemberInventory = memberInventory;
            ItemAssemblyName = itemAssemblyName ?? string.Empty;
            DebuggeeProcessId = debuggeeProcessId;
            DebuggeeBufferAddress = debuggeeBufferAddress;
            SourcePointerAddress = sourcePointerAddress;
            SourcePointerLabel = sourcePointerLabel ?? string.Empty;
        }

        public string DisplayName { get; }
        public string SourceType { get; }
        public string MetadataPath { get; }
        public string ErrorMessage { get; }
        public List<VisualizerMemberInventoryItem>? MemberInventory { get; }
        public string ItemAssemblyName { get; }
        public int DebuggeeProcessId { get; }
        public long DebuggeeBufferAddress { get; }
        public long SourcePointerAddress { get; }
        public string SourcePointerLabel { get; }

        public bool IsError
        {
            get { return !string.IsNullOrWhiteSpace(ErrorMessage); }
        }

        public static VisualizerStoredSnapshot CreateSuccess(
            string displayName,
            string sourceType,
            string metadataPath,
            int debuggeeProcessId = 0,
            long debuggeeBufferAddress = 0,
            long sourcePointerAddress = 0,
            string? sourcePointerLabel = null)
        {
            return new VisualizerStoredSnapshot(
                displayName,
                sourceType,
                metadataPath,
                string.Empty,
                null,
                string.Empty,
                debuggeeProcessId,
                debuggeeBufferAddress,
                sourcePointerAddress,
                sourcePointerLabel ?? string.Empty);
        }

        public static VisualizerStoredSnapshot CreateError(
            string displayName,
            string sourceType,
            string errorMessage,
            List<VisualizerMemberInventoryItem>? memberInventory = null,
            string? itemAssemblyName = null,
            int debuggeeProcessId = 0)
        {
            return new VisualizerStoredSnapshot(
                displayName,
                sourceType,
                string.Empty,
                errorMessage,
                memberInventory,
                itemAssemblyName ?? string.Empty,
                debuggeeProcessId,
                0,
                0,
                string.Empty);
        }
    }
}
