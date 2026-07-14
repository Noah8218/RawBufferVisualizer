using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RawBufferVisualizer.VisualStudio
{
    public sealed class VisualizerSupportReportData
    {
        public string ReportType { get; set; } = "Diagnostics";
        public string ErrorId { get; set; } = string.Empty;
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string ExtensionVersion { get; set; } = string.Empty;
        public string VisualStudioVersion { get; set; } = string.Empty;
        public string OperatingSystem { get; set; } = string.Empty;
        public string ProcessArchitecture { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public string SourceType { get; set; } = string.Empty;
        public string ErrorType { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string ErrorDetails { get; set; } = string.Empty;
        public string Descriptor { get; set; } = string.Empty;
        public string DisplayPath { get; set; } = string.Empty;
        public string PackageLogPath { get; set; } = string.Empty;
        public string ActivityLogPath { get; set; } = string.Empty;
        public IList<string> Diagnostics { get; } = new List<string>();
    }

    public static class VisualizerSupportReport
    {
        private const int MaxDetailLength = 32768;
        private const int MaxDiagnosticCount = 128;
        private const int MaxDiagnosticLength = 2048;

        public static string Create(VisualizerSupportReportData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }

            var builder = new StringBuilder();
            builder.AppendLine("Raw Buffer Visualizer Support Report");
            builder.AppendLine();
            AppendField(builder, "Report type", data.ReportType);
            AppendField(builder, "Error ID", data.ErrorId);
            AppendField(builder, "Timestamp UTC", data.TimestampUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
            AppendField(builder, "Extension version", data.ExtensionVersion);
            AppendField(builder, "Visual Studio version", data.VisualStudioVersion);
            AppendField(builder, "Operating system", data.OperatingSystem);
            AppendField(builder, "Process architecture", data.ProcessArchitecture);
            AppendField(builder, "Source name", data.SourceName);
            AppendField(builder, "Source type", data.SourceType);
            AppendField(builder, "Error type", data.ErrorType);
            AppendField(builder, "Error message", data.ErrorMessage);
            AppendField(builder, "Descriptor", data.Descriptor);
            AppendField(builder, "Display path", data.DisplayPath);
            AppendField(builder, "Package log", data.PackageLogPath);
            AppendField(builder, "Visual Studio activity log", data.ActivityLogPath);
            builder.AppendLine("Image payload included: No");

            if (data.Diagnostics.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Diagnostics:");
                var count = Math.Min(data.Diagnostics.Count, MaxDiagnosticCount);
                for (var index = 0; index < count; index++)
                {
                    builder.Append("- ");
                    builder.AppendLine(Trim(data.Diagnostics[index], MaxDiagnosticLength));
                }
            }

            if (!string.IsNullOrWhiteSpace(data.ErrorDetails))
            {
                builder.AppendLine();
                builder.AppendLine("Error details:");
                builder.AppendLine(Trim(data.ErrorDetails, MaxDetailLength));
            }

            return builder.ToString();
        }

        private static void AppendField(StringBuilder builder, string name, string value)
        {
            builder.Append(name);
            builder.Append(": ");
            builder.AppendLine(ToSingleLine(value));
        }

        private static string ToSingleLine(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Unavailable";
            }

            return Trim(value.Replace("\r\n", " | ").Replace('\r', ' ').Replace('\n', ' '), MaxDiagnosticLength);
        }

        private static string Trim(string value, int maximumLength)
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "Unavailable" : value.Trim();
            return normalized.Length <= maximumLength
                ? normalized
                : normalized.Substring(0, maximumLength) + "... [truncated]";
        }
    }
}
