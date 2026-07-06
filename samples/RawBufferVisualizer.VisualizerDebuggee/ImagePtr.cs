using System;
using OpenCvSharp;

namespace ImageModel
{
    public sealed class ImagePtr
    {
        private IntPtr _ptr;
        private long _length;
        private int _width;
        private int _height;
        private int _step;
        private int _bpp;

        public ImagePtr(IntPtr ptr, long length, int width, int height, int step, int bpp)
        {
            _ptr = ptr;
            _length = length;
            _width = width;
            _height = height;
            _step = step;
            _bpp = bpp;
        }

        public IntPtr Ptr
        {
            get { return _ptr; }
            set { _ptr = value; }
        }

        public long Length
        {
            get { return _length; }
            set { _length = value; }
        }

        public int Width
        {
            get { return _width; }
            set { _width = value; }
        }

        public int Height
        {
            get { return _height; }
            set { _height = value; }
        }

        public int Step
        {
            get { return _step; }
            set { _step = value; }
        }

        public int Bpp
        {
            get { return _bpp; }
            set { _bpp = value; }
        }

        public Mat ToMat()
        {
            var matType = MatType.CV_8UC1;
            if (_bpp == 3)
            {
                matType = MatType.CV_8UC3;
            }
            else if (_bpp == 4)
            {
                matType = MatType.CV_8UC4;
            }

            return Mat.FromPixelData(Height, Width, matType, Ptr, Step);
        }

        public OpenCvSharp.Size GetSize()
        {
            return new OpenCvSharp.Size(Width, Height);
        }

        public static ImagePtr Zero
        {
            get { return new ImagePtr(IntPtr.Zero, 0, 0, 0, 0, 0); }
        }
    }
}
