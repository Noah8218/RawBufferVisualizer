using System;
using System.Collections.Generic;

namespace RawBufferVisualizer.Core
{
    public static class RawBufferRenderer
    {
        public static RenderedImage Render(byte[] buffer, RawImageDescriptor descriptor, RawRenderOptions? options = null)
        {
            var diagnostics = RawBufferDiagnostics.Analyze(buffer, descriptor);
            if (RawBufferDiagnostics.HasErrors(diagnostics))
            {
                throw new ArgumentException(string.Join(Environment.NewLine, ToStrings(diagnostics)));
            }

            options = options ?? new RawRenderOptions();
            var pixels = new byte[descriptor.Width * descriptor.Height * 4];

            switch (descriptor.PixelFormat)
            {
                case RawPixelFormat.Mono8:
                case RawPixelFormat.Binary:
                    RenderMono8(buffer, descriptor, pixels, descriptor.PixelFormat == RawPixelFormat.Binary);
                    break;
                case RawPixelFormat.Mono16:
                    RenderMono16(buffer, descriptor, pixels, options);
                    break;
                case RawPixelFormat.Float32:
                    RenderFloat32(buffer, descriptor, pixels, options);
                    break;
                case RawPixelFormat.RGB24:
                    RenderRgb24(buffer, descriptor, pixels, true);
                    break;
                case RawPixelFormat.BGR24:
                    RenderRgb24(buffer, descriptor, pixels, false);
                    break;
                case RawPixelFormat.BGRA32:
                    RenderBgra32(buffer, descriptor, pixels);
                    break;
                case RawPixelFormat.BayerRGGB8:
                case RawPixelFormat.BayerGRBG8:
                case RawPixelFormat.BayerGBRG8:
                case RawPixelFormat.BayerBGGR8:
                    RenderBayer8(buffer, descriptor, pixels);
                    break;
                default:
                    throw new NotSupportedException("Unsupported pixel format: " + descriptor.PixelFormat);
            }

            return new RenderedImage(descriptor.Width, descriptor.Height, pixels);
        }

        private static IEnumerable<string> ToStrings(IReadOnlyList<RawDiagnostic> diagnostics)
        {
            for (var i = 0; i < diagnostics.Count; i++)
            {
                yield return diagnostics[i].ToString();
            }
        }

        private static void RenderMono8(byte[] buffer, RawImageDescriptor descriptor, byte[] pixels, bool binary)
        {
            for (var y = 0; y < descriptor.Height; y++)
            {
                var sourceRow = y * descriptor.Stride;
                var targetRow = y * descriptor.Width * 4;
                for (var x = 0; x < descriptor.Width; x++)
                {
                    var value = buffer[sourceRow + x];
                    if (binary)
                    {
                        value = value == 0 ? (byte)0 : (byte)255;
                    }

                    WriteBgra(pixels, targetRow + (x * 4), value, value, value, 255);
                }
            }
        }

        private static void RenderMono16(byte[] buffer, RawImageDescriptor descriptor, byte[] pixels, RawRenderOptions options)
        {
            var levels = GetMono16Levels(buffer, descriptor, options);
            var black = levels.Item1;
            var white = levels.Item2 <= black ? black + 1 : levels.Item2;

            for (var y = 0; y < descriptor.Height; y++)
            {
                var sourceRow = y * descriptor.Stride;
                var targetRow = y * descriptor.Width * 4;
                for (var x = 0; x < descriptor.Width; x++)
                {
                    var raw = ReadUInt16(buffer, sourceRow + (x * 2), descriptor.ByteOrder);
                    var value = ToByte(raw, black, white);
                    WriteBgra(pixels, targetRow + (x * 4), value, value, value, 255);
                }
            }
        }

        private static Tuple<double, double> GetMono16Levels(byte[] buffer, RawImageDescriptor descriptor, RawRenderOptions options)
        {
            if (!options.AutoScale)
            {
                return Tuple.Create(options.BlackLevel, options.WhiteLevel);
            }

            ushort min = ushort.MaxValue;
            ushort max = 0;
            for (var y = 0; y < descriptor.Height; y++)
            {
                var sourceRow = y * descriptor.Stride;
                for (var x = 0; x < descriptor.Width; x++)
                {
                    var raw = ReadUInt16(buffer, sourceRow + (x * 2), descriptor.ByteOrder);
                    if (raw < min)
                    {
                        min = raw;
                    }

                    if (raw > max)
                    {
                        max = raw;
                    }
                }
            }

            return Tuple.Create((double)min, (double)max);
        }

        private static void RenderFloat32(byte[] buffer, RawImageDescriptor descriptor, byte[] pixels, RawRenderOptions options)
        {
            var levels = GetFloat32Levels(buffer, descriptor, options);
            var black = levels.Item1;
            var white = levels.Item2 <= black ? black + 1 : levels.Item2;

            for (var y = 0; y < descriptor.Height; y++)
            {
                var sourceRow = y * descriptor.Stride;
                var targetRow = y * descriptor.Width * 4;
                for (var x = 0; x < descriptor.Width; x++)
                {
                    var raw = ReadSingle(buffer, sourceRow + (x * 4), descriptor.ByteOrder);
                    var value = double.IsNaN(raw) || double.IsInfinity(raw) ? (byte)0 : ToByte(raw, black, white);
                    WriteBgra(pixels, targetRow + (x * 4), value, value, value, 255);
                }
            }
        }

        private static Tuple<double, double> GetFloat32Levels(byte[] buffer, RawImageDescriptor descriptor, RawRenderOptions options)
        {
            if (!options.AutoScale)
            {
                return Tuple.Create(options.BlackLevel, options.WhiteLevel);
            }

            var min = double.MaxValue;
            var max = double.MinValue;
            for (var y = 0; y < descriptor.Height; y++)
            {
                var sourceRow = y * descriptor.Stride;
                for (var x = 0; x < descriptor.Width; x++)
                {
                    var raw = ReadSingle(buffer, sourceRow + (x * 4), descriptor.ByteOrder);
                    if (double.IsNaN(raw) || double.IsInfinity(raw))
                    {
                        continue;
                    }

                    if (raw < min)
                    {
                        min = raw;
                    }

                    if (raw > max)
                    {
                        max = raw;
                    }
                }
            }

            if (min == double.MaxValue)
            {
                return Tuple.Create(0.0, 1.0);
            }

            return Tuple.Create(min, max);
        }

        private static void RenderRgb24(byte[] buffer, RawImageDescriptor descriptor, byte[] pixels, bool sourceIsRgb)
        {
            for (var y = 0; y < descriptor.Height; y++)
            {
                var sourceRow = y * descriptor.Stride;
                var targetRow = y * descriptor.Width * 4;
                for (var x = 0; x < descriptor.Width; x++)
                {
                    var source = sourceRow + (x * 3);
                    var target = targetRow + (x * 4);
                    var c0 = buffer[source];
                    var c1 = buffer[source + 1];
                    var c2 = buffer[source + 2];
                    if (sourceIsRgb)
                    {
                        WriteBgra(pixels, target, c2, c1, c0, 255);
                    }
                    else
                    {
                        WriteBgra(pixels, target, c0, c1, c2, 255);
                    }
                }
            }
        }

        private static void RenderBgra32(byte[] buffer, RawImageDescriptor descriptor, byte[] pixels)
        {
            for (var y = 0; y < descriptor.Height; y++)
            {
                Buffer.BlockCopy(buffer, y * descriptor.Stride, pixels, y * descriptor.Width * 4, descriptor.Width * 4);
            }
        }

        private static void RenderBayer8(byte[] buffer, RawImageDescriptor descriptor, byte[] pixels)
        {
            // ponytail: simple 3x3 demosaic, replace with a camera-tuned pipeline if color quality matters.
            for (var y = 0; y < descriptor.Height; y++)
            {
                var targetRow = y * descriptor.Width * 4;
                for (var x = 0; x < descriptor.Width; x++)
                {
                    var r = AverageBayerColor(buffer, descriptor, x, y, 0);
                    var g = AverageBayerColor(buffer, descriptor, x, y, 1);
                    var b = AverageBayerColor(buffer, descriptor, x, y, 2);
                    WriteBgra(pixels, targetRow + (x * 4), b, g, r, 255);
                }
            }
        }

        private static byte AverageBayerColor(byte[] buffer, RawImageDescriptor descriptor, int x, int y, int color)
        {
            var sum = 0;
            var count = 0;
            for (var dy = -1; dy <= 1; dy++)
            {
                var yy = y + dy;
                if (yy < 0 || yy >= descriptor.Height)
                {
                    continue;
                }

                for (var dx = -1; dx <= 1; dx++)
                {
                    var xx = x + dx;
                    if (xx < 0 || xx >= descriptor.Width)
                    {
                        continue;
                    }

                    if (GetBayerColor(descriptor.PixelFormat, xx, yy) == color)
                    {
                        sum += buffer[(yy * descriptor.Stride) + xx];
                        count++;
                    }
                }
            }

            return count == 0 ? (byte)0 : (byte)(sum / count);
        }

        internal static int GetBayerColor(RawPixelFormat format, int x, int y)
        {
            var evenX = (x & 1) == 0;
            var evenY = (y & 1) == 0;

            switch (format)
            {
                case RawPixelFormat.BayerRGGB8:
                    return evenY ? (evenX ? 0 : 1) : (evenX ? 1 : 2);
                case RawPixelFormat.BayerGRBG8:
                    return evenY ? (evenX ? 1 : 0) : (evenX ? 2 : 1);
                case RawPixelFormat.BayerGBRG8:
                    return evenY ? (evenX ? 1 : 2) : (evenX ? 0 : 1);
                case RawPixelFormat.BayerBGGR8:
                    return evenY ? (evenX ? 2 : 1) : (evenX ? 1 : 0);
                default:
                    return 1;
            }
        }

        internal static ushort ReadUInt16(byte[] buffer, int offset, RawByteOrder byteOrder)
        {
            if (byteOrder == RawByteOrder.BigEndian)
            {
                return (ushort)((buffer[offset] << 8) | buffer[offset + 1]);
            }

            return (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
        }

        internal static float ReadSingle(byte[] buffer, int offset, RawByteOrder byteOrder)
        {
            var bytes = new[] { buffer[offset], buffer[offset + 1], buffer[offset + 2], buffer[offset + 3] };
            if ((byteOrder == RawByteOrder.BigEndian) == BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToSingle(bytes, 0);
        }

        private static byte ToByte(double raw, double black, double white)
        {
            var normalized = (raw - black) / (white - black);
            if (normalized <= 0)
            {
                return 0;
            }

            if (normalized >= 1)
            {
                return 255;
            }

            return (byte)Math.Round(normalized * 255);
        }

        private static void WriteBgra(byte[] pixels, int offset, byte b, byte g, byte r, byte a)
        {
            pixels[offset] = b;
            pixels[offset + 1] = g;
            pixels[offset + 2] = r;
            pixels[offset + 3] = a;
        }
    }
}

