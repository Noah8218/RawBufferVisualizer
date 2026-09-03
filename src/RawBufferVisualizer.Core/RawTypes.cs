namespace RawBufferVisualizer.Core
{
    public enum RawPixelFormat
    {
        Mono8,
        Mono16,
        Mono10PackedLsb,
        Mono12PackedLsb,
        Binary,
        RGB24,
        BGR24,
        BGRA32,
        Float32,
        BayerRGGB8,
        BayerGRBG8,
        BayerGBRG8,
        BayerBGGR8,
        Int32
    }

    public enum RawByteOrder
    {
        LittleEndian,
        BigEndian
    }

    public enum RawDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class RawImageDescriptor
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int Stride { get; set; }
        public RawPixelFormat PixelFormat { get; set; }
        public int ValidBits { get; set; }
        public RawByteOrder ByteOrder { get; set; }

        public int GetBytesPerPixel()
        {
            switch (PixelFormat)
            {
                case RawPixelFormat.Mono16:
                    return 2;
                case RawPixelFormat.RGB24:
                case RawPixelFormat.BGR24:
                    return 3;
                case RawPixelFormat.BGRA32:
                case RawPixelFormat.Float32:
                case RawPixelFormat.Int32:
                    return 4;
                default:
                    return 1;
            }
        }

        public int GetMinimumStride()
        {
            int minimumStride;
            return TryGetMinimumStride(out minimumStride) ? minimumStride : 0;
        }

        public bool TryGetMinimumStride(out int minimumStride)
        {
            minimumStride = 0;
            if (Width <= 0 || !System.Enum.IsDefined(typeof(RawPixelFormat), PixelFormat))
            {
                return false;
            }

            long calculatedStride;
            switch (PixelFormat)
            {
                case RawPixelFormat.Mono10PackedLsb:
                    calculatedStride = GetPackedStride(10);
                    break;
                case RawPixelFormat.Mono12PackedLsb:
                    calculatedStride = GetPackedStride(12);
                    break;
                default:
                    calculatedStride = checked((long)Width * GetBytesPerPixel());
                    break;
            }

            if (calculatedStride <= 0 || calculatedStride > int.MaxValue)
            {
                return false;
            }

            minimumStride = checked((int)calculatedStride);
            return true;
        }

        private long GetPackedStride(int bitsPerPixel)
        {
            return checked(((long)Width * bitsPerPixel + 7) / 8);
        }

        public long GetRequiredByteCount()
        {
            long requiredByteCount;
            return TryGetRequiredByteCount(out requiredByteCount) ? requiredByteCount : 0;
        }

        public bool TryGetRequiredByteCount(out long requiredByteCount)
        {
            requiredByteCount = 0;
            int minimumStride;
            if (Height <= 0 || Stride <= 0 || !TryGetMinimumStride(out minimumStride))
            {
                return false;
            }

            requiredByteCount = checked(((long)Stride * (Height - 1)) + minimumStride);
            return requiredByteCount > 0;
        }

        public RawImageDescriptor Clone()
        {
            return new RawImageDescriptor
            {
                Width = Width,
                Height = Height,
                Stride = Stride,
                PixelFormat = PixelFormat,
                ValidBits = ValidBits,
                ByteOrder = ByteOrder
            };
        }
    }

    public sealed class RawDiagnostic
    {
        public RawDiagnosticSeverity Severity { get; private set; }
        public string Message { get; private set; }

        public RawDiagnostic(RawDiagnosticSeverity severity, string message)
        {
            Severity = severity;
            Message = message;
        }

        public override string ToString()
        {
            return Severity + ": " + Message;
        }
    }

    public sealed class RawRenderOptions
    {
        public bool AutoScale { get; set; }
        public double BlackLevel { get; set; }
        public double WhiteLevel { get; set; }

        public RawRenderOptions()
        {
            AutoScale = true;
            WhiteLevel = 255;
        }
    }

    public sealed class RenderedImage
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Stride { get; private set; }
        public byte[] Bgra32 { get; private set; }

        public RenderedImage(int width, int height, byte[] bgra32)
        {
            Width = width;
            Height = height;
            Stride = width * 4;
            Bgra32 = bgra32;
        }
    }
}
