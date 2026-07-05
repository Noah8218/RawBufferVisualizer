using System;
using RawBufferVisualizer.Core;

namespace RawBufferVisualizer.Sdk
{
    public sealed class RawBufferView
    {
        public IntPtr Buffer { get; set; }
        public long BufferLength { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Stride { get; set; }
        public RawPixelFormat PixelFormat { get; set; }
        public int Channels { get; set; }
        public int BitDepth { get; set; }
        public RawByteOrder ByteOrder { get; set; }
        public string? Name { get; set; }

        public RawBufferView()
        {
            PixelFormat = RawPixelFormat.Mono8;
            Channels = 1;
            BitDepth = 8;
            ByteOrder = RawByteOrder.LittleEndian;
        }

        public RawImageDescriptor ToDescriptor()
        {
            var descriptor = new RawImageDescriptor
            {
                Width = Width,
                Height = Height,
                Stride = Stride,
                PixelFormat = PixelFormat,
                ValidBits = BitDepth > 0 ? BitDepth : GetDefaultBitDepth(PixelFormat),
                ByteOrder = ByteOrder
            };

            ValidateChannelCount(descriptor);
            return descriptor;
        }

        public long GetBufferLength()
        {
            if (BufferLength > 0)
            {
                return BufferLength;
            }

            var descriptor = ToDescriptor();
            if (descriptor.Stride <= 0 || descriptor.Height <= 0)
            {
                return 0;
            }

            return checked((long)descriptor.Stride * descriptor.Height);
        }

        public RawBufferSnapshot ToSnapshot()
        {
            if (Buffer == IntPtr.Zero)
            {
                throw new InvalidOperationException("Buffer pointer is empty.");
            }

            var length = GetBufferLength();
            if (length > int.MaxValue)
            {
                throw new InvalidOperationException("RawBufferView is too large to copy into a single managed snapshot.");
            }

            return RawBufferSnapshot.FromIntPtr(Buffer, checked((int)length), ToDescriptor());
        }

        private void ValidateChannelCount(RawImageDescriptor descriptor)
        {
            if (Channels <= 0)
            {
                return;
            }

            var expected = GetExpectedChannelCount(descriptor.PixelFormat);
            if (expected > 0 && Channels != expected)
            {
                throw new InvalidOperationException(
                    "Channel count " + Channels + " does not match " + descriptor.PixelFormat + " which expects " + expected + ".");
            }
        }

        private static int GetExpectedChannelCount(RawPixelFormat pixelFormat)
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

        private static int GetDefaultBitDepth(RawPixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case RawPixelFormat.Mono16:
                    return 16;
                case RawPixelFormat.Mono10PackedLsb:
                    return 10;
                case RawPixelFormat.Mono12PackedLsb:
                    return 12;
                case RawPixelFormat.Binary:
                    return 1;
                case RawPixelFormat.Float32:
                    return 32;
                default:
                    return 8;
            }
        }
    }
}
