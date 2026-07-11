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
            var packageVersion = args.Length == 1 ? args[0] : "unknown";
            try
            {
                VerifyBitmap();
                VerifyEmgu(packageVersion);
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

        private static void VerifyBitmap()
        {
            using (var bitmap = new Bitmap(3, 2, PixelFormat.Format24bppRgb))
            {
                bitmap.SetPixel(0, 0, Color.FromArgb(10, 20, 30));
                var transfer = BitmapVisualizerTransfer.CreateTransfer(bitmap, "legacy-bitmap");
                Require(transfer.Descriptor.Width == 3 && transfer.Descriptor.Height == 2, "Bitmap dimensions");
                Require(transfer.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Bitmap pixel format");
                Require(transfer.Buffer.Length >= 18, "Bitmap buffer length");
            }
        }

        private static void VerifyEmgu(string packageVersion)
        {
            using (var mat = new Mat(2, 3, Emgu.CV.CvEnum.DepthType.Cv8U, 3))
            {
                var view = EmguCvMatVisualizerTransfer.CreateView(mat, "legacy-emgu");
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

                var matType = typeof(Mat);
                Console.WriteLine(
                    "PASS package={0} assembly={1} typeAssembly={2} stride={3}",
                    packageVersion,
                    matType.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                        ?? matType.Assembly.GetName().Version?.ToString()
                        ?? "unknown",
                    matType.Assembly.FullName,
                    view.Descriptor.Stride);
            }
        }

        private static void Require(bool condition, string name)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Compatibility check failed: " + name);
            }
        }
    }
}
