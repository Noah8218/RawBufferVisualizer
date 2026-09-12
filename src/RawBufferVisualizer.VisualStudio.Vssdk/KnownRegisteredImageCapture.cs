using System;
using System.Globalization;
using Microsoft.VisualStudio.Shell;
using RawBufferVisualizer.Core;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    internal sealed class KnownRegisteredImageBuffer
    {
        public long Address { get; set; }
        public long SourcePointerAddress { get; set; }
        public string SourcePointerLabel { get; set; } = string.Empty;
        public long BufferLength { get; set; }
        public RawImageDescriptor Descriptor { get; set; } = new RawImageDescriptor();
    }

    /// <summary>
    /// Reads only the documented metadata contract of explicitly supported Mat types.
    /// OpenCvSharp exposes Step/Depth/Channels as documented metadata query methods;
    /// no arbitrary method name supplied by a scanned object is ever evaluated here.
    /// </summary>
    internal static class KnownRegisteredImageCapture
    {
        private const int ExpressionTimeoutMilliseconds = 2000;

        public static bool TryCreateBuffer(
            EnvDTE.Debugger debugger,
            string baseExpression,
            AutomaticKnownImageKind kind,
            out KnownRegisteredImageBuffer buffer,
            out string error)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            buffer = new KnownRegisteredImageBuffer();
            error = string.Empty;

            switch (kind)
            {
                case AutomaticKnownImageKind.OpenCvSharpMat:
                    return TryCreateOpenCvSharpBuffer(debugger, baseExpression, out buffer, out error);
                case AutomaticKnownImageKind.EmguCvMat:
                    return TryCreateEmguCvBuffer(debugger, baseExpression, out buffer, out error);
                default:
                    error = "This registered image type does not expose a safe automatic raw-buffer contract.";
                    return false;
            }
        }

        private static bool TryCreateOpenCvSharpBuffer(
            EnvDTE.Debugger debugger,
            string baseExpression,
            out KnownRegisteredImageBuffer buffer,
            out string error)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            buffer = new KnownRegisteredImageBuffer();
            error = string.Empty;

            long address;
            int width;
            int height;
            int stride;
            int depth;
            int channels;
            long sourcePointerAddress;
            string ignoredPointerError;
            if (!TryReadPointer(debugger, baseExpression + ".CvPtr", out sourcePointerAddress, out ignoredPointerError))
            {
                TryReadPointer(debugger, baseExpression + ".Ptr", out sourcePointerAddress, out ignoredPointerError);
            }
            if (!TryReadPointer(debugger, baseExpression + ".Data", out address, out error)
                || !TryReadPositiveInt(debugger, baseExpression + ".Cols", out width, out error)
                || !TryReadPositiveInt(debugger, baseExpression + ".Rows", out height, out error)
                || !TryReadPositiveInt(debugger, baseExpression + ".Step()", out stride, out error)
                || !TryReadInt(debugger, baseExpression + ".Depth()", out depth, out error)
                || !TryReadPositiveInt(debugger, baseExpression + ".Channels()", out channels, out error))
            {
                return false;
            }

            RawPixelFormat pixelFormat;
            int validBits;
            if (!TryMapMatFormat(depth, channels, out pixelFormat, out validBits))
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    "Unsupported OpenCvSharp Mat depth/channels: {0} C{1}.",
                    depth,
                    channels);
                return false;
            }

            return TryCreateValidatedBuffer(
                address,
                width,
                height,
                stride,
                pixelFormat,
                validBits,
                sourcePointerAddress,
                "Ptr",
                out buffer,
                out error);
        }

        private static bool TryCreateEmguCvBuffer(
            EnvDTE.Debugger debugger,
            string baseExpression,
            out KnownRegisteredImageBuffer buffer,
            out string error)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            buffer = new KnownRegisteredImageBuffer();
            error = string.Empty;

            long address;
            int width;
            int height;
            int stride;
            int channels;
            string depthValue;
            long sourcePointerAddress;
            string ignoredPointerError;
            TryReadPointer(debugger, baseExpression + ".Ptr", out sourcePointerAddress, out ignoredPointerError);
            if (!TryReadPointer(debugger, baseExpression + ".DataPointer", out address, out error)
                || !TryReadPositiveInt(debugger, baseExpression + ".Cols", out width, out error)
                || !TryReadPositiveInt(debugger, baseExpression + ".Rows", out height, out error)
                || !TryReadPositiveInt(debugger, baseExpression + ".Step", out stride, out error)
                || !TryReadValue(debugger, baseExpression + ".Depth", out depthValue, out error)
                || !TryReadPositiveInt(debugger, baseExpression + ".NumberOfChannels", out channels, out error))
            {
                return false;
            }

            int depth;
            if (!TryParseEmguDepth(depthValue, out depth))
            {
                error = "Unsupported Emgu CV Mat depth value: " + depthValue + ".";
                return false;
            }

            RawPixelFormat pixelFormat;
            int validBits;
            if (!TryMapMatFormat(depth, channels, out pixelFormat, out validBits))
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    "Unsupported Emgu CV Mat depth/channels: {0} C{1}.",
                    depthValue,
                    channels);
                return false;
            }

            return TryCreateValidatedBuffer(
                address,
                width,
                height,
                stride,
                pixelFormat,
                validBits,
                sourcePointerAddress,
                "Ptr",
                out buffer,
                out error);
        }

        private static bool TryCreateValidatedBuffer(
            long address,
            int width,
            int height,
            int stride,
            RawPixelFormat pixelFormat,
            int validBits,
            long sourcePointerAddress,
            string sourcePointerLabel,
            out KnownRegisteredImageBuffer buffer,
            out string error)
        {
            buffer = new KnownRegisteredImageBuffer();
            error = string.Empty;
            if (address == 0)
            {
                error = "Image data pointer is empty.";
                return false;
            }

            var descriptor = new RawImageDescriptor
            {
                Width = width,
                Height = height,
                Stride = stride,
                PixelFormat = pixelFormat,
                ValidBits = validBits,
                ByteOrder = RawByteOrder.LittleEndian
            };

            long bufferLength;
            try
            {
                bufferLength = descriptor.GetRequiredByteCount();
            }
            catch (OverflowException)
            {
                error = "Image buffer length exceeds the supported numeric range.";
                return false;
            }

            var diagnostics = RawBufferDiagnostics.AnalyzeLength(bufferLength, descriptor);
            for (var i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity == RawDiagnosticSeverity.Error)
                {
                    error = diagnostics[i].Message;
                    return false;
                }
            }

            buffer = new KnownRegisteredImageBuffer
            {
                Address = address,
                SourcePointerAddress = sourcePointerAddress,
                SourcePointerLabel = sourcePointerAddress == 0 ? string.Empty : sourcePointerLabel,
                BufferLength = bufferLength,
                Descriptor = descriptor
            };
            return true;
        }

        private static bool TryMapMatFormat(
            int depth,
            int channels,
            out RawPixelFormat pixelFormat,
            out int validBits)
        {
            pixelFormat = RawPixelFormat.Mono8;
            validBits = 0;
            if (depth == 0 && channels == 1)
            {
                pixelFormat = RawPixelFormat.Mono8;
                validBits = 8;
                return true;
            }

            if (depth == 0 && channels == 3)
            {
                pixelFormat = RawPixelFormat.BGR24;
                validBits = 8;
                return true;
            }

            if (depth == 0 && channels == 4)
            {
                pixelFormat = RawPixelFormat.BGRA32;
                validBits = 8;
                return true;
            }

            if (depth == 2 && channels == 1)
            {
                pixelFormat = RawPixelFormat.Mono16;
                validBits = 16;
                return true;
            }

            if (depth == 5 && channels == 1)
            {
                pixelFormat = RawPixelFormat.Float32;
                validBits = 32;
                return true;
            }

            if (depth == 4 && channels == 1)
            {
                pixelFormat = RawPixelFormat.Int32;
                validBits = 32;
                return true;
            }

            return false;
        }

        private static bool TryParseEmguDepth(string value, out int depth)
        {
            if (TryParseInteger(value, out depth))
            {
                return true;
            }

            var normalized = value.Trim().Trim('{', '}');
            var equals = normalized.LastIndexOf('=');
            if (equals >= 0 && equals + 1 < normalized.Length)
            {
                normalized = normalized.Substring(equals + 1).Trim();
            }

            var dot = normalized.LastIndexOf('.');
            if (dot >= 0 && dot + 1 < normalized.Length)
            {
                normalized = normalized.Substring(dot + 1);
            }

            switch (normalized.ToUpperInvariant())
            {
                case "CV8U":
                    depth = 0;
                    return true;
                case "CV16U":
                    depth = 2;
                    return true;
                case "CV32F":
                    depth = 5;
                    return true;
                case "CV32S":
                    depth = 4;
                    return true;
                default:
                    depth = 0;
                    return false;
            }
        }

        private static bool TryReadPointer(
            EnvDTE.Debugger debugger,
            string expressionText,
            out long address,
            out string error)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            address = 0;
            string value;
            if (!TryReadValue(debugger, expressionText, out value, out error))
            {
                return false;
            }

            var token = GetLeadingToken(value.Trim().Trim('{', '}'));
            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                ulong unsignedAddress;
                if (ulong.TryParse(
                    token.Substring(2),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out unsignedAddress))
                {
                    address = unchecked((long)unsignedAddress);
                    return address != 0;
                }
            }
            else if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out address))
            {
                return address != 0;
            }

            error = "Image data pointer could not be read from " + expressionText + ".";
            return false;
        }

        private static bool TryReadPositiveInt(
            EnvDTE.Debugger debugger,
            string expressionText,
            out int value,
            out string error)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (!TryReadInt(debugger, expressionText, out value, out error))
            {
                return false;
            }

            if (value <= 0)
            {
                error = expressionText + " is not a positive value.";
                return false;
            }

            return true;
        }

        private static bool TryReadInt(
            EnvDTE.Debugger debugger,
            string expressionText,
            out int value,
            out string error)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            value = 0;
            string text;
            if (!TryReadValue(debugger, expressionText, out text, out error)
                || !TryParseInteger(text, out value))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = expressionText + " is not a readable integer.";
                }

                return false;
            }

            return true;
        }

        private static bool TryParseInteger(string value, out int parsed)
        {
            var token = GetLeadingToken(value.Trim().Trim('{', '}'));
            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return int.TryParse(
                    token.Substring(2),
                    NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture,
                    out parsed);
            }

            return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
        }

#pragma warning disable VSTHRD010
        private static bool TryReadValue(
            EnvDTE.Debugger debugger,
            string expressionText,
            out string value,
            out string error)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            value = string.Empty;
            error = string.Empty;
            try
            {
                var expression = debugger.GetExpression(
                    expressionText,
                    true,
                    ExpressionTimeoutMilliseconds);
                if (expression == null || !expression.IsValidValue)
                {
                    error = "Debugger could not evaluate " + expressionText + ".";
                    return false;
                }

                value = expression.Value ?? string.Empty;
                if (string.IsNullOrWhiteSpace(value))
                {
                    error = "Debugger returned no value for " + expressionText + ".";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = "Debugger evaluation failed for " + expressionText + ": " + ex.Message;
                return false;
            }
        }
#pragma warning restore VSTHRD010

        private static string GetLeadingToken(string value)
        {
            var separator = value.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
            return separator < 0 ? value : value.Substring(0, separator);
        }
    }
}
