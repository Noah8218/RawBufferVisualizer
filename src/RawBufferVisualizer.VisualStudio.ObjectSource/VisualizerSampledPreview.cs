using System;
using RawBufferVisualizer.Core;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    public static unsafe class VisualizerSampledPreview
    {
        public const int DefaultMaximumDimension = 512;
        private const int MaximumAllowedDimension = 2048;

        public static VisualizerSnapshotTransfer Create(
            byte[] buffer,
            RawImageDescriptor descriptor,
            string sourceType,
            string? displayName,
            int maximumWidth,
            int maximumHeight)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            return CreateCore(
                RawByteAccessor.FromBuffer(buffer, descriptor),
                descriptor,
                sourceType,
                displayName,
                maximumWidth,
                maximumHeight);
        }

        public static VisualizerSnapshotTransfer Create(
            IntPtr pointer,
            long bufferLength,
            RawImageDescriptor descriptor,
            string sourceType,
            string? displayName,
            int maximumWidth,
            int maximumHeight)
        {
            return CreateCore(
                RawByteAccessor.FromPointer(pointer, bufferLength, descriptor, descriptor.Stride),
                descriptor,
                sourceType,
                displayName,
                maximumWidth,
                maximumHeight);
        }

        public static VisualizerSnapshotTransfer CreateFromRows(
            IntPtr scan0,
            int sourceStride,
            long bufferLength,
            RawImageDescriptor descriptor,
            string sourceType,
            string? displayName,
            int maximumWidth,
            int maximumHeight)
        {
            return CreateCore(
                RawByteAccessor.FromPointer(scan0, bufferLength, descriptor, sourceStride),
                descriptor,
                sourceType,
                displayName,
                maximumWidth,
                maximumHeight);
        }

        private static VisualizerSnapshotTransfer CreateCore(
            RawByteAccessor source,
            RawImageDescriptor descriptor,
            string sourceType,
            string? displayName,
            int maximumWidth,
            int maximumHeight)
        {
            ValidateDescriptor(descriptor);
            ValidateMaximumDimension(maximumWidth, nameof(maximumWidth));
            ValidateMaximumDimension(maximumHeight, nameof(maximumHeight));

            var horizontalStep = DivideRoundUp(descriptor.Width, maximumWidth);
            var verticalStep = DivideRoundUp(descriptor.Height, maximumHeight);
            var sampleStep = Math.Max(1, Math.Max(horizontalStep, verticalStep));
            var previewWidth = DivideRoundUp(descriptor.Width, sampleStep);
            var previewHeight = DivideRoundUp(descriptor.Height, sampleStep);
            var pixels = new byte[checked(previewWidth * previewHeight * 4)];

            if (descriptor.PixelFormat == RawPixelFormat.Float32)
            {
                WriteFloatPreview(source, descriptor, sampleStep, previewWidth, previewHeight, pixels);
            }
            else
            {
                WritePreview(source, descriptor, sampleStep, previewWidth, previewHeight, pixels);
            }

            return new VisualizerSnapshotTransfer
            {
                Descriptor = new RawImageDescriptor
                {
                    Width = previewWidth,
                    Height = previewHeight,
                    Stride = checked(previewWidth * 4),
                    PixelFormat = RawPixelFormat.BGRA32,
                    ValidBits = 8,
                    ByteOrder = RawByteOrder.LittleEndian
                },
                Buffer = pixels,
                SourceType = sourceType ?? string.Empty,
                DisplayName = displayName ?? string.Empty
            };
        }

        private static void WritePreview(
            RawByteAccessor source,
            RawImageDescriptor descriptor,
            int sampleStep,
            int previewWidth,
            int previewHeight,
            byte[] pixels)
        {
            var target = 0;
            for (var previewY = 0; previewY < previewHeight; previewY++)
            {
                var sourceY = Math.Min(descriptor.Height - 1, previewY * sampleStep);
                for (var previewX = 0; previewX < previewWidth; previewX++)
                {
                    var sourceX = Math.Min(descriptor.Width - 1, previewX * sampleStep);
                    byte b;
                    byte g;
                    byte r;
                    ReadBgr(source, descriptor, sourceX, sourceY, out b, out g, out r);
                    pixels[target++] = b;
                    pixels[target++] = g;
                    pixels[target++] = r;
                    pixels[target++] = 255;
                }
            }
        }

        private static void WriteFloatPreview(
            RawByteAccessor source,
            RawImageDescriptor descriptor,
            int sampleStep,
            int previewWidth,
            int previewHeight,
            byte[] pixels)
        {
            var values = new float[checked(previewWidth * previewHeight)];
            var minimum = float.PositiveInfinity;
            var maximum = float.NegativeInfinity;
            var index = 0;
            for (var previewY = 0; previewY < previewHeight; previewY++)
            {
                var sourceY = Math.Min(descriptor.Height - 1, previewY * sampleStep);
                for (var previewX = 0; previewX < previewWidth; previewX++)
                {
                    var sourceX = Math.Min(descriptor.Width - 1, previewX * sampleStep);
                    var value = ReadFloat(source, descriptor, sourceX, sourceY);
                    values[index++] = value;
                    if (!float.IsNaN(value) && !float.IsInfinity(value))
                    {
                        minimum = Math.Min(minimum, value);
                        maximum = Math.Max(maximum, value);
                    }
                }
            }

            var hasRange = !float.IsInfinity(minimum) && maximum > minimum;
            var target = 0;
            for (index = 0; index < values.Length; index++)
            {
                var value = values[index];
                byte gray;
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    gray = 0;
                }
                else if (hasRange)
                {
                    gray = ScaleToByte((value - minimum) / (maximum - minimum));
                }
                else
                {
                    gray = 128;
                }

                pixels[target++] = gray;
                pixels[target++] = gray;
                pixels[target++] = gray;
                pixels[target++] = 255;
            }
        }

        private static void ReadBgr(
            RawByteAccessor source,
            RawImageDescriptor descriptor,
            int x,
            int y,
            out byte b,
            out byte g,
            out byte r)
        {
            switch (descriptor.PixelFormat)
            {
                case RawPixelFormat.Binary:
                    var binary = source.Read(y, x) == 0 ? (byte)0 : (byte)255;
                    b = binary;
                    g = binary;
                    r = binary;
                    return;

                case RawPixelFormat.Mono8:
                case RawPixelFormat.BayerRGGB8:
                case RawPixelFormat.BayerGRBG8:
                case RawPixelFormat.BayerGBRG8:
                case RawPixelFormat.BayerBGGR8:
                    var mono8 = source.Read(y, x);
                    b = mono8;
                    g = mono8;
                    r = mono8;
                    return;

                case RawPixelFormat.Mono16:
                    var mono16 = ReadUInt16(source, descriptor, y, x * 2);
                    var gray16 = ScaleUnsigned(mono16, descriptor.ValidBits <= 0 ? 16 : descriptor.ValidBits);
                    b = gray16;
                    g = gray16;
                    r = gray16;
                    return;

                case RawPixelFormat.Mono10PackedLsb:
                    var mono10 = ReadPackedLsb(source, y, x, 10);
                    var gray10 = ScaleUnsigned(mono10, 10);
                    b = gray10;
                    g = gray10;
                    r = gray10;
                    return;

                case RawPixelFormat.Mono12PackedLsb:
                    var mono12 = ReadPackedLsb(source, y, x, 12);
                    var gray12 = ScaleUnsigned(mono12, 12);
                    b = gray12;
                    g = gray12;
                    r = gray12;
                    return;

                case RawPixelFormat.RGB24:
                    var rgbOffset = x * 3;
                    r = source.Read(y, rgbOffset);
                    g = source.Read(y, rgbOffset + 1);
                    b = source.Read(y, rgbOffset + 2);
                    return;

                case RawPixelFormat.BGR24:
                    var bgrOffset = x * 3;
                    b = source.Read(y, bgrOffset);
                    g = source.Read(y, bgrOffset + 1);
                    r = source.Read(y, bgrOffset + 2);
                    return;

                case RawPixelFormat.BGRA32:
                    var bgraOffset = x * 4;
                    b = source.Read(y, bgraOffset);
                    g = source.Read(y, bgraOffset + 1);
                    r = source.Read(y, bgraOffset + 2);
                    return;

                default:
                    throw new NotSupportedException("Sampled preview is not supported for " + descriptor.PixelFormat + ".");
            }
        }

        private static ushort ReadUInt16(RawByteAccessor source, RawImageDescriptor descriptor, int y, int offset)
        {
            var first = source.Read(y, offset);
            var second = source.Read(y, offset + 1);
            return descriptor.ByteOrder == RawByteOrder.BigEndian
                ? (ushort)((first << 8) | second)
                : (ushort)(first | (second << 8));
        }

        private static uint ReadPackedLsb(RawByteAccessor source, int y, int x, int bitsPerPixel)
        {
            var bitOffset = (long)x * bitsPerPixel;
            var byteOffset = checked((int)(bitOffset / 8));
            var shift = (int)(bitOffset % 8);
            uint packed = source.Read(y, byteOffset);
            packed |= (uint)source.Read(y, byteOffset + 1) << 8;
            if (shift + bitsPerPixel > 16)
            {
                packed |= (uint)source.Read(y, byteOffset + 2) << 16;
            }

            return (packed >> shift) & ((1u << bitsPerPixel) - 1u);
        }

        private static float ReadFloat(RawByteAccessor source, RawImageDescriptor descriptor, int x, int y)
        {
            var offset = x * 4;
            uint bits;
            if (descriptor.ByteOrder == RawByteOrder.BigEndian)
            {
                bits = (uint)(source.Read(y, offset) << 24)
                    | (uint)(source.Read(y, offset + 1) << 16)
                    | (uint)(source.Read(y, offset + 2) << 8)
                    | source.Read(y, offset + 3);
            }
            else
            {
                bits = source.Read(y, offset)
                    | (uint)(source.Read(y, offset + 1) << 8)
                    | (uint)(source.Read(y, offset + 2) << 16)
                    | (uint)(source.Read(y, offset + 3) << 24);
            }

            return *(float*)&bits;
        }

        private static byte ScaleUnsigned(uint value, int validBits)
        {
            validBits = Math.Max(1, Math.Min(16, validBits));
            var maximum = (1u << validBits) - 1u;
            return (byte)Math.Min(255u, (value * 255u + (maximum / 2u)) / maximum);
        }

        private static byte ScaleToByte(double normalized)
        {
            if (normalized <= 0)
            {
                return 0;
            }

            if (normalized >= 1)
            {
                return 255;
            }

            return (byte)Math.Round(normalized * 255.0);
        }

        private static void ValidateDescriptor(RawImageDescriptor descriptor)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            if (descriptor.Width <= 0 || descriptor.Height <= 0 || descriptor.Stride < descriptor.GetMinimumStride())
            {
                throw new ArgumentException("Image descriptor is invalid.", nameof(descriptor));
            }
        }

        private static void ValidateMaximumDimension(int value, string name)
        {
            if (value <= 0 || value > MaximumAllowedDimension)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        private static int DivideRoundUp(int value, int divisor)
        {
            return (int)(((long)value + divisor - 1) / divisor);
        }

        private readonly struct RawByteAccessor
        {
            private readonly byte[]? _buffer;
            private readonly long _pointerAddress;
            private readonly long _length;
            private readonly int _logicalStride;
            private readonly int _sourceStride;
            private readonly int _height;

            private RawByteAccessor(
                byte[]? buffer,
                long pointerAddress,
                long length,
                int logicalStride,
                int sourceStride,
                int height)
            {
                _buffer = buffer;
                _pointerAddress = pointerAddress;
                _length = length;
                _logicalStride = logicalStride;
                _sourceStride = sourceStride;
                _height = height;
            }

            public static RawByteAccessor FromBuffer(byte[] buffer, RawImageDescriptor descriptor)
            {
                ValidateLength(buffer.LongLength, descriptor);
                return new RawByteAccessor(
                    buffer,
                    0,
                    buffer.LongLength,
                    descriptor.Stride,
                    descriptor.Stride,
                    descriptor.Height);
            }

            public static RawByteAccessor FromPointer(
                IntPtr pointer,
                long bufferLength,
                RawImageDescriptor descriptor,
                int sourceStride)
            {
                if (pointer == IntPtr.Zero)
                {
                    throw new ArgumentException("Image data pointer is empty.", nameof(pointer));
                }

                ValidateLength(bufferLength, descriptor);
                if (Math.Abs((long)sourceStride) < descriptor.GetMinimumStride())
                {
                    throw new ArgumentException("Source stride is smaller than the image row.", nameof(sourceStride));
                }

                return new RawByteAccessor(
                    null,
                    pointer.ToInt64(),
                    bufferLength,
                    descriptor.Stride,
                    sourceStride,
                    descriptor.Height);
            }

            public byte Read(int y, int rowOffset)
            {
                if (y < 0 || y >= _height || rowOffset < 0 || rowOffset >= _logicalStride)
                {
                    throw new ArgumentOutOfRangeException(nameof(rowOffset));
                }

                var normalizedOffset = checked(((long)y * _logicalStride) + rowOffset);
                if (normalizedOffset < 0 || normalizedOffset >= _length)
                {
                    throw new ArgumentOutOfRangeException(nameof(rowOffset));
                }

                if (_buffer != null)
                {
                    return _buffer[checked((int)normalizedOffset)];
                }

                var rowOffsetBytes = _sourceStride >= 0
                    ? checked((long)y * _sourceStride)
                    : checked((long)(_height - 1 - y) * -(long)_sourceStride);
                return *((byte*)(_pointerAddress + rowOffsetBytes + rowOffset));
            }

            private static void ValidateLength(long length, RawImageDescriptor descriptor)
            {
                if (length < descriptor.GetRequiredByteCount())
                {
                    throw new ArgumentException("Image buffer is smaller than the descriptor requires.", nameof(length));
                }
            }
        }
    }
}
