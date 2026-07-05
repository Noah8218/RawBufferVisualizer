using System.Collections.Generic;

namespace RawBufferVisualizer.VisualStudio.Extensibility
{
    internal sealed class DockedVisualizerPreviewFiles
    {
        public string PreviewPath { get; }
        public string ThumbnailPath { get; }
        public int PreviewWidth { get; }
        public int PreviewHeight { get; }
        public IReadOnlyList<string> Diagnostics { get; }

        public DockedVisualizerPreviewFiles(
            string previewPath,
            string thumbnailPath,
            int previewWidth,
            int previewHeight,
            IReadOnlyList<string> diagnostics)
        {
            PreviewPath = previewPath;
            ThumbnailPath = thumbnailPath;
            PreviewWidth = previewWidth;
            PreviewHeight = previewHeight;
            Diagnostics = diagnostics;
        }
    }
}
