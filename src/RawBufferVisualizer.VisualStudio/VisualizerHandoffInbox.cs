using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace RawBufferVisualizer.VisualStudio
{
    public static class VisualizerHandoffInbox
    {
        public static string InboxDirectory
        {
            get { return Path.Combine(VisualStudioTempStore.RootDirectory, "Inbox"); }
        }

        public static string WriteSnapshotRequest(string metadataPath, string? displayName = null, string? sourceType = null)
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
            WriteRequestFile(
                requestPath,
                new VisualizerHandoffRequest(
                    Path.GetFullPath(metadataPath),
                    displayName ?? string.Empty,
                    sourceType ?? string.Empty));
            return requestPath;
        }

        public static string ReadSnapshotRequest(string requestPath)
        {
            return ReadSnapshotRequestInfo(requestPath).MetadataPath;
        }

        public static VisualizerHandoffRequest ReadSnapshotRequestInfo(string requestPath)
        {
            if (string.IsNullOrWhiteSpace(requestPath))
            {
                throw new ArgumentException("Request path is required.", "requestPath");
            }

            var text = File.ReadAllText(requestPath).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidDataException("Handoff request did not contain a metadata path.");
            }

            if (text[0] != '{')
            {
                return new VisualizerHandoffRequest(Path.GetFullPath(text), string.Empty, string.Empty);
            }

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(text)))
            {
                var serializer = new DataContractJsonSerializer(typeof(VisualizerHandoffRequestDto));
                var loaded = serializer.ReadObject(stream) as VisualizerHandoffRequestDto;
                if (loaded == null || string.IsNullOrWhiteSpace(loaded.MetadataPath))
                {
                    throw new InvalidDataException("Handoff request did not contain a metadata path.");
                }

                return new VisualizerHandoffRequest(
                    Path.GetFullPath(loaded.MetadataPath),
                    loaded.DisplayName ?? string.Empty,
                    loaded.SourceType ?? string.Empty);
            }
        }

        private static void WriteRequestFile(string requestPath, VisualizerHandoffRequest request)
        {
            var dto = new VisualizerHandoffRequestDto
            {
                MetadataPath = request.MetadataPath,
                DisplayName = request.DisplayName,
                SourceType = request.SourceType
            };

            using (var stream = new MemoryStream())
            {
                var serializer = new DataContractJsonSerializer(typeof(VisualizerHandoffRequestDto));
                serializer.WriteObject(stream, dto);
                File.WriteAllText(requestPath, Encoding.UTF8.GetString(stream.ToArray()));
            }
        }
    }

    public sealed class VisualizerHandoffRequest
    {
        public string MetadataPath { get; private set; }
        public string DisplayName { get; private set; }
        public string SourceType { get; private set; }

        public VisualizerHandoffRequest(string metadataPath, string displayName, string sourceType)
        {
            if (string.IsNullOrWhiteSpace(metadataPath))
            {
                throw new ArgumentException("Metadata path is required.", "metadataPath");
            }

            MetadataPath = Path.GetFullPath(metadataPath);
            DisplayName = displayName ?? string.Empty;
            SourceType = sourceType ?? string.Empty;
        }
    }

    [DataContract]
    internal sealed class VisualizerHandoffRequestDto
    {
        [DataMember(Name = "metadataPath")]
        public string? MetadataPath { get; set; }

        [DataMember(Name = "displayName")]
        public string? DisplayName { get; set; }

        [DataMember(Name = "sourceType")]
        public string? SourceType { get; set; }
    }
}
