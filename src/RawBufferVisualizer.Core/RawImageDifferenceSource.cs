using System;
using System.Globalization;
using System.Threading;

namespace RawBufferVisualizer.Core
{
    public sealed class RawImageDifferenceSource : RawImageSource
    {
        private readonly RawImageSource _a;
        private readonly RawImageSource _b;
        private Tuple<RawRenderOptions, RawRenderOptions>? _inputOptions;

        public override bool IsFileBacked
        {
            get { return _a.IsFileBacked || _b.IsFileBacked; }
        }

        public override bool IsLiveProcessBacked
        {
            get { return _a.IsLiveProcessBacked || _b.IsLiveProcessBacked; }
        }

        public override bool CanExportRaw
        {
            get { return false; }
        }

        public RawImageDifferenceSource(RawImageSource a, RawImageSource b)
            : base(CreateDescriptor(a, b), CreateDescriptor(a, b).GetRequiredByteCount(), null)
        {
            _a = a ?? throw new ArgumentNullException("a");
            _b = b ?? throw new ArgumentNullException("b");
        }

        public override RawImageSource WithDescriptor(RawImageDescriptor descriptor)
        {
            throw new NotSupportedException("Diff view does not support descriptor reinterpretation.");
        }

        public override RawRenderOptions CreateRenderOptions()
        {
            Volatile.Write(ref _inputOptions, CreateInputOptions());
            return new RawRenderOptions { AutoScale = false, BlackLevel = 0, WhiteLevel = 255 };
        }

        private Tuple<RawRenderOptions, RawRenderOptions> CreateInputOptions()
        {
            var left = CopyOptions(_a.CreateRenderOptions());
            var right = CopyOptions(_b.CreateRenderOptions());
            return Tuple.Create(left, right);
        }

        private static RawRenderOptions CopyOptions(RawRenderOptions options)
        {
            return new RawRenderOptions { AutoScale = options.AutoScale, BlackLevel = options.BlackLevel, WhiteLevel = options.WhiteLevel };
        }

        public override RenderedImage RenderTile(int x, int y, int width, int height, RawRenderOptions? options)
        {
            return RenderTileSampled(x, y, width, height, 1, options);
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
            var inputOptions = Volatile.Read(ref _inputOptions);
            if (inputOptions == null)
            {
                var created = CreateInputOptions();
                inputOptions = Interlocked.CompareExchange(ref _inputOptions, created, null) ?? created;
            }
            var left = _a.RenderTileSampled(x, y, width, height, sampleStep, CopyOptions(inputOptions.Item1), cancellationToken);
            var right = _b.RenderTileSampled(x, y, width, height, sampleStep, CopyOptions(inputOptions.Item2), cancellationToken);
            var diff = new byte[left.Bgra32.Length];
            for (var i = 0; i < diff.Length; i += 4)
            {
                if ((i & 0x3FFF) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                diff[i] = AbsByte(left.Bgra32[i], right.Bgra32[i]);
                diff[i + 1] = AbsByte(left.Bgra32[i + 1], right.Bgra32[i + 1]);
                diff[i + 2] = AbsByte(left.Bgra32[i + 2], right.Bgra32[i + 2]);
                diff[i + 3] = 255;
            }

            return new RenderedImage(left.Width, left.Height, diff);
        }

        public override string DescribePixel(int x, int y)
        {
            var pixel = RenderTile(x, y, 1, 1, null);
            var descriptor = new RawImageDescriptor { Width = 1, Height = 1, Stride = 4, PixelFormat = RawPixelFormat.BGRA32, ValidBits = 8 };
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}, GV={1:R}, A=[{2}], B=[{3}]",
                RawPixelInspector.Describe(pixel.Bgra32, descriptor, 0, 0, x, y),
                (pixel.Bgra32[0] + pixel.Bgra32[1] + pixel.Bgra32[2]) / 3.0,
                _a.DescribePixel(x, y),
                _b.DescribePixel(x, y));
        }

        internal override Func<int, int, RenderedImage> CreateHistogramPixelReader()
        {
            var left = _a.CreateHistogramPixelReader();
            var right = _b.CreateHistogramPixelReader();
            return (x, y) =>
            {
                var a = left(x, y).Bgra32;
                var b = right(x, y).Bgra32;
                return new RenderedImage(1, 1, new[] { AbsByte(a[0], b[0]), AbsByte(a[1], b[1]), AbsByte(a[2], b[2]), (byte)255 });
            };
        }

        public override RawPixelStatistics GetStatistics(int x, int y, int width, int height)
        {
            EnsureTileBounds(SourceDescriptor, x, y, width, height);
            var rendered = RenderTile(x, y, width, height, null);
            var descriptor = new RawImageDescriptor { Width = width, Height = height, Stride = checked(width * 4), PixelFormat = RawPixelFormat.BGRA32, ValidBits = 8 };
            return RawPixelStatistics.Calculate(rendered.Bgra32, descriptor, 0, 0, width, height);
        }

        public override byte[] ReadAllBytes()
        {
            throw new NotSupportedException("Diff view is generated on demand and has no raw byte payload.");
        }

        public override void CopyRawTo(string rawPath)
        {
            throw new NotSupportedException("Diff view is generated on demand and cannot be exported as raw bytes.");
        }

        private static RawImageDescriptor CreateDescriptor(RawImageSource a, RawImageSource b)
        {
            if (a == null)
            {
                throw new ArgumentNullException("a");
            }

            if (b == null)
            {
                throw new ArgumentNullException("b");
            }

            var left = a.Descriptor;
            var right = b.Descriptor;
            if (left.Width != right.Width || left.Height != right.Height)
            {
                throw new InvalidOperationException("A/B diff requires images with the same width and height.");
            }

            return new RawImageDescriptor
            {
                Width = left.Width,
                Height = left.Height,
                Stride = checked(left.Width * 4),
                PixelFormat = RawPixelFormat.BGRA32,
                ValidBits = 8,
                ByteOrder = RawByteOrder.LittleEndian
            };
        }

        private static byte AbsByte(byte left, byte right)
        {
            return (byte)Math.Abs(left - right);
        }
    }
}
