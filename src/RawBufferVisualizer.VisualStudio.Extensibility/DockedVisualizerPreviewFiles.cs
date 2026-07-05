using System.Collections.Generic;

namespace RawBufferVisualizer.VisualStudio.Extensibility
{
    internal sealed class DockedVisualizerPreviewFiles
    {
        public string PreviewPath { get; }
        public string ThumbnailPath { get; }
        public IReadOnlyList<string> Diagnostics { get; }

        public DockedVisualizerPreviewFiles(string previewPath, string thumbnailPath, IReadOnlyList<string> diagnostics)
        {
            PreviewPath = previewPath;
            ThumbnailPath = thumbnailPath;
            Diagnostics = diagnostics;
        }
    }
}
