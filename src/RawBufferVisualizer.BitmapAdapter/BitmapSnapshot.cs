using System;
using System.Drawing;
using System.Drawing.Imaging;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.Sdk;

namespace RawBufferVisualizer.BitmapAdapter
{
    public static class BitmapSnapshot
    {
        public static RawBufferSnapshot FromBitmap(Bitmap bitmap)
        {
            if (bitmap == null)
            {
                throw new ArgumentNullException(nameof(bitmap));
            }

            var format = ToRawPixelFormat(bitmap.PixelFormat);
            var bytesPerPixel = GetBytesPerPixel(format);
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, bitmap.PixelFormat);
            try
            {
                var stride = Math.Abs(data.Stride);
                var buffer = new byte[stride * bitmap.Height];
                for (var y = 0; y < bitmap.Height; y++)
                {
                    var source = data.Scan0 + (data.Stride >= 0 ? y * data.Stride : (bitmap.Height - 1 - y) * stride);
                    System.Runtime.InteropServices.Marshal.Copy(source, buffer, y * stride, bitmap.Width * bytesPerPixel);
                }

                return new RawBufferSnapshot(buffer, new RawImageDescriptor
                {
                    Width = bitmap.Width,
                    Height = bitmap.Height,
                    Stride = stride,
                    PixelFormat = format,
                    ValidBits = bytesPerPixel == 1 ? 8 : bytesPerPixel * 8,
                    ByteOrder = RawByteOrder.LittleEndian
                });
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static RawPixelFormat ToRawPixelFormat(PixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case PixelFormat.Format8bppIndexed:
                    return RawPixelFormat.Mono8;
                case PixelFormat.Format24bppRgb:
                    return RawPixelFormat.BGR24;
                case PixelFormat.Format32bppArgb:
                case PixelFormat.Format32bppPArgb:
                case PixelFormat.Format32bppRgb:
                    return RawPixelFormat.BGRA32;
                default:
                    throw new NotSupportedException("Unsupported Bitmap pixel format: " + pixelFormat);
            }
        }

        private static int GetBytesPerPixel(RawPixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case RawPixelFormat.BGR24:
                    return 3;
                case RawPixelFormat.BGRA32:
                    return 4;
                default:
                    return 1;
            }
        }
    }
}

