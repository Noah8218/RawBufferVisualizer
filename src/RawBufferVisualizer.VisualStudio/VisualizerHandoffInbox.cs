using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using RawBufferVisualizer.Core;

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
            string? sourceType = null,
            string? handoffId = null,
            bool isPreview = false)
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
                    sourceType ?? string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    handoffId ?? string.Empty,
                    isPreview));
        }

        public static string WriteErrorRequest(
            int visualStudioProcessId,
            string? displayName,
            string? sourceType,
            string errorMessage,
            string? errorType = null,
            string? errorDetails = null,
            string? handoffId = null)
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
                    errorMessage,
                    errorType ?? string.Empty,
                    errorDetails ?? string.Empty,
                    handoffId ?? string.Empty,
                    false));
        }

        public static string WriteLiveMemoryRequest(
            int visualStudioProcessId,
            int debuggeeProcessId,
            long bufferAddress,
            long bufferLength,
            RawImageDescriptor descriptor,
            string? displayName = null,
            string? sourceType = null,
            string? handoffId = null)
        {
            if (debuggeeProcessId <= 0)
            {
                throw new ArgumentOutOfRangeException("debuggeeProcessId");
            }

            if (bufferAddress == 0)
            {
                throw new ArgumentException("Buffer address is required.", "bufferAddress");
            }

            if (descriptor == null)
            {
                throw new ArgumentNullException("descriptor");
            }

            return WriteRequest(
                visualStudioProcessId,
                new VisualizerHandoffRequest(
                    string.Empty,
                    displayName ?? string.Empty,
                    sourceType ?? string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    handoffId ?? string.Empty,
                    false,
                    debuggeeProcessId,
                    bufferAddress,
                    bufferLength,
                    descriptor));
        }

        public static string ReadSnapshotRequest(string requestPath)
        {
            var request = ReadSnapshotRequestInfo(requestPath);
            if (request.IsError)
            {
                throw new InvalidDataException("Handoff request contains an error instead of a metadata path.");
            }

            if (request.IsLiveMemory)
            {
                throw new InvalidDataException("Handoff request contains live debugger memory instead of a metadata path.");
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
                        && string.IsNullOrWhiteSpace(loaded.ErrorMessage)
                        && loaded.LiveProcessId <= 0))
                {
                    throw new InvalidDataException("Handoff request did not contain an image source or an error message.");
                }

                RawImageDescriptor? liveDescriptor = null;
                if (loaded.LiveProcessId > 0)
                {
                    liveDescriptor = new RawImageDescriptor
                    {
                        Width = loaded.LiveWidth,
                        Height = loaded.LiveHeight,
                        Stride = loaded.LiveStride,
                        PixelFormat = (RawPixelFormat)loaded.LivePixelFormat,
                        ValidBits = loaded.LiveValidBits,
                        ByteOrder = (RawByteOrder)loaded.LiveByteOrder
                    };
                }

                return new VisualizerHandoffRequest(
                    loaded.MetadataPath ?? string.Empty,
                    loaded.DisplayName ?? string.Empty,
                    loaded.SourceType ?? string.Empty,
                    loaded.ErrorMessage ?? string.Empty,
                    loaded.ErrorType ?? string.Empty,
                    loaded.ErrorDetails ?? string.Empty,
                    loaded.HandoffId ?? string.Empty,
                    loaded.IsPreview,
                    loaded.LiveProcessId,
                    loaded.LiveBufferAddress,
                    loaded.LiveBufferLength,
                    liveDescriptor);
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
                ErrorMessage = request.ErrorMessage,
                ErrorType = request.ErrorType,
                ErrorDetails = request.ErrorDetails,
                HandoffId = request.HandoffId,
                IsPreview = request.IsPreview,
                LiveProcessId = request.LiveProcessId,
                LiveBufferAddress = request.LiveBufferAddress,
                LiveBufferLength = request.LiveBufferLength,
                LiveWidth = request.LiveDescriptor == null ? 0 : request.LiveDescriptor.Width,
                LiveHeight = request.LiveDescriptor == null ? 0 : request.LiveDescriptor.Height,
                LiveStride = request.LiveDescriptor == null ? 0 : request.LiveDescriptor.Stride,
                LivePixelFormat = request.LiveDescriptor == null ? 0 : (int)request.LiveDescriptor.PixelFormat,
                LiveValidBits = request.LiveDescriptor == null ? 0 : request.LiveDescriptor.ValidBits,
                LiveByteOrder = request.LiveDescriptor == null ? 0 : (int)request.LiveDescriptor.ByteOrder
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
        public string ErrorType { get; private set; }
        public string ErrorDetails { get; private set; }
        public string HandoffId { get; private set; }
        public bool IsPreview { get; private set; }
        public int LiveProcessId { get; private set; }
        public long LiveBufferAddress { get; private set; }
        public long LiveBufferLength { get; private set; }
        public RawImageDescriptor? LiveDescriptor { get; private set; }

        public bool IsError
        {
            get { return !string.IsNullOrWhiteSpace(ErrorMessage); }
        }

        public VisualizerHandoffRequest(string metadataPath, string displayName, string sourceType)
            : this(metadataPath, displayName, sourceType, string.Empty, string.Empty, string.Empty, string.Empty, false)
        {
        }

        public bool IsLiveMemory
        {
            get
            {
                return LiveProcessId > 0
                    && LiveBufferAddress != 0
                    && LiveBufferLength > 0
                    && LiveDescriptor != null;
            }
        }

        public VisualizerHandoffRequest(string metadataPath, string displayName, string sourceType, string errorMessage)
            : this(metadataPath, displayName, sourceType, errorMessage, string.Empty, string.Empty, string.Empty, false)
        {
        }

        public VisualizerHandoffRequest(
            string metadataPath,
            string displayName,
            string sourceType,
            string errorMessage,
            string errorType,
            string errorDetails)
            : this(metadataPath, displayName, sourceType, errorMessage, errorType, errorDetails, string.Empty, false)
        {
        }

        public VisualizerHandoffRequest(
            string metadataPath,
            string displayName,
            string sourceType,
            string errorMessage,
            string errorType,
            string errorDetails,
            string handoffId,
            bool isPreview,
            int liveProcessId = 0,
            long liveBufferAddress = 0,
            long liveBufferLength = 0,
            RawImageDescriptor? liveDescriptor = null)
        {
            var hasLiveMemory = liveProcessId > 0
                && liveBufferAddress != 0
                && liveBufferLength > 0
                && liveDescriptor != null;
            if (string.IsNullOrWhiteSpace(metadataPath)
                && string.IsNullOrWhiteSpace(errorMessage)
                && !hasLiveMemory)
            {
                throw new ArgumentException("A metadata path, live memory source, or error message is required.", "metadataPath");
            }

            MetadataPath = string.IsNullOrWhiteSpace(metadataPath) ? string.Empty : Path.GetFullPath(metadataPath);
            DisplayName = displayName ?? string.Empty;
            SourceType = sourceType ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
            ErrorType = errorType ?? string.Empty;
            ErrorDetails = errorDetails ?? string.Empty;
            HandoffId = handoffId ?? string.Empty;
            IsPreview = isPreview;
            LiveProcessId = liveProcessId;
            LiveBufferAddress = liveBufferAddress;
            LiveBufferLength = liveBufferLength;
            LiveDescriptor = liveDescriptor == null ? null : liveDescriptor.Clone();
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

        [DataMember(Name = "errorType", EmitDefaultValue = false)]
        public string? ErrorType { get; set; }

        [DataMember(Name = "errorDetails", EmitDefaultValue = false)]
        public string? ErrorDetails { get; set; }

        [DataMember(Name = "handoffId", EmitDefaultValue = false)]
        public string? HandoffId { get; set; }

        [DataMember(Name = "isPreview", EmitDefaultValue = false)]
        public bool IsPreview { get; set; }

        [DataMember(Name = "liveProcessId", EmitDefaultValue = false)]
        public int LiveProcessId { get; set; }

        [DataMember(Name = "liveBufferAddress", EmitDefaultValue = false)]
        public long LiveBufferAddress { get; set; }

        [DataMember(Name = "liveBufferLength", EmitDefaultValue = false)]
        public long LiveBufferLength { get; set; }

        [DataMember(Name = "liveWidth", EmitDefaultValue = false)]
        public int LiveWidth { get; set; }

        [DataMember(Name = "liveHeight", EmitDefaultValue = false)]
        public int LiveHeight { get; set; }

        [DataMember(Name = "liveStride", EmitDefaultValue = false)]
        public int LiveStride { get; set; }

        [DataMember(Name = "livePixelFormat", EmitDefaultValue = false)]
        public int LivePixelFormat { get; set; }

        [DataMember(Name = "liveValidBits", EmitDefaultValue = false)]
        public int LiveValidBits { get; set; }

        [DataMember(Name = "liveByteOrder", EmitDefaultValue = false)]
        public int LiveByteOrder { get; set; }
    }
}
