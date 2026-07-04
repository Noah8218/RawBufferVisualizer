namespace RawBufferVisualizer.Core
{
    public enum RawPixelFormat
    {
        Mono8,
        Mono16,
        Binary,
        RGB24,
        BGR24,
        BGRA32,
        Float32,
        BayerRGGB8,
        BayerGRBG8,
        BayerGBRG8,
        BayerBGGR8
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
                    return 4;
                default:
                    return 1;
            }
        }

        public int GetMinimumStride()
        {
            return Width > 0 ? Width * GetBytesPerPixel() : 0;
        }

        public long GetRequiredByteCount()
        {
            if (Height <= 0)
            {
                return 0;
            }

            var minimumStride = GetMinimumStride();
            if (Stride <= 0 || minimumStride <= 0)
            {
                return 0;
            }

            return ((long)Stride * (Height - 1)) + minimumStride;
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

