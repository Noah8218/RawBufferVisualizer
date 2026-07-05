using System.Diagnostics;
using System.Collections.Generic;

namespace RawBufferVisualizer.VisualStudio
{
    public sealed class VisualizerLaunchRequest
    {
        public string ViewerExecutablePath { get; private set; }
        public string MetadataPath { get { return MetadataPaths[0]; } }
        public string RawPath { get { return RawPaths[0]; } }
        public IReadOnlyList<string> MetadataPaths { get; private set; }
        public IReadOnlyList<string> RawPaths { get; private set; }
        public string WorkingDirectory { get; private set; }

        internal VisualizerLaunchRequest(string viewerExecutablePath, string metadataPath, string rawPath, string workingDirectory)
            : this(viewerExecutablePath, new[] { metadataPath }, new[] { rawPath }, workingDirectory)
        {
        }

        internal VisualizerLaunchRequest(string viewerExecutablePath, string[] metadataPaths, string[] rawPaths, string workingDirectory)
        {
            ViewerExecutablePath = viewerExecutablePath;
            MetadataPaths = metadataPaths;
            RawPaths = rawPaths;
            WorkingDirectory = workingDirectory;
        }

        public ProcessStartInfo CreateStartInfo()
        {
            return new ProcessStartInfo
            {
                FileName = ViewerExecutablePath,
                Arguments = CreateArguments(),
                WorkingDirectory = WorkingDirectory,
                UseShellExecute = false
            };
        }

        private string CreateArguments()
        {
            var arguments = new string[MetadataPaths.Count];
            for (var i = 0; i < MetadataPaths.Count; i++)
            {
                arguments[i] = QuoteArgument(MetadataPaths[i]);
            }

            return string.Join(" ", arguments);
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
