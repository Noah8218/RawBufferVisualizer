using System;

namespace RawBufferVisualizer.Core
{
    /// <summary>Normalizes imported Bitmap samples to the straight-alpha BGRA contract.</summary>
    public static class BitmapPixelConverter
    {
        public static bool IsIdentityGrayPalette(int[] palette)
        {
            if (palette == null || palette.Length != 256) return false;
            for (var i = 0; i < palette.Length; i++)
                if (palette[i] != unchecked((int)(0xFF000000u | (uint)(i * 0x010101)))) return false;
            return true;
        }

        public static void NormalizeBgra32(byte[] buffer, int offset, int pixelCount, int[]? palette, bool premultiplied, bool opaque, int sourcePixelStride = 4)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || pixelCount < 0 || (long)offset + (long)pixelCount * 4 > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(pixelCount));
            if (sourcePixelStride != 4 && (sourcePixelStride != 1 || palette == null))
                throw new ArgumentOutOfRangeException(nameof(sourcePixelStride));
            if (palette == null && !premultiplied && !opaque) return;

            // Backward expansion lets indexed rows reuse their destination storage.
            for (var pixel = pixelCount - 1; pixel >= 0; pixel--)
            {
                var source = offset + pixel * sourcePixelStride;
                var target = offset + pixel * 4;
                if (palette != null)
                {
                    var argb = palette[buffer[source]];
                    buffer[target] = (byte)argb;
                    buffer[target + 1] = (byte)(argb >> 8);
                    buffer[target + 2] = (byte)(argb >> 16);
                    buffer[target + 3] = (byte)(argb >> 24);
                    continue;
                }

                var alpha = opaque ? (byte)255 : buffer[source + 3];
                if (premultiplied)
                {
                    for (var channel = 0; channel < 3; channel++)
                        buffer[target + channel] = alpha == 0 ? (byte)0 : (byte)Math.Min(255, (buffer[source + channel] * 255 + alpha / 2) / alpha);
                }

                buffer[target + 3] = alpha;
            }
        }
    }
}
