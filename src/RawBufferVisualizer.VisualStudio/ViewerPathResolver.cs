using System;
using System.IO;

namespace RawBufferVisualizer.VisualStudio
{
    public static class ViewerPathResolver
    {
        public const string ViewerPathEnvironmentVariable = "RAW_BUFFER_VISUALIZER_VIEWER";
        private const string ViewerExecutableName = "RawBufferVisualizer.Wpf.exe";

        public static string ResolveViewerExecutablePath(string? baseDirectory = null)
        {
            var path = TryResolveViewerExecutablePath(baseDirectory);
            if (path != null)
            {
                return path;
            }

            throw new FileNotFoundException(
                "Raw Buffer Visualizer executable was not found. Set RAW_BUFFER_VISUALIZER_VIEWER to RawBufferVisualizer.Wpf.exe.");
        }

        public static string? TryResolveViewerExecutablePath(string? baseDirectory = null)
        {
            var configuredPath = Environment.GetEnvironmentVariable(ViewerPathEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                var fullConfiguredPath = Path.GetFullPath(configuredPath);
                if (File.Exists(fullConfiguredPath))
                {
                    return fullConfiguredPath;
                }
            }

            var root = string.IsNullOrWhiteSpace(baseDirectory) ? AppContext.BaseDirectory : baseDirectory;
            var sideBySidePath = Path.Combine(Path.GetFullPath(root), ViewerExecutableName);
            return File.Exists(sideBySidePath) ? sideBySidePath : null;
        }
    }
}
