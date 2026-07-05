using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RawBufferVisualizer.Core;

namespace RawBufferVisualizer.VisualStudio.Extensibility
{
    internal static class DockedVisualizerPreviewRenderer
    {
        public static DockedVisualizerPreviewFiles CreatePreviewFiles(
            string rawPath,
            RawImageDescriptor descriptor,
            string outputDirectory)
        {
            var diagnostics = new List<string>();
            using (var source = RawImageSource.FromFile(rawPath, descriptor))
            {
                var analysis = source.Analyze();
                diagnostics.AddRange(analysis.Select(diagnostic => diagnostic.ToString()));
                if (RawBufferDiagnostics.HasErrors(analysis))
                {
                    return new DockedVisualizerPreviewFiles(string.Empty, string.Empty, diagnostics);
                }

                var previewPath = Path.Combine(outputDirectory, "preview.png");
                var thumbnailPath = Path.Combine(outputDirectory, "thumbnail.png");
                SaveSampledPng(source, descriptor, previewPath, 1200);
                SaveSampledPng(source, descriptor, thumbnailPath, 96);
                diagnostics.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "Info: VS docked preview is sampled PNG. Full tiled OpenGL viewing remains in the standalone viewer path."));
                return new DockedVisualizerPreviewFiles(previewPath, thumbnailPath, diagnostics);
            }
        }

        private static void SaveSampledPng(RawImageSource source, RawImageDescriptor descriptor, string path, int maxDimension)
        {
            var sampleStep = GetSampleStep(descriptor, maxDimension);
            var rendered = source.RenderTileSampled(0, 0, descriptor.Width, descriptor.Height, sampleStep, source.CreateRenderOptions());
            var bitmap = BitmapSource.Create(
                rendered.Width,
                rendered.Height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                rendered.Bgra32,
                rendered.Stride);
            bitmap.Freeze();

            using (var stream = File.Create(path))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(stream);
            }
        }

        private static int GetSampleStep(RawImageDescriptor descriptor, int maxDimension)
        {
            var longest = Math.Max(descriptor.Width, descriptor.Height);
            return Math.Max(1, (int)Math.Ceiling(longest / (double)Math.Max(1, maxDimension)));
        }
    }
}
