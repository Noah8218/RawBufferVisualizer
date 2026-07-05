using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using OpenCvSharp;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.Sdk;

namespace RawBufferVisualizer.VisualizerDebuggee
{
    internal static class Program
    {
        private const int Width = 640;
        private const int Height = 484;

        private static int Main(string[] args)
        {
            var shouldBreak = Array.IndexOf(args, "--no-break") < 0;
            Console.WriteLine("Raw Buffer Visualizer debuggee");
            Console.WriteLine("Set RawBufferVisualizer.VisualStudio.Extensibility as installed, then inspect each variable at each break.");

            var rawMono8Snapshot = RawBufferSnapshot.FromByteArray(
                CreateMono8Buffer(Width, Height, Width),
                CreateDescriptor(Width, Height, Width, RawPixelFormat.Mono8, 8));
            Console.WriteLine("1. Inspect rawMono8Snapshot as RawBufferSnapshot.");
            if (shouldBreak) Debugger.Break();

            var rawBgr24Snapshot = RawBufferSnapshot.FromByteArray(
                CreateBgr24Buffer(Width, Height, Width * 3),
                CreateDescriptor(Width, Height, Width * 3, RawPixelFormat.BGR24, 8));
            Console.WriteLine("2. Inspect rawBgr24Snapshot as RawBufferSnapshot.");
            if (shouldBreak) Debugger.Break();

            var rawMono16Snapshot = RawBufferSnapshot.FromByteArray(
                CreateMono16Buffer(Width, Height, Width * 2),
                CreateDescriptor(Width, Height, Width * 2, RawPixelFormat.Mono16, 16));
            Console.WriteLine("3. Inspect rawMono16Snapshot as RawBufferSnapshot.");
            if (shouldBreak) Debugger.Break();

            var industrialBuffer = CreateBgr24Buffer(Width, Height, Width * 3);
            var industrialHandle = GCHandle.Alloc(industrialBuffer, GCHandleType.Pinned);
            try
            {
                var rawBufferView = new RawBufferView
                {
                    Buffer = industrialHandle.AddrOfPinnedObject(),
                    BufferLength = industrialBuffer.Length,
                    Width = Width,
                    Height = Height,
                    Stride = Width * 3,
                    PixelFormat = RawPixelFormat.BGR24,
                    Channels = 3,
                    BitDepth = 8,
                    Name = "industrial-buffer-view"
                };
                Console.WriteLine("4. Inspect rawBufferView as RawBufferView.");
                if (shouldBreak) Debugger.Break();
                GC.KeepAlive(rawBufferView);
            }
            finally
            {
                industrialHandle.Free();
            }

            var bitmapMono8 = CreateMono8Bitmap(Width, Height);
            Console.WriteLine("5. Inspect bitmapMono8 as System.Drawing.Bitmap.");
            if (shouldBreak) Debugger.Break();

            var bitmapBgr24 = CreateBgr24Bitmap(Width, Height);
            Console.WriteLine("6. Inspect bitmapBgr24 as System.Drawing.Bitmap.");
            if (shouldBreak) Debugger.Break();

            var bitmapBgra32 = CreateBgra32Bitmap(Width, Height);
            Console.WriteLine("7. Inspect bitmapBgra32 as System.Drawing.Bitmap.");
            if (shouldBreak) Debugger.Break();

            using (var matMono8 = CreateMatMono8(Width, Height))
            {
                Console.WriteLine("8. Inspect matMono8 as OpenCvSharp.Mat.");
                if (shouldBreak) Debugger.Break();

                using (var matBgr24 = CreateMatBgr24(Width, Height))
                {
                    Console.WriteLine("9. Inspect matBgr24 as OpenCvSharp.Mat.");
                    if (shouldBreak) Debugger.Break();

                    using (var matBgra32 = CreateMatBgra32(Width, Height))
                    {
                        Console.WriteLine("10. Inspect matBgra32 as OpenCvSharp.Mat.");
                        if (shouldBreak) Debugger.Break();

                        using (var matMono16 = CreateMatMono16(Width, Height))
                        {
                            Console.WriteLine("11. Inspect matMono16 as OpenCvSharp.Mat.");
                            if (shouldBreak) Debugger.Break();

                            using (var matFloat32 = CreateMatFloat32(Width, Height))
                            {
                                Console.WriteLine("12. Inspect matFloat32 as OpenCvSharp.Mat.");
                                if (shouldBreak) Debugger.Break();

                                GC.KeepAlive(matFloat32);
                            }

                            GC.KeepAlive(matMono16);
                        }

                        GC.KeepAlive(matBgra32);
                    }

                    GC.KeepAlive(matBgr24);
                }

                GC.KeepAlive(matMono8);
            }

            GC.KeepAlive(rawMono8Snapshot);
            GC.KeepAlive(rawBgr24Snapshot);
            GC.KeepAlive(rawMono16Snapshot);
            GC.KeepAlive(bitmapMono8);
            GC.KeepAlive(bitmapBgr24);
            GC.KeepAlive(bitmapBgra32);

            bitmapMono8.Dispose();
            bitmapBgr24.Dispose();
            bitmapBgra32.Dispose();

            Console.WriteLine("Done.");
            return 0;
        }

        private static RawImageDescriptor CreateDescriptor(int width, int height, int stride, RawPixelFormat pixelFormat, int validBits)
        {
            return new RawImageDescriptor
            {
                Width = width,
                Height = height,
                Stride = stride,
                PixelFormat = pixelFormat,
                ValidBits = validBits,
                ByteOrder = RawByteOrder.LittleEndian
            };
        }

        private static Bitmap CreateMono8Bitmap(int width, int height)
        {
            var stride = width;
            var bitmap = new Bitmap(width, height, PixelFormat.Format8bppIndexed);
            var palette = bitmap.Palette;
            for (var i = 0; i < palette.Entries.Length; i++)
            {
                palette.Entries[i] = Color.FromArgb(i, i, i);
            }

            bitmap.Palette = palette;
            CopyToBitmap(bitmap, CreateMono8Buffer(width, height, stride), stride, 1);
            return bitmap;
        }

        private static Bitmap CreateBgr24Bitmap(int width, int height)
        {
            var stride = width * 3;
            var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            CopyToBitmap(bitmap, CreateBgr24Buffer(width, height, stride), stride, 3);
            return bitmap;
        }

        private static Bitmap CreateBgra32Bitmap(int width, int height)
        {
            var stride = width * 4;
            var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            CopyToBitmap(bitmap, CreateBgra32Buffer(width, height, stride), stride, 4);
            return bitmap;
        }

        private static void CopyToBitmap(Bitmap bitmap, byte[] source, int sourceStride, int bytesPerPixel)
        {
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(rect, ImageLockMode.WriteOnly, bitmap.PixelFormat);
            try
            {
                var targetStride = Math.Abs(data.Stride);
                for (var y = 0; y < bitmap.Height; y++)
                {
                    var sourceOffset = y * sourceStride;
                    var target = data.Scan0 + (data.Stride >= 0 ? y * data.Stride : (bitmap.Height - 1 - y) * targetStride);
                    Marshal.Copy(source, sourceOffset, target, bitmap.Width * bytesPerPixel);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static Mat CreateMatMono8(int width, int height)
        {
            return CreateMat(width, height, MatType.CV_8UC1, CreateMono8Buffer(width, height, width));
        }

        private static Mat CreateMatBgr24(int width, int height)
        {
            return CreateMat(width, height, MatType.CV_8UC3, CreateBgr24Buffer(width, height, width * 3));
        }

        private static Mat CreateMatBgra32(int width, int height)
        {
            return CreateMat(width, height, MatType.CV_8UC4, CreateBgra32Buffer(width, height, width * 4));
        }

        private static Mat CreateMatMono16(int width, int height)
        {
            return CreateMat(width, height, MatType.CV_16UC1, CreateMono16Buffer(width, height, width * 2));
        }

        private static Mat CreateMatFloat32(int width, int height)
        {
            return CreateMat(width, height, MatType.CV_32FC1, CreateFloat32Buffer(width, height, width * 4));
        }

        private static Mat CreateMat(int width, int height, MatType matType, byte[] buffer)
        {
            var mat = new Mat(height, width, matType);
            Marshal.Copy(buffer, 0, mat.Data, buffer.Length);
            return mat;
        }

        private static byte[] CreateMono8Buffer(int width, int height, int stride)
        {
            var buffer = new byte[stride * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    buffer[(y * stride) + x] = (byte)((x + y) & 0xFF);
                }
            }

            return buffer;
        }

        private static byte[] CreateBgr24Buffer(int width, int height, int stride)
        {
            var buffer = new byte[stride * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var offset = (y * stride) + (x * 3);
                    buffer[offset] = (byte)((x + y) & 0xFF);
                    buffer[offset + 1] = (byte)(y * 255 / Math.Max(height - 1, 1));
                    buffer[offset + 2] = (byte)(x * 255 / Math.Max(width - 1, 1));
                }
            }

            return buffer;
        }

        private static byte[] CreateBgra32Buffer(int width, int height, int stride)
        {
            var buffer = new byte[stride * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var offset = (y * stride) + (x * 4);
                    buffer[offset] = (byte)((x + y) & 0xFF);
                    buffer[offset + 1] = (byte)(y * 255 / Math.Max(height - 1, 1));
                    buffer[offset + 2] = (byte)(x * 255 / Math.Max(width - 1, 1));
                    buffer[offset + 3] = 255;
                }
            }

            return buffer;
        }

        private static byte[] CreateMono16Buffer(int width, int height, int stride)
        {
            var buffer = new byte[stride * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var value = (ushort)(((x * 256) + (y * 128)) & 0xFFFF);
                    var offset = (y * stride) + (x * 2);
                    buffer[offset] = (byte)(value & 0xFF);
                    buffer[offset + 1] = (byte)(value >> 8);
                }
            }

            return buffer;
        }

        private static byte[] CreateFloat32Buffer(int width, int height, int stride)
        {
            var buffer = new byte[stride * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var value = (float)(Math.Sin(x * 0.05) + Math.Cos(y * 0.05));
                    var bytes = BitConverter.GetBytes(value);
                    Buffer.BlockCopy(bytes, 0, buffer, (y * stride) + (x * 4), 4);
                }
            }

            return buffer;
        }
    }
}
