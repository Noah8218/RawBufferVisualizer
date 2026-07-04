using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using OpenCvSharp;
using RawBufferVisualizer.BitmapAdapter;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.OpenCvSharpAdapter;
using RawBufferVisualizer.Recorder;
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
                Mono10PackedLsbRenders();
                Mono12PackedLsbInspects();
                Bgr24KeepsChannelOrder();
                TilePlannerSplitsLargeImage();
                TileRenderMatchesFullRender();
                InvalidStrideIsReported();
                SnapshotRoundTrips();
                BitmapAdapterCreatesSnapshot();
                MatAdapterCreatesSnapshot();
                VisionRecorderWritesManifestPackage();
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

        private static void TilePlannerSplitsLargeImage()
        {
            var tiles = RawImageTilePlanner.CreateTiles(16384, 16384);
            Assert(tiles.Count == 16, "16K image should split into 16 OpenGL upload tiles.");
            Assert(tiles[0].X == 0 && tiles[0].Y == 0 && tiles[0].Width == 5000 && tiles[0].Height == 5000, "First tile bounds failed.");
            Assert(tiles[15].X == 15000 && tiles[15].Y == 15000 && tiles[15].Width == 1384 && tiles[15].Height == 1384, "Last tile bounds failed.");

            var descriptor = new RawImageDescriptor
            {
                Width = 16384,
                Height = 16384,
                Stride = 16384,
                PixelFormat = RawPixelFormat.Mono8,
                ValidBits = 8,
                ByteOrder = RawByteOrder.LittleEndian
            };
            Assert(RawImageTilePlanner.EstimateBgraByteCount(descriptor) == 1073741824L, "BGRA memory estimate failed.");
        }

        private static void TileRenderMatchesFullRender()
        {
            var descriptor = new RawImageDescriptor
            {
                Width = 4,
                Height = 3,
                Stride = 4,
                PixelFormat = RawPixelFormat.Mono8,
                ValidBits = 8,
                ByteOrder = RawByteOrder.LittleEndian
            };
            var buffer = new byte[]
            {
                1, 2, 3, 4,
                5, 6, 7, 8,
                9, 10, 11, 12
            };

            var full = RawBufferRenderer.Render(buffer, descriptor);
            var tile = RawBufferRenderer.RenderTile(buffer, descriptor, 1, 1, 2, 2);
            Assert(tile.Width == 2 && tile.Height == 2, "Tile render dimensions failed.");
            Assert(tile.Bgra32[0] == full.Bgra32[((1 * descriptor.Width) + 1) * 4], "Tile render first pixel failed.");
            Assert(tile.Bgra32[12] == full.Bgra32[((2 * descriptor.Width) + 2) * 4], "Tile render last pixel failed.");
        }

        private static void Mono10PackedLsbRenders()
        {
            var descriptor = new RawImageDescriptor
            {
                Width = 4,
                Height = 1,
                Stride = 5,
                PixelFormat = RawPixelFormat.Mono10PackedLsb,
                ValidBits = 10,
                ByteOrder = RawByteOrder.LittleEndian
            };

            Assert(descriptor.GetMinimumStride() == 5, "Mono10 packed stride failed.");
            var rendered = RawBufferRenderer.Render(PackLsb(new[] { 0, 128, 512, 1023 }, 10), descriptor);
            Assert(rendered.Bgra32[0] == 0, "Mono10 packed black failed.");
            Assert(rendered.Bgra32[12] == 255, "Mono10 packed white failed.");
        }

        private static void Mono12PackedLsbInspects()
        {
            var descriptor = new RawImageDescriptor
            {
                Width = 2,
                Height = 1,
                Stride = 3,
                PixelFormat = RawPixelFormat.Mono12PackedLsb,
                ValidBits = 12,
                ByteOrder = RawByteOrder.LittleEndian
            };

            var text = RawPixelInspector.Describe(PackLsb(new[] { 7, 4095 }, 12), descriptor, 1, 0);
            Assert(text.Contains("Value=4095"), "Mono12 packed inspector failed.");
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

        private static void VisionRecorderWritesManifestPackage()
        {
            var directory = Path.Combine(Path.GetTempPath(), "RawBufferVisualizerTests", Guid.NewGuid().ToString("N"));
            var vrecPath = Path.Combine(directory, "shot.vrec");
            Directory.CreateDirectory(directory);

            using (var shot = VisionRecorder.Begin("Cam1", "T001", "Main"))
            {
                var descriptor = new RawImageDescriptor
                {
                    Width = 2,
                    Height = 2,
                    Stride = 2,
                    PixelFormat = RawPixelFormat.Mono8,
                    ValidBits = 8,
                    ByteOrder = RawByteOrder.LittleEndian
                };
                shot.AddImage("01_raw", "Raw", new byte[] { 1, 2, 3, 4 }, descriptor);
                shot.AddParam("Threshold", 120, "01_raw");
                shot.AddMeasure("Width", 12.345, "mm", 12.0, 13.0, true, "01_raw");
                shot.AddEvent("PLC Trigger", 0.0);
                shot.AddRectangleRoi("01_raw", "SearchROI", 10, 20, 30, 40);
                shot.AddException(new InvalidOperationException("No edge found."), "01_raw");
                shot.Result(false);
                shot.Save(vrecPath);
            }

            using (var archive = new ZipArchive(File.OpenRead(vrecPath), ZipArchiveMode.Read))
            {
                var entry = archive.GetEntry("manifest.json");
                if (entry == null)
                {
                    throw new InvalidOperationException("VREC manifest entry missing.");
                }

                using (var reader = new StreamReader(entry.Open()))
                {
                    var json = reader.ReadToEnd();
                    Assert(json.Contains("\"schema\":\"vrec\""), "VREC schema missing.");
                    Assert(json.Contains("\"camera\":\"Cam1\""), "VREC camera missing.");
                    Assert(json.Contains("\"result\":\"NG\""), "VREC result missing.");
                    Assert(json.Contains("\"id\":\"01_raw\""), "VREC stage missing.");
                    Assert(json.Contains("\"image\":\"images\\/01_raw.rbuf.json\""), "VREC stage image missing.");
                }

                var descriptorEntry = archive.GetEntry("images/01_raw.rbuf.json");
                if (descriptorEntry == null)
                {
                    throw new InvalidOperationException("VREC image descriptor missing.");
                }

                using (var reader = new StreamReader(descriptorEntry.Open()))
                {
                    var json = reader.ReadToEnd();
                    Assert(json.Contains("\"rawFile\":\"01_raw.raw\""), "VREC rawFile missing.");
                    Assert(json.Contains("\"pixelFormat\":\"Mono8\""), "VREC pixel format missing.");
                }

                var rawEntry = archive.GetEntry("images/01_raw.raw");
                if (rawEntry == null)
                {
                    throw new InvalidOperationException("VREC raw payload missing.");
                }

                using (var rawStream = rawEntry.Open())
                {
                    Assert(rawStream.ReadByte() == 1, "VREC raw first byte failed.");
                    Assert(rawStream.ReadByte() == 2, "VREC raw second byte failed.");
                }

                Assert(ReadZipEntry(archive, "params.json").Contains("\"name\":\"Threshold\""), "VREC params missing.");
                Assert(ReadZipEntry(archive, "params.json").Contains("\"type\":\"int\""), "VREC param type missing.");
                Assert(ReadZipEntry(archive, "params.json").Contains("\"value\":120"), "VREC param value missing.");
                Assert(ReadZipEntry(archive, "measures.json").Contains("\"unit\":\"mm\""), "VREC measure missing.");
                Assert(ReadZipEntry(archive, "events.json").Contains("\"name\":\"PLC Trigger\""), "VREC event missing.");
                Assert(ReadZipEntry(archive, "overlays.json").Contains("\"kind\":\"rectangle\""), "VREC ROI overlay missing.");
                Assert(ReadZipEntry(archive, "exceptions.json").Contains("\"message\":\"No edge found.\""), "VREC exception missing.");
            }

            Directory.Delete(directory, true);
        }

        private static string ReadZipEntry(ZipArchive archive, string path)
        {
            var entry = archive.GetEntry(path);
            if (entry == null)
            {
                throw new InvalidOperationException("VREC entry missing: " + path);
            }

            using (var reader = new StreamReader(entry.Open()))
            {
                return reader.ReadToEnd();
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
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
    }
}
