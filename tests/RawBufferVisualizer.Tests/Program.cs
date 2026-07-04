using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using OpenCvSharp;
using RawBufferVisualizer.BitmapAdapter;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.OpenCvSharpAdapter;
using RawBufferVisualizer.Sdk;

namespace RawBufferVisualizer.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            try
            {
                Mono8RendersToBgra();
                Bgr24KeepsChannelOrder();
                InvalidStrideIsReported();
                SnapshotRoundTrips();
                BitmapAdapterCreatesSnapshot();
                MatAdapterCreatesSnapshot();
                Console.WriteLine("RawBufferVisualizer self-tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void Mono8RendersToBgra()
        {
            var descriptor = new RawImageDescriptor
            {
                Width = 2,
                Height = 1,
                Stride = 2,
                PixelFormat = RawPixelFormat.Mono8,
                ValidBits = 8,
                ByteOrder = RawByteOrder.LittleEndian
            };

            var rendered = RawBufferRenderer.Render(new byte[] { 10, 250 }, descriptor);
            Assert(rendered.Bgra32[0] == 10 && rendered.Bgra32[1] == 10 && rendered.Bgra32[2] == 10 && rendered.Bgra32[3] == 255, "Mono8 first pixel failed.");
            Assert(rendered.Bgra32[4] == 250 && rendered.Bgra32[5] == 250 && rendered.Bgra32[6] == 250 && rendered.Bgra32[7] == 255, "Mono8 second pixel failed.");
        }

        private static void Bgr24KeepsChannelOrder()
        {
            var descriptor = new RawImageDescriptor
            {
                Width = 1,
                Height = 1,
                Stride = 3,
                PixelFormat = RawPixelFormat.BGR24,
                ValidBits = 8,
                ByteOrder = RawByteOrder.LittleEndian
            };

            var rendered = RawBufferRenderer.Render(new byte[] { 1, 2, 3 }, descriptor);
            Assert(rendered.Bgra32[0] == 1 && rendered.Bgra32[1] == 2 && rendered.Bgra32[2] == 3 && rendered.Bgra32[3] == 255, "BGR24 channel order failed.");
        }

        private static void InvalidStrideIsReported()
        {
            var descriptor = new RawImageDescriptor
            {
                Width = 2,
                Height = 1,
                Stride = 1,
                PixelFormat = RawPixelFormat.Mono8,
                ValidBits = 8,
                ByteOrder = RawByteOrder.LittleEndian
            };

            var diagnostics = RawBufferDiagnostics.Analyze(new byte[] { 1, 2 }, descriptor);
            Assert(RawBufferDiagnostics.HasErrors(diagnostics), "Invalid stride should be an error.");
        }

        private static void SnapshotRoundTrips()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            var metadataPath = Path.Combine(directory, "roundtrip.rbuf.json");
            var descriptor = new RawImageDescriptor
            {
                Width = 2,
                Height = 2,
                Stride = 2,
                PixelFormat = RawPixelFormat.Mono8,
                ValidBits = 8,
                ByteOrder = RawByteOrder.LittleEndian
            };

            RawBufferSnapshot.Save(metadataPath, new byte[] { 1, 2, 3, 4 }, descriptor);
            var loaded = RawBufferSnapshot.Load(metadataPath);
            Assert(loaded.Descriptor.Width == 2 && loaded.Descriptor.Height == 2, "Snapshot descriptor roundtrip failed.");
            Assert(loaded.Buffer.Length == 4 && loaded.Buffer[3] == 4, "Snapshot buffer roundtrip failed.");
            Directory.Delete(directory, true);
        }

        private static void BitmapAdapterCreatesSnapshot()
        {
            using (var bitmap = new Bitmap(2, 1, PixelFormat.Format24bppRgb))
            {
                bitmap.SetPixel(0, 0, Color.FromArgb(10, 20, 30));
                bitmap.SetPixel(1, 0, Color.FromArgb(40, 50, 60));

                var snapshot = BitmapSnapshot.FromBitmap(bitmap);
                Assert(snapshot.Descriptor.Width == 2 && snapshot.Descriptor.Height == 1, "Bitmap dimensions failed.");
                Assert(snapshot.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Bitmap pixel format failed.");
                Assert(snapshot.Buffer.Length >= 6, "Bitmap buffer length failed.");
            }
        }

        private static void MatAdapterCreatesSnapshot()
        {
            using (var mat = new Mat(1, 2, MatType.CV_8UC3, new Scalar(3, 2, 1)))
            {
                var snapshot = MatSnapshot.FromMat(mat);
                Assert(snapshot.Descriptor.Width == 2 && snapshot.Descriptor.Height == 1, "Mat dimensions failed.");
                Assert(snapshot.Descriptor.PixelFormat == RawPixelFormat.BGR24, "Mat pixel format failed.");
                Assert(snapshot.Buffer.Length >= 6, "Mat buffer length failed.");
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
