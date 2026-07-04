using System;
using OpenCvSharp;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.Sdk;

namespace RawBufferVisualizer.OpenCvSharpAdapter
{
    public static class MatSnapshot
    {
        public static RawBufferSnapshot FromMat(Mat mat)
        {
            if (mat == null)
            {
                throw new ArgumentNullException(nameof(mat));
            }

            if (mat.Empty())
            {
                throw new ArgumentException("Mat is empty.", nameof(mat));
            }

            if (mat.Dims != 2)
            {
                throw new NotSupportedException("Only 2D Mat images are supported.");
            }

            var type = mat.Type();
            var pixelFormat = ToRawPixelFormat(type);
            var stride = checked((int)mat.Step());
            var descriptor = new RawImageDescriptor
            {
                Width = mat.Width,
                Height = mat.Height,
                Stride = stride,
                PixelFormat = pixelFormat,
                ValidBits = GetValidBits(type),
                ByteOrder = RawByteOrder.LittleEndian
            };

            var byteCount = checked(stride * mat.Height);
            return RawBufferSnapshot.FromIntPtr(mat.Data, byteCount, descriptor);
        }

        private static RawPixelFormat ToRawPixelFormat(MatType type)
        {
            switch (type.Depth)
            {
                case MatType.CV_8U:
                    if (type.Channels == 1)
                    {
                        return RawPixelFormat.Mono8;
                    }

                    if (type.Channels == 3)
                    {
                        return RawPixelFormat.BGR24;
                    }

                    if (type.Channels == 4)
                    {
                        return RawPixelFormat.BGRA32;
                    }

                    break;
                case MatType.CV_16U:
                    if (type.Channels == 1)
                    {
                        return RawPixelFormat.Mono16;
                    }

                    break;
                case MatType.CV_32F:
                    if (type.Channels == 1)
                    {
                        return RawPixelFormat.Float32;
                    }

                    break;
            }

            throw new NotSupportedException("Unsupported Mat type: " + type);
        }

        private static int GetValidBits(MatType type)
        {
            switch (type.Depth)
            {
                case MatType.CV_8U:
                    return 8;
                case MatType.CV_16U:
                    return 16;
                case MatType.CV_32F:
                    return 32;
                default:
                    return 0;
            }
        }
    }
}

