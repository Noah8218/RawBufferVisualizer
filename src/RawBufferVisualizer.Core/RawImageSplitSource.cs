using System;
using System.Globalization;
using System.Threading;

namespace RawBufferVisualizer.Core
{
    public sealed class RawImageSplitSource : RawImageSource
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

        public RawImageSplitSource(RawImageSource a, RawImageSource b)
            : base(CreateDescriptor(a, b), CreateDescriptor(a, b).GetRequiredByteCount(), null)
        {
            _a = a ?? throw new ArgumentNullException("a");
            _b = b ?? throw new ArgumentNullException("b");
        }

        public override RawImageSource WithDescriptor(RawImageDescriptor descriptor)
        {
            throw new NotSupportedException("Split view does not support descriptor reinterpretation.");
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
            var split = SourceDescriptor.Width / 2;
            var output = new byte[left.Bgra32.Length];

            for (var row = 0; row < left.Height; row++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var column = 0; column < left.Width; column++)
                {
                    var sourceX = x + (column * Math.Max(1, sampleStep));
                    var source = sourceX < split ? left.Bgra32 : right.Bgra32;
                    var offset = (row * left.Width + column) * 4;
                    output[offset] = source[offset];
                    output[offset + 1] = source[offset + 1];
                    output[offset + 2] = source[offset + 2];
                    output[offset + 3] = 255;
                }
            }

            return new RenderedImage(left.Width, left.Height, output);
        }

        public override string DescribePixel(int x, int y)
        {
            var split = SourceDescriptor.Width / 2;
            return string.Format(
                CultureInfo.InvariantCulture,
                "X={0}, Y={1}, Split={2}, {3}",
                x,
                y,
                split,
                x < split ? "A=[" + _a.DescribePixel(x, y) + "]" : "B=[" + _b.DescribePixel(x, y) + "]");
        }

        internal override Func<int, int, RenderedImage> CreateHistogramPixelReader()
        {
            var left = _a.CreateHistogramPixelReader();
            var right = _b.CreateHistogramPixelReader();
            var split = SourceDescriptor.Width / 2;
            return (x, y) => x < split ? left(x, y) : right(x, y);
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
            throw new NotSupportedException("Split view is generated on demand and has no raw byte payload.");
        }

        public override void CopyRawTo(string rawPath)
        {
            throw new NotSupportedException("Split view is generated on demand and cannot be exported as raw bytes.");
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
                throw new InvalidOperationException("A/B split requires images with the same width and height.");
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
    }
}
