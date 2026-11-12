using System;

namespace RawBufferVisualizer.VisualizerDebuggee
{
    // A borrowed view for breakpoint samples. The caller keeps the pixel allocation alive.
    public sealed class PointerFrame
    {
        public IntPtr DataPointer;
        public long ByteLength;
        public int Width;
        public int Height;
        public int Pitch;
        public int BytesPerPixel;
        public string? Format;
    }
}
