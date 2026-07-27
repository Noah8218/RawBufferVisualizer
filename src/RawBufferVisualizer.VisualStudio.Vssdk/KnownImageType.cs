using System;
using System.Collections.Generic;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    /// <summary>
    /// Registry of image types that Raw Buffer Visualizer can open directly.
    /// </summary>
    internal static class KnownImageType
    {
        public static bool IsKnownType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return false;
            }

            return typeName.IndexOf("RawBufferSnapshot", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("PinnedRawBufferView", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("OpenCvSharp.Mat", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("Emgu.CV.Mat", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("System.Drawing.Bitmap", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("BitmapSource", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsSupportedCollection(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return false;
            }

            return typeName.IndexOf("List", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("Array", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("IEnumerable", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
