using System;
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

                VisualizerHandoffInbox.WriteSnapshotRequest(metadataPath, metadata.DisplayName, metadata.SourceType);
                TryWakeDockedToolWindow();
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

        private static void TryWakeDockedToolWindow()
        {
            try
            {
                var thread = new Thread(TryWakeDockedToolWindowOnSta)
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

        private static void TryWakeDockedToolWindowOnSta()
        {
            try
            {
                using var messageFilter = ComMessageFilter.Register();
                var deadline = DateTime.UtcNow.AddSeconds(30);
                while (DateTime.UtcNow < deadline)
                {
                    try
                    {
                        RaiseShowToolWindowCommand();
                        return;
                    }
                    catch (COMException ex) when (IsRejectedComCall(ex))
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

        private static void RaiseShowToolWindowCommand()
        {
            dynamic dte = Marshal.GetActiveObject("VisualStudio.DTE.17.0");
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

                await stream.WriteAsync(chunk.Buffer, 0, chunk.Buffer.Length, cancellationToken);
                offset += chunk.Buffer.Length;
            }

            if (offset != metadata.BufferLength)
            {
                throw new InvalidOperationException("Visualizer object source returned an incomplete buffer.");
            }
        }
    }
}
