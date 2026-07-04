using System;

namespace RawBufferVisualizer.OpenGlCanvas
{
    public sealed class RawOpenGlPixelEventArgs : EventArgs
    {
        public int X { get; private set; }
        public int Y { get; private set; }

        public RawOpenGlPixelEventArgs(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
}
