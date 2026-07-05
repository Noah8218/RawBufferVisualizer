using System;
using System.Globalization;
using System.IO;

namespace RawBufferVisualizer.VisualStudio
{
    public static class VisualizerHandoffInbox
    {
        public static string InboxDirectory
        {
            get { return Path.Combine(Path.GetTempPath(), "RawBufferVisualizer", "VisualStudio", "Inbox"); }
        }

        public static string WriteSnapshotRequest(string metadataPath)
        {
            if (string.IsNullOrWhiteSpace(metadataPath))
            {
                throw new ArgumentException("Metadata path is required.", "metadataPath");
            }

            Directory.CreateDirectory(InboxDirectory);
            var fileName = string.Format(
                CultureInfo.InvariantCulture,
                "{0}_{1:N}.rbuf-handoff",
                DateTime.UtcNow.ToString("yyyyMMddHHmmssfffffff", CultureInfo.InvariantCulture),
                Guid.NewGuid());
            var requestPath = Path.Combine(InboxDirectory, fileName);
            File.WriteAllText(requestPath, Path.GetFullPath(metadataPath));
            return requestPath;
        }

        public static string ReadSnapshotRequest(string requestPath)
        {
            if (string.IsNullOrWhiteSpace(requestPath))
            {
                throw new ArgumentException("Request path is required.", "requestPath");
            }

            var metadataPath = File.ReadAllText(requestPath).Trim();
            if (string.IsNullOrWhiteSpace(metadataPath))
            {
                throw new InvalidDataException("Handoff request did not contain a metadata path.");
            }

            return Path.GetFullPath(metadataPath);
        }
    }
}
