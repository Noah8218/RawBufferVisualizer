using System;

namespace Cressem.ImageModel
{
    // Test-only shape matching the exact debugger-visualizer registration.
    public sealed class ImagePtr
    {
        public ImagePtr(IntPtr ptr, long length, int width, int height, int step, int bpp)
        {
            Ptr = ptr;
            Length = length;
            Width = width;
            Height = height;
            Step = step;
            Bpp = bpp;
        }

        public IntPtr Ptr { get; set; }

        public long Length { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public int Step { get; set; }

        public int Bpp { get; set; }
    }
}
