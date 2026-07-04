using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace RawBufferVisualizer.Core
{
    public abstract class RawImageSource : IDisposable
    {
        private readonly RawImageDescriptor _descriptor;

        public RawImageDescriptor Descriptor
        {
            get { return _descriptor.Clone(); }
        }

        protected RawImageDescriptor SourceDescriptor
        {
            get { return _descriptor; }
        }

        public long Length { get; private set; }
        public string? RawPath { get; private set; }
        public abstract bool IsFileBacked { get; }

        protected RawImageSource(RawImageDescriptor descriptor, long length, string? rawPath)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException("descriptor");
            }

            if (length < 0)
            {
                throw new ArgumentOutOfRangeException("length");
            }

            _descriptor = descriptor.Clone();
            Length = length;
            RawPath = rawPath;
        }

        public static RawImageSource FromMemory(byte[] buffer, RawImageDescriptor descriptor)
        {
            return new MemoryRawImageSource(buffer, descriptor);
        }

        public static RawImageSource FromFile(string rawPath, RawImageDescriptor descriptor)
        {
            return new FileRawImageSource(rawPath, descriptor);
        }

        public static bool CanStreamFormat(RawPixelFormat pixelFormat)
        {
            return pixelFormat != RawPixelFormat.Mono10PackedLsb
                && pixelFormat != RawPixelFormat.Mono12PackedLsb;
        }

        public IReadOnlyList<RawDiagnostic> Analyze()
        {
            return RawBufferDiagnostics.AnalyzeLength(Length, SourceDescriptor);
        }

        public abstract RawImageSource WithDescriptor(RawImageDescriptor descriptor);
        public abstract RawRenderOptions CreateRenderOptions();
        public abstract RenderedImage RenderTile(int x, int y, int width, int height, RawRenderOptions? options);
        public abstract string DescribePixel(int x, int y);
        public abstract byte[] ReadAllBytes();
        public abstract void CopyRawTo(string rawPath);

        public virtual RenderedImage RenderTileSampled(int x, int y, int width, int height, int sampleStep, RawRenderOptions? options)
        {
            var rendered = RenderTile(x, y, width, height, options);
            if (sampleStep <= 1)
            {
                return rendered;
            }

            return Downsample(rendered, sampleStep);
        }

        public virtual void Dispose()
        {
        }

        protected static void EnsureTileBounds(RawImageDescriptor descriptor, int x, int y, int width, int height)
        {
            if (x < 0 || y < 0 || width <= 0 || height <= 0 || x + width > descriptor.Width || y + height > descriptor.Height)
            {
                throw new ArgumentOutOfRangeException("x", "Tile bounds must be inside the image.");
            }
        }

        protected static byte ScaleToByte(double raw, RawRenderOptions options)
        {
            var white = options.WhiteLevel <= options.BlackLevel ? options.BlackLevel + 1 : options.WhiteLevel;
            var normalized = (raw - options.BlackLevel) / (white - options.BlackLevel);
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

        protected static void EnsureParentDirectory(string path)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static RenderedImage Downsample(RenderedImage source, int sampleStep)
        {
            var sampledWidth = Math.Max(1, (source.Width + sampleStep - 1) / sampleStep);
            var sampledHeight = Math.Max(1, (source.Height + sampleStep - 1) / sampleStep);
            var target = new byte[checked(sampledWidth * sampledHeight * 4)];
            var targetIndex = 0;
            for (var y = 0; y < source.Height; y += sampleStep)
            {
                var sourceRow = y * source.Width * 4;
                for (var x = 0; x < source.Width; x += sampleStep)
                {
                    var sourceIndex = sourceRow + (x * 4);
                    target[targetIndex++] = source.Bgra32[sourceIndex];
                    target[targetIndex++] = source.Bgra32[sourceIndex + 1];
                    target[targetIndex++] = source.Bgra32[sourceIndex + 2];
                    target[targetIndex++] = source.Bgra32[sourceIndex + 3];
                }
            }

            return new RenderedImage(sampledWidth, sampledHeight, target);
        }
    }

    internal sealed class MemoryRawImageSource : RawImageSource
    {
        private readonly byte[] _buffer;

        public override bool IsFileBacked
        {
            get { return false; }
        }

        public MemoryRawImageSource(byte[] buffer, RawImageDescriptor descriptor)
            : base(descriptor, buffer == null ? 0 : buffer.Length, null)
        {
            _buffer = buffer ?? throw new ArgumentNullException("buffer");
        }

        public override RawImageSource WithDescriptor(RawImageDescriptor descriptor)
        {
            return new MemoryRawImageSource(_buffer, descriptor);
        }

        public override RawRenderOptions CreateRenderOptions()
        {
            return RawBufferRenderer.CreateFixedScaleOptions(_buffer, SourceDescriptor);
        }

        public override RenderedImage RenderTile(int x, int y, int width, int height, RawRenderOptions? options)
        {
            return RawBufferRenderer.RenderTile(_buffer, SourceDescriptor, x, y, width, height, options);
        }

        public override string DescribePixel(int x, int y)
        {
            return RawPixelInspector.Describe(_buffer, SourceDescriptor, x, y);
        }

        public override byte[] ReadAllBytes()
        {
            return (byte[])_buffer.Clone();
        }

        public override void CopyRawTo(string rawPath)
        {
            EnsureParentDirectory(rawPath);
            File.WriteAllBytes(rawPath, _buffer);
        }
    }

    internal sealed class FileRawImageSource : RawImageSource
    {
        private readonly string _rawPath;

        public override bool IsFileBacked
        {
            get { return true; }
        }

        public FileRawImageSource(string rawPath, RawImageDescriptor descriptor)
            : base(descriptor, GetFileLength(rawPath), Path.GetFullPath(rawPath))
        {
            if (!CanStreamFormat(descriptor.PixelFormat))
            {
                throw new NotSupportedException("File-backed tiled display does not support " + descriptor.PixelFormat + " yet. Use an in-memory snapshot for this packed format.");
            }

            _rawPath = Path.GetFullPath(rawPath);
        }

        public override RawImageSource WithDescriptor(RawImageDescriptor descriptor)
        {
            return new FileRawImageSource(_rawPath, descriptor);
        }

        public override RawRenderOptions CreateRenderOptions()
        {
            switch (SourceDescriptor.PixelFormat)
            {
                case RawPixelFormat.Mono16:
                    return CreateFixedRangeOptions(GetMaxForValidBits(SourceDescriptor.ValidBits, 16));
                case RawPixelFormat.Float32:
                    return CreateFixedRangeOptions(1);
                default:
                    return new RawRenderOptions();
            }
        }

        public override RenderedImage RenderTile(int x, int y, int width, int height, RawRenderOptions? options)
        {
            EnsureTileBounds(SourceDescriptor, x, y, width, height);

            RawImageDescriptor tileDescriptor;
            int localX;
            int localY;
            var buffer = ReadTileWindow(x, y, width, height, IsBayer(SourceDescriptor.PixelFormat), out tileDescriptor, out localX, out localY);
            return RawBufferRenderer.RenderTile(buffer, tileDescriptor, localX, localY, width, height, options ?? CreateRenderOptions());
        }

        public override RenderedImage RenderTileSampled(int x, int y, int width, int height, int sampleStep, RawRenderOptions? options)
        {
            if (sampleStep <= 1)
            {
                return RenderTile(x, y, width, height, options);
            }

            EnsureTileBounds(SourceDescriptor, x, y, width, height);
            options = options ?? CreateRenderOptions();

            var bytesPerPixel = GetStreamableBytesPerPixel(SourceDescriptor.PixelFormat);
            var sampledWidth = Math.Max(1, (width + sampleStep - 1) / sampleStep);
            var sampledHeight = Math.Max(1, (height + sampleStep - 1) / sampleStep);
            var pixels = new byte[checked(sampledWidth * sampledHeight * 4)];
            var rowByteCount = checked(width * bytesPerPixel);
            var rowBuffer = new byte[rowByteCount];

            using (var stream = OpenRawReadStream())
            {
                for (var sampledY = 0; sampledY < sampledHeight; sampledY++)
                {
                    var globalY = y + (sampledY * sampleStep);
                    ReadRowSpan(stream, globalY, x, rowByteCount, rowBuffer);
                    var targetRow = sampledY * sampledWidth * 4;

                    for (var sampledX = 0; sampledX < sampledWidth; sampledX++)
                    {
                        var globalX = x + (sampledX * sampleStep);
                        var sourceOffset = sampledX * sampleStep * bytesPerPixel;
                        var targetOffset = targetRow + (sampledX * 4);
                        WriteSampledPixel(rowBuffer, sourceOffset, pixels, targetOffset, globalX, globalY, options);
                    }
                }
            }

            return new RenderedImage(sampledWidth, sampledHeight, pixels);
        }

        public override string DescribePixel(int x, int y)
        {
            EnsureTileBounds(SourceDescriptor, x, y, 1, 1);
            RawImageDescriptor pixelDescriptor;
            int localX;
            int localY;
            var buffer = ReadTileWindow(x, y, 1, 1, false, out pixelDescriptor, out localX, out localY);
            if (IsBayer(SourceDescriptor.PixelFormat))
            {
                var color = RawBufferRenderer.GetBayerColor(SourceDescriptor.PixelFormat, x, y);
                var colorName = color == 0 ? "R" : color == 1 ? "G" : "B";
                return string.Format(CultureInfo.InvariantCulture, "X={0}, Y={1}, Bayer {2}={3}", x, y, colorName, buffer[0]);
            }

            return RawPixelInspector.Describe(buffer, pixelDescriptor, localX, localY, x, y);
        }

        public override byte[] ReadAllBytes()
        {
            if (Length > int.MaxValue)
            {
                throw new InvalidOperationException("The raw payload is too large to read into a single byte array.");
            }

            return File.ReadAllBytes(_rawPath);
        }

        public override void CopyRawTo(string rawPath)
        {
            var fullPath = Path.GetFullPath(rawPath);
            if (string.Equals(fullPath, _rawPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            EnsureParentDirectory(fullPath);
            File.Copy(_rawPath, fullPath, true);
        }

        private byte[] ReadTileWindow(int x, int y, int width, int height, bool includeBayerHalo, out RawImageDescriptor tileDescriptor, out int localX, out int localY)
        {
            var bytesPerPixel = GetStreamableBytesPerPixel(SourceDescriptor.PixelFormat);
            var windowX = x;
            var windowY = y;
            var windowRight = x + width;
            var windowBottom = y + height;

            if (includeBayerHalo)
            {
                windowX = Math.Max(0, x - 1);
                windowY = Math.Max(0, y - 1);
                if ((windowX & 1) != 0)
                {
                    windowX--;
                }

                if ((windowY & 1) != 0)
                {
                    windowY--;
                }

                windowRight = Math.Min(SourceDescriptor.Width, x + width + 1);
                windowBottom = Math.Min(SourceDescriptor.Height, y + height + 1);
            }

            var windowWidth = windowRight - windowX;
            var windowHeight = windowBottom - windowY;
            var windowStride = checked(windowWidth * bytesPerPixel);
            var byteCount = checked((long)windowStride * windowHeight);
            if (byteCount > int.MaxValue)
            {
                throw new InvalidOperationException("Raw tile window is too large to stage in memory.");
            }

            var buffer = new byte[(int)byteCount];
            using (var stream = OpenRawReadStream())
            {
                for (var row = 0; row < windowHeight; row++)
                {
                    var targetOffset = row * windowStride;
                    ReadRowSpan(stream, windowY + row, windowX, windowStride, buffer, targetOffset);
                }
            }

            tileDescriptor = SourceDescriptor.Clone();
            tileDescriptor.Width = windowWidth;
            tileDescriptor.Height = windowHeight;
            tileDescriptor.Stride = windowStride;
            localX = x - windowX;
            localY = y - windowY;
            return buffer;
        }

        private void WriteSampledPixel(byte[] rowBuffer, int sourceOffset, byte[] pixels, int targetOffset, int x, int y, RawRenderOptions options)
        {
            switch (SourceDescriptor.PixelFormat)
            {
                case RawPixelFormat.Mono8:
                    WriteBgra(pixels, targetOffset, rowBuffer[sourceOffset], rowBuffer[sourceOffset], rowBuffer[sourceOffset], 255);
                    break;
                case RawPixelFormat.Binary:
                    var binary = rowBuffer[sourceOffset] == 0 ? (byte)0 : (byte)255;
                    WriteBgra(pixels, targetOffset, binary, binary, binary, 255);
                    break;
                case RawPixelFormat.Mono16:
                    var mono16 = RawBufferRenderer.ReadUInt16(rowBuffer, sourceOffset, SourceDescriptor.ByteOrder);
                    var mono16Value = ScaleToByte(mono16, options);
                    WriteBgra(pixels, targetOffset, mono16Value, mono16Value, mono16Value, 255);
                    break;
                case RawPixelFormat.Float32:
                    var floatValue = RawBufferRenderer.ReadSingle(rowBuffer, sourceOffset, SourceDescriptor.ByteOrder);
                    var floatByte = double.IsNaN(floatValue) || double.IsInfinity(floatValue) ? (byte)0 : ScaleToByte(floatValue, options);
                    WriteBgra(pixels, targetOffset, floatByte, floatByte, floatByte, 255);
                    break;
                case RawPixelFormat.RGB24:
                    WriteBgra(pixels, targetOffset, rowBuffer[sourceOffset + 2], rowBuffer[sourceOffset + 1], rowBuffer[sourceOffset], 255);
                    break;
                case RawPixelFormat.BGR24:
                    WriteBgra(pixels, targetOffset, rowBuffer[sourceOffset], rowBuffer[sourceOffset + 1], rowBuffer[sourceOffset + 2], 255);
                    break;
                case RawPixelFormat.BGRA32:
                    WriteBgra(pixels, targetOffset, rowBuffer[sourceOffset], rowBuffer[sourceOffset + 1], rowBuffer[sourceOffset + 2], rowBuffer[sourceOffset + 3]);
                    break;
                case RawPixelFormat.BayerRGGB8:
                case RawPixelFormat.BayerGRBG8:
                case RawPixelFormat.BayerGBRG8:
                case RawPixelFormat.BayerBGGR8:
                    WriteBayerSample(rowBuffer[sourceOffset], pixels, targetOffset, x, y);
                    break;
                default:
                    throw new NotSupportedException("Unsupported file-backed pixel format: " + SourceDescriptor.PixelFormat);
            }
        }

        private void WriteBayerSample(byte value, byte[] pixels, int targetOffset, int x, int y)
        {
            var color = RawBufferRenderer.GetBayerColor(SourceDescriptor.PixelFormat, x, y);
            var b = (byte)0;
            var g = (byte)0;
            var r = (byte)0;
            if (color == 0)
            {
                r = value;
            }
            else if (color == 1)
            {
                g = value;
            }
            else
            {
                b = value;
            }

            WriteBgra(pixels, targetOffset, b, g, r, 255);
        }

        private void ReadRowSpan(FileStream stream, int y, int x, int byteCount, byte[] target)
        {
            ReadRowSpan(stream, y, x, byteCount, target, 0);
        }

        private void ReadRowSpan(FileStream stream, int y, int x, int byteCount, byte[] target, int targetOffset)
        {
            var bytesPerPixel = GetStreamableBytesPerPixel(SourceDescriptor.PixelFormat);
            var sourceOffset = checked(((long)y * SourceDescriptor.Stride) + ((long)x * bytesPerPixel));
            stream.Position = sourceOffset;
            ReadExactly(stream, target, targetOffset, byteCount);
        }

        private FileStream OpenRawReadStream()
        {
            return new FileStream(_rawPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        private static void ReadExactly(Stream stream, byte[] buffer, int offset, int count)
        {
            var remaining = count;
            var currentOffset = offset;
            while (remaining > 0)
            {
                var read = stream.Read(buffer, currentOffset, remaining);
                if (read == 0)
                {
                    throw new EndOfStreamException("Raw payload ended before the requested tile was read.");
                }

                currentOffset += read;
                remaining -= read;
            }
        }

        private static long GetFileLength(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                throw new ArgumentException("Raw path is required.", "rawPath");
            }

            return new FileInfo(Path.GetFullPath(rawPath)).Length;
        }

        private static int GetStreamableBytesPerPixel(RawPixelFormat format)
        {
            switch (format)
            {
                case RawPixelFormat.Mono8:
                case RawPixelFormat.Binary:
                case RawPixelFormat.BayerRGGB8:
                case RawPixelFormat.BayerGRBG8:
                case RawPixelFormat.BayerGBRG8:
                case RawPixelFormat.BayerBGGR8:
                    return 1;
                case RawPixelFormat.Mono16:
                    return 2;
                case RawPixelFormat.RGB24:
                case RawPixelFormat.BGR24:
                    return 3;
                case RawPixelFormat.BGRA32:
                case RawPixelFormat.Float32:
                    return 4;
                default:
                    throw new NotSupportedException("File-backed tiled display does not support " + format + " yet.");
            }
        }

        private static bool IsBayer(RawPixelFormat format)
        {
            return format == RawPixelFormat.BayerRGGB8
                || format == RawPixelFormat.BayerGRBG8
                || format == RawPixelFormat.BayerGBRG8
                || format == RawPixelFormat.BayerBGGR8;
        }

        private static RawRenderOptions CreateFixedRangeOptions(double whiteLevel)
        {
            return new RawRenderOptions
            {
                AutoScale = false,
                BlackLevel = 0,
                WhiteLevel = whiteLevel <= 0 ? 1 : whiteLevel
            };
        }

        private static int GetMaxForValidBits(int validBits, int fallbackBits)
        {
            var bits = validBits > 0 && validBits <= fallbackBits ? validBits : fallbackBits;
            return (1 << bits) - 1;
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
