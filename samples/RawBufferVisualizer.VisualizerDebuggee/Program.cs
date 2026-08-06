using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using OpenCvSharp;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.Sdk;
using RawBufferVisualizer.VisualStudio.ObjectSource;

namespace RawBufferVisualizer.VisualizerDebuggee
{
    internal static class Program
    {
        private const int Width = 640;
        private const int Height = 484;

        private static int Main(string[] args)
        {
            if (Array.IndexOf(args, "--large-mat-debug") >= 0)
            {
                return RunLargeMatDebug(args);
            }

            if (Array.IndexOf(args, "--buffer-doctor-debug") >= 0)
            {
                return RunBufferDoctorDebug();
            }

            if (Array.IndexOf(args, "--smart-type-mapper-debug") >= 0)
            {
                return RunSmartTypeMapperDebug();
            }

            if (Array.IndexOf(args, "--smart-type-mapper-fallback-debug") >= 0)
            {
                return RunSmartTypeMapperFallbackDebug();
            }

            if (Array.IndexOf(args, "--multi-library-debug") >= 0)
            {
                return RunMultiLibraryDebug();
            }

            if (Array.IndexOf(args, "--automatic-collections-debug") >= 0)
            {
                return RunAutomaticCollectionsDebug();
            }

            if (TryGetArgument(args, "--industrial-image-debug", out var industrialImagePath))
            {
                return RunIndustrialImageDebug(
                    industrialImagePath,
                    Array.IndexOf(args, "--no-break") < 0);
            }

            if (TryGetArgument(args, "--emgu-tiff-smoke", out var tiffPath))
            {
                return RunEmguTiffSmoke(tiffPath);
            }

            var shouldBreak = Array.IndexOf(args, "--no-break") < 0;
            var collectionOnly = Array.IndexOf(args, "--collection-only") >= 0;
            if (collectionOnly)
            {
                shouldBreak = false;
            }
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
            ImageModel.ImagePtr? imagePtrBgr24 = null;

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

                imagePtrBgr24 = new ImageModel.ImagePtr(
                    rawViewBgr24.Buffer,
                    rawViewBgr24.BufferLength,
                    rawViewBgr24.Width,
                    rawViewBgr24.Height,
                    rawViewBgr24.Stride,
                    3);
                PrintCase(ref caseNumber, "imagePtrBgr24 as ImageModel.ImagePtr / BGR24");
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

                var contiguousMonoFrame = new IndustrialCameraFrame(
                    PinView(pinnedViews, "contiguous-mono", CreateMono8Buffer(Width, Height, Width), CreateDescriptor(Width, Height, Width, RawPixelFormat.Mono8, 8), 1));
                PrintCase(ref caseNumber, "contiguousMonoFrame.View as RawBufferView / contiguous Mono8 object");
                if (shouldBreak) Debugger.Break();

                var bgrPointerFrame = new IndustrialCameraFrame(
                    PinView(pinnedViews, "bgr-pointer", CreateBgr24Buffer(Width, Height, Width * 3), CreateDescriptor(Width, Height, Width * 3, RawPixelFormat.BGR24, 8), 3));
                PrintCase(ref caseNumber, "bgrPointerFrame.View as RawBufferView / BGR24 pointer object");
                if (shouldBreak) Debugger.Break();

                var bayerPointerFrame = new IndustrialCameraFrame(
                    PinView(pinnedViews, "bayer-pointer", CreateBayer8Buffer(Width, Height, Width, RawPixelFormat.BayerRGGB8), CreateDescriptor(Width, Height, Width, RawPixelFormat.BayerRGGB8, 8), 1));
                PrintCase(ref caseNumber, "bayerPointerFrame.View as RawBufferView / BayerRGGB8 pointer object");
                if (shouldBreak) Debugger.Break();

                var mono16BoardBuffer = new IndustrialCameraFrame(
                    PinView(pinnedViews, "mono16-board-buffer", CreateMono16Buffer(Width, Height, Width * 2), CreateDescriptor(Width, Height, Width * 2, RawPixelFormat.Mono16, 16), 1));
                PrintCase(ref caseNumber, "mono16BoardBuffer.View as RawBufferView / Mono16 board buffer");
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

                var openCvMatList = new List<Mat> { matMono8, matBgr24 };
                PrintCase(ref caseNumber, "openCvMatList as List<OpenCvSharp.Mat>");
                if (shouldBreak || collectionOnly) Debugger.Break();

                var emguMatList = new List<Emgu.CV.Mat> { emguMono8, emguBgr24 };
                PrintCase(ref caseNumber, "emguMatList as List<Emgu.CV.Mat>");
                if (shouldBreak || collectionOnly) Debugger.Break();

                var bitmapList = new List<Bitmap> { bitmapMono8, bitmapBgr24 };
                PrintCase(ref caseNumber, "bitmapList as List<System.Drawing.Bitmap>");
                if (shouldBreak || collectionOnly) Debugger.Break();

                var openCvMatArray = new[] { matMono8, matBgr24, matBgra32 };
                PrintCase(ref caseNumber, "openCvMatArray as OpenCvSharp.Mat[]");
                if (shouldBreak || collectionOnly) Debugger.Break();

                var emguMatArray = new[] { emguMono8, emguBgr24, emguBgra32 };
                PrintCase(ref caseNumber, "emguMatArray as Emgu.CV.Mat[]");
                if (shouldBreak || collectionOnly) Debugger.Break();

                var bitmapArray = new[] { bitmapMono8, bitmapBgr24, bitmapBgra32 };
                PrintCase(ref caseNumber, "bitmapArray as System.Drawing.Bitmap[]");
                if (shouldBreak || collectionOnly) Debugger.Break();

                var disposedCollectionMat = new Mat(1, 1, MatType.CV_8UC1, Scalar.All(1));
                disposedCollectionMat.Dispose();
                var partialOpenCvMatList = new List<Mat>
                {
                    matMono8,
                    matBgr24,
                    null!,
                    matBgra32,
                    disposedCollectionMat
                };
                PrintCase(ref caseNumber, "partialOpenCvMatList as List<OpenCvSharp.Mat> / 3 valid, 2 failed");
                if (shouldBreak || collectionOnly) Debugger.Break();

                var openCvMatDictionary = new Dictionary<string, Mat>
                {
                    ["mono"] = matMono8,
                    ["bgr"] = matBgr24
                };
                PrintCase(ref caseNumber, "openCvMatDictionary as Dictionary<string, OpenCvSharp.Mat>");
                if (shouldBreak || collectionOnly) Debugger.Break();

                var emguMatDictionary = new Dictionary<string, Emgu.CV.Mat>
                {
                    ["mono"] = emguMono8,
                    ["bgr"] = emguBgr24
                };
                PrintCase(ref caseNumber, "emguMatDictionary as Dictionary<string, Emgu.CV.Mat>");
                if (shouldBreak || collectionOnly) Debugger.Break();

                var bitmapDictionary = new Dictionary<string, Bitmap>
                {
                    ["mono"] = bitmapMono8,
                    ["bgr"] = bitmapBgr24
                };
                PrintCase(ref caseNumber, "bitmapDictionary as Dictionary<string, System.Drawing.Bitmap>");
                if (shouldBreak || collectionOnly) Debugger.Break();

                var imageList = new List<object>
                {
                    bitmapMono8,
                    bitmapBgr24,
                    matMono8,
                    matBgr24,
                    emguMono8,
                    emguBgr24,
                    rawMono8Snapshot,
                    imagePtrBgr24
                };
                PrintCase(ref caseNumber, "imageList as List<object> / mixed supported images");
                if (shouldBreak) Debugger.Break();

                var imageDictionary = new Dictionary<string, object>
                {
                    ["bitmap-input"] = bitmapBgr24,
                    ["opencv-result"] = matBgr24,
                    ["emgu-result"] = emguBgr24,
                    ["raw-result"] = rawBgr24Snapshot
                };
                PrintCase(ref caseNumber, "imageDictionary as Dictionary<string, object> / named images");
                if (shouldBreak) Debugger.Break();

                var imageArray = new object[] { bitmapBgra32, matBgra32, emguBgra32, rawBgra32Snapshot };
                PrintCase(ref caseNumber, "imageArray as object[] / mixed supported images");
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
                GC.KeepAlive(imagePtrBgr24);
                GC.KeepAlive(rawViewMono16);
                GC.KeepAlive(rawViewBgra32);
                GC.KeepAlive(contiguousMonoFrame);
                GC.KeepAlive(bgrPointerFrame);
                GC.KeepAlive(bayerPointerFrame);
                GC.KeepAlive(mono16BoardBuffer);
                GC.KeepAlive(imageList);
                GC.KeepAlive(imageDictionary);
                GC.KeepAlive(imageArray);
                GC.KeepAlive(openCvMatList);
                GC.KeepAlive(emguMatList);
                GC.KeepAlive(bitmapList);
                GC.KeepAlive(openCvMatArray);
                GC.KeepAlive(emguMatArray);
                GC.KeepAlive(bitmapArray);
                GC.KeepAlive(partialOpenCvMatList);
                GC.KeepAlive(disposedCollectionMat);
                GC.KeepAlive(openCvMatDictionary);
                GC.KeepAlive(emguMatDictionary);
                GC.KeepAlive(bitmapDictionary);

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

        private static bool TryGetArgument(string[] args, string name, out string value)
        {
            var index = Array.IndexOf(args, name);
            if (index >= 0 && index + 1 < args.Length)
            {
                value = args[index + 1];
                return true;
            }

            value = string.Empty;
            return false;
        }

        private static int RunAutomaticCollectionsDebug()
        {
            var caseNumber = 1;
            var partialOpenCvMatList = CreatePartialOpenCvMatList();
            var emguMatArray = CreateAutomaticEmguMatArray();
            try
            {
                PrintCase(
                    ref caseNumber,
                    "partialOpenCvMatList (3 valid, 2 failed) and emguMatArray (2 valid)");
                Debugger.Break();
                GC.KeepAlive(partialOpenCvMatList);
                GC.KeepAlive(emguMatArray);
                return 0;
            }
            finally
            {
                for (var index = 0; index < partialOpenCvMatList.Count; index++)
                {
                    partialOpenCvMatList[index]?.Dispose();
                }

                for (var index = 0; index < emguMatArray.Length; index++)
                {
                    emguMatArray[index]?.Dispose();
                }
            }
        }

        private static List<Mat> CreatePartialOpenCvMatList()
        {
            var disposed = CreateMatMono8(64, 48);
            disposed.Dispose();
            return new List<Mat>
            {
                CreateMatMono8(64, 48),
                CreateMatBgr24(64, 48),
                null!,
                CreateMatBgra32(64, 48),
                disposed
            };
        }

        private static Emgu.CV.Mat[] CreateAutomaticEmguMatArray()
        {
            return new[]
            {
                CreateEmguMatMono8(64, 48),
                CreateEmguMatBgr24(64, 48)
            };
        }

        private static int RunIndustrialImageDebug(string path, bool shouldBreak)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Console.Error.WriteLine("Industrial image was not found: " + path);
                return 2;
            }

            var pinnedViews = new List<PinnedRawBufferView>();
            Bitmap? industrialBitmap = null;
            Mat? industrialOpenCvMat = null;
            Emgu.CV.Mat? industrialEmguMat = null;
            try
            {
                industrialBitmap = LoadBgr24Bitmap(path);
                var width = industrialBitmap.Width;
                var height = industrialBitmap.Height;
                var bgr24 = ReadBgr24Buffer(industrialBitmap);
                var mono8 = ConvertBgr24ToMono8(bgr24, width, height);
                const int doctorWidth = 2448;
                const int doctorHeight = 2048;
                const int doctorPadding = 112;
                var doctorMono8 = ResizeMono8Nearest(
                    mono8,
                    width,
                    height,
                    doctorWidth,
                    doctorHeight);
                var paddedMono8 = AddRowPadding(
                    doctorMono8,
                    doctorWidth,
                    doctorHeight,
                    doctorPadding);

                var industrialBgrSnapshot = RawBufferSnapshot.FromByteArray(
                    bgr24,
                    CreateDescriptor(width, height, width * 3, RawPixelFormat.BGR24, 8));
                var industrialMonoSnapshot = RawBufferSnapshot.FromByteArray(
                    mono8,
                    CreateDescriptor(width, height, width, RawPixelFormat.Mono8, 8));
                var industrialBadStrideSnapshot = RawBufferSnapshot.FromByteArray(
                    paddedMono8,
                    CreateDescriptor(
                        doctorWidth,
                        doctorHeight,
                        doctorWidth,
                        RawPixelFormat.Mono8,
                        8));
                var industrialFrameOwner = PinView(
                    pinnedViews,
                    "industrial-bgr24",
                    bgr24,
                    CreateDescriptor(width, height, width * 3, RawPixelFormat.BGR24, 8),
                    3);
                var industrialFrame = new IndustrialCameraFrame(industrialFrameOwner);
                industrialOpenCvMat = CreateMat(width, height, MatType.CV_8UC3, bgr24);
                industrialEmguMat = CreateEmguMat(
                    width,
                    height,
                    Emgu.CV.CvEnum.DepthType.Cv8U,
                    3,
                    bgr24);

                Console.WriteLine(
                    "Industrial image debugger smoke ready: " +
                    Path.GetFileName(path) + ", " +
                    width.ToString() + " x " + height.ToString() +
                    ", BGR24/Mono8; Buffer Doctor Mono8 " +
                    doctorWidth.ToString() + " x " + doctorHeight.ToString() +
                    " with " + doctorPadding.ToString() + " bytes of row padding.");
                if (shouldBreak)
                {
                    Debugger.Break();
                }

                GC.KeepAlive(industrialBgrSnapshot);
                GC.KeepAlive(industrialMonoSnapshot);
                GC.KeepAlive(industrialBadStrideSnapshot);
                GC.KeepAlive(industrialFrame);
                GC.KeepAlive(industrialOpenCvMat);
                GC.KeepAlive(industrialEmguMat);
                GC.KeepAlive(industrialBitmap);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Industrial image debugger smoke failed.");
                Console.Error.WriteLine("Type: " + ex.GetType().FullName);
                Console.Error.WriteLine("Message: " + ex.Message);
                return 1;
            }
            finally
            {
                industrialOpenCvMat?.Dispose();
                industrialEmguMat?.Dispose();
                industrialBitmap?.Dispose();
                foreach (var pinnedView in pinnedViews)
                {
                    pinnedView.Dispose();
                }
            }
        }

        private static Bitmap LoadBgr24Bitmap(string path)
        {
            using (var source = new Bitmap(path))
            {
                const int maxDimension = 1280;
                var scale = Math.Min(1.0, maxDimension / (double)Math.Max(source.Width, source.Height));
                var width = Math.Max(1, (int)Math.Round(source.Width * scale));
                var height = Math.Max(1, (int)Math.Round(source.Height * scale));
                var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.DrawImage(source, 0, 0, width, height);
                }

                return bitmap;
            }
        }

        private static byte[] ReadBgr24Buffer(Bitmap bitmap)
        {
            var stride = checked(bitmap.Width * 3);
            var buffer = new byte[checked(stride * bitmap.Height)];
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                var sourceStride = Math.Abs(data.Stride);
                for (var y = 0; y < bitmap.Height; y++)
                {
                    var source = data.Scan0 +
                        (data.Stride >= 0 ? y * data.Stride : (bitmap.Height - 1 - y) * sourceStride);
                    Marshal.Copy(source, buffer, y * stride, stride);
                }

                return buffer;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static byte[] ConvertBgr24ToMono8(byte[] bgr24, int width, int height)
        {
            var mono8 = new byte[checked(width * height)];
            for (var index = 0; index < mono8.Length; index++)
            {
                var offset = index * 3;
                mono8[index] = (byte)((
                    (bgr24[offset] * 29) +
                    (bgr24[offset + 1] * 150) +
                    (bgr24[offset + 2] * 77) + 128) >> 8);
            }

            return mono8;
        }

        private static byte[] AddRowPadding(byte[] source, int width, int height, int rowPadding)
        {
            var stride = checked(width + rowPadding);
            var padded = new byte[checked(stride * height)];
            for (var y = 0; y < height; y++)
            {
                Buffer.BlockCopy(source, y * width, padded, y * stride, width);
            }

            return padded;
        }

        private static byte[] ResizeMono8Nearest(
            byte[] source,
            int sourceWidth,
            int sourceHeight,
            int width,
            int height)
        {
            var resized = new byte[checked(width * height)];
            for (var y = 0; y < height; y++)
            {
                var sourceY = (int)((long)y * sourceHeight / height);
                for (var x = 0; x < width; x++)
                {
                    var sourceX = (int)((long)x * sourceWidth / width);
                    resized[(y * width) + x] = source[(sourceY * sourceWidth) + sourceX];
                }
            }

            return resized;
        }

        private static int RunBufferDoctorDebug()
        {
            const int width = 2448;
            const int height = 2048;
            const int padding = 112;
            const int stride = width + padding;
            var caseNumber = 1;

            var buffer = CreateMono8Buffer(width, height, stride);
            // Descriptor intentionally reports the wrong stride to simulate a mis-described buffer.
            var descriptor = CreateDescriptor(width, height, width, RawPixelFormat.Mono8, 8);
            var badStrideSnapshot = RawBufferSnapshot.FromByteArray(buffer, descriptor);
            PrintCase(ref caseNumber, "badStrideSnapshot as RawBufferSnapshot / mis-described Mono8 stride");
            Debugger.Break();
            GC.KeepAlive(badStrideSnapshot);
            return 0;
        }

        private static int RunSmartTypeMapperDebug()
        {
            var parameterOwner = new PinnedRawBufferView(
                "parameter-mono8",
                CreateMono8Buffer(Width, Height, Width),
                CreateDescriptor(Width, Height, Width, RawPixelFormat.Mono8, 8),
                1);
            try
            {
                return RunSmartTypeMapperDebug(new ParameterCompanyFrame(parameterOwner));
            }
            finally
            {
                parameterOwner.Dispose();
            }
        }

        private static int RunSmartTypeMapperDebug(ParameterCompanyFrame parameterFrame)
        {
            const int width = Width;
            const int height = Height;
            var caseNumber = 1;
            var pinnedViews = new List<PinnedRawBufferView>();
            var companyMono8Buffer = CreateMono8Buffer(width, height, width);

            var mono8Owner = PinView(
                pinnedViews,
                "company-mono8",
                companyMono8Buffer,
                CreateDescriptor(width, height, width, RawPixelFormat.Mono8, 8),
                1);
            var bgr24Owner = PinView(
                pinnedViews,
                "company-bgr24",
                CreateBgr24Buffer(width, height, width * 3),
                CreateDescriptor(width, height, width * 3, RawPixelFormat.BGR24, 8),
                3);
            var companyFrameList = new List<CompanyFrame>
            {
                new CompanyFrame(mono8Owner),
                new CompanyFrame(bgr24Owner)
            };
            var companyFrame = companyFrameList[0];
            const int arrayWidth = 64;
            const int arrayHeight = 48;
            var companyArrayFrame = new CompanyArrayFrame(
                CreateMono8Buffer(arrayWidth, arrayHeight, arrayWidth),
                arrayWidth,
                arrayHeight,
                arrayWidth,
                CompanyPixelType.Mono8);
            var nestedCompanyFrame = new NestedCompanyFrame(mono8Owner);
            var incompleteAutomaticFrame = new IncompleteAutomaticFrame(mono8Owner);
            var invalidAutomaticFrame = new InvalidAutomaticFrame(width, height);

            PrintCase(
                ref caseNumber,
                "locals + parameterFrame + mapping-required/open-failed rows / Automatic Vision Inspector");
            Debugger.Break();

            GC.KeepAlive(companyFrameList);
            GC.KeepAlive(companyFrame);
            GC.KeepAlive(companyArrayFrame);
            GC.KeepAlive(nestedCompanyFrame);
            GC.KeepAlive(incompleteAutomaticFrame);
            GC.KeepAlive(invalidAutomaticFrame);
            GC.KeepAlive(parameterFrame);
            GC.KeepAlive(mono8Owner);
            GC.KeepAlive(bgr24Owner);
            return 0;
        }

        private static int RunSmartTypeMapperFallbackDebug()
        {
            const int width = Width;
            const int height = Height;
            var caseNumber = 1;
            var pinnedViews = new List<PinnedRawBufferView>();
            var mono12Owner = PinView(
                pinnedViews,
                "company-mono12-packed",
                CreatePackedMonoBuffer(width, height, 12),
                CreateDescriptor(width, height, GetPackedStride(width, 12), RawPixelFormat.Mono12PackedLsb, 12),
                1);
            var unmappedCompanyFrame = new UnmappedCompanyFrame(mono12Owner);

            PrintCase(ref caseNumber, "unmappedCompanyFrame / Smart Type Mapper automatic fallback");
            Debugger.Break();

            GC.KeepAlive(unmappedCompanyFrame);
            GC.KeepAlive(mono12Owner);
            return 0;
        }

        private static int RunMultiLibraryDebug()
        {
            const int width = Width;
            const int height = Height;
            const int padding = 112;
            const int stride = width + padding;
            var caseNumber = 1;

            var buffer = CreateMono8Buffer(width, height, stride);
            var descriptor = CreateDescriptor(width, height, width, RawPixelFormat.Mono8, 8);
            var badStrideSnapshot = RawBufferSnapshot.FromByteArray(buffer, descriptor);
            PrintCase(ref caseNumber, "badStrideSnapshot as RawBufferSnapshot / mis-described Mono8 stride");

            var mono8Owner = PinView(
                new List<PinnedRawBufferView>(),
                "company-mono8",
                CreateMono8Buffer(width, height, width),
                CreateDescriptor(width, height, width, RawPixelFormat.Mono8, 8),
                1);
            var bgr24Owner = PinView(
                new List<PinnedRawBufferView>(),
                "company-bgr24",
                CreateBgr24Buffer(width, height, width * 3),
                CreateDescriptor(width, height, width * 3, RawPixelFormat.BGR24, 8),
                3);

            using (var openCvMat = new Mat(height, width, MatType.CV_8UC1))
            using (var emguMat = new Emgu.CV.Mat(height, width, Emgu.CV.CvEnum.DepthType.Cv8U, 1))
            using (var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb))
            {
                openCvMat.SetTo(new Scalar(37));
                emguMat.SetTo(new Emgu.CV.Structure.MCvScalar(173));
                var rawBufferView = mono8Owner.View;
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.FromArgb(17, 97, 201));
                }

                var paddingAwareFrame = new SimulatedPaddingAwareFrame(mono8Owner);
                var strideAwareFrame = new SimulatedStrideAwareFrame(mono8Owner);
                var offsetAwareFrame = new SimulatedOffsetAwareFrame(mono8Owner);
                var sizedBufferFrame = new SimulatedSizedBufferFrame(mono8Owner);

                Debugger.Break();

                GC.KeepAlive(badStrideSnapshot);
                GC.KeepAlive(rawBufferView);
                GC.KeepAlive(openCvMat);
                GC.KeepAlive(emguMat);
                GC.KeepAlive(bitmap);
                GC.KeepAlive(paddingAwareFrame);
                GC.KeepAlive(strideAwareFrame);
                GC.KeepAlive(offsetAwareFrame);
                GC.KeepAlive(sizedBufferFrame);
                GC.KeepAlive(mono8Owner);
                GC.KeepAlive(bgr24Owner);

                Console.WriteLine("Multi-library debug scenario completed. Press Enter to exit.");
                Console.ReadLine();
                return 0;
            }
        }

        private static int RunLargeMatDebug(string[] args)
        {
            var width = GetPositiveIntArgument(args, "--width", 8192);
            var height = GetPositiveIntArgument(args, "--height", 8192);
            var shouldBreak = Array.IndexOf(args, "--no-break") < 0;
            TryGetArgument(args, "--ready-file", out var readyPath);

            using (var largeOpenCvMat = new Mat(height, width, MatType.CV_8UC1))
            using (var largeEmguMat = new Emgu.CV.Mat(height, width, Emgu.CV.CvEnum.DepthType.Cv8U, 1))
            {
                largeOpenCvMat.SetTo(new Scalar(37));
                largeEmguMat.SetTo(new Emgu.CV.Structure.MCvScalar(173));

                var openCvView = OpenCvSharpMatVisualizerTransfer.CreateView(largeOpenCvMat, nameof(largeOpenCvMat));
                var openCvMetadata = OpenCvSharpMatVisualizerTransfer.CreateMetadata(openCvView);
                var emguView = EmguCvMatVisualizerTransfer.CreateView(largeEmguMat, nameof(largeEmguMat));
                var emguMetadata = EmguCvMatVisualizerTransfer.CreateMetadata(emguView);
                if (!openCvMetadata.SupportsDirectMemory || !emguMetadata.SupportsDirectMemory)
                {
                    throw new InvalidOperationException("Large Mat metadata must support direct debugger memory.");
                }

                if (!string.IsNullOrWhiteSpace(readyPath))
                {
                    var directory = Path.GetDirectoryName(Path.GetFullPath(readyPath));
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllLines(
                        readyPath,
                        new[]
                        {
                            "processId=" + Process.GetCurrentProcess().Id.ToString(),
                            "width=" + width.ToString(),
                            "height=" + height.ToString(),
                            "openCvBytes=" + openCvMetadata.BufferLength.ToString(),
                            "emguBytes=" + emguMetadata.BufferLength.ToString(),
                            "openCvValue=37",
                            "emguValue=173"
                        });
                }

                Console.WriteLine(
                    "Large Mat debugger smoke ready: " + width.ToString() + " x " + height.ToString() +
                    ", OpenCvSharp " + openCvMetadata.BufferLength.ToString() + " bytes" +
                    ", Emgu " + emguMetadata.BufferLength.ToString() + " bytes.");
                BreakForLargeMats(largeOpenCvMat, largeEmguMat, shouldBreak);
                return 0;
            }
        }

        private static void BreakForLargeMats(
            Mat largeOpenCvMat,
            Emgu.CV.Mat largeEmguMat,
            bool shouldBreak)
        {
            if (shouldBreak)
            {
                Debugger.Break();
            }

            GC.KeepAlive(largeOpenCvMat);
            GC.KeepAlive(largeEmguMat);
        }

        private static int GetPositiveIntArgument(string[] args, string name, int defaultValue)
        {
            if (!TryGetArgument(args, name, out var value))
            {
                return defaultValue;
            }

            if (!int.TryParse(value, out var parsed) || parsed <= 0)
            {
                throw new ArgumentOutOfRangeException(name, "A positive integer is required.");
            }

            return parsed;
        }

        private static int RunEmguTiffSmoke(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Console.Error.WriteLine("TIFF file was not found: " + path);
                return 2;
            }

            try
            {
                using (var mat = new Emgu.CV.Mat(path, Emgu.CV.CvEnum.ImreadModes.Unchanged))
                {
                    var view = EmguCvMatVisualizerTransfer.CreateView(mat, Path.GetFileName(path));
                    var metadata = EmguCvMatVisualizerTransfer.CreateMetadata(view);
                    var chunk = EmguCvMatVisualizerTransfer.CreateChunk(
                        view,
                        new VisualizerSnapshotChunkRequest
                        {
                            Offset = 0,
                            Count = Math.Min(4096, VisualizerChunkedTransfer.DefaultChunkSize)
                        });

                    Console.WriteLine("Emgu TIFF smoke passed.");
                    Console.WriteLine(
                        metadata.Descriptor.Width.ToString() + " x " +
                        metadata.Descriptor.Height.ToString() + ", " +
                        metadata.Descriptor.PixelFormat.ToString() + ", stride " +
                        metadata.Descriptor.Stride.ToString() + ", bytes " +
                        metadata.BufferLength.ToString() + ", first chunk " +
                        chunk.Buffer.Length.ToString());
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Emgu TIFF smoke failed.");
                Console.Error.WriteLine("Type: " + ex.GetType().FullName);
                Console.Error.WriteLine("Message: " + ex.Message);
                return 1;
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

    internal enum CompanyPixelType
    {
        Mono8,
        Mono12,
        Bgr
    }

    internal sealed class CompanyFrame
    {
        private readonly PinnedRawBufferView _owner;

        public IntPtr ImageAddress { get; private set; }
        public int SizeX { get; private set; }
        public int SizeY { get; private set; }
        public int LinePitch { get; private set; }
        public CompanyPixelType PixelType { get; private set; }

        public CompanyFrame(PinnedRawBufferView owner)
        {
            _owner = owner;
            ImageAddress = owner.View.Buffer;
            SizeX = owner.View.Width;
            SizeY = owner.View.Height;
            LinePitch = owner.View.Stride;
            PixelType = MapPixelFormat(owner.View.PixelFormat);
        }

        private static CompanyPixelType MapPixelFormat(RawPixelFormat format)
        {
            switch (format)
            {
                case RawPixelFormat.BGR24:
                    return CompanyPixelType.Bgr;
                case RawPixelFormat.Mono12PackedLsb:
                    return CompanyPixelType.Mono12;
                case RawPixelFormat.Mono8:
                default:
                    return CompanyPixelType.Mono8;
            }
        }
    }

    internal sealed class ParameterCompanyFrame
    {
        private readonly PinnedRawBufferView _owner;

        public IntPtr ImageAddress { get; private set; }
        public int SizeX { get; private set; }
        public int SizeY { get; private set; }
        public int LinePitch { get; private set; }
        public CompanyPixelType PixelType { get; private set; }

        public ParameterCompanyFrame(PinnedRawBufferView owner)
        {
            _owner = owner;
            ImageAddress = owner.View.Buffer;
            SizeX = owner.View.Width;
            SizeY = owner.View.Height;
            LinePitch = owner.View.Stride;
            PixelType = CompanyPixelType.Mono8;
        }
    }

    internal sealed class IncompleteAutomaticFrame
    {
        private readonly PinnedRawBufferView _owner;

        public IntPtr ImageAddress { get; private set; }
        public int SizeX { get; private set; }
        public int SizeY { get; private set; }
        public int LinePitch { get; private set; }

        public IncompleteAutomaticFrame(PinnedRawBufferView owner)
        {
            _owner = owner;
            ImageAddress = owner.View.Buffer;
            SizeX = owner.View.Width;
            SizeY = owner.View.Height;
            LinePitch = owner.View.Stride;
        }
    }

    internal sealed class InvalidAutomaticFrame
    {
        public IntPtr ImageAddress { get; private set; }
        public int SizeX { get; private set; }
        public int SizeY { get; private set; }
        public int LinePitch { get; private set; }
        public CompanyPixelType PixelType { get; private set; }

        public InvalidAutomaticFrame(int width, int height)
        {
            ImageAddress = IntPtr.Zero;
            SizeX = width;
            SizeY = height;
            LinePitch = width;
            PixelType = CompanyPixelType.Mono8;
        }
    }

    internal sealed class UnmappedCompanyFrame
    {
        private readonly PinnedRawBufferView _owner;

        public IntPtr ImageAddress { get; private set; }
        public int SizeX { get; private set; }
        public int SizeY { get; private set; }
        public int LinePitch { get; private set; }
        public CompanyPixelType PixelType { get; private set; }

        public UnmappedCompanyFrame(PinnedRawBufferView owner)
        {
            _owner = owner;
            ImageAddress = owner.View.Buffer;
            SizeX = owner.View.Width;
            SizeY = owner.View.Height;
            LinePitch = owner.View.Stride;
            PixelType = CompanyPixelType.Mono12;
        }
    }

    internal sealed class CompanyArrayFrame
    {
        public byte[] Pixels { get; private set; }
        public int FrameWidth { get; private set; }
        public int FrameHeight { get; private set; }
        public int LinePitch { get; private set; }
        public CompanyPixelType PixelType { get; private set; }

        public CompanyArrayFrame(
            byte[] pixels,
            int frameWidth,
            int frameHeight,
            int linePitch,
            CompanyPixelType pixelType)
        {
            Pixels = pixels;
            FrameWidth = frameWidth;
            FrameHeight = frameHeight;
            LinePitch = linePitch;
            PixelType = pixelType;
        }
    }

    internal sealed class NestedCompanyFrame
    {
        private readonly PinnedRawBufferView _owner;

        public NestedCompanyStorage Storage { get; private set; }
        public NestedCompanyInfo Info { get; private set; }

        public NestedCompanyFrame(PinnedRawBufferView owner)
        {
            _owner = owner;
            Storage = new NestedCompanyStorage { ImageAddress = owner.View.Buffer };
            Info = new NestedCompanyInfo
            {
                Width = owner.View.Width,
                Height = owner.View.Height,
                Stride = owner.View.Stride,
                PixelType = CompanyPixelType.Mono8
            };
        }
    }

    internal sealed class NestedCompanyStorage
    {
        public IntPtr ImageAddress { get; set; }
    }

    internal sealed class NestedCompanyInfo
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int Stride { get; set; }
        public CompanyPixelType PixelType { get; set; }
    }

    // Simulation of OpenCvSharp.Mat
    internal enum SimulatedMatType
    {
        Mono8,
        Mono16,
        Bgr24,
        Bgra32
    }

    internal sealed class SimulatedOpenCvSharpMat
    {
        private readonly PinnedRawBufferView _owner;

        public IntPtr Data { get; private set; }
        public int Rows { get; private set; }
        public int Cols { get; private set; }
        public int Step { get; private set; }
        public SimulatedMatType Type { get; private set; }

        public SimulatedOpenCvSharpMat(PinnedRawBufferView owner)
        {
            _owner = owner;
            Data = owner.View.Buffer;
            Rows = owner.View.Height;
            Cols = owner.View.Width;
            Step = owner.View.Stride;
            Type = MapMatType(owner.View.PixelFormat);
        }

        private static SimulatedMatType MapMatType(RawPixelFormat format)
        {
            switch (format)
            {
                case RawPixelFormat.Mono16:
                    return SimulatedMatType.Mono16;
                case RawPixelFormat.BGR24:
                    return SimulatedMatType.Bgr24;
                case RawPixelFormat.BGRA32:
                    return SimulatedMatType.Bgra32;
                case RawPixelFormat.Mono8:
                default:
                    return SimulatedMatType.Mono8;
            }
        }
    }

    // Simulation of Emgu.CV.Mat
    internal enum SimulatedDepthType
    {
        Mono8,
        Mono16,
        Bgr24,
        Bgra32
    }

    internal sealed class SimulatedEmguCvMat
    {
        private readonly PinnedRawBufferView _owner;

        public IntPtr DataPointer { get; private set; }
        public int Rows { get; private set; }
        public int Cols { get; private set; }
        public int Step { get; private set; }
        public SimulatedDepthType Depth { get; private set; }
        public int Channels { get; private set; }

        public SimulatedEmguCvMat(PinnedRawBufferView owner)
        {
            _owner = owner;
            DataPointer = owner.View.Buffer;
            Rows = owner.View.Height;
            Cols = owner.View.Width;
            Step = owner.View.Stride;
            Depth = MapDepthType(owner.View.PixelFormat);
            Channels = owner.View.Channels;
        }

        private static SimulatedDepthType MapDepthType(RawPixelFormat format)
        {
            switch (format)
            {
                case RawPixelFormat.Mono16:
                    return SimulatedDepthType.Mono16;
                case RawPixelFormat.BGR24:
                    return SimulatedDepthType.Bgr24;
                case RawPixelFormat.BGRA32:
                    return SimulatedDepthType.Bgra32;
                case RawPixelFormat.Mono8:
                default:
                    return SimulatedDepthType.Mono8;
            }
        }
    }

    internal sealed class SimulatedPaddingAwareFrame
    {
        private readonly PinnedRawBufferView _owner;

        public IntPtr PixelDataPointer { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int PaddingX { get; private set; }
        public long PayloadSize { get; private set; }
        public RawPixelFormat PixelTypeValue { get; private set; }

        public SimulatedPaddingAwareFrame(PinnedRawBufferView owner)
        {
            _owner = owner;
            PixelDataPointer = owner.View.Buffer;
            Width = owner.View.Width;
            Height = owner.View.Height;
            PaddingX = owner.View.Stride - owner.View.ToDescriptor().GetMinimumStride();
            PayloadSize = owner.View.BufferLength;
            PixelTypeValue = owner.View.PixelFormat;
        }
    }

    internal sealed class SimulatedStrideAwareFrame
    {
        private readonly PinnedRawBufferView _owner;

        public IntPtr DataPtr { get; private set; }
        public uint Width { get; private set; }
        public uint Height { get; private set; }
        public uint Stride { get; private set; }
        public RawPixelFormat PixelFormat { get; private set; }

        public SimulatedStrideAwareFrame(PinnedRawBufferView owner)
        {
            _owner = owner;
            DataPtr = owner.View.Buffer;
            Width = (uint)owner.View.Width;
            Height = (uint)owner.View.Height;
            Stride = (uint)owner.View.Stride;
            PixelFormat = owner.View.PixelFormat;
        }
    }

    internal sealed class SimulatedOffsetAwareFrame
    {
        private readonly PinnedRawBufferView _owner;

        public IntPtr Buffer { get; private set; }
        public uint BufferSize { get; private set; }
        public IntPtr ImageData { get; private set; }
        public uint Width { get; private set; }
        public uint Height { get; private set; }
        public RawPixelFormat PixelFormat { get; private set; }

        public SimulatedOffsetAwareFrame(PinnedRawBufferView owner)
        {
            _owner = owner;
            Buffer = owner.View.Buffer;
            BufferSize = (uint)owner.View.BufferLength;
            ImageData = owner.View.Buffer;
            Width = (uint)owner.View.Width;
            Height = (uint)owner.View.Height;
            PixelFormat = owner.View.PixelFormat;
        }
    }

    internal sealed class SimulatedSizedBufferFrame
    {
        private readonly PinnedRawBufferView _owner;

        public IntPtr Data { get; private set; }
        public uint Width { get; private set; }
        public uint Height { get; private set; }
        public RawPixelFormat PixelFormat { get; private set; }
        public long SizeInBytes { get; private set; }

        public SimulatedSizedBufferFrame(PinnedRawBufferView owner)
        {
            _owner = owner;
            Data = owner.View.Buffer;
            Width = (uint)owner.View.Width;
            Height = (uint)owner.View.Height;
            PixelFormat = owner.View.PixelFormat;
            SizeInBytes = owner.View.BufferLength;
        }
    }


}
