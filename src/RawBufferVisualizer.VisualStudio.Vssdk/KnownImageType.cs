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

        public static bool UsesRegisteredVisualizerPath(string typeName)
        {
            var runtimeTypeName = GetRuntimeTypeName(typeName);
            if (runtimeTypeName.Length == 0)
            {
                return false;
            }

            return string.Equals(
                    runtimeTypeName,
                    "RawBufferVisualizer.Sdk.RawBufferSnapshot",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    runtimeTypeName,
                    "RawBufferVisualizer.Sdk.RawBufferView",
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(runtimeTypeName, "OpenCvSharp.Mat", StringComparison.OrdinalIgnoreCase)
                || string.Equals(runtimeTypeName, "Emgu.CV.Mat", StringComparison.OrdinalIgnoreCase)
                || string.Equals(runtimeTypeName, "System.Drawing.Bitmap", StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    runtimeTypeName,
                    "Cressem.ImageModel.ImagePtr",
                    StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsMetadataOnlyType(string typeName)
        {
            return string.Equals(
                GetRuntimeTypeName(typeName),
                "RawBufferVisualizer.Core.RawImageDescriptor",
                StringComparison.OrdinalIgnoreCase);
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

        private static string GetRuntimeTypeName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return string.Empty;
            }

            var normalized = typeName.Trim();
            var assemblySeparator = normalized.IndexOf(',');
            return assemblySeparator < 0
                ? normalized
                : normalized.Substring(0, assemblySeparator).Trim();
        }
    }
}
