using System;
using RawBufferVisualizer.Core;

namespace RawBufferVisualizer.OpenGlCanvas
{
    public enum RawOpenGlViewMode
    {
        Fit,
        Manual
    }

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
        public RawOpenGlViewMode Mode { get; private set; }
        public bool IsFitMode { get { return Mode == RawOpenGlViewMode.Fit; } }

        public RawOpenGlViewState(int imageWidth, int imageHeight, double left, double top, double width, double height)
            : this(imageWidth, imageHeight, left, top, width, height, RawOpenGlViewMode.Manual)
        {
        }

        public RawOpenGlViewState(
            int imageWidth,
            int imageHeight,
            double left,
            double top,
            double width,
            double height,
            RawOpenGlViewMode mode)
        {
            ImageWidth = imageWidth;
            ImageHeight = imageHeight;
            Left = left;
            Top = top;
            Width = width;
            Height = height;
            Mode = mode;
        }

        public bool Matches(int imageWidth, int imageHeight)
        {
            return ImageWidth == imageWidth && ImageHeight == imageHeight && Width > 0 && Height > 0;
        }
    }
}
