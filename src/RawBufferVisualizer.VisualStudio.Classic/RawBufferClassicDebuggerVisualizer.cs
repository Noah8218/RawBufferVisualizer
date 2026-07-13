using System;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.DebuggerVisualizers;
using RawBufferVisualizer.Sdk;
using RawBufferVisualizer.VisualStudio.ObjectSource;

[assembly: DebuggerVisualizer(
    typeof(RawBufferVisualizer.VisualStudio.Classic.RawBufferClassicDebuggerVisualizer),
    typeof(RawBufferSnapshotVisualizerObjectSource),
    Target = typeof(RawBufferSnapshot),
    Description = "Raw Buffer Visualizer")]
[assembly: DebuggerVisualizer(
    typeof(RawBufferVisualizer.VisualStudio.Classic.RawBufferClassicDebuggerVisualizer),
    typeof(RawBufferViewVisualizerObjectSource),
    Target = typeof(RawBufferView),
    Description = "Raw Buffer Visualizer")]
[assembly: DebuggerVisualizer(
    typeof(RawBufferVisualizer.VisualStudio.Classic.RawBufferClassicDebuggerVisualizer),
    typeof(BitmapVisualizerObjectSource),
    TargetTypeName = "System.Drawing.Bitmap, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
    Description = "Raw Buffer Visualizer")]
[assembly: DebuggerVisualizer(
    typeof(RawBufferVisualizer.VisualStudio.Classic.RawBufferClassicDebuggerVisualizer),
    typeof(BitmapVisualizerObjectSource),
    TargetTypeName = "System.Drawing.Bitmap, System.Drawing.Common",
    Description = "Raw Buffer Visualizer")]
[assembly: DebuggerVisualizer(
    typeof(RawBufferVisualizer.VisualStudio.Classic.RawBufferClassicDebuggerVisualizer),
    typeof(OpenCvSharpMatVisualizerObjectSource),
    TargetTypeName = "OpenCvSharp.Mat, OpenCvSharp, Version=4.0.0.0, Culture=neutral, PublicKeyToken=6adad1e807fea099",
    Description = "Raw Buffer Visualizer")]
[assembly: DebuggerVisualizer(
    typeof(RawBufferVisualizer.VisualStudio.Classic.RawBufferClassicDebuggerVisualizer),
    typeof(OpenCvSharpMatVisualizerObjectSource),
    TargetTypeName = "OpenCvSharp.Mat, OpenCvSharp",
    Description = "Raw Buffer Visualizer")]
[assembly: DebuggerVisualizer(
    typeof(RawBufferVisualizer.VisualStudio.Classic.RawBufferClassicDebuggerVisualizer),
    typeof(EmguCvMatVisualizerObjectSource),
    TargetTypeName = "Emgu.CV.Mat, Emgu.CV, Version=4.13.0.5924, Culture=neutral, PublicKeyToken=7281126722ab4438",
    Description = "Raw Buffer Visualizer")]
[assembly: DebuggerVisualizer(
    typeof(RawBufferVisualizer.VisualStudio.Classic.RawBufferClassicDebuggerVisualizer),
    typeof(EmguCvMatVisualizerObjectSource),
    TargetTypeName = "Emgu.CV.Mat, Emgu.CV",
    Description = "Raw Buffer Visualizer")]
[assembly: DebuggerVisualizer(
    typeof(RawBufferVisualizer.VisualStudio.Classic.RawBufferClassicDebuggerVisualizer),
    typeof(EmguCvMatVisualizerObjectSource),
    TargetTypeName = "Emgu.CV.Mat, Emgu.CV.World",
    Description = "Raw Buffer Visualizer")]
[assembly: DebuggerVisualizer(
    typeof(RawBufferVisualizer.VisualStudio.Classic.RawBufferClassicDebuggerVisualizer),
    typeof(EmguCvMatVisualizerObjectSource),
    TargetTypeName = "Emgu.CV.Mat, Emgu.CV.World.NetStandard",
    Description = "Raw Buffer Visualizer")]
[assembly: DebuggerVisualizer(
    typeof(RawBufferVisualizer.VisualStudio.Classic.RawBufferClassicDebuggerVisualizer),
    typeof(EmguCvMatVisualizerObjectSource),
    TargetTypeName = "Emgu.CV.Mat, Emgu.CV.Platform.NetStandard",
    Description = "Raw Buffer Visualizer")]
[assembly: DebuggerVisualizer(
    typeof(RawBufferVisualizer.VisualStudio.Classic.RawBufferClassicDebuggerVisualizer),
    typeof(ImagePtrVisualizerObjectSource),
    TargetTypeName = "Cressem.ImageModel.ImagePtr, Cressem.ImageModel, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
    Description = "Raw Buffer Visualizer")]

namespace RawBufferVisualizer.VisualStudio.Classic
{
    public sealed class RawBufferClassicDebuggerVisualizer : DialogDebuggerVisualizer
    {
        private const string CommandSetGuid = "{8e7bc2db-12a4-4f45-8f5a-38c1846a0f26}";
        private const int ShowToolWindowCommandId = 0x0100;

        public RawBufferClassicDebuggerVisualizer()
            : base(FormatterPolicy.NewtonsoftJson)
        {
        }

        protected override void Show(
            IDialogVisualizerService windowService,
            IVisualizerObjectProvider objectProvider)
        {
            var visualStudioProcessId = Process.GetCurrentProcess().Id;
            var displayName = "Raw buffer";
            var sourceType = "Debugger visualizer";
            string? metadataPath = null;

            try
            {
                var objectProvider2 = objectProvider as IVisualizerObjectProvider2
                    ?? throw new NotSupportedException("The Visual Studio object provider does not support JSON data.");
                var metadata = objectProvider2.GetDeserializableObject()
                    .ToObject<VisualizerSnapshotMetadata>()
                    ?? throw new InvalidDataException("The debugger visualizer returned no metadata.");

                displayName = string.IsNullOrWhiteSpace(metadata.DisplayName) ? displayName : metadata.DisplayName;
                sourceType = string.IsNullOrWhiteSpace(metadata.SourceType) ? sourceType : metadata.SourceType;
                metadataPath = VisualizerSnapshotStore.WriteSnapshot(
                    metadata,
                    request => objectProvider2.TransferDeserializableObject(request)
                        .ToObject<VisualizerSnapshotChunk>()
                        ?? throw new InvalidDataException("The debugger visualizer returned an invalid chunk."));

                VisualizerHandoffInbox.WriteSnapshotRequest(
                    visualStudioProcessId,
                    metadataPath,
                    displayName,
                    sourceType);
            }
            catch (Exception ex)
            {
                if (metadataPath != null)
                {
                    VisualStudioTempStore.TryDeleteSnapshotDirectoryForMetadata(metadataPath);
                }

                VisualizerHandoffInbox.WriteErrorRequest(
                    visualStudioProcessId,
                    displayName,
                    sourceType,
                    ex.Message);
            }

            WakeDockedToolWindow(visualStudioProcessId);
        }

        internal static void WakeDockedToolWindow(int visualStudioProcessId)
        {
            dynamic dte = VisualStudioInstance.GetDte(visualStudioProcessId)
                ?? throw new InvalidOperationException("The hosting Visual Studio DTE is not available.");
            object? input = null;
            object? output = null;
            dte.Commands.Raise(CommandSetGuid, ShowToolWindowCommandId, ref input, ref output);
        }
    }
}
