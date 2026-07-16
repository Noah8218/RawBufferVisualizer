using System;
using System.Globalization;
using System.Threading;

namespace RawBufferVisualizer.Core
{
    public sealed class RawImageDifferenceSource : RawImageSource
    {
        private readonly RawImageSource _a;
        private readonly RawImageSource _b;

        public override bool IsFileBacked
        {
            get { return _a.IsFileBacked || _b.IsFileBacked; }
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
            return new RawRenderOptions { AutoScale = false, BlackLevel = 0, WhiteLevel = 255 };
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
            var aOptions = _a.CreateRenderOptions();
            var bOptions = _b.CreateRenderOptions();
            var left = _a.RenderTileSampled(x, y, width, height, sampleStep, aOptions, cancellationToken);
            var right = _b.RenderTileSampled(x, y, width, height, sampleStep, bOptions, cancellationToken);
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
            return string.Format(
                CultureInfo.InvariantCulture,
                "X={0}, Y={1}, A=[{2}], B=[{3}]",
                x,
                y,
                _a.DescribePixel(x, y),
                _b.DescribePixel(x, y));
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
