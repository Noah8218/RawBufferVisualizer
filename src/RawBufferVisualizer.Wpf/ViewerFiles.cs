using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.Sdk;

namespace RawBufferVisualizer.Wpf
{
    internal static class ViewerFiles
    {
        public const long PreviewLimit = 512L * 1024 * 1024;

        public static long GetLength(string path) => new FileInfo(path).Length;

        public static ViewerDocument Open(string path, RawImageDescriptor? rawDescriptor)
        {
            var rawPath = path;
            RawImageDescriptor descriptor;
            if (path.EndsWith(".rbuf.json", StringComparison.OrdinalIgnoreCase))
            {
                var reference = RawBufferSnapshot.LoadReference(path);
                rawPath = reference.RawPath;
                descriptor = reference.Descriptor;
            }
            else descriptor = rawDescriptor?.Clone() ?? throw new InvalidOperationException("Confirm RAW image settings before opening.");

            var length = new FileInfo(rawPath).Length;
            var errors = RawBufferDiagnostics.AnalyzeLength(length, descriptor).Where(item => item.Severity == RawDiagnosticSeverity.Error).ToArray();
            if (errors.Length > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors.Select(item => item.Message)));
            if (length > PreviewLimit && !RawImageSource.CanStreamFormat(descriptor.PixelFormat))
                throw new NotSupportedException("This format exceeds the in-memory limit and cannot be read as tiles.");
            var source = length > PreviewLimit ? RawImageSource.FromFile(rawPath, descriptor) : RawImageSource.FromMemory(File.ReadAllBytes(rawPath), descriptor);
            try
            {
                var currentErrors = source.Analyze().Where(item => item.Severity == RawDiagnosticSeverity.Error).ToArray();
                if (currentErrors.Length > 0) throw new InvalidDataException(string.Join(Environment.NewLine, currentErrors.Select(item => item.Message)));
                return new ViewerDocument(path, source);
            }
            catch { source.Dispose(); throw; }
        }

        public static ViewerDocument CreateExample()
        {
            var descriptor = new RawImageDescriptor { Width = 640, Height = 480, Stride = 640, PixelFormat = RawPixelFormat.Mono8, ValidBits = 8 };
            var bytes = new byte[descriptor.Width * descriptor.Height];
            for (var y = 0; y < descriptor.Height; y++)
                for (var x = 0; x < descriptor.Width; x++) bytes[y * descriptor.Stride + x] = (byte)(x * 255 / (descriptor.Width - 1));
            return new ViewerDocument(null, RawImageSource.FromMemory(bytes, descriptor));
        }

        public static BitmapSource CreateBitmap(RenderedImage image)
        {
            var bitmap = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null, image.Bgra32, image.Stride);
            bitmap.Freeze();
            return bitmap;
        }

        public static BitmapSource? CreateThumbnail(RawImageSource source)
        {
            try
            {
                var descriptor = source.Descriptor;
                var step = Math.Max(1, Math.Max((int)Math.Ceiling(descriptor.Width / 96d), (int)Math.Ceiling(descriptor.Height / 72d)));
                return CreateBitmap(source.RenderTileSampled(0, 0, descriptor.Width, descriptor.Height, step, source.CreateRenderOptions()));
            }
            catch { return null; }
        }

        public static void SavePng(string path, RenderedImage image)
        {
            // Stage beside the destination so an encoder failure cannot truncate an existing PNG.
            var fullPath = Path.GetFullPath(path);
            var temporary = Path.Combine(Path.GetDirectoryName(fullPath)!, ".rbv-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(CreateBitmap(image)));
                    encoder.Save(stream);
                    stream.Flush(true);
                }
                if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
                else File.Move(temporary, fullPath);
            }
            finally
            {
                try { File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
