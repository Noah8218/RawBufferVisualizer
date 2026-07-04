using System;
using System.Diagnostics;
using System.IO;
using RawBufferVisualizer.VisualStudio.ObjectSource;

namespace RawBufferVisualizer.VisualStudio
{
    public static class StandaloneViewerBridge
    {
        public static VisualizerLaunchRequest PrepareLaunch(
            VisualizerSnapshotTransfer transfer,
            string viewerExecutablePath,
            string? snapshotRootDirectory = null)
        {
            if (transfer == null)
            {
                throw new ArgumentNullException(nameof(transfer));
            }

            if (string.IsNullOrWhiteSpace(viewerExecutablePath))
            {
                throw new ArgumentException("Viewer executable path is required.", nameof(viewerExecutablePath));
            }

            var fullViewerPath = Path.GetFullPath(viewerExecutablePath);
            if (!File.Exists(fullViewerPath))
            {
                throw new FileNotFoundException("Raw Buffer Visualizer executable was not found.", fullViewerPath);
            }

            var root = string.IsNullOrWhiteSpace(snapshotRootDirectory)
                ? Path.Combine(Path.GetTempPath(), "RawBufferVisualizer", "VisualStudio")
                : Path.GetFullPath(snapshotRootDirectory);
            var snapshotDirectory = Path.Combine(root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(snapshotDirectory);

            var metadataPath = Path.Combine(snapshotDirectory, GetSnapshotName(transfer.DisplayName) + ".rbuf.json");
            transfer.ToSnapshot().Save(metadataPath);

            return new VisualizerLaunchRequest(
                fullViewerPath,
                metadataPath,
                Path.GetDirectoryName(fullViewerPath) ?? Directory.GetCurrentDirectory());
        }

        public static Process Launch(VisualizerLaunchRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var process = Process.Start(request.CreateStartInfo());
            if (process == null)
            {
                throw new InvalidOperationException("Raw Buffer Visualizer process could not be started.");
            }

            return process;
        }

        private static string GetSnapshotName(string displayName)
        {
            var name = string.IsNullOrWhiteSpace(displayName) ? "snapshot" : displayName.Trim();
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return name.Length <= 64 ? name : name.Substring(0, 64);
        }
    }
}
