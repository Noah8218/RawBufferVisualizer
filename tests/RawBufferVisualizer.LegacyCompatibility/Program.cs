using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using Emgu.CV;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.VisualStudio.ObjectSource;

namespace RawBufferVisualizer.LegacyCompatibility
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var library = args.Length >= 1 ? args[0] : "all";
            var packageVersion = args.Length >= 2 ? args[1] : "unknown";
            try
            {
                VerifyBitmap();
                if (library == "all" || library == "emgu")
                {
                    VerifyEmgu(packageVersion);
                }

                if (library == "all" || library == "opencvsharp")
                {
                    VerifyOpenCvSharp(packageVersion);
                }

                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    "FAIL package={0} type={1} message={2}",
                    packageVersion,
                    exception.GetType().FullName,
                    exception.Message);
                return 1;
            }
        }

        private static void VerifyOpenCvSharp(string packageVersion)
        {
            using (var mat = new OpenCvSharp.Mat(2, 3, OpenCvSharp.MatType.CV_8UC3))
            {
                var view = OpenCvSharpMatVisualizerTransfer.CreateView(mat, "legacy-opencvsharp");
                var metadata = OpenCvSharpMatVisualizerTransfer.CreateMetadata(view);
                var chunk = OpenCvSharpMatVisualizerTransfer.CreateChunk(
                    view,
                    new VisualizerSnapshotChunkRequest
                    {
                        Offset = 0,
                        Count = (int)Math.Min(view.BufferLength, 16)
                    });

                Require(view.Descriptor.Width == 3 && view.Descriptor.Height == 2, "OpenCvSharp dimensions");
                Require(view.Descriptor.PixelFormat == RawPixelFormat.BGR24, "OpenCvSharp pixel format");
                Require(view.BufferLength == (long)view.Descriptor.Stride * 2, "OpenCvSharp buffer length");
                Require(chunk.Buffer.Length > 0, "OpenCvSharp chunk length");
                Require(metadata.BufferAddress == mat.Data.ToInt64(), "OpenCvSharp pixel Data address");
                Require(metadata.SourcePointerAddress == ReadPointerProperty(mat, "CvPtr").ToInt64(), "OpenCvSharp Ptr address");
                Require(metadata.SourcePointerLabel == "Ptr", "OpenCvSharp Ptr label");

                var matType = typeof(OpenCvSharp.Mat);
                Console.WriteLine(
                    "PASS library=OpenCvSharp package={0} assembly={1} typeAssembly={2} stride={3}",
                    packageVersion,
                    matType.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                        ?? matType.Assembly.GetName().Version?.ToString()
                        ?? "unknown",
                    matType.Assembly.FullName,
                    view.Descriptor.Stride);
            }

            using (var mat = new OpenCvSharp.Mat(2, 3, OpenCvSharp.MatType.CV_32SC1))
            {
                var view = OpenCvSharpMatVisualizerTransfer.CreateView(mat, "legacy-opencvsharp-int32");
                Require(view.Descriptor.PixelFormat == RawPixelFormat.Int32, "OpenCvSharp CV_32SC1 pixel format");
                Require(view.Descriptor.ValidBits == 32, "OpenCvSharp CV_32SC1 valid bits");
                Require(view.Descriptor.Stride >= 12, "OpenCvSharp CV_32SC1 stride");
            }
        }

        private static void VerifyBitmap()
        {
            using (var bitmap = new Bitmap(3, 2, PixelFormat.Format24bppRgb))
            {
                bitmap.SetPixel(0, 0, Color.FromArgb(10, 20, 30));
                var view = BitmapVisualizerTransfer.CreateView(bitmap, "legacy-bitmap");
                var metadata = BitmapVisualizerTransfer.CreateMetadata(view);
                var transfer = BitmapVisualizerTransfer.CreateTransfer(bitmap, "legacy-bitmap");
                Require(transfer.Descriptor.Width == 3 && transfer.Descriptor.Height == 2, "Bitmap dimensions");
                Require(transfer.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Bitmap pixel format");
                Require(transfer.Buffer.Length >= 18, "Bitmap buffer length");
                Require(view.CapturedScan0 != IntPtr.Zero, "Bitmap captured Scan0");
                Require(metadata.SourcePointerAddress == view.CapturedScan0.ToInt64(), "Bitmap Scan0 address");
                Require(metadata.SourcePointerLabel == "Scan0", "Bitmap Scan0 label");
                Require(!metadata.SupportsDirectMemory, "Bitmap Scan0 must be historical, not live memory");
            }
        }

        private static void VerifyEmgu(string packageVersion)
        {
            using (var mat = new Mat(2, 3, Emgu.CV.CvEnum.DepthType.Cv8U, 3))
            {
                var view = EmguCvMatVisualizerTransfer.CreateView(mat, "legacy-emgu");
                var metadata = EmguCvMatVisualizerTransfer.CreateMetadata(view);
                var chunk = EmguCvMatVisualizerTransfer.CreateChunk(
                    view,
                    new VisualizerSnapshotChunkRequest
                    {
                        Offset = 0,
                        Count = (int)Math.Min(view.BufferLength, 16)
                    });

                Require(view.Descriptor.Width == 3 && view.Descriptor.Height == 2, "Emgu dimensions");
                Require(view.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Emgu pixel format");
                Require(view.BufferLength == (long)view.Descriptor.Stride * 2, "Emgu buffer length");
                Require(chunk.Buffer.Length > 0, "Emgu chunk length");
                Require(metadata.BufferAddress == mat.DataPointer.ToInt64(), "Emgu pixel DataPointer address");
                Require(metadata.SourcePointerAddress == ReadPointerProperty(mat, "Ptr").ToInt64(), "Emgu Ptr address");
                Require(metadata.SourcePointerLabel == "Ptr", "Emgu Ptr label");

                var matType = typeof(Mat);
                Console.WriteLine(
                    "PASS library=Emgu package={0} assembly={1} typeAssembly={2} stride={3}",
                    packageVersion,
                    matType.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                        ?? matType.Assembly.GetName().Version?.ToString()
                        ?? "unknown",
                    matType.Assembly.FullName,
                    view.Descriptor.Stride);
            }

            using (var mat = new Mat(2, 3, Emgu.CV.CvEnum.DepthType.Cv32S, 1))
            {
                var view = EmguCvMatVisualizerTransfer.CreateView(mat, "legacy-emgu-int32");
                Require(view.Descriptor.PixelFormat == RawPixelFormat.Int32, "Emgu CV CV_32SC1 pixel format");
                Require(view.Descriptor.ValidBits == 32, "Emgu CV CV_32SC1 valid bits");
                Require(view.Descriptor.Stride >= 12, "Emgu CV CV_32SC1 stride");
            }
        }

        private static void Require(bool condition, string name)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Compatibility check failed: " + name);
            }
        }

        private static IntPtr ReadPointerProperty(object instance, string propertyName)
        {
            for (var type = instance.GetType(); type != null; type = type.BaseType)
            {
                var property = type.GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (property != null && property.PropertyType == typeof(IntPtr))
                {
                    return (IntPtr)(property.GetValue(instance, null)
                        ?? throw new InvalidOperationException(propertyName + " returned null."));
                }
            }

            throw new MissingMemberException(instance.GetType().FullName, propertyName);
        }
    }
}
