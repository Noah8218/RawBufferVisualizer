using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

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
        public virtual bool IsLiveProcessBacked
        {
            get { return false; }
        }

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

        public static RawImageSource FromProcessMemory(
            int processId,
            long bufferAddress,
            long bufferLength,
            RawImageDescriptor descriptor)
        {
            return new ProcessMemoryRawImageSource(processId, bufferAddress, bufferLength, descriptor);
        }

        public static bool CanStreamFormat(RawPixelFormat pixelFormat)
        {
            return true;
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

        public virtual RenderedImage RenderTileSampled(
            int x,
            int y,
            int width,
            int height,
            int sampleStep,
            RawRenderOptions? options,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return RenderTileSampled(x, y, width, height, sampleStep, options);
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

        protected static void WriteBgra(byte[] pixels, int offset, byte b, byte g, byte r, byte a)
        {
            pixels[offset] = b;
            pixels[offset + 1] = g;
            pixels[offset + 2] = r;
            pixels[offset + 3] = a;
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

        public override RenderedImage RenderTileSampled(int x, int y, int width, int height, int sampleStep, RawRenderOptions? options)
        {
            return RenderTileSampled(x, y, width, height, sampleStep, options, CancellationToken.None);
        }

        public override RenderedImage RenderTileSampled(
            int x,
            int y,
            int width,
            int height,
            int sampleStep,
            RawRenderOptions? options,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sampleStep <= 1)
            {
                return RenderTile(x, y, width, height, options);
            }

            EnsureTileBounds(SourceDescriptor, x, y, width, height);
            options = options ?? CreateRenderOptions();

            var sampledWidth = Math.Max(1, (width + sampleStep - 1) / sampleStep);
            var sampledHeight = Math.Max(1, (height + sampleStep - 1) / sampleStep);
            var pixels = new byte[checked(sampledWidth * sampledHeight * 4)];

            for (var sampledY = 0; sampledY < sampledHeight; sampledY++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourceY = y + (sampledY * sampleStep);
                var targetRow = sampledY * sampledWidth * 4;
                for (var sampledX = 0; sampledX < sampledWidth; sampledX++)
                {
                    var sourceX = x + (sampledX * sampleStep);
                    WriteSampledPixel(sourceX, sourceY, pixels, targetRow + (sampledX * 4), options);
                }
            }

            return new RenderedImage(sampledWidth, sampledHeight, pixels);
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

        private void WriteSampledPixel(int x, int y, byte[] pixels, int targetOffset, RawRenderOptions options)
        {
            var row = y * SourceDescriptor.Stride;
            switch (SourceDescriptor.PixelFormat)
            {
                case RawPixelFormat.Mono8:
                    WriteBgra(pixels, targetOffset, _buffer[row + x], _buffer[row + x], _buffer[row + x], 255);
                    break;
                case RawPixelFormat.Binary:
                    var binary = _buffer[row + x] == 0 ? (byte)0 : (byte)255;
                    WriteBgra(pixels, targetOffset, binary, binary, binary, 255);
                    break;
                case RawPixelFormat.Mono16:
                    var mono16 = RawBufferRenderer.ReadUInt16(_buffer, row + (x * 2), SourceDescriptor.ByteOrder);
                    var mono16Value = ScaleToByte(mono16, options);
                    WriteBgra(pixels, targetOffset, mono16Value, mono16Value, mono16Value, 255);
                    break;
                case RawPixelFormat.Mono10PackedLsb:
                    var mono10 = RawBufferRenderer.ReadPackedLsb(_buffer, row, x, 10);
                    var mono10Value = ScaleToByte(mono10, options);
                    WriteBgra(pixels, targetOffset, mono10Value, mono10Value, mono10Value, 255);
                    break;
                case RawPixelFormat.Mono12PackedLsb:
                    var mono12 = RawBufferRenderer.ReadPackedLsb(_buffer, row, x, 12);
                    var mono12Value = ScaleToByte(mono12, options);
                    WriteBgra(pixels, targetOffset, mono12Value, mono12Value, mono12Value, 255);
                    break;
                case RawPixelFormat.Float32:
                    var floatValue = RawBufferRenderer.ReadSingle(_buffer, row + (x * 4), SourceDescriptor.ByteOrder);
                    var floatByte = double.IsNaN(floatValue) || double.IsInfinity(floatValue) ? (byte)0 : ScaleToByte(floatValue, options);
                    WriteBgra(pixels, targetOffset, floatByte, floatByte, floatByte, 255);
                    break;
                case RawPixelFormat.RGB24:
                    var rgbOffset = row + (x * 3);
                    WriteBgra(pixels, targetOffset, _buffer[rgbOffset + 2], _buffer[rgbOffset + 1], _buffer[rgbOffset], 255);
                    break;
                case RawPixelFormat.BGR24:
                    var bgrOffset = row + (x * 3);
                    WriteBgra(pixels, targetOffset, _buffer[bgrOffset], _buffer[bgrOffset + 1], _buffer[bgrOffset + 2], 255);
                    break;
                case RawPixelFormat.BGRA32:
                    var bgraOffset = row + (x * 4);
                    WriteBgra(pixels, targetOffset, _buffer[bgraOffset], _buffer[bgraOffset + 1], _buffer[bgraOffset + 2], _buffer[bgraOffset + 3]);
                    break;
                case RawPixelFormat.BayerRGGB8:
                case RawPixelFormat.BayerGRBG8:
                case RawPixelFormat.BayerGBRG8:
                case RawPixelFormat.BayerBGGR8:
                    WriteBayerSample(_buffer[row + x], pixels, targetOffset, x, y);
                    break;
                default:
                    throw new NotSupportedException("Unsupported pixel format: " + SourceDescriptor.PixelFormat);
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
    }

    internal abstract class RandomAccessRawImageSource : RawImageSource
    {
        protected RandomAccessRawImageSource(
            RawImageDescriptor descriptor,
            long length,
            string? rawPath)
            : base(descriptor, length, rawPath)
        {
        }

        public override RawRenderOptions CreateRenderOptions()
        {
            switch (SourceDescriptor.PixelFormat)
            {
                case RawPixelFormat.Mono16:
                    return CreateFixedRangeOptions(GetMaxForValidBits(SourceDescriptor.ValidBits, 16));
                case RawPixelFormat.Mono10PackedLsb:
                    return CreateFixedRangeOptions(1023);
                case RawPixelFormat.Mono12PackedLsb:
                    return CreateFixedRangeOptions(4095);
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
            return RenderTileSampled(x, y, width, height, sampleStep, options, CancellationToken.None);
        }

        public override RenderedImage RenderTileSampled(
            int x,
            int y,
            int width,
            int height,
            int sampleStep,
            RawRenderOptions? options,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sampleStep <= 1)
            {
                return RenderTile(x, y, width, height, options);
            }

            EnsureTileBounds(SourceDescriptor, x, y, width, height);
            options = options ?? CreateRenderOptions();

            if (IsPackedMono(SourceDescriptor.PixelFormat))
            {
                return RenderPackedTileSampled(x, y, width, height, sampleStep, options, cancellationToken);
            }

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
                    cancellationToken.ThrowIfCancellationRequested();
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
            if (IsPackedMono(SourceDescriptor.PixelFormat))
            {
                var bitsPerPixel = GetPackedBitsPerPixel(SourceDescriptor.PixelFormat);
                var value = ReadPackedPixel(x, y, bitsPerPixel);
                using (var stream = OpenRawReadStream())
                {
                    var row = ReadPackedRowWindow(stream, y, x, 1, bitsPerPixel, out _);
                    return string.Format(CultureInfo.InvariantCulture, "X={0}, Y={1}, GV={2}, Value={2}, Raw={3}", x, y, value, FormatBytes(row));
                }
            }

            RawImageDescriptor pixelDescriptor;
            int localX;
            int localY;
            var buffer = ReadTileWindow(x, y, 1, 1, false, out pixelDescriptor, out localX, out localY);
            if (IsBayer(SourceDescriptor.PixelFormat))
            {
                var color = RawBufferRenderer.GetBayerColor(SourceDescriptor.PixelFormat, x, y);
                var colorName = color == 0 ? "R" : color == 1 ? "G" : "B";
                return string.Format(CultureInfo.InvariantCulture, "X={0}, Y={1}, Bayer {2}={3}, Raw={4:X2}", x, y, colorName, buffer[0], buffer[0]);
            }

            return RawPixelInspector.Describe(buffer, pixelDescriptor, localX, localY, x, y);
        }

        public override byte[] ReadAllBytes()
        {
            if (Length > int.MaxValue)
            {
                throw new InvalidOperationException("The raw payload is too large to read into a single byte array.");
            }

            var buffer = new byte[(int)Length];
            using (var stream = OpenRawReadStream())
            {
                ReadExactly(stream, buffer, 0, buffer.Length);
            }

            return buffer;
        }

        public override void CopyRawTo(string rawPath)
        {
            var fullPath = Path.GetFullPath(rawPath);
            EnsureParentDirectory(fullPath);
            using (var source = OpenRawReadStream())
            using (var target = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                source.CopyTo(target);
            }
        }

        private byte[] ReadTileWindow(int x, int y, int width, int height, bool includeBayerHalo, out RawImageDescriptor tileDescriptor, out int localX, out int localY)
        {
            if (IsPackedMono(SourceDescriptor.PixelFormat))
            {
                return ReadPackedTileWindow(x, y, width, height, out tileDescriptor, out localX, out localY);
            }

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

        private byte[] ReadPackedTileWindow(int x, int y, int width, int height, out RawImageDescriptor tileDescriptor, out int localX, out int localY)
        {
            var bitsPerPixel = GetPackedBitsPerPixel(SourceDescriptor.PixelFormat);
            var localStride = GetPackedStride(width, bitsPerPixel);
            var byteCount = checked((long)localStride * height);
            if (byteCount > int.MaxValue)
            {
                throw new InvalidOperationException("Packed raw tile window is too large to stage in memory.");
            }

            var buffer = new byte[(int)byteCount];
            using (var stream = OpenRawReadStream())
            {
                for (var row = 0; row < height; row++)
                {
                    var sourceRow = ReadPackedRowWindow(stream, y + row, x, width, bitsPerPixel, out var firstBitOffset);
                    var targetRow = row * localStride;
                    for (var tileX = 0; tileX < width; tileX++)
                    {
                        var value = ReadPackedBits(sourceRow, firstBitOffset + (tileX * bitsPerPixel), bitsPerPixel);
                        WritePackedBits(buffer, targetRow + (tileX * bitsPerPixel / 8), (tileX * bitsPerPixel) % 8, bitsPerPixel, value);
                    }
                }
            }

            tileDescriptor = SourceDescriptor.Clone();
            tileDescriptor.Width = width;
            tileDescriptor.Height = height;
            tileDescriptor.Stride = localStride;
            localX = 0;
            localY = 0;
            return buffer;
        }

        private RenderedImage RenderPackedTileSampled(
            int x,
            int y,
            int width,
            int height,
            int sampleStep,
            RawRenderOptions options,
            CancellationToken cancellationToken)
        {
            var bitsPerPixel = GetPackedBitsPerPixel(SourceDescriptor.PixelFormat);
            var sampledWidth = Math.Max(1, (width + sampleStep - 1) / sampleStep);
            var sampledHeight = Math.Max(1, (height + sampleStep - 1) / sampleStep);
            var pixels = new byte[checked(sampledWidth * sampledHeight * 4)];

            using (var stream = OpenRawReadStream())
            {
                for (var sampledY = 0; sampledY < sampledHeight; sampledY++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var sourceY = y + (sampledY * sampleStep);
                    var sourceRow = ReadPackedRowWindow(stream, sourceY, x, width, bitsPerPixel, out var firstBitOffset);
                    var targetRow = sampledY * sampledWidth * 4;

                    for (var sampledX = 0; sampledX < sampledWidth; sampledX++)
                    {
                        var tileX = sampledX * sampleStep;
                        var value = ReadPackedBits(sourceRow, firstBitOffset + (tileX * bitsPerPixel), bitsPerPixel);
                        var byteValue = ScaleToByte(value, options);
                        WriteBgra(pixels, targetRow + (sampledX * 4), byteValue, byteValue, byteValue, 255);
                    }
                }
            }

            return new RenderedImage(sampledWidth, sampledHeight, pixels);
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

        private void ReadRowSpan(Stream stream, int y, int x, int byteCount, byte[] target)
        {
            ReadRowSpan(stream, y, x, byteCount, target, 0);
        }

        private void ReadRowSpan(Stream stream, int y, int x, int byteCount, byte[] target, int targetOffset)
        {
            var bytesPerPixel = GetStreamableBytesPerPixel(SourceDescriptor.PixelFormat);
            var sourceOffset = checked(((long)y * SourceDescriptor.Stride) + ((long)x * bytesPerPixel));
            stream.Position = sourceOffset;
            ReadExactly(stream, target, targetOffset, byteCount);
        }

        private byte[] ReadPackedRowWindow(Stream stream, int y, int x, int width, int bitsPerPixel, out int firstBitOffset)
        {
            var startBit = checked(x * bitsPerPixel);
            var endBit = checked((x + width) * bitsPerPixel);
            var startByte = startBit / 8;
            var endByte = (endBit + 7) / 8;
            var byteCount = endByte - startByte;
            var buffer = new byte[byteCount];
            stream.Position = checked(((long)y * SourceDescriptor.Stride) + startByte);
            ReadExactly(stream, buffer, 0, byteCount);
            firstBitOffset = startBit - (startByte * 8);
            return buffer;
        }

        private int ReadPackedPixel(int x, int y, int bitsPerPixel)
        {
            using (var stream = OpenRawReadStream())
            {
                var row = ReadPackedRowWindow(stream, y, x, 1, bitsPerPixel, out var firstBitOffset);
                return ReadPackedBits(row, firstBitOffset, bitsPerPixel);
            }
        }

        protected abstract Stream OpenRawReadStream();

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

        private static bool IsPackedMono(RawPixelFormat format)
        {
            return format == RawPixelFormat.Mono10PackedLsb
                || format == RawPixelFormat.Mono12PackedLsb;
        }

        private static int GetPackedBitsPerPixel(RawPixelFormat format)
        {
            switch (format)
            {
                case RawPixelFormat.Mono10PackedLsb:
                    return 10;
                case RawPixelFormat.Mono12PackedLsb:
                    return 12;
                default:
                    throw new NotSupportedException("Pixel format is not packed mono: " + format);
            }
        }

        private static int GetPackedStride(int width, int bitsPerPixel)
        {
            return (int)(((long)width * bitsPerPixel + 7) / 8);
        }

        private static int ReadPackedBits(byte[] buffer, int bitOffset, int bitsPerPixel)
        {
            var byteOffset = bitOffset / 8;
            var shift = bitOffset % 8;
            var value = 0;
            var bytesToRead = (shift + bitsPerPixel + 7) / 8;
            for (var i = 0; i < bytesToRead; i++)
            {
                value |= buffer[byteOffset + i] << (i * 8);
            }

            return (value >> shift) & ((1 << bitsPerPixel) - 1);
        }

        private static void WritePackedBits(byte[] buffer, int byteOffset, int shift, int bitsPerPixel, int value)
        {
            var bytesToWrite = (shift + bitsPerPixel + 7) / 8;
            var packedValue = value << shift;
            for (var i = 0; i < bytesToWrite; i++)
            {
                buffer[byteOffset + i] |= (byte)((packedValue >> (i * 8)) & 0xFF);
            }
        }

        private static bool IsBayer(RawPixelFormat format)
        {
            return format == RawPixelFormat.BayerRGGB8
                || format == RawPixelFormat.BayerGRBG8
                || format == RawPixelFormat.BayerGBRG8
                || format == RawPixelFormat.BayerBGGR8;
        }

        private static string FormatBytes(byte[] buffer)
        {
            var parts = new string[buffer.Length];
            for (var i = 0; i < buffer.Length; i++)
            {
                parts[i] = buffer[i].ToString("X2", CultureInfo.InvariantCulture);
            }

            return string.Join(" ", parts);
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
    }

    internal sealed class FileRawImageSource : RandomAccessRawImageSource
    {
        private readonly string _rawPath;

        public override bool IsFileBacked
        {
            get { return true; }
        }

        public FileRawImageSource(string rawPath, RawImageDescriptor descriptor)
            : base(descriptor, GetFileLength(rawPath), Path.GetFullPath(rawPath))
        {
            _rawPath = Path.GetFullPath(rawPath);
        }

        public override RawImageSource WithDescriptor(RawImageDescriptor descriptor)
        {
            return new FileRawImageSource(_rawPath, descriptor);
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

        protected override Stream OpenRawReadStream()
        {
            return new FileStream(_rawPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.RandomAccess);
        }

        private static long GetFileLength(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                throw new ArgumentException("Raw path is required.", "rawPath");
            }

            return new FileInfo(Path.GetFullPath(rawPath)).Length;
        }
    }

    internal sealed class ProcessMemoryRawImageSource : RandomAccessRawImageSource
    {
        private readonly int _processId;
        private readonly long _bufferAddress;

        public override bool IsFileBacked
        {
            get { return true; }
        }

        public override bool IsLiveProcessBacked
        {
            get { return true; }
        }

        public ProcessMemoryRawImageSource(
            int processId,
            long bufferAddress,
            long bufferLength,
            RawImageDescriptor descriptor)
            : base(descriptor, ValidateBufferLength(bufferAddress, bufferLength, descriptor), null)
        {
            if (processId <= 0)
            {
                throw new ArgumentOutOfRangeException("processId");
            }

            _processId = processId;
            _bufferAddress = bufferAddress;
        }

        public override RawImageSource WithDescriptor(RawImageDescriptor descriptor)
        {
            return new ProcessMemoryRawImageSource(_processId, _bufferAddress, Length, descriptor);
        }

        protected override Stream OpenRawReadStream()
        {
            return new ProcessMemoryReadStream(_processId, _bufferAddress, Length);
        }

        private static long ValidateBufferLength(
            long bufferAddress,
            long bufferLength,
            RawImageDescriptor descriptor)
        {
            if (bufferAddress == 0)
            {
                throw new ArgumentException("Debuggee buffer address is empty.", "bufferAddress");
            }

            if (descriptor == null)
            {
                throw new ArgumentNullException("descriptor");
            }

            if (bufferLength < descriptor.GetRequiredByteCount())
            {
                throw new ArgumentException("Debuggee buffer is smaller than descriptor requires.", "bufferLength");
            }

            return bufferLength;
        }
    }

    internal sealed class ProcessMemoryReadStream : Stream
    {
        private const int ProcessVmRead = 0x0010;
        private const int ProcessQueryLimitedInformation = 0x1000;

        private readonly IntPtr _processHandle;
        private readonly long _bufferAddress;
        private readonly long _length;
        private long _position;
        private bool _disposed;

        public ProcessMemoryReadStream(int processId, long bufferAddress, long length)
        {
            _processHandle = OpenProcess(ProcessVmRead | ProcessQueryLimitedInformation, false, processId);
            if (_processHandle == IntPtr.Zero)
            {
                throw CreateWin32Exception("Unable to open the paused debuggee for image reads.");
            }

            _bufferAddress = bufferAddress;
            _length = length;
        }

        public override bool CanRead { get { return !_disposed; } }
        public override bool CanSeek { get { return !_disposed; } }
        public override bool CanWrite { get { return false; } }
        public override long Length { get { ThrowIfDisposed(); return _length; } }

        public override long Position
        {
            get { ThrowIfDisposed(); return _position; }
            set
            {
                ThrowIfDisposed();
                if (value < 0 || value > _length)
                {
                    throw new ArgumentOutOfRangeException("value");
                }

                _position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ThrowIfDisposed();
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            if (offset < 0 || count < 0 || offset + count > buffer.Length)
            {
                throw new ArgumentOutOfRangeException("offset");
            }

            if (count == 0 || _position >= _length)
            {
                return 0;
            }

            var requested = (int)Math.Min(count, _length - _position);
            var pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var target = new IntPtr(checked(pin.AddrOfPinnedObject().ToInt64() + offset));
                var source = new IntPtr(checked(_bufferAddress + _position));
                UIntPtr bytesRead;
                var succeeded = ReadProcessMemory(
                    _processHandle,
                    source,
                    target,
                    new UIntPtr((uint)requested),
                    out bytesRead);
                var read = checked((int)bytesRead.ToUInt64());
                if (!succeeded || read <= 0)
                {
                    throw CreateWin32Exception(
                        "Debuggee image memory is no longer readable. Pause the debuggee and open the visualizer again.");
                }

                _position += read;
                return read;
            }
            finally
            {
                pin.Free();
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            ThrowIfDisposed();
            long next;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    next = offset;
                    break;
                case SeekOrigin.Current:
                    next = checked(_position + offset);
                    break;
                case SeekOrigin.End:
                    next = checked(_length + offset);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("origin");
            }

            Position = next;
            return _position;
        }

        public override void Flush()
        {
            ThrowIfDisposed();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                CloseHandle(_processHandle);
            }

            base.Dispose(disposing);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ProcessMemoryReadStream));
            }
        }

        private static RawImageSourceUnavailableException CreateWin32Exception(string message)
        {
            var errorCode = Marshal.GetLastWin32Error();
            return new RawImageSourceUnavailableException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} Win32 error {1}.",
                    message,
                    errorCode),
                errorCode);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(int desiredAccess, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReadProcessMemory(
            IntPtr processHandle,
            IntPtr baseAddress,
            IntPtr buffer,
            UIntPtr size,
            out UIntPtr numberOfBytesRead);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
