using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace RawBufferVisualizer.VisualStudio
{
    public enum VisualizerEnvironmentCheckState
    {
        Ready,
        Attention
    }

    public enum VisualizerEnvironmentAction
    {
        None,
        OpenDotNetDownload,
        OpenVisualStudioInstaller,
        OpenFfmpegGuide
    }

    public sealed class VisualizerEnvironmentCheckItem
    {
        public string Name { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public VisualizerEnvironmentCheckState State { get; set; }
        public VisualizerEnvironmentAction Action { get; set; }
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
        public bool DotNet8SdkInstalled { get; set; }
        public string DotNet8SdkPath { get; set; } = string.Empty;
        public bool VisualStudioExtensionWorkloadInstalled { get; set; }
        public string VisualStudioInstallerPath { get; set; } = string.Empty;
        public bool FfmpegAvailable { get; set; }
        public string FfmpegPath { get; set; } = string.Empty;
    }

    public sealed class VisualizerEnvironmentCheckResult
    {
        public VisualizerEnvironmentCheckResult(
            VisualizerEnvironmentSnapshot snapshot,
            IList<VisualizerEnvironmentCheckItem> required,
            IList<VisualizerEnvironmentCheckItem> optional)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Required = required ?? throw new ArgumentNullException(nameof(required));
            Optional = optional ?? throw new ArgumentNullException(nameof(optional));
        }

        public VisualizerEnvironmentSnapshot Snapshot { get; }
        public IList<VisualizerEnvironmentCheckItem> Required { get; }
        public IList<VisualizerEnvironmentCheckItem> Optional { get; }

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
            AppendItems(builder, "Optional contributor/media tools", Optional);
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
        public const string DotNet8DownloadUrl =
            "https://dotnet.microsoft.com/en-us/download/dotnet/8.0";
        public const string VisualStudioModifyUrl =
            "https://learn.microsoft.com/en-us/visualstudio/install/modify-visual-studio?view=visualstudio";
        public const string FfmpegDownloadUrl = "https://ffmpeg.org/download.html";

        public static VisualizerEnvironmentCheckResult Capture(
            string extensionVersion,
            string visualStudioVersion,
            string visualStudioInstallationPath,
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
            CaptureDotNet8Sdk(snapshot);
            CaptureVisualStudioWorkload(snapshot, visualStudioInstallationPath);
            CaptureFfmpeg(snapshot);
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

            var optional = new List<VisualizerEnvironmentCheckItem>
            {
                new VisualizerEnvironmentCheckItem
                {
                    Name = ".NET 8 SDK",
                    Detail = snapshot.DotNet8SdkInstalled
                        ? snapshot.DotNet8SdkPath + " - available"
                        : "Not detected; required only for contributors and source-based validation",
                    State = snapshot.DotNet8SdkInstalled
                        ? VisualizerEnvironmentCheckState.Ready
                        : VisualizerEnvironmentCheckState.Attention,
                    Action = VisualizerEnvironmentAction.OpenDotNetDownload
                },
                new VisualizerEnvironmentCheckItem
                {
                    Name = "Visual Studio extension workload",
                    Detail = snapshot.VisualStudioExtensionWorkloadInstalled
                        ? "VSSDK workload files detected"
                        : "Not detected; optional for the verified command-line build",
                    State = snapshot.VisualStudioExtensionWorkloadInstalled
                        ? VisualizerEnvironmentCheckState.Ready
                        : VisualizerEnvironmentCheckState.Attention,
                    Action = VisualizerEnvironmentAction.OpenVisualStudioInstaller
                },
                new VisualizerEnvironmentCheckItem
                {
                    Name = "FFmpeg (demo media only)",
                    Detail = snapshot.FfmpegAvailable
                        ? snapshot.FfmpegPath + " - available"
                        : "Not detected; not required by the extension runtime",
                    State = snapshot.FfmpegAvailable
                        ? VisualizerEnvironmentCheckState.Ready
                        : VisualizerEnvironmentCheckState.Attention,
                    Action = VisualizerEnvironmentAction.OpenFfmpegGuide
                }
            };

            return new VisualizerEnvironmentCheckResult(snapshot, required, optional);
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
                || (version.Major == 17 && version.Minor >= 14);
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

        private static void CaptureDotNet8Sdk(VisualizerEnvironmentSnapshot snapshot)
        {
            try
            {
                var dotnetRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "dotnet");
                var sdkRoot = Path.Combine(dotnetRoot, "sdk");
                var sdkDirectory = Directory.Exists(sdkRoot)
                    ? Directory.GetDirectories(sdkRoot, "8.*", SearchOption.TopDirectoryOnly)
                        .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                        .FirstOrDefault()
                    : null;
                snapshot.DotNet8SdkInstalled = !string.IsNullOrWhiteSpace(sdkDirectory)
                    && File.Exists(Path.Combine(dotnetRoot, "dotnet.exe"));
                snapshot.DotNet8SdkPath = sdkDirectory ?? string.Empty;
            }
            catch
            {
                snapshot.DotNet8SdkInstalled = false;
                snapshot.DotNet8SdkPath = string.Empty;
            }
        }

        private static void CaptureVisualStudioWorkload(
            VisualizerEnvironmentSnapshot snapshot,
            string visualStudioInstallationPath)
        {
            try
            {
                snapshot.VisualStudioExtensionWorkloadInstalled =
                    !string.IsNullOrWhiteSpace(visualStudioInstallationPath)
                    && Directory.Exists(Path.Combine(visualStudioInstallationPath, "VSSDK"));
                var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                var installerPath = Path.Combine(
                    programFilesX86,
                    "Microsoft Visual Studio",
                    "Installer",
                    "setup.exe");
                snapshot.VisualStudioInstallerPath = File.Exists(installerPath)
                    ? installerPath
                    : string.Empty;
            }
            catch
            {
                snapshot.VisualStudioExtensionWorkloadInstalled = false;
                snapshot.VisualStudioInstallerPath = string.Empty;
            }
        }

        private static void CaptureFfmpeg(VisualizerEnvironmentSnapshot snapshot)
        {
            try
            {
                var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                foreach (var pathEntry in pathValue.Split(Path.PathSeparator))
                {
                    var directory = pathEntry.Trim().Trim('"');
                    if (string.IsNullOrWhiteSpace(directory))
                    {
                        continue;
                    }

                    var candidate = Path.Combine(directory, "ffmpeg.exe");
                    if (File.Exists(candidate))
                    {
                        snapshot.FfmpegAvailable = true;
                        snapshot.FfmpegPath = candidate;
                        return;
                    }
                }
            }
            catch
            {
            }

            snapshot.FfmpegAvailable = false;
            snapshot.FfmpegPath = string.Empty;
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
