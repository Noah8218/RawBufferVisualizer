using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using RawBufferVisualizer.VisualStudio;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    public static class RawBufferVisualizerPackageLog
    {
        public static void Write(string message)
        {
            var metricsPath = Environment.GetEnvironmentVariable("RAWBUFFERVISUALIZER_DOCKED_PERF_JSON");
            try
            {
                var logPath = string.IsNullOrWhiteSpace(metricsPath)
                    ? Path.Combine(VisualStudioTempStore.RootDirectory, "package.log")
                    : Path.ChangeExtension(metricsPath, ".package.log");
                var logDirectory = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrWhiteSpace(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                if (File.Exists(logPath) && new FileInfo(logPath).Length > 1024 * 1024)
                {
                    File.Delete(logPath);
                }

                File.AppendAllText(
                    logPath,
                    DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                    + " [devenv:"
                    + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture)
                    + "] "
                    + message
                    + Environment.NewLine);
            }
            catch
            {
                // Diagnostics must not affect Visual Studio package load.
            }
        }
    }
}
