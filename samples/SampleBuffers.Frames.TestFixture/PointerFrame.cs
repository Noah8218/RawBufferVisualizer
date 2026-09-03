using System;

namespace SampleBuffers.Frames
{
    public sealed class PointerFrame
    {
        public readonly IntPtr DataPointer;
        public readonly long ByteLength;
        public readonly int Width;
        public readonly int Height;
        public readonly int Pitch;
        public readonly int BytesPerPixel;
        public string PixelFormat;

        public PointerFrame(IntPtr address, long byteCount, int columns, int rows, int pitch, int bytesPerPixel)
        {
            DataPointer = address;
            ByteLength = byteCount;
            Width = columns;
            Height = rows;
            Pitch = pitch;
            BytesPerPixel = bytesPerPixel;
        }
    }
}
