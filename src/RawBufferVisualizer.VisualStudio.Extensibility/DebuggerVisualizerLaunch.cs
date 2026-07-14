using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
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
        private static readonly TimeSpan HandoffAcknowledgementTimeout = TimeSpan.FromSeconds(30);

        public static async Task<IRemoteUserControl> CreateControlAsync(
            VisualizerTarget visualizerTarget,
            CancellationToken cancellationToken)
        {
            var session = new DockedVisualizerSession();
            var visualStudioProcessId = 0;
            var displayName = visualizerTarget.TargetTypeFullName ?? "Debugger visualizer";
            var sourceType = displayName;
            var closeWhenLoaded = false;
            string? snapshotDirectory = null;
            string? requestPath = null;
            try
            {
                visualStudioProcessId = VisualStudioInstance.GetCurrentProcessId();
                var metadata = await visualizerTarget.ObjectSource.RequestDataAsync<VisualizerSnapshotMetadata>(
                    jsonSerializer: null,
                    cancellationToken);
                if (metadata == null)
                {
                    throw new InvalidOperationException("Visualizer object source returned no data.");
                }

                displayName = metadata.DisplayName;
                sourceType = metadata.SourceType;
                snapshotDirectory = VisualStudioTempStore.CreateSnapshotDirectory();
                EnsureTempDiskSpace(snapshotDirectory, metadata.BufferLength);
                var metadataPath = Path.Combine(snapshotDirectory, GetSnapshotName(metadata.DisplayName) + ".rbuf.json");
                var rawPath = RawBufferSnapshot.SaveMetadata(metadataPath, metadata.Descriptor);
                await WriteRawChunksAsync(visualizerTarget.ObjectSource, metadata, rawPath, cancellationToken);

                requestPath = VisualizerHandoffInbox.WriteSnapshotRequest(
                    visualStudioProcessId,
                    metadataPath,
                    metadata.DisplayName,
                    metadata.SourceType);
                if (await TryCompleteHandoffAsync(visualStudioProcessId, new[] { requestPath }, cancellationToken))
                {
                    session.ReportForwarded(metadata);
                    closeWhenLoaded = true;
                }
                else
                {
                    session.ReportFailure("The docked Raw Buffer Visualizer did not acknowledge the image handoff.");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (requestPath == null && snapshotDirectory != null)
                {
                    VisualStudioTempStore.TryDeleteDirectory(snapshotDirectory);
                }

                throw;
            }
            catch (Exception ex)
            {
                if (snapshotDirectory != null && requestPath == null)
                {
                    VisualStudioTempStore.TryDeleteDirectory(snapshotDirectory);
                }

                closeWhenLoaded = await TryForwardFailureAsync(
                    visualStudioProcessId,
                    displayName,
                    sourceType,
                    ex.Message,
                    cancellationToken,
                    errorType: ex.GetType().FullName,
                    errorDetails: ex.ToString());
                if (!closeWhenLoaded)
                {
                    session.ReportFailure(ex.Message);
                }
            }

            return new DockedVisualizerControl(session, closeWhenLoaded ? visualizerTarget : null);
        }

        public static async Task<IRemoteUserControl> CreateCollectionControlAsync(
            VisualizerTarget visualizerTarget,
            CancellationToken cancellationToken)
        {
            var session = new DockedVisualizerSession();
            var visualStudioProcessId = 0;
            var sourceType = visualizerTarget.TargetTypeFullName ?? "Image collection";
            var closeWhenLoaded = false;
            var requestPaths = new List<string>();
            try
            {
                visualStudioProcessId = VisualStudioInstance.GetCurrentProcessId();
                var summary = await visualizerTarget.ObjectSource.RequestDataAsync<VisualizerCollectionSummary>(
                    jsonSerializer: null,
                    cancellationToken);
                if (summary == null)
                {
                    throw new InvalidOperationException("Collection visualizer object source returned no data.");
                }

                if (summary.TotalCount == 0)
                {
                    throw new InvalidOperationException("Collection contains no items.");
                }

                sourceType = summary.SourceType;
                var forwarded = 0;
                var failed = 0;
                for (var index = 0; index < summary.ItemCount; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var item = await visualizerTarget.ObjectSource.RequestDataAsync<VisualizerCollectionItemRequest, VisualizerCollectionItemMetadata>(
                        new VisualizerCollectionItemRequest
                        {
                            Operation = VisualizerCollectionOperation.Metadata,
                            Index = index
                        },
                        jsonSerializer: null,
                        cancellationToken);
                    if (item == null || item.Metadata == null)
                    {
                        failed++;
                        requestPaths.Add(VisualizerHandoffInbox.WriteErrorRequest(
                            visualStudioProcessId,
                            item == null || string.IsNullOrWhiteSpace(item.DisplayName) ? "Item " + index : item.DisplayName,
                            summary.SourceType,
                            item == null || string.IsNullOrWhiteSpace(item.Error)
                                ? "Collection item returned no metadata."
                                : item.Error));
                        continue;
                    }

                    string? snapshotDirectory = null;
                    try
                    {
                        var metadata = item.Metadata;
                        snapshotDirectory = VisualStudioTempStore.CreateSnapshotDirectory();
                        EnsureTempDiskSpace(snapshotDirectory, metadata.BufferLength);
                        var metadataPath = Path.Combine(snapshotDirectory, GetSnapshotName(metadata.DisplayName) + ".rbuf.json");
                        var rawPath = RawBufferSnapshot.SaveMetadata(metadataPath, metadata.Descriptor);
                        await WriteCollectionRawChunksAsync(
                            visualizerTarget.ObjectSource,
                            index,
                            metadata,
                            rawPath,
                            cancellationToken);

                        requestPaths.Add(VisualizerHandoffInbox.WriteSnapshotRequest(
                            visualStudioProcessId,
                            metadataPath,
                            metadata.DisplayName,
                            metadata.SourceType));
                        forwarded++;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        if (snapshotDirectory != null)
                        {
                            VisualStudioTempStore.TryDeleteDirectory(snapshotDirectory);
                        }
                        throw;
                    }
                    catch (Exception ex)
                    {
                        if (snapshotDirectory != null)
                        {
                            VisualStudioTempStore.TryDeleteDirectory(snapshotDirectory);
                        }

                        failed++;
                        requestPaths.Add(VisualizerHandoffInbox.WriteErrorRequest(
                            visualStudioProcessId,
                            string.IsNullOrWhiteSpace(item.DisplayName) ? "Item " + index : item.DisplayName,
                            summary.SourceType,
                            ex.Message,
                            ex.GetType().FullName,
                            ex.ToString()));
                    }
                }

                if (requestPaths.Count == 0)
                {
                    throw new InvalidOperationException("Collection contains no supported image items.");
                }

                if (await TryCompleteHandoffAsync(visualStudioProcessId, requestPaths, cancellationToken))
                {
                    session.ReportCollectionForwarded(
                        summary.TotalCount,
                        forwarded,
                        failed,
                        Math.Max(0, summary.TotalCount - summary.ItemCount));
                    closeWhenLoaded = true;
                }
                else
                {
                    session.ReportFailure("The docked Raw Buffer Visualizer did not acknowledge the collection handoff.");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                closeWhenLoaded = await TryForwardFailureAsync(
                    visualStudioProcessId,
                    "Image collection",
                    sourceType,
                    ex.Message,
                    cancellationToken,
                    requestPaths,
                    ex.GetType().FullName,
                    ex.ToString());
                if (!closeWhenLoaded)
                {
                    session.ReportFailure(ex.Message);
                }
            }

            return new DockedVisualizerControl(session, closeWhenLoaded ? visualizerTarget : null);
        }

        private static async Task<bool> TryForwardFailureAsync(
            int visualStudioProcessId,
            string displayName,
            string sourceType,
            string errorMessage,
            CancellationToken cancellationToken,
            List<string>? requestPaths = null,
            string? errorType = null,
            string? errorDetails = null)
        {
            if (visualStudioProcessId <= 0)
            {
                return false;
            }

            try
            {
                requestPaths ??= new List<string>();
                requestPaths.Add(VisualizerHandoffInbox.WriteErrorRequest(
                    visualStudioProcessId,
                    displayName,
                    sourceType,
                    errorMessage,
                    errorType,
                    errorDetails));
                if (!await TryCompleteHandoffAsync(visualStudioProcessId, requestPaths, cancellationToken))
                {
                    return false;
                }

                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<bool> TryCompleteHandoffAsync(
            int visualStudioProcessId,
            IReadOnlyList<string> requestPaths,
            CancellationToken cancellationToken)
        {
            if (requestPaths.Count == 0)
            {
                return false;
            }

            TryWakeDockedToolWindow(visualStudioProcessId);
            var deadline = DateTime.UtcNow.Add(HandoffAcknowledgementTimeout);
            while (DateTime.UtcNow < deadline)
            {
                var pending = false;
                for (var index = 0; index < requestPaths.Count; index++)
                {
                    if (File.Exists(requestPaths[index]))
                    {
                        pending = true;
                        break;
                    }
                }

                if (!pending)
                {
                    return true;
                }

                await Task.Delay(50, cancellationToken);
            }

            return false;
        }

        private static void TryWakeDockedToolWindow(int visualStudioProcessId)
        {
            try
            {
                var thread = new Thread(() => TryWakeDockedToolWindowOnSta(visualStudioProcessId))
                {
                    IsBackground = true,
                    Name = "RawBufferVisualizerWakeDockedToolWindow"
                };
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
            }
            catch
            {
                // Autoload and inbox polling remain the fallback path.
            }
        }

        private static void TryWakeDockedToolWindowOnSta(int visualStudioProcessId)
        {
            try
            {
                using var messageFilter = ComMessageFilter.Register();
                var deadline = DateTime.UtcNow.AddSeconds(30);
                while (DateTime.UtcNow < deadline)
                {
                    try
                    {
                        RaiseShowToolWindowCommand(visualStudioProcessId);
                        return;
                    }
                    catch (COMException ex) when (IsRejectedComCall(ex))
                    {
                        Thread.Sleep(250);
                    }
                    catch (InvalidOperationException)
                    {
                        Thread.Sleep(250);
                    }
                }
            }
            catch
            {
                // Autoload and inbox polling remain the fallback path.
            }
        }

        private static void RaiseShowToolWindowCommand(int visualStudioProcessId)
        {
            dynamic dte = VisualStudioInstance.GetDte(visualStudioProcessId)
                ?? throw new InvalidOperationException("The hosting Visual Studio DTE is not available.");
            object? input = null;
            object? output = null;
            dte.Commands.Raise("{8e7bc2db-12a4-4f45-8f5a-38c1846a0f26}", 0x0100, ref input, ref output);
        }

        private static bool IsRejectedComCall(COMException ex)
        {
            const int RpcECallRejected = unchecked((int)0x80010001);
            const int RpcEServerCallRetryLater = unchecked((int)0x8001010A);
            return ex.ErrorCode == RpcECallRejected || ex.ErrorCode == RpcEServerCallRetryLater;
        }

        [ComImport]
        [Guid("00000016-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IOleMessageFilter
        {
            [PreserveSig]
            int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);

            [PreserveSig]
            int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);

            [PreserveSig]
            int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
        }

        private sealed class ComMessageFilter : IOleMessageFilter, IDisposable
        {
            private IOleMessageFilter? previousFilter;

            [DllImport("ole32.dll")]
            private static extern int CoRegisterMessageFilter(IOleMessageFilter? newFilter, out IOleMessageFilter? oldFilter);

            public static ComMessageFilter Register()
            {
                var filter = new ComMessageFilter();
                CoRegisterMessageFilter(filter, out filter.previousFilter);
                return filter;
            }

            public void Dispose()
            {
                CoRegisterMessageFilter(previousFilter, out _);
            }

            public int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo)
            {
                return 0;
            }

            public int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType)
            {
                return dwTickCount < 30000 ? 250 : -1;
            }

            public int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType)
            {
                return 2;
            }
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

        private static async Task WriteRawChunksAsync(
            VisualizerObjectSourceClient objectSource,
            VisualizerSnapshotMetadata metadata,
            string rawPath,
            CancellationToken cancellationToken)
        {
            await WriteRawChunksCoreAsync(
                metadata,
                rawPath,
                (offset, count) => objectSource.RequestDataAsync<VisualizerSnapshotChunkRequest, VisualizerSnapshotChunk>(
                    new VisualizerSnapshotChunkRequest
                    {
                        Offset = offset,
                        Count = count
                    },
                    jsonSerializer: null,
                    cancellationToken),
                cancellationToken);
        }

        private static async Task WriteCollectionRawChunksAsync(
            VisualizerObjectSourceClient objectSource,
            int index,
            VisualizerSnapshotMetadata metadata,
            string rawPath,
            CancellationToken cancellationToken)
        {
            await WriteRawChunksCoreAsync(
                metadata,
                rawPath,
                (offset, count) => objectSource.RequestDataAsync<VisualizerCollectionItemRequest, VisualizerSnapshotChunk>(
                    new VisualizerCollectionItemRequest
                    {
                        Operation = VisualizerCollectionOperation.Chunk,
                        Index = index,
                        Offset = offset,
                        Count = count
                    },
                    jsonSerializer: null,
                    cancellationToken),
                cancellationToken);
        }

        private static async Task WriteRawChunksCoreAsync(
            VisualizerSnapshotMetadata metadata,
            string rawPath,
            Func<long, int, Task<VisualizerSnapshotChunk>> requestChunk,
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
                var chunk = await requestChunk(offset, count);

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

                await stream.WriteAsync(chunk.Buffer, 0, chunk.Buffer.Length, cancellationToken);
                offset += chunk.Buffer.Length;
            }

            if (offset != metadata.BufferLength)
            {
                throw new InvalidOperationException("Visualizer object source returned an incomplete buffer.");
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
                    "Not enough temporary disk space for this image. Required about {0}, available {1}.",
                    FormatBytes(required),
                    FormatBytes(drive.AvailableFreeSpace)));
            }
        }

        private static string FormatBytes(long bytes)
        {
            var value = (double)Math.Max(0, bytes);
            string[] units = { "bytes", "KB", "MB", "GB", "TB" };
            var unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return string.Format(CultureInfo.InvariantCulture, "{0:0.##} {1}", value, units[unit]);
        }
    }
}
