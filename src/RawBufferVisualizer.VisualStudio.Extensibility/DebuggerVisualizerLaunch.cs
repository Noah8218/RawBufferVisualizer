using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility.DebuggerVisualizers;
using Microsoft.VisualStudio.RpcContracts.RemoteUI;
using RawBufferVisualizer.VisualStudio.ObjectSource;

namespace RawBufferVisualizer.VisualStudio.Extensibility
{
    internal static class DebuggerVisualizerLaunch
    {
        public static async Task<IRemoteUserControl> CreateControlAsync(
            VisualizerTarget visualizerTarget,
            CancellationToken cancellationToken)
        {
            var status = new VisualizerLaunchStatus();
            try
            {
                var transfer = await visualizerTarget.ObjectSource.RequestDataAsync<VisualizerSnapshotTransfer>(
                    jsonSerializer: null,
                    cancellationToken);
                if (transfer == null)
                {
                    throw new InvalidOperationException("Visualizer object source returned no data.");
                }

                var viewerPath = ViewerPathResolver.ResolveViewerExecutablePath();
                var request = StandaloneViewerBridge.PrepareLaunch(transfer, viewerPath);
                StandaloneViewerBridge.Launch(request);

                status.Message = "Raw Buffer Visualizer opened.";
                status.MetadataPath = request.MetadataPath;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                status.Message = ex.Message;
            }

            return new VisualizerLaunchStatusControl(status);
        }
    }
}
