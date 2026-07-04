using System.Collections.Generic;

namespace RawBufferVisualizer.Core
{
    public static class RawBufferDiagnostics
    {
        public static IReadOnlyList<RawDiagnostic> Analyze(byte[] buffer, RawImageDescriptor descriptor)
        {
            var diagnostics = new List<RawDiagnostic>();

            if (buffer == null)
            {
                diagnostics.Add(new RawDiagnostic(RawDiagnosticSeverity.Error, "Buffer is null."));
                return diagnostics;
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
                diagnostics.Add(new RawDiagnostic(RawDiagnosticSeverity.Info, "Stride includes row padding."));
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
            if (requiredBytes > buffer.Length)
            {
                diagnostics.Add(new RawDiagnostic(RawDiagnosticSeverity.Error, "Buffer is smaller than descriptor requires."));
            }
            else if (requiredBytes > 0 && buffer.Length > requiredBytes)
            {
                diagnostics.Add(new RawDiagnostic(RawDiagnosticSeverity.Info, "Buffer has trailing bytes after the image."));
            }

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
    }
}
