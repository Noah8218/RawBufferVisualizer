using System;
using RawBufferVisualizer.Core;

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

    public sealed class RawOpenGlSourceUnavailableEventArgs : EventArgs
    {
        public RawImageSourceUnavailableException Exception { get; private set; }

        public RawOpenGlSourceUnavailableEventArgs(RawImageSourceUnavailableException exception)
        {
            Exception = exception ?? throw new ArgumentNullException("exception");
        }
    }

    public sealed class RawOpenGlViewState
    {
        public int ImageWidth { get; private set; }
        public int ImageHeight { get; private set; }
        public double Left { get; private set; }
        public double Top { get; private set; }
        public double Width { get; private set; }
        public double Height { get; private set; }

        public RawOpenGlViewState(int imageWidth, int imageHeight, double left, double top, double width, double height)
        {
            ImageWidth = imageWidth;
            ImageHeight = imageHeight;
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }

        public bool Matches(int imageWidth, int imageHeight)
        {
            return ImageWidth == imageWidth && ImageHeight == imageHeight && Width > 0 && Height > 0;
        }
    }
}
