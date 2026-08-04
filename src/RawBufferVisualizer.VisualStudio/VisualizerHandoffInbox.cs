using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.VisualStudio.ObjectSource;

namespace RawBufferVisualizer.VisualStudio
{
    public static class VisualizerHandoffInbox
    {
        private const string RequestSuffix = ".rbuf-handoff";
        private const string ClaimSuffix = ".claim";
        private const string ProcessingSuffix = ".processing.";
        private const string AcknowledgementSuffix = ".ack";
        private const string RejectionSuffix = ".nack";
        private const string RejectionReasonSuffix = ".reason";
        private const string PublishingSuffix = ".publishing.";
        private const int TerminalPublishAttemptCount = 10;
        private const int TerminalPublishRetryDelayMilliseconds = 50;

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
            string? handoffId = null,
            List<VisualizerMemberInventoryItem>? memberInventory = null,
            string? itemAssemblyName = null,
            int debuggeeProcessId = 0)
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
                    false,
                    0,
                    0,
                    0,
                    null,
                    memberInventory,
                    itemAssemblyName,
                    debuggeeProcessId));
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
                    liveDescriptor,
                    loaded.MemberInventory,
                    loaded.ItemAssemblyName,
                    loaded.DebuggeeProcessId);
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

        public static bool TryClaimRequest(string requestPath, out string processingPath)
        {
            var fullRequestPath = NormalizeRequestPath(requestPath);
            processingPath = fullRequestPath
                + ProcessingSuffix
                + Guid.NewGuid().ToString("N");
            var claimPath = fullRequestPath + ClaimSuffix;
            try
            {
                using (var claim = new FileStream(
                    claimPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    1,
                    FileOptions.DeleteOnClose))
                {
                    File.Move(fullRequestPath, processingPath);
                    return true;
                }
            }
            catch (IOException)
            {
                processingPath = string.Empty;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                processingPath = string.Empty;
                return false;
            }
            finally
            {
                TryDeleteRequest(claimPath);
            }
        }

        public static bool TryAcknowledgeRequest(string requestPath, string processingPath)
        {
            return TryMoveClaimToMarker(
                requestPath,
                processingPath,
                GetAcknowledgementPath(requestPath));
        }

        public static bool TryRejectRequest(string requestPath, string processingPath, string? reason = null)
        {
            var reasonPath = GetRejectionReasonPath(requestPath);
            var rejectionPath = GetRejectionPath(requestPath);
            var reasonPublished = !string.IsNullOrWhiteSpace(reason)
                && TryWriteTextAtomically(reasonPath, reason!);
            if (!TryMoveClaimToMarker(
                    requestPath,
                    processingPath,
                    rejectionPath))
            {
                if (reasonPublished && !File.Exists(rejectionPath))
                {
                    TryDeleteRequest(reasonPath);
                }

                return false;
            }

            return true;
        }

        public static VisualizerHandoffRequestState GetRequestState(string requestPath)
        {
            var fullRequestPath = NormalizeRequestPath(requestPath);
            var acknowledgementExists = File.Exists(GetAcknowledgementPath(fullRequestPath));
            var rejectionExists = File.Exists(GetRejectionPath(fullRequestPath));
            if (acknowledgementExists && rejectionExists)
            {
                return VisualizerHandoffRequestState.Conflicted;
            }

            if (acknowledgementExists)
            {
                return VisualizerHandoffRequestState.Acknowledged;
            }

            if (rejectionExists)
            {
                return VisualizerHandoffRequestState.Rejected;
            }

            if (File.Exists(fullRequestPath))
            {
                return VisualizerHandoffRequestState.Ready;
            }

            return HasProcessingRequest(fullRequestPath)
                ? VisualizerHandoffRequestState.Processing
                : VisualizerHandoffRequestState.Missing;
        }

        public static string GetAcknowledgementPath(string requestPath)
        {
            return NormalizeRequestPath(requestPath) + AcknowledgementSuffix;
        }

        public static string GetRejectionPath(string requestPath)
        {
            return NormalizeRequestPath(requestPath) + RejectionSuffix;
        }

        public static bool TryReadRejectionReason(string requestPath, out string reason)
        {
            reason = string.Empty;
            try
            {
                var reasonPath = GetRejectionReasonPath(requestPath);
                if (!File.Exists(reasonPath))
                {
                    return false;
                }

                reason = File.ReadAllText(reasonPath);
                return true;
            }
            catch (IOException)
            {
                reason = string.Empty;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                reason = string.Empty;
                return false;
            }
        }

        public static void CleanupRequestArtifacts(string requestPath)
        {
            var fullRequestPath = NormalizeRequestPath(requestPath);
            TryDeleteRequest(fullRequestPath);
            TryDeleteRequest(GetAcknowledgementPath(fullRequestPath));
            TryDeleteRequest(GetRejectionPath(fullRequestPath));
            TryDeleteRequest(GetRejectionReasonPath(fullRequestPath));
            TryDeleteRequest(fullRequestPath + ClaimSuffix);
            TryDeleteMatchingFiles(fullRequestPath + ProcessingSuffix + "*");
            TryDeleteMatchingFiles(fullRequestPath + PublishingSuffix + "*");
            TryDeleteMatchingFiles(
                GetRejectionReasonPath(fullRequestPath) + PublishingSuffix + "*");
        }

        public static void ScheduleTerminalArtifactCleanup(
            IEnumerable<string> requestPaths,
            TimeSpan maxWait)
        {
            if (requestPaths == null)
            {
                throw new ArgumentNullException("requestPaths");
            }

            if (maxWait <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    "maxWait",
                    "Terminal cleanup wait must be positive.");
            }

            var pending = new List<string>();
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var requestPath in requestPaths)
            {
                var fullRequestPath = NormalizeRequestPath(requestPath);
                if (unique.Add(fullRequestPath))
                {
                    pending.Add(fullRequestPath);
                }
            }

            if (pending.Count == 0)
            {
                return;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                var deadline = DateTime.UtcNow.Add(maxWait);
                var terminalObserved = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
                var consecutiveMissing = new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);
                while (pending.Count > 0 && DateTime.UtcNow < deadline)
                {
                    for (var index = pending.Count - 1; index >= 0; index--)
                    {
                        try
                        {
                            var requestPath = pending[index];
                            if (terminalObserved.Contains(requestPath))
                            {
                                CleanupRequestArtifacts(requestPath);
                                if (!HasRequestArtifacts(requestPath))
                                {
                                    pending.RemoveAt(index);
                                    consecutiveMissing.Remove(requestPath);
                                }

                                continue;
                            }

                            var state = GetRequestState(requestPath);
                            if (state == VisualizerHandoffRequestState.Acknowledged
                                || state == VisualizerHandoffRequestState.Rejected
                                || state == VisualizerHandoffRequestState.Conflicted)
                            {
                                terminalObserved.Add(requestPath);
                                CleanupRequestArtifacts(requestPath);
                                if (!HasRequestArtifacts(requestPath))
                                {
                                    pending.RemoveAt(index);
                                    consecutiveMissing.Remove(requestPath);
                                }
                            }
                            else if (state == VisualizerHandoffRequestState.Missing)
                            {
                                int count;
                                consecutiveMissing.TryGetValue(
                                    requestPath,
                                    out count);
                                count++;
                                if (count >= 2)
                                {
                                    pending.RemoveAt(index);
                                    consecutiveMissing.Remove(requestPath);
                                }
                                else
                                {
                                    consecutiveMissing[requestPath] = count;
                                }
                            }
                            else
                            {
                                consecutiveMissing.Remove(requestPath);
                            }
                        }
                        catch
                        {
                            // A later pass can retry transient inbox access failures.
                        }
                    }

                    if (pending.Count > 0)
                    {
                        Thread.Sleep(100);
                    }
                }
            });
        }

        private static bool HasRequestArtifacts(string requestPath)
        {
            var fullRequestPath = NormalizeRequestPath(requestPath);
            if (File.Exists(fullRequestPath)
                || File.Exists(GetAcknowledgementPath(fullRequestPath))
                || File.Exists(GetRejectionPath(fullRequestPath))
                || File.Exists(GetRejectionReasonPath(fullRequestPath))
                || File.Exists(fullRequestPath + ClaimSuffix))
            {
                return true;
            }

            return HasMatchingFiles(fullRequestPath + ProcessingSuffix + "*")
                || HasMatchingFiles(fullRequestPath + PublishingSuffix + "*")
                || HasMatchingFiles(
                    GetRejectionReasonPath(fullRequestPath)
                    + PublishingSuffix
                    + "*");
        }

        private static bool HasMatchingFiles(string patternPath)
        {
            var directory = Path.GetDirectoryName(patternPath);
            return !string.IsNullOrWhiteSpace(directory)
                && Directory.Exists(directory)
                && Directory.GetFiles(
                    directory,
                    Path.GetFileName(patternPath),
                    SearchOption.TopDirectoryOnly).Length > 0;
        }

        private static string WriteRequest(int visualStudioProcessId, VisualizerHandoffRequest request)
        {
            var inboxDirectory = GetInboxDirectory(visualStudioProcessId);
            Directory.CreateDirectory(inboxDirectory);
            var fileName = Guid.NewGuid().ToString("N").Substring(0, 24) + RequestSuffix;
            var requestPath = Path.Combine(inboxDirectory, fileName);
            var publishingPath = requestPath
                + PublishingSuffix
                + Guid.NewGuid().ToString("N");
            try
            {
                WriteRequestFile(publishingPath, request);
                File.Move(publishingPath, requestPath);
            }
            finally
            {
                TryDeleteRequest(publishingPath);
            }

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
                LiveByteOrder = request.LiveDescriptor == null ? 0 : (int)request.LiveDescriptor.ByteOrder,
                MemberInventory = request.MemberInventory,
                ItemAssemblyName = request.ItemAssemblyName,
                DebuggeeProcessId = request.DebuggeeProcessId
            };

            using (var stream = new MemoryStream())
            {
                var serializer = new DataContractJsonSerializer(typeof(VisualizerHandoffRequestDto));
                serializer.WriteObject(stream, dto);
                File.WriteAllText(requestPath, Encoding.UTF8.GetString(stream.ToArray()));
            }
        }

        private static bool TryMoveClaimToMarker(
            string requestPath,
            string processingPath,
            string markerPath)
        {
            var fullRequestPath = NormalizeRequestPath(requestPath);
            var fullProcessingPath = Path.GetFullPath(processingPath);
            ValidateProcessingPath(fullRequestPath, fullProcessingPath);
            if (File.Exists(GetAcknowledgementPath(fullRequestPath))
                || File.Exists(GetRejectionPath(fullRequestPath)))
            {
                return false;
            }

            for (var attempt = 0; attempt < TerminalPublishAttemptCount; attempt++)
            {
                try
                {
                    File.Move(fullProcessingPath, markerPath);
                    return true;
                }
                catch (Exception ex) when (
                    ex is IOException
                    || ex is UnauthorizedAccessException)
                {
                    if (File.Exists(markerPath)
                        || !File.Exists(fullProcessingPath))
                    {
                        return false;
                    }

                    if (attempt + 1 < TerminalPublishAttemptCount)
                    {
                        Thread.Sleep(TerminalPublishRetryDelayMilliseconds);
                    }
                }
            }

            return false;
        }

        private static bool HasProcessingRequest(string requestPath)
        {
            var directory = Path.GetDirectoryName(requestPath);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return false;
            }

            return Directory.GetFiles(
                directory,
                Path.GetFileName(requestPath) + ProcessingSuffix + "*",
                SearchOption.TopDirectoryOnly).Length > 0;
        }

        private static string GetRejectionReasonPath(string requestPath)
        {
            return GetRejectionPath(requestPath) + RejectionReasonSuffix;
        }

        private static string NormalizeRequestPath(string requestPath)
        {
            if (string.IsNullOrWhiteSpace(requestPath))
            {
                throw new ArgumentException("Request path is required.", "requestPath");
            }

            var fullRequestPath = Path.GetFullPath(requestPath);
            if (!Path.GetFileName(fullRequestPath).EndsWith(
                    RequestSuffix,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Request path must end with " + RequestSuffix + ".",
                    "requestPath");
            }

            return fullRequestPath;
        }

        private static void ValidateProcessingPath(
            string requestPath,
            string processingPath)
        {
            var expectedPrefix = requestPath + ProcessingSuffix;
            if (!processingPath.StartsWith(
                    expectedPrefix,
                    StringComparison.OrdinalIgnoreCase)
                || processingPath.Length <= expectedPrefix.Length
                || processingPath.IndexOf(
                    Path.DirectorySeparatorChar,
                    expectedPrefix.Length) >= 0
                || processingPath.IndexOf(
                    Path.AltDirectorySeparatorChar,
                    expectedPrefix.Length) >= 0)
            {
                throw new ArgumentException(
                    "Processing path does not belong to the handoff request.",
                    "processingPath");
            }
        }

        private static bool TryWriteTextAtomically(string path, string text)
        {
            var publishingPath = path
                + PublishingSuffix
                + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(publishingPath, text, new UTF8Encoding(false));
                File.Move(publishingPath, path);
                return true;
            }
            catch (IOException)
            {
                // The terminal NACK is authoritative; an optional reason must not
                // invalidate completion if another observer wins the write race.
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                // The terminal NACK is authoritative; an optional reason must not
                // invalidate completion if security software temporarily owns it.
                return false;
            }
            finally
            {
                TryDeleteRequest(publishingPath);
            }
        }

        private static void TryDeleteMatchingFiles(string patternPath)
        {
            try
            {
                var directory = Path.GetDirectoryName(patternPath);
                if (string.IsNullOrWhiteSpace(directory)
                    || !Directory.Exists(directory))
                {
                    return;
                }

                foreach (var path in Directory.GetFiles(
                    directory,
                    Path.GetFileName(patternPath),
                    SearchOption.TopDirectoryOnly))
                {
                    TryDeleteRequest(path);
                }
            }
            catch
            {
                // Temp handoff cleanup must not affect visualizer display.
            }
        }
    }

    public enum VisualizerHandoffRequestState
    {
        Missing = 0,
        Ready = 1,
        Processing = 2,
        Acknowledged = 3,
        Rejected = 4,
        Conflicted = 5
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
        public List<VisualizerMemberInventoryItem>? MemberInventory { get; private set; }
        public string ItemAssemblyName { get; private set; }
        public int DebuggeeProcessId { get; private set; }

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
            RawImageDescriptor? liveDescriptor = null,
            List<VisualizerMemberInventoryItem>? memberInventory = null,
            string? itemAssemblyName = null,
            int debuggeeProcessId = 0)
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
            MemberInventory = memberInventory;
            ItemAssemblyName = itemAssemblyName ?? string.Empty;
            DebuggeeProcessId = debuggeeProcessId;
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

        [DataMember(Name = "memberInventory", EmitDefaultValue = false)]
        public List<VisualizerMemberInventoryItem>? MemberInventory { get; set; }

        [DataMember(Name = "itemAssemblyName", EmitDefaultValue = false)]
        public string? ItemAssemblyName { get; set; }

        [DataMember(Name = "debuggeeProcessId", EmitDefaultValue = false)]
        public int DebuggeeProcessId { get; set; }
    }
}
