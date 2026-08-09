using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace RawBufferVisualizer.VisualStudio
{
    public enum VisualizerEnvironmentCheckState
    {
        Ready,
        Attention
    }

    public sealed class VisualizerEnvironmentCheckItem
    {
        public string Name { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public VisualizerEnvironmentCheckState State { get; set; }
    }

    public sealed class VisualizerEnvironmentSnapshot
    {
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string VisualStudioVersion { get; set; } = string.Empty;
        public bool Is64BitProcess { get; set; }
        public string ExtensionVersion { get; set; } = string.Empty;
        public bool TempStorageWritable { get; set; }
        public string TempStoragePath { get; set; } = string.Empty;
        public string TempStorageError { get; set; } = string.Empty;
    }

    public sealed class VisualizerEnvironmentCheckResult
    {
        public VisualizerEnvironmentCheckResult(
            VisualizerEnvironmentSnapshot snapshot,
            IList<VisualizerEnvironmentCheckItem> required)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Required = required ?? throw new ArgumentNullException(nameof(required));
        }

        public VisualizerEnvironmentSnapshot Snapshot { get; }
        public IList<VisualizerEnvironmentCheckItem> Required { get; }

        public string CreateDiagnosticReport()
        {
            var builder = new StringBuilder();
            builder.AppendLine("Raw Buffer Visualizer Environment Report");
            builder.AppendLine();
            builder.Append("Timestamp UTC: ");
            builder.AppendLine(Snapshot.TimestampUtc.ToUniversalTime().ToString(
                "yyyy-MM-ddTHH:mm:ss.fffZ",
                CultureInfo.InvariantCulture));
            AppendItems(builder, "Required runtime", Required);
            builder.AppendLine();
            builder.AppendLine("Local paths included: Yes - review this report before sharing.");
            builder.AppendLine("Credentials or environment-variable values included: No");
            builder.AppendLine("Image payload included: No");
            return builder.ToString();
        }

        private static void AppendItems(
            StringBuilder builder,
            string heading,
            IEnumerable<VisualizerEnvironmentCheckItem> items)
        {
            builder.AppendLine();
            builder.AppendLine(heading + ":");
            foreach (var item in items)
            {
                builder.Append("- [");
                builder.Append(item.State == VisualizerEnvironmentCheckState.Ready
                    ? "Ready"
                    : "Attention");
                builder.Append("] ");
                builder.Append(item.Name);
                builder.Append(": ");
                builder.AppendLine(string.IsNullOrWhiteSpace(item.Detail)
                    ? "Unavailable"
                    : item.Detail.Trim());
            }
        }
    }

    public static class VisualizerEnvironmentCheck
    {
        public static VisualizerEnvironmentCheckResult Capture(
            string extensionVersion,
            string visualStudioVersion,
            string tempStoragePath)
        {
            var snapshot = new VisualizerEnvironmentSnapshot
            {
                TimestampUtc = DateTime.UtcNow,
                ExtensionVersion = extensionVersion ?? string.Empty,
                VisualStudioVersion = visualStudioVersion ?? string.Empty,
                Is64BitProcess = Environment.Is64BitProcess,
                TempStoragePath = tempStoragePath ?? string.Empty
            };

            CaptureTempStorage(snapshot);
            return Create(snapshot);
        }

        public static VisualizerEnvironmentCheckResult Create(
            VisualizerEnvironmentSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var supportedHost = IsSupportedVisualStudioVersion(snapshot.VisualStudioVersion)
                && snapshot.Is64BitProcess;
            var extensionLoaded = !string.IsNullOrWhiteSpace(snapshot.ExtensionVersion)
                && !string.Equals(snapshot.ExtensionVersion, "Unknown", StringComparison.OrdinalIgnoreCase);
            var required = new List<VisualizerEnvironmentCheckItem>
            {
                new VisualizerEnvironmentCheckItem
                {
                    Name = "Visual Studio host",
                    Detail = supportedHost
                        ? snapshot.VisualStudioVersion + " x64 - supported"
                        : snapshot.VisualStudioVersion + (snapshot.Is64BitProcess
                            ? " - unsupported or unknown"
                            : " - x64 is required"),
                    State = supportedHost
                        ? VisualizerEnvironmentCheckState.Ready
                        : VisualizerEnvironmentCheckState.Attention
                },
                new VisualizerEnvironmentCheckItem
                {
                    Name = "Raw Buffer Visualizer",
                    Detail = extensionLoaded
                        ? snapshot.ExtensionVersion + " - loaded and registered in this session"
                        : "Extension version is unavailable",
                    State = extensionLoaded
                        ? VisualizerEnvironmentCheckState.Ready
                        : VisualizerEnvironmentCheckState.Attention
                },
                new VisualizerEnvironmentCheckItem
                {
                    Name = "Temporary storage",
                    Detail = snapshot.TempStorageWritable
                        ? snapshot.TempStoragePath + " - writable"
                        : snapshot.TempStoragePath + " - " + NormalizeError(snapshot.TempStorageError),
                    State = snapshot.TempStorageWritable
                        ? VisualizerEnvironmentCheckState.Ready
                        : VisualizerEnvironmentCheckState.Attention
                }
            };

            return new VisualizerEnvironmentCheckResult(snapshot, required);
        }

        public static bool IsSupportedVisualStudioVersion(string versionText)
        {
            var normalizedVersion = (versionText ?? string.Empty).Trim();
            var suffixIndex = normalizedVersion.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
            if (suffixIndex > 0)
            {
                normalizedVersion = normalizedVersion.Substring(0, suffixIndex);
            }

            Version? version;
            if (!Version.TryParse(normalizedVersion, out version) || version == null)
            {
                return false;
            }

            return version.Major >= 18
                || (version.Major == 17 && version.Minor >= 9);
        }

        private static void CaptureTempStorage(VisualizerEnvironmentSnapshot snapshot)
        {
            var probePath = string.Empty;
            try
            {
                Directory.CreateDirectory(snapshot.TempStoragePath);
                probePath = Path.Combine(
                    snapshot.TempStoragePath,
                    ".environment-check-" + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(probePath, "Raw Buffer Visualizer environment check", new UTF8Encoding(false));
                File.Delete(probePath);
                snapshot.TempStorageWritable = true;
            }
            catch (Exception ex)
            {
                snapshot.TempStorageWritable = false;
                snapshot.TempStorageError = ex.GetType().Name;
                TryDeleteFile(probePath);
            }
        }

        private static string NormalizeError(string error)
        {
            return string.IsNullOrWhiteSpace(error) ? "not writable" : error;
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }
}
