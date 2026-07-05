using System;
using System.Collections.Generic;
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
            var caseNumber = 1;
            var pinnedViews = new List<PinnedRawBufferView>();

            Console.WriteLine("Raw Buffer Visualizer debuggee");
            Console.WriteLine("Install RawBufferVisualizer.VisualStudio.Extensibility, start debugging, then inspect the variable printed at each break.");

            Bitmap? bitmapMono8 = null;
            Bitmap? bitmapBgr24 = null;
            Bitmap? bitmapBgra32 = null;
            Mat? matMono8 = null;
            Mat? matBgr24 = null;
            Mat? matBgra32 = null;
            Mat? matMono16 = null;
            Mat? matFloat32 = null;
            Emgu.CV.Mat? emguMono8 = null;
            Emgu.CV.Mat? emguBgr24 = null;
            Emgu.CV.Mat? emguBgra32 = null;
            Emgu.CV.Mat? emguMono16 = null;
            Emgu.CV.Mat? emguFloat32 = null;

            try
            {
                var rawMono8Snapshot = RawBufferSnapshot.FromByteArray(
                    CreateMono8Buffer(Width, Height, Width),
                    CreateDescriptor(Width, Height, Width, RawPixelFormat.Mono8, 8));
                PrintCase(ref caseNumber, "rawMono8Snapshot as RawBufferSnapshot / Mono8");
                if (shouldBreak) Debugger.Break();

                var rawMono16Snapshot = RawBufferSnapshot.FromByteArray(
                    CreateMono16Buffer(Width, Height, Width * 2),
                    CreateDescriptor(Width, Height, Width * 2, RawPixelFormat.Mono16, 16));
                PrintCase(ref caseNumber, "rawMono16Snapshot as RawBufferSnapshot / Mono16");
                if (shouldBreak) Debugger.Break();

                var rawMono10PackedSnapshot = RawBufferSnapshot.FromByteArray(
                    CreatePackedMonoBuffer(Width, Height, 10),
                    CreateDescriptor(Width, Height, GetPackedStride(Width, 10), RawPixelFormat.Mono10PackedLsb, 10));
                PrintCase(ref caseNumber, "rawMono10PackedSnapshot as RawBufferSnapshot / Mono10PackedLsb");
                if (shouldBreak) Debugger.Break();

                var rawMono12PackedSnapshot = RawBufferSnapshot.FromByteArray(
                    CreatePackedMonoBuffer(Width, Height, 12),
                    CreateDescriptor(Width, Height, GetPackedStride(Width, 12), RawPixelFormat.Mono12PackedLsb, 12));
                PrintCase(ref caseNumber, "rawMono12PackedSnapshot as RawBufferSnapshot / Mono12PackedLsb");
                if (shouldBreak) Debugger.Break();

                var rawBinarySnapshot = RawBufferSnapshot.FromByteArray(
                    CreateBinaryBuffer(Width, Height, Width),
                    CreateDescriptor(Width, Height, Width, RawPixelFormat.Binary, 1));
                PrintCase(ref caseNumber, "rawBinarySnapshot as RawBufferSnapshot / Binary");
                if (shouldBreak) Debugger.Break();

                var rawRgb24Snapshot = RawBufferSnapshot.FromByteArray(
                    CreateRgb24Buffer(Width, Height, Width * 3),
                    CreateDescriptor(Width, Height, Width * 3, RawPixelFormat.RGB24, 8));
                PrintCase(ref caseNumber, "rawRgb24Snapshot as RawBufferSnapshot / RGB24");
                if (shouldBreak) Debugger.Break();

                var rawBgr24Snapshot = RawBufferSnapshot.FromByteArray(
                    CreateBgr24Buffer(Width, Height, Width * 3),
                    CreateDescriptor(Width, Height, Width * 3, RawPixelFormat.BGR24, 8));
                PrintCase(ref caseNumber, "rawBgr24Snapshot as RawBufferSnapshot / BGR24");
                if (shouldBreak) Debugger.Break();

                var rawBgra32Snapshot = RawBufferSnapshot.FromByteArray(
                    CreateBgra32Buffer(Width, Height, Width * 4),
                    CreateDescriptor(Width, Height, Width * 4, RawPixelFormat.BGRA32, 8));
                PrintCase(ref caseNumber, "rawBgra32Snapshot as RawBufferSnapshot / BGRA32");
                if (shouldBreak) Debugger.Break();

                var rawFloat32Snapshot = RawBufferSnapshot.FromByteArray(
                    CreateFloat32Buffer(Width, Height, Width * 4),
                    CreateDescriptor(Width, Height, Width * 4, RawPixelFormat.Float32, 32));
                PrintCase(ref caseNumber, "rawFloat32Snapshot as RawBufferSnapshot / Float32");
                if (shouldBreak) Debugger.Break();

                var rawBayerRggb8Snapshot = RawBufferSnapshot.FromByteArray(
                    CreateBayer8Buffer(Width, Height, Width, RawPixelFormat.BayerRGGB8),
                    CreateDescriptor(Width, Height, Width, RawPixelFormat.BayerRGGB8, 8));
                PrintCase(ref caseNumber, "rawBayerRggb8Snapshot as RawBufferSnapshot / BayerRGGB8");
                if (shouldBreak) Debugger.Break();

                var rawBayerGrbg8Snapshot = RawBufferSnapshot.FromByteArray(
                    CreateBayer8Buffer(Width, Height, Width, RawPixelFormat.BayerGRBG8),
                    CreateDescriptor(Width, Height, Width, RawPixelFormat.BayerGRBG8, 8));
                PrintCase(ref caseNumber, "rawBayerGrbg8Snapshot as RawBufferSnapshot / BayerGRBG8");
                if (shouldBreak) Debugger.Break();

                var rawBayerGbrg8Snapshot = RawBufferSnapshot.FromByteArray(
                    CreateBayer8Buffer(Width, Height, Width, RawPixelFormat.BayerGBRG8),
                    CreateDescriptor(Width, Height, Width, RawPixelFormat.BayerGBRG8, 8));
                PrintCase(ref caseNumber, "rawBayerGbrg8Snapshot as RawBufferSnapshot / BayerGBRG8");
                if (shouldBreak) Debugger.Break();

                var rawBayerBggr8Snapshot = RawBufferSnapshot.FromByteArray(
                    CreateBayer8Buffer(Width, Height, Width, RawPixelFormat.BayerBGGR8),
                    CreateDescriptor(Width, Height, Width, RawPixelFormat.BayerBGGR8, 8));
                PrintCase(ref caseNumber, "rawBayerBggr8Snapshot as RawBufferSnapshot / BayerBGGR8");
                if (shouldBreak) Debugger.Break();

                var rawViewMono8Owner = PinView(
                    pinnedViews,
                    "rawViewMono8",
                    CreateMono8Buffer(Width, Height, Width),
                    CreateDescriptor(Width, Height, Width, RawPixelFormat.Mono8, 8),
                    1);
                var rawViewMono8 = rawViewMono8Owner.View;
                PrintCase(ref caseNumber, "rawViewMono8 as RawBufferView / IntPtr Mono8");
                if (shouldBreak) Debugger.Break();

                var rawViewBgr24Owner = PinView(
                    pinnedViews,
                    "rawViewBgr24",
                    CreateBgr24Buffer(Width, Height, Width * 3),
                    CreateDescriptor(Width, Height, Width * 3, RawPixelFormat.BGR24, 8),
                    3);
                var rawViewBgr24 = rawViewBgr24Owner.View;
                PrintCase(ref caseNumber, "rawViewBgr24 as RawBufferView / IntPtr BGR24");
                if (shouldBreak) Debugger.Break();

                var rawViewMono16Owner = PinView(
                    pinnedViews,
                    "rawViewMono16",
                    CreateMono16Buffer(Width, Height, Width * 2),
                    CreateDescriptor(Width, Height, Width * 2, RawPixelFormat.Mono16, 16),
                    1);
                var rawViewMono16 = rawViewMono16Owner.View;
                PrintCase(ref caseNumber, "rawViewMono16 as RawBufferView / IntPtr Mono16");
                if (shouldBreak) Debugger.Break();

                var rawViewBgra32Owner = PinView(
                    pinnedViews,
                    "rawViewBgra32",
                    CreateBgra32Buffer(Width, Height, Width * 4),
                    CreateDescriptor(Width, Height, Width * 4, RawPixelFormat.BGRA32, 8),
                    4);
                var rawViewBgra32 = rawViewBgra32Owner.View;
                PrintCase(ref caseNumber, "rawViewBgra32 as RawBufferView / IntPtr BGRA32");
                if (shouldBreak) Debugger.Break();

                var baslerPylonLikeFrame = new IndustrialCameraFrame(
                    PinView(pinnedViews, "basler-pylon-like", CreateMono8Buffer(Width, Height, Width), CreateDescriptor(Width, Height, Width, RawPixelFormat.Mono8, 8), 1));
                PrintCase(ref caseNumber, "baslerPylonLikeFrame.View as RawBufferView / Basler pylon-like object");
                if (shouldBreak) Debugger.Break();

                var hikrobotMvsLikeFrame = new IndustrialCameraFrame(
                    PinView(pinnedViews, "hikrobot-mvs-like", CreateBgr24Buffer(Width, Height, Width * 3), CreateDescriptor(Width, Height, Width * 3, RawPixelFormat.BGR24, 8), 3));
                PrintCase(ref caseNumber, "hikrobotMvsLikeFrame.View as RawBufferView / HIKROBOT MVS-like object");
                if (shouldBreak) Debugger.Break();

                var spinnakerLikeFrame = new IndustrialCameraFrame(
                    PinView(pinnedViews, "spinnaker-like", CreateBayer8Buffer(Width, Height, Width, RawPixelFormat.BayerRGGB8), CreateDescriptor(Width, Height, Width, RawPixelFormat.BayerRGGB8, 8), 1));
                PrintCase(ref caseNumber, "spinnakerLikeFrame.View as RawBufferView / Spinnaker-like object");
                if (shouldBreak) Debugger.Break();

                var frameGrabberLikeBuffer = new IndustrialCameraFrame(
                    PinView(pinnedViews, "framegrabber-like", CreateMono16Buffer(Width, Height, Width * 2), CreateDescriptor(Width, Height, Width * 2, RawPixelFormat.Mono16, 16), 1));
                PrintCase(ref caseNumber, "frameGrabberLikeBuffer.View as RawBufferView / eGrabber-Sapera-MIL-like object");
                if (shouldBreak) Debugger.Break();

                bitmapMono8 = CreateMono8Bitmap(Width, Height);
                PrintCase(ref caseNumber, "bitmapMono8 as System.Drawing.Bitmap / Format8bppIndexed");
                if (shouldBreak) Debugger.Break();

                bitmapBgr24 = CreateBgr24Bitmap(Width, Height);
                PrintCase(ref caseNumber, "bitmapBgr24 as System.Drawing.Bitmap / Format24bppRgb");
                if (shouldBreak) Debugger.Break();

                bitmapBgra32 = CreateBgra32Bitmap(Width, Height);
                PrintCase(ref caseNumber, "bitmapBgra32 as System.Drawing.Bitmap / Format32bppArgb");
                if (shouldBreak) Debugger.Break();

                matMono8 = CreateMatMono8(Width, Height);
                PrintCase(ref caseNumber, "matMono8 as OpenCvSharp.Mat / CV_8UC1");
                if (shouldBreak) Debugger.Break();

                matBgr24 = CreateMatBgr24(Width, Height);
                PrintCase(ref caseNumber, "matBgr24 as OpenCvSharp.Mat / CV_8UC3");
                if (shouldBreak) Debugger.Break();

                matBgra32 = CreateMatBgra32(Width, Height);
                PrintCase(ref caseNumber, "matBgra32 as OpenCvSharp.Mat / CV_8UC4");
                if (shouldBreak) Debugger.Break();

                matMono16 = CreateMatMono16(Width, Height);
                PrintCase(ref caseNumber, "matMono16 as OpenCvSharp.Mat / CV_16UC1");
                if (shouldBreak) Debugger.Break();

                matFloat32 = CreateMatFloat32(Width, Height);
                PrintCase(ref caseNumber, "matFloat32 as OpenCvSharp.Mat / CV_32FC1");
                if (shouldBreak) Debugger.Break();

                emguMono8 = CreateEmguMatMono8(Width, Height);
                PrintCase(ref caseNumber, "emguMono8 as Emgu.CV.Mat / Cv8U C1");
                if (shouldBreak) Debugger.Break();

                emguBgr24 = CreateEmguMatBgr24(Width, Height);
                PrintCase(ref caseNumber, "emguBgr24 as Emgu.CV.Mat / Cv8U C3");
                if (shouldBreak) Debugger.Break();

                emguBgra32 = CreateEmguMatBgra32(Width, Height);
                PrintCase(ref caseNumber, "emguBgra32 as Emgu.CV.Mat / Cv8U C4");
                if (shouldBreak) Debugger.Break();

                emguMono16 = CreateEmguMatMono16(Width, Height);
                PrintCase(ref caseNumber, "emguMono16 as Emgu.CV.Mat / Cv16U C1");
                if (shouldBreak) Debugger.Break();

                emguFloat32 = CreateEmguMatFloat32(Width, Height);
                PrintCase(ref caseNumber, "emguFloat32 as Emgu.CV.Mat / Cv32F C1");
                if (shouldBreak) Debugger.Break();

                GC.KeepAlive(rawMono8Snapshot);
                GC.KeepAlive(rawMono16Snapshot);
                GC.KeepAlive(rawMono10PackedSnapshot);
                GC.KeepAlive(rawMono12PackedSnapshot);
                GC.KeepAlive(rawBinarySnapshot);
                GC.KeepAlive(rawRgb24Snapshot);
                GC.KeepAlive(rawBgr24Snapshot);
                GC.KeepAlive(rawBgra32Snapshot);
                GC.KeepAlive(rawFloat32Snapshot);
                GC.KeepAlive(rawBayerRggb8Snapshot);
                GC.KeepAlive(rawBayerGrbg8Snapshot);
                GC.KeepAlive(rawBayerGbrg8Snapshot);
                GC.KeepAlive(rawBayerBggr8Snapshot);
                GC.KeepAlive(rawViewMono8);
                GC.KeepAlive(rawViewBgr24);
                GC.KeepAlive(rawViewMono16);
                GC.KeepAlive(rawViewBgra32);
                GC.KeepAlive(baslerPylonLikeFrame);
                GC.KeepAlive(hikrobotMvsLikeFrame);
                GC.KeepAlive(spinnakerLikeFrame);
                GC.KeepAlive(frameGrabberLikeBuffer);

                Console.WriteLine("Done.");
                return 0;
            }
            finally
            {
                bitmapMono8?.Dispose();
                bitmapBgr24?.Dispose();
                bitmapBgra32?.Dispose();
                matMono8?.Dispose();
                matBgr24?.Dispose();
                matBgra32?.Dispose();
                matMono16?.Dispose();
                matFloat32?.Dispose();
                emguMono8?.Dispose();
                emguBgr24?.Dispose();
                emguBgra32?.Dispose();
                emguMono16?.Dispose();
                emguFloat32?.Dispose();

                foreach (var pinnedView in pinnedViews)
                {
                    pinnedView.Dispose();
                }
            }
        }

        private static void PrintCase(ref int caseNumber, string message)
        {
            Console.WriteLine(caseNumber.ToString("00") + ". Inspect " + message + ".");
            caseNumber++;
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

        private static PinnedRawBufferView PinView(
            ICollection<PinnedRawBufferView> pinnedViews,
            string name,
            byte[] buffer,
            RawImageDescriptor descriptor,
            int channels)
        {
            var pinned = new PinnedRawBufferView(name, buffer, descriptor, channels);
            pinnedViews.Add(pinned);
            return pinned;
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

        private static Emgu.CV.Mat CreateEmguMatMono8(int width, int height)
        {
            return CreateEmguMat(width, height, Emgu.CV.CvEnum.DepthType.Cv8U, 1, CreateMono8Buffer(width, height, width));
        }

        private static Emgu.CV.Mat CreateEmguMatBgr24(int width, int height)
        {
            return CreateEmguMat(width, height, Emgu.CV.CvEnum.DepthType.Cv8U, 3, CreateBgr24Buffer(width, height, width * 3));
        }

        private static Emgu.CV.Mat CreateEmguMatBgra32(int width, int height)
        {
            return CreateEmguMat(width, height, Emgu.CV.CvEnum.DepthType.Cv8U, 4, CreateBgra32Buffer(width, height, width * 4));
        }

        private static Emgu.CV.Mat CreateEmguMatMono16(int width, int height)
        {
            return CreateEmguMat(width, height, Emgu.CV.CvEnum.DepthType.Cv16U, 1, CreateMono16Buffer(width, height, width * 2));
        }

        private static Emgu.CV.Mat CreateEmguMatFloat32(int width, int height)
        {
            return CreateEmguMat(width, height, Emgu.CV.CvEnum.DepthType.Cv32F, 1, CreateFloat32Buffer(width, height, width * 4));
        }

        private static Emgu.CV.Mat CreateEmguMat(int width, int height, Emgu.CV.CvEnum.DepthType depth, int channels, byte[] buffer)
        {
            var mat = new Emgu.CV.Mat(height, width, depth, channels);
            Marshal.Copy(buffer, 0, mat.DataPointer, buffer.Length);
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

        private static byte[] CreateBinaryBuffer(int width, int height, int stride)
        {
            var buffer = new byte[stride * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    buffer[(y * stride) + x] = ((x / 32 + y / 32) & 1) == 0 ? (byte)0 : (byte)255;
                }
            }

            return buffer;
        }

        private static byte[] CreateRgb24Buffer(int width, int height, int stride)
        {
            var buffer = new byte[stride * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var offset = (y * stride) + (x * 3);
                    buffer[offset] = (byte)(x * 255 / Math.Max(width - 1, 1));
                    buffer[offset + 1] = (byte)(y * 255 / Math.Max(height - 1, 1));
                    buffer[offset + 2] = (byte)((x + y) & 0xFF);
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

        private static byte[] CreatePackedMonoBuffer(int width, int height, int bitsPerPixel)
        {
            var stride = GetPackedStride(width, bitsPerPixel);
            var buffer = new byte[stride * height];
            var max = (1 << bitsPerPixel) - 1;
            for (var y = 0; y < height; y++)
            {
                var values = new int[width];
                for (var x = 0; x < width; x++)
                {
                    values[x] = ((x * max) / Math.Max(width - 1, 1) + y * 17) & max;
                }

                Buffer.BlockCopy(PackLsb(values, bitsPerPixel), 0, buffer, y * stride, stride);
            }

            return buffer;
        }

        private static byte[] CreateBayer8Buffer(int width, int height, int stride, RawPixelFormat format)
        {
            var buffer = new byte[stride * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var color = GetBayerColor(format, x, y);
                    buffer[(y * stride) + x] = color == 0
                        ? (byte)(x * 255 / Math.Max(width - 1, 1))
                        : color == 1
                            ? (byte)(y * 255 / Math.Max(height - 1, 1))
                            : (byte)((x + y) & 0xFF);
                }
            }

            return buffer;
        }

        private static int GetPackedStride(int width, int bitsPerPixel)
        {
            return (width * bitsPerPixel + 7) / 8;
        }

        private static byte[] PackLsb(int[] values, int bitsPerPixel)
        {
            var buffer = new byte[((values.Length * bitsPerPixel) + 7) / 8];
            for (var x = 0; x < values.Length; x++)
            {
                var value = values[x];
                for (var bit = 0; bit < bitsPerPixel; bit++)
                {
                    if (((value >> bit) & 1) == 0)
                    {
                        continue;
                    }

                    var bitIndex = (x * bitsPerPixel) + bit;
                    buffer[bitIndex / 8] |= (byte)(1 << (bitIndex % 8));
                }
            }

            return buffer;
        }

        private static int GetBayerColor(RawPixelFormat format, int x, int y)
        {
            var evenX = (x & 1) == 0;
            var evenY = (y & 1) == 0;

            switch (format)
            {
                case RawPixelFormat.BayerRGGB8:
                    return evenY ? (evenX ? 0 : 1) : (evenX ? 1 : 2);
                case RawPixelFormat.BayerGRBG8:
                    return evenY ? (evenX ? 1 : 0) : (evenX ? 2 : 1);
                case RawPixelFormat.BayerGBRG8:
                    return evenY ? (evenX ? 1 : 2) : (evenX ? 0 : 1);
                case RawPixelFormat.BayerBGGR8:
                    return evenY ? (evenX ? 2 : 1) : (evenX ? 1 : 0);
                default:
                    return 1;
            }
        }
    }

    internal sealed class PinnedRawBufferView : IDisposable
    {
        private readonly GCHandle _handle;

        public RawBufferView View { get; private set; }

        public PinnedRawBufferView(string name, byte[] buffer, RawImageDescriptor descriptor, int channels)
        {
            _handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            View = new RawBufferView
            {
                Buffer = _handle.AddrOfPinnedObject(),
                BufferLength = buffer.LongLength,
                Width = descriptor.Width,
                Height = descriptor.Height,
                Stride = descriptor.Stride,
                PixelFormat = descriptor.PixelFormat,
                Channels = channels,
                BitDepth = descriptor.ValidBits,
                ByteOrder = descriptor.ByteOrder,
                Name = name
            };
        }

        public void Dispose()
        {
            if (_handle.IsAllocated)
            {
                _handle.Free();
            }
        }
    }

    internal sealed class IndustrialCameraFrame
    {
        private readonly PinnedRawBufferView _owner;

        public IntPtr Buffer { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Stride { get; private set; }
        public RawPixelFormat PixelFormat { get; private set; }
        public int Channels { get; private set; }
        public int BitDepth { get; private set; }

        public RawBufferView View
        {
            get { return _owner.View; }
        }

        public IndustrialCameraFrame(PinnedRawBufferView owner)
        {
            _owner = owner;
            Buffer = owner.View.Buffer;
            Width = owner.View.Width;
            Height = owner.View.Height;
            Stride = owner.View.Stride;
            PixelFormat = owner.View.PixelFormat;
            Channels = owner.View.Channels;
            BitDepth = owner.View.BitDepth;
        }
    }
}
