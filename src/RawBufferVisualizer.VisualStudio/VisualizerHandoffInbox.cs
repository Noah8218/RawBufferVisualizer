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
        public static string GetInboxDirectory(int visualStudioProcessId)
        {
            if (visualStudioProcessId <= 0)
            {
                throw new ArgumentOutOfRangeException("visualStudioProcessId", "Visual Studio process ID must be positive.");
            }

            return Path.Combine(
                VisualStudioTempStore.RootDirectory,
                "Inbox",
                visualStudioProcessId.ToString(CultureInfo.InvariantCulture));
        }

        public static string WriteSnapshotRequest(
            int visualStudioProcessId,
            string metadataPath,
            string? displayName = null,
            string? sourceType = null)
        {
            if (string.IsNullOrWhiteSpace(metadataPath))
            {
                throw new ArgumentException("Metadata path is required.", "metadataPath");
            }

            return WriteRequest(
                visualStudioProcessId,
                new VisualizerHandoffRequest(
                    Path.GetFullPath(metadataPath),
                    displayName ?? string.Empty,
                    sourceType ?? string.Empty));
        }

        public static string WriteErrorRequest(
            int visualStudioProcessId,
            string? displayName,
            string? sourceType,
            string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new ArgumentException("Error message is required.", "errorMessage");
            }

            return WriteRequest(
                visualStudioProcessId,
                new VisualizerHandoffRequest(
                    string.Empty,
                    displayName ?? string.Empty,
                    sourceType ?? string.Empty,
                    errorMessage));
        }

        public static string ReadSnapshotRequest(string requestPath)
        {
            var request = ReadSnapshotRequestInfo(requestPath);
            if (request.IsError)
            {
                throw new InvalidDataException("Handoff request contains an error instead of a metadata path.");
            }

            return request.MetadataPath;
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
                if (loaded == null
                    || (string.IsNullOrWhiteSpace(loaded.MetadataPath)
                        && string.IsNullOrWhiteSpace(loaded.ErrorMessage)))
                {
                    throw new InvalidDataException("Handoff request did not contain a metadata path or an error message.");
                }

                return new VisualizerHandoffRequest(
                    loaded.MetadataPath ?? string.Empty,
                    loaded.DisplayName ?? string.Empty,
                    loaded.SourceType ?? string.Empty,
                    loaded.ErrorMessage ?? string.Empty);
            }
        }

        public static void TryDeleteRequest(string requestPath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(requestPath) && File.Exists(requestPath))
                {
                    File.Delete(requestPath);
                }
            }
            catch
            {
                // Temp handoff cleanup must not affect visualizer display.
            }
        }

        private static string WriteRequest(int visualStudioProcessId, VisualizerHandoffRequest request)
        {
            var inboxDirectory = GetInboxDirectory(visualStudioProcessId);
            Directory.CreateDirectory(inboxDirectory);
            var fileName = string.Format(
                CultureInfo.InvariantCulture,
                "{0}_{1:N}.rbuf-handoff",
                DateTime.UtcNow.ToString("yyyyMMddHHmmssfffffff", CultureInfo.InvariantCulture),
                Guid.NewGuid());
            var requestPath = Path.Combine(inboxDirectory, fileName);
            WriteRequestFile(requestPath, request);
            return requestPath;
        }

        private static void WriteRequestFile(string requestPath, VisualizerHandoffRequest request)
        {
            var dto = new VisualizerHandoffRequestDto
            {
                MetadataPath = request.MetadataPath,
                DisplayName = request.DisplayName,
                SourceType = request.SourceType,
                ErrorMessage = request.ErrorMessage
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
        public string ErrorMessage { get; private set; }

        public bool IsError
        {
            get { return !string.IsNullOrWhiteSpace(ErrorMessage); }
        }

        public VisualizerHandoffRequest(string metadataPath, string displayName, string sourceType)
            : this(metadataPath, displayName, sourceType, string.Empty)
        {
        }

        public VisualizerHandoffRequest(string metadataPath, string displayName, string sourceType, string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(metadataPath) && string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new ArgumentException("A metadata path or error message is required.", "metadataPath");
            }

            MetadataPath = string.IsNullOrWhiteSpace(metadataPath) ? string.Empty : Path.GetFullPath(metadataPath);
            DisplayName = displayName ?? string.Empty;
            SourceType = sourceType ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
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

        [DataMember(Name = "errorMessage")]
        public string? ErrorMessage { get; set; }
    }
}
