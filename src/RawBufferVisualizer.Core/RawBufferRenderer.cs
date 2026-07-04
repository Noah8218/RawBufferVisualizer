using System;
using System.Collections.Generic;

namespace RawBufferVisualizer.Core
{
    public static class RawBufferRenderer
    {
        public static RenderedImage Render(byte[] buffer, RawImageDescriptor descriptor, RawRenderOptions? options = null)
        {
            return RenderTile(buffer, descriptor, 0, 0, descriptor.Width, descriptor.Height, options);
        }

        public static RenderedImage RenderTile(byte[] buffer, RawImageDescriptor descriptor, int x, int y, int width, int height, RawRenderOptions? options = null)
        {
            var diagnostics = RawBufferDiagnostics.Analyze(buffer, descriptor);
            if (RawBufferDiagnostics.HasErrors(diagnostics))
            {
                throw new ArgumentException(string.Join(Environment.NewLine, ToStrings(diagnostics)));
            }

            if (x < 0 || y < 0 || width <= 0 || height <= 0 || x + width > descriptor.Width || y + height > descriptor.Height)
            {
                throw new ArgumentOutOfRangeException("x", "Tile bounds must be inside the image.");
            }

            options = options ?? new RawRenderOptions();
            var pixelByteCount = checked((long)width * height * 4);
            if (pixelByteCount > int.MaxValue)
            {
                throw new InvalidOperationException("Rendered image exceeds a single BGRA buffer. Use tiled rendering for this image.");
            }

            var pixels = new byte[(int)pixelByteCount];

            switch (descriptor.PixelFormat)
            {
                case RawPixelFormat.Mono8:
                case RawPixelFormat.Binary:
                    RenderMono8(buffer, descriptor, pixels, x, y, width, height, descriptor.PixelFormat == RawPixelFormat.Binary);
                    break;
                case RawPixelFormat.Mono16:
                    RenderMono16(buffer, descriptor, pixels, x, y, width, height, options);
                    break;
                case RawPixelFormat.Mono10PackedLsb:
                    RenderPackedMono(buffer, descriptor, pixels, x, y, width, height, 10, options);
                    break;
                case RawPixelFormat.Mono12PackedLsb:
                    RenderPackedMono(buffer, descriptor, pixels, x, y, width, height, 12, options);
                    break;
                case RawPixelFormat.Float32:
                    RenderFloat32(buffer, descriptor, pixels, x, y, width, height, options);
                    break;
                case RawPixelFormat.RGB24:
                    RenderRgb24(buffer, descriptor, pixels, x, y, width, height, true);
                    break;
                case RawPixelFormat.BGR24:
                    RenderRgb24(buffer, descriptor, pixels, x, y, width, height, false);
                    break;
                case RawPixelFormat.BGRA32:
                    RenderBgra32(buffer, descriptor, pixels, x, y, width, height);
                    break;
                case RawPixelFormat.BayerRGGB8:
                case RawPixelFormat.BayerGRBG8:
                case RawPixelFormat.BayerGBRG8:
                case RawPixelFormat.BayerBGGR8:
                    RenderBayer8(buffer, descriptor, pixels, x, y, width, height);
                    break;
                default:
                    throw new NotSupportedException("Unsupported pixel format: " + descriptor.PixelFormat);
            }

            return new RenderedImage(width, height, pixels);
        }

        private static IEnumerable<string> ToStrings(IReadOnlyList<RawDiagnostic> diagnostics)
        {
            for (var i = 0; i < diagnostics.Count; i++)
            {
                yield return diagnostics[i].ToString();
            }
        }

        public static RawRenderOptions CreateFixedScaleOptions(byte[] buffer, RawImageDescriptor descriptor)
        {
            var options = new RawRenderOptions();
            Tuple<double, double>? levels = null;
            switch (descriptor.PixelFormat)
            {
                case RawPixelFormat.Mono16:
                    levels = GetMono16Levels(buffer, descriptor, options);
                    break;
                case RawPixelFormat.Mono10PackedLsb:
                    levels = GetPackedMonoLevels(buffer, descriptor, 10, options);
                    break;
                case RawPixelFormat.Mono12PackedLsb:
                    levels = GetPackedMonoLevels(buffer, descriptor, 12, options);
                    break;
                case RawPixelFormat.Float32:
                    levels = GetFloat32Levels(buffer, descriptor, options);
                    break;
            }

            if (levels == null)
            {
                return options;
            }

            return new RawRenderOptions
            {
                AutoScale = false,
                BlackLevel = levels.Item1,
                WhiteLevel = levels.Item2 <= levels.Item1 ? levels.Item1 + 1 : levels.Item2
            };
        }

        private static void RenderPackedMono(byte[] buffer, RawImageDescriptor descriptor, byte[] pixels, int x, int y, int width, int height, int bitsPerPixel, RawRenderOptions options)
        {
            var levels = GetPackedMonoLevels(buffer, descriptor, bitsPerPixel, options);
            var black = levels.Item1;
            var white = levels.Item2 <= black ? black + 1 : levels.Item2;

            for (var tileY = 0; tileY < height; tileY++)
            {
                var sourceRow = (y + tileY) * descriptor.Stride;
                var targetRow = tileY * width * 4;
                for (var tileX = 0; tileX < width; tileX++)
                {
                    var raw = ReadPackedLsb(buffer, sourceRow, x + tileX, bitsPerPixel);
                    var value = ToByte(raw, black, white);
                    WriteBgra(pixels, targetRow + (tileX * 4), value, value, value, 255);
                }
            }
        }

        private static Tuple<double, double> GetPackedMonoLevels(byte[] buffer, RawImageDescriptor descriptor, int bitsPerPixel, RawRenderOptions options)
        {
            if (!options.AutoScale)
            {
                return Tuple.Create(options.BlackLevel, options.WhiteLevel);
            }

            var maxValue = (1 << bitsPerPixel) - 1;
            var min = maxValue;
            var max = 0;
            for (var y = 0; y < descriptor.Height; y++)
            {
                var sourceRow = y * descriptor.Stride;
                for (var x = 0; x < descriptor.Width; x++)
                {
                    var raw = ReadPackedLsb(buffer, sourceRow, x, bitsPerPixel);
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

        private static void RenderMono8(byte[] buffer, RawImageDescriptor descriptor, byte[] pixels, int x, int y, int width, int height, bool binary)
        {
            for (var tileY = 0; tileY < height; tileY++)
            {
                var sourceRow = (y + tileY) * descriptor.Stride;
                var targetRow = tileY * width * 4;
                for (var tileX = 0; tileX < width; tileX++)
                {
                    var value = buffer[sourceRow + x + tileX];
                    if (binary)
                    {
                        value = value == 0 ? (byte)0 : (byte)255;
                    }

                    WriteBgra(pixels, targetRow + (tileX * 4), value, value, value, 255);
                }
            }
        }

        private static void RenderMono16(byte[] buffer, RawImageDescriptor descriptor, byte[] pixels, int x, int y, int width, int height, RawRenderOptions options)
        {
            var levels = GetMono16Levels(buffer, descriptor, options);
            var black = levels.Item1;
            var white = levels.Item2 <= black ? black + 1 : levels.Item2;

            for (var tileY = 0; tileY < height; tileY++)
            {
                var sourceRow = (y + tileY) * descriptor.Stride;
                var targetRow = tileY * width * 4;
                for (var tileX = 0; tileX < width; tileX++)
                {
                    var raw = ReadUInt16(buffer, sourceRow + ((x + tileX) * 2), descriptor.ByteOrder);
                    var value = ToByte(raw, black, white);
                    WriteBgra(pixels, targetRow + (tileX * 4), value, value, value, 255);
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

        private static void RenderFloat32(byte[] buffer, RawImageDescriptor descriptor, byte[] pixels, int x, int y, int width, int height, RawRenderOptions options)
        {
            var levels = GetFloat32Levels(buffer, descriptor, options);
            var black = levels.Item1;
            var white = levels.Item2 <= black ? black + 1 : levels.Item2;

            for (var tileY = 0; tileY < height; tileY++)
            {
                var sourceRow = (y + tileY) * descriptor.Stride;
                var targetRow = tileY * width * 4;
                for (var tileX = 0; tileX < width; tileX++)
                {
                    var raw = ReadSingle(buffer, sourceRow + ((x + tileX) * 4), descriptor.ByteOrder);
                    var value = double.IsNaN(raw) || double.IsInfinity(raw) ? (byte)0 : ToByte(raw, black, white);
                    WriteBgra(pixels, targetRow + (tileX * 4), value, value, value, 255);
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

        private static void RenderRgb24(byte[] buffer, RawImageDescriptor descriptor, byte[] pixels, int x, int y, int width, int height, bool sourceIsRgb)
        {
            for (var tileY = 0; tileY < height; tileY++)
            {
                var sourceRow = (y + tileY) * descriptor.Stride;
                var targetRow = tileY * width * 4;
                for (var tileX = 0; tileX < width; tileX++)
                {
                    var source = sourceRow + ((x + tileX) * 3);
                    var target = targetRow + (tileX * 4);
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

        private static void RenderBgra32(byte[] buffer, RawImageDescriptor descriptor, byte[] pixels, int x, int y, int width, int height)
        {
            for (var tileY = 0; tileY < height; tileY++)
            {
                Buffer.BlockCopy(buffer, ((y + tileY) * descriptor.Stride) + (x * 4), pixels, tileY * width * 4, width * 4);
            }
        }

        private static void RenderBayer8(byte[] buffer, RawImageDescriptor descriptor, byte[] pixels, int x, int y, int width, int height)
        {
            // ponytail: simple 3x3 demosaic, replace with a camera-tuned pipeline if color quality matters.
            for (var tileY = 0; tileY < height; tileY++)
            {
                var targetRow = tileY * width * 4;
                for (var tileX = 0; tileX < width; tileX++)
                {
                    var imageX = x + tileX;
                    var imageY = y + tileY;
                    var r = AverageBayerColor(buffer, descriptor, imageX, imageY, 0);
                    var g = AverageBayerColor(buffer, descriptor, imageX, imageY, 1);
                    var b = AverageBayerColor(buffer, descriptor, imageX, imageY, 2);
                    WriteBgra(pixels, targetRow + (tileX * 4), b, g, r, 255);
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

        internal static int ReadPackedLsb(byte[] buffer, int rowOffset, int x, int bitsPerPixel)
        {
            var bitOffset = x * bitsPerPixel;
            var byteOffset = rowOffset + (bitOffset / 8);
            var shift = bitOffset % 8;
            var value = 0;
            var bytesToRead = (shift + bitsPerPixel + 7) / 8;
            for (var i = 0; i < bytesToRead; i++)
            {
                value |= buffer[byteOffset + i] << (i * 8);
            }

            return (value >> shift) & ((1 << bitsPerPixel) - 1);
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
