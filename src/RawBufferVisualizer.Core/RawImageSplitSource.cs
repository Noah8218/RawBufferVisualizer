using System;
using System.Globalization;

namespace RawBufferVisualizer.Core
{
    public sealed class RawImageSplitSource : RawImageSource
    {
        private readonly RawImageSource _a;
        private readonly RawImageSource _b;

        public override bool IsFileBacked
        {
            get { return _a.IsFileBacked || _b.IsFileBacked; }
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
            return new RawRenderOptions { AutoScale = false, BlackLevel = 0, WhiteLevel = 255 };
        }

        public override RenderedImage RenderTile(int x, int y, int width, int height, RawRenderOptions? options)
        {
            return RenderTileSampled(x, y, width, height, 1, options);
        }

        public override RenderedImage RenderTileSampled(int x, int y, int width, int height, int sampleStep, RawRenderOptions? options)
        {
            var aOptions = _a.CreateRenderOptions();
            var bOptions = _b.CreateRenderOptions();
            var left = _a.RenderTileSampled(x, y, width, height, sampleStep, aOptions);
            var right = _b.RenderTileSampled(x, y, width, height, sampleStep, bOptions);
            var split = SourceDescriptor.Width / 2;
            var output = new byte[left.Bgra32.Length];

            for (var row = 0; row < left.Height; row++)
            {
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
