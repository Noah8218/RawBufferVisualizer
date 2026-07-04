using System.Diagnostics;

namespace RawBufferVisualizer.VisualStudio
{
    public sealed class VisualizerLaunchRequest
    {
        public string ViewerExecutablePath { get; private set; }
        public string MetadataPath { get; private set; }
        public string RawPath { get; private set; }
        public string WorkingDirectory { get; private set; }

        internal VisualizerLaunchRequest(string viewerExecutablePath, string metadataPath, string rawPath, string workingDirectory)
        {
            ViewerExecutablePath = viewerExecutablePath;
            MetadataPath = metadataPath;
            RawPath = rawPath;
            WorkingDirectory = workingDirectory;
        }

        public ProcessStartInfo CreateStartInfo()
        {
            return new ProcessStartInfo
            {
                FileName = ViewerExecutablePath,
                Arguments = QuoteArgument(MetadataPath),
                WorkingDirectory = WorkingDirectory,
                UseShellExecute = false
            };
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
