using System.Collections.Generic;
using System.Globalization;

namespace RawBufferVisualizer.Core
{
    public static class RawBufferDiagnostics
    {
        public static IReadOnlyList<RawDiagnostic> Analyze(byte[] buffer, RawImageDescriptor descriptor)
        {
            if (buffer == null)
            {
                var diagnostics = new List<RawDiagnostic>();
                diagnostics.Add(new RawDiagnostic(RawDiagnosticSeverity.Error, "Buffer is null."));
                return diagnostics;
            }

            return AnalyzeLength(buffer.Length, descriptor);
        }

        public static IReadOnlyList<RawDiagnostic> AnalyzeLength(long bufferLength, RawImageDescriptor descriptor)
        {
            var diagnostics = new List<RawDiagnostic>();

            if (bufferLength < 0)
            {
                diagnostics.Add(new RawDiagnostic(RawDiagnosticSeverity.Error, "Buffer length must not be negative."));
            }

            if (descriptor == null)
            {
                diagnostics.Add(new RawDiagnostic(RawDiagnosticSeverity.Error, "Descriptor is null."));
                return diagnostics;
            }

            if (descriptor.Width <= 0)
            {
                diagnostics.Add(new RawDiagnostic(RawDiagnosticSeverity.Error, "Width must be greater than zero."));
            }

            if (descriptor.Height <= 0)
            {
                diagnostics.Add(new RawDiagnostic(RawDiagnosticSeverity.Error, "Height must be greater than zero."));
            }

            var minimumStride = descriptor.GetMinimumStride();
            if (descriptor.Stride <= 0)
            {
                diagnostics.Add(new RawDiagnostic(RawDiagnosticSeverity.Error, "Stride must be greater than zero."));
            }
            else if (minimumStride > 0 && descriptor.Stride < minimumStride)
            {
                diagnostics.Add(new RawDiagnostic(RawDiagnosticSeverity.Error, "Stride is smaller than the pixel format requires."));
            }
            else if (minimumStride > 0 && descriptor.Stride > minimumStride)
            {
                diagnostics.Add(new RawDiagnostic(
                    RawDiagnosticSeverity.Info,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Stride includes row padding: stride {0:N0}, minimum {1:N0}, padding {2:N0} bytes/row.",
                        descriptor.Stride,
                        minimumStride,
                        descriptor.Stride - minimumStride)));
            }
            else if (minimumStride > 0 && descriptor.Stride == minimumStride)
            {
                diagnostics.Add(new RawDiagnostic(
                    RawDiagnosticSeverity.Info,
                    string.Format(CultureInfo.InvariantCulture, "Stride matches pixel format minimum: {0:N0} bytes/row.", minimumStride)));
            }

            if (descriptor.PixelFormat == RawPixelFormat.Mono16 && (descriptor.ValidBits < 1 || descriptor.ValidBits > 16))
            {
                diagnostics.Add(new RawDiagnostic(RawDiagnosticSeverity.Warning, "Mono16 valid bits should be between 1 and 16."));
            }

            if (descriptor.PixelFormat == RawPixelFormat.Mono10PackedLsb && descriptor.ValidBits != 10)
            {
                diagnostics.Add(new RawDiagnostic(RawDiagnosticSeverity.Info, "Mono10PackedLsb uses 10 valid bits per pixel."));
            }

            if (descriptor.PixelFormat == RawPixelFormat.Mono12PackedLsb && descriptor.ValidBits != 12)
            {
                diagnostics.Add(new RawDiagnostic(RawDiagnosticSeverity.Info, "Mono12PackedLsb uses 12 valid bits per pixel."));
            }

            var requiredBytes = descriptor.GetRequiredByteCount();
            if (requiredBytes > 0)
            {
                diagnostics.Add(new RawDiagnostic(
                    RawDiagnosticSeverity.Info,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Expected image byte range: {0:N0} bytes; buffer length: {1:N0} bytes.",
                        requiredBytes,
                        bufferLength)));
            }

            if (requiredBytes > bufferLength)
            {
                diagnostics.Add(new RawDiagnostic(
                    RawDiagnosticSeverity.Error,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Buffer is smaller than descriptor requires by {0:N0} bytes.",
                        requiredBytes - bufferLength)));
            }
            else if (requiredBytes > 0 && bufferLength > requiredBytes)
            {
                diagnostics.Add(new RawDiagnostic(
                    RawDiagnosticSeverity.Info,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Buffer has {0:N0} trailing bytes after the image.",
                        bufferLength - requiredBytes)));
            }

            diagnostics.Add(new RawDiagnostic(
                RawDiagnosticSeverity.Info,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Interpretation: {0}, {1} valid bits, {2}, {3} channel(s).",
                    descriptor.PixelFormat,
                    descriptor.ValidBits,
                    descriptor.ByteOrder,
                    GetChannelCount(descriptor.PixelFormat))));

            return diagnostics;
        }

        public static bool HasErrors(IReadOnlyList<RawDiagnostic> diagnostics)
        {
            for (var i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity == RawDiagnosticSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetChannelCount(RawPixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case RawPixelFormat.RGB24:
                case RawPixelFormat.BGR24:
                    return 3;
                case RawPixelFormat.BGRA32:
                    return 4;
                default:
                    return 1;
            }
        }
    }
}
