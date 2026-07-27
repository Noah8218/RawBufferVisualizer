using RawBufferVisualizer.Core;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    internal sealed class NullRawImageSource : RawImageSource
    {
        public NullRawImageSource()
            : base(new RawImageDescriptor { Width = 1, Height = 1, Stride = 1, PixelFormat = RawPixelFormat.Mono8 }, 1, null)
        {
        }

        public override bool IsFileBacked
        {
            get { return false; }
        }

        public override RawImageSource WithDescriptor(RawImageDescriptor descriptor)
        {
            return this;
        }

        public override RawRenderOptions CreateRenderOptions()
        {
            return new RawRenderOptions();
        }

        public override RenderedImage RenderTile(int x, int y, int width, int height, RawRenderOptions? options)
        {
            return new RenderedImage(width, height, new byte[0]);
        }

        public override string DescribePixel(int x, int y)
        {
            return string.Empty;
        }

        public override byte[] ReadAllBytes()
        {
            return new byte[0];
        }

        public override void CopyRawTo(string rawPath)
        {
        }
    }
}
