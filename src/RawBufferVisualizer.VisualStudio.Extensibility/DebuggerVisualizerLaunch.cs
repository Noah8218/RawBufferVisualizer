using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility.DebuggerVisualizers;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;
using RawBufferVisualizer.Sdk;
using RawBufferVisualizer.VisualStudio;
using RawBufferVisualizer.VisualStudio.ObjectSource;

namespace RawBufferVisualizer.VisualStudio.Extensibility
{
    internal static class DebuggerVisualizerLaunch
    {
        public static async Task<IRemoteUserControl> CreateControlAsync(
            VisualizerTarget visualizerTarget,
            CancellationToken cancellationToken)
        {
            try
            {
                var metadata = await visualizerTarget.ObjectSource.RequestDataAsync<VisualizerSnapshotMetadata>(
                    jsonSerializer: null,
                    cancellationToken);
                if (metadata == null)
                {
                    throw new InvalidOperationException("Visualizer object source returned no data.");
                }

                var snapshotDirectory = CreateSnapshotDirectory();
                var metadataPath = Path.Combine(snapshotDirectory, GetSnapshotName(metadata.DisplayName) + ".rbuf.json");
                var rawPath = RawBufferSnapshot.SaveMetadata(metadataPath, metadata.Descriptor);
                await WriteRawChunksAsync(visualizerTarget.ObjectSource, metadata, rawPath, cancellationToken);

                VisualizerHandoffInbox.WriteSnapshotRequest(metadataPath);
                DockedVisualizerSession.Shared.ReportForwarded(metadata);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                DockedVisualizerSession.Shared.ReportFailure(ex.Message);
            }

            return new DockedVisualizerControl();
        }

        private static string GetSnapshotName(string displayName)
        {
            var name = string.IsNullOrWhiteSpace(displayName) ? "snapshot" : displayName.Trim();
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return string.Format(CultureInfo.InvariantCulture, "{0}_{1}", name.Length <= 64 ? name : name.Substring(0, 64), DateTime.UtcNow.Ticks);
        }

        private static string CreateSnapshotDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizer", "VisualStudio", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static async Task WriteRawChunksAsync(
            VisualizerObjectSourceClient objectSource,
            VisualizerSnapshotMetadata metadata,
            string rawPath,
            CancellationToken cancellationToken)
        {
            if (metadata.ChunkSize <= 0)
            {
                throw new InvalidOperationException("Chunk size must be greater than zero.");
            }

            using var stream = File.Create(rawPath);
            long offset = 0;
            while (offset < metadata.BufferLength)
            {
                var count = (int)Math.Min(metadata.ChunkSize, metadata.BufferLength - offset);
                var chunk = await objectSource.RequestDataAsync<VisualizerSnapshotChunkRequest, VisualizerSnapshotChunk>(
                    new VisualizerSnapshotChunkRequest
                    {
                        Offset = offset,
                        Count = count
                    },
                    jsonSerializer: null,
                    cancellationToken);

                if (chunk == null || chunk.Buffer == null)
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

                if (chunk.Buffer.Length == 0)
                {
                    throw new InvalidOperationException("Visualizer object source returned an empty chunk.");
                }

                if (chunk.Buffer.Length > count || offset + chunk.Buffer.Length > metadata.BufferLength)
                {
                    throw new InvalidOperationException("Visualizer object source returned an invalid chunk length.");
                }

                await stream.WriteAsync(chunk.Buffer, cancellationToken);
                offset += chunk.Buffer.Length;
            }

            if (offset != metadata.BufferLength)
            {
                throw new InvalidOperationException("Visualizer object source returned an incomplete buffer.");
            }
        }
    }
}
