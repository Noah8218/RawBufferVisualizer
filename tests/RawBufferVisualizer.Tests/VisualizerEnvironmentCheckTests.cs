using System;
using System.IO;
using System.Linq;
using RawBufferVisualizer.VisualStudio;

namespace RawBufferVisualizer.Tests
{
    internal static class VisualizerEnvironmentCheckTests
    {
        public static void RunAll()
        {
            SupportedVisualStudioVersionsAreExplicit();
            RequiredAndOptionalChecksRemainSeparated();
            DiagnosticReportDeclaresPrivacyBoundary();
            CaptureVerifiesAndCleansTemporaryStorage();
        }

        private static void SupportedVisualStudioVersionsAreExplicit()
        {
            Assert(
                VisualizerEnvironmentCheck.IsSupportedVisualStudioVersion("17.14.37516.0"),
                "Visual Studio 2022 17.14 should be supported.");
            Assert(
                VisualizerEnvironmentCheck.IsSupportedVisualStudioVersion("17.14.37516.0 built by: d17.14"),
                "Visual Studio 2022 file versions with a build suffix should be supported.");
            Assert(
                VisualizerEnvironmentCheck.IsSupportedVisualStudioVersion("18.8.12023.21"),
                "Visual Studio 2026 should be supported.");
            Assert(
                VisualizerEnvironmentCheck.IsSupportedVisualStudioVersion("18.8.12023.21 built by: d18.8"),
                "Visual Studio 2026 file versions with a build suffix should be supported.");
            Assert(
                !VisualizerEnvironmentCheck.IsSupportedVisualStudioVersion("17.13.0.0"),
                "Visual Studio 2022 below 17.14 must be rejected.");
            Assert(
                !VisualizerEnvironmentCheck.IsSupportedVisualStudioVersion("17.13.99999.0 built by: d17.13"),
                "Visual Studio 2022 below 17.14 must remain rejected when a build suffix is present.");
            Assert(
                !VisualizerEnvironmentCheck.IsSupportedVisualStudioVersion("Unknown"),
                "Unknown Visual Studio versions must not be reported as supported.");
        }

        private static void RequiredAndOptionalChecksRemainSeparated()
        {
            var snapshot = CreateReadySnapshot();
            snapshot.VisualStudioExtensionWorkloadInstalled = false;
            snapshot.FfmpegAvailable = false;

            var result = VisualizerEnvironmentCheck.Create(snapshot);

            Assert(result.Required.Count == 3, "Environment check required-item count changed.");
            Assert(result.Optional.Count == 3, "Environment check optional-item count changed.");
            Assert(result.Required[0].Name == "Visual Studio host", "Visual Studio must be the first required check.");
            Assert(result.Required[1].Name == "Raw Buffer Visualizer", "Extension registration must be the second required check.");
            Assert(result.Required[2].Name == "Temporary storage", "Temporary storage must be the third required check.");
            Assert(result.Optional[0].Action == VisualizerEnvironmentAction.OpenDotNetDownload, ".NET action changed.");
            Assert(result.Optional[1].Action == VisualizerEnvironmentAction.OpenVisualStudioInstaller, "Visual Studio Installer action changed.");
            Assert(result.Optional[2].Action == VisualizerEnvironmentAction.OpenFfmpegGuide, "FFmpeg action changed.");
            Assert(
                result.Optional[1].State == VisualizerEnvironmentCheckState.Attention,
                "Missing optional workload should be visible without failing required runtime checks.");
        }

        private static void DiagnosticReportDeclaresPrivacyBoundary()
        {
            var result = VisualizerEnvironmentCheck.Create(CreateReadySnapshot());
            var report = result.CreateDiagnosticReport();

            Assert(report.Contains("Required runtime:"), "Environment report required heading is missing.");
            Assert(report.Contains("Optional contributor/media tools:"), "Environment report optional heading is missing.");
            Assert(report.Contains("Local paths included: Yes - review this report before sharing."), "Environment report local-path warning is missing.");
            Assert(report.Contains("Credentials or environment-variable values included: No"), "Environment report credential declaration is missing.");
            Assert(report.Contains("Image payload included: No"), "Environment report image privacy declaration is missing.");
        }

        private static void CaptureVerifiesAndCleansTemporaryStorage()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "RawBufferVisualizer-EnvironmentCheckTests",
                Guid.NewGuid().ToString("N"));
            try
            {
                var result = VisualizerEnvironmentCheck.Capture(
                    "1.0.53.0",
                    "18.8.12023.21",
                    string.Empty,
                    directory);

                Assert(
                    result.Required[2].State == VisualizerEnvironmentCheckState.Ready,
                    "Writable temporary storage was not reported ready.");
                Assert(
                    !Directory.EnumerateFiles(directory, ".environment-check-*.tmp").Any(),
                    "Environment check left a temporary probe file behind.");
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static VisualizerEnvironmentSnapshot CreateReadySnapshot()
        {
            return new VisualizerEnvironmentSnapshot
            {
                TimestampUtc = new DateTime(2026, 8, 3, 1, 2, 3, DateTimeKind.Utc),
                VisualStudioVersion = "18.8.12023.21",
                Is64BitProcess = true,
                ExtensionVersion = "1.0.53.0",
                TempStorageWritable = true,
                TempStoragePath = @"D:\OpenVisionLab-TestData\RawBufferVisualizer\temp",
                DotNet8SdkInstalled = true,
                DotNet8SdkPath = @"C:\Program Files\dotnet\sdk\8.0.423",
                VisualStudioExtensionWorkloadInstalled = true,
                VisualStudioInstallerPath = @"C:\Program Files (x86)\Microsoft Visual Studio\Installer\setup.exe",
                FfmpegAvailable = true,
                FfmpegPath = @"C:\Tools\ffmpeg.exe"
            };
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
