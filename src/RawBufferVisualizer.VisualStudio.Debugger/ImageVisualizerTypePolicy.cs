namespace RawBufferVisualizer.VisualStudio.Debugger
{
    internal static class ImageVisualizerTypePolicy
    {
        internal static bool IsOwnedObjectSource(string? assemblyName)
        {
            return assemblyName?.Split(',')[0].Trim() == "RawBufferVisualizer.VisualStudio.ObjectSource";
        }

        internal static bool IsFrameworkCollectionAssembly(string? assemblyName)
        {
            return assemblyName == "mscorlib" || assemblyName == "System.Private.CoreLib"
                || assemblyName == "System.Collections" || assemblyName == "System.Collections.Concurrent";
        }

        internal static int GetCollectionValueArgument(string? definitionName, string? assemblyName)
        {
            if (!IsFrameworkCollectionAssembly(assemblyName)) return -1;

            switch (definitionName)
            {
                case "System.Collections.Generic.List`1": return 0;
                case "System.Collections.Generic.Dictionary`2":
                case "System.Collections.Concurrent.ConcurrentDictionary`2": return 1;
                default: return -1;
            }
        }

        internal static string? GetObjectSource(string? fullName, string? assemblyName)
        {
            switch (fullName)
            {
                case "RawBufferVisualizer.Sdk.RawBufferSnapshot" when assemblyName == "RawBufferVisualizer.Sdk":
                    return "RawBufferSnapshotVisualizerObjectSource";
                case "RawBufferVisualizer.Sdk.RawBufferView" when assemblyName == "RawBufferVisualizer.Sdk":
                    return "RawBufferViewVisualizerObjectSource";
                case "System.Drawing.Bitmap" when assemblyName == "System.Drawing" || assemblyName == "System.Drawing.Common":
                    return "BitmapVisualizerObjectSource";
                case "OpenCvSharp.Mat" when assemblyName == "OpenCvSharp":
                    return "OpenCvSharpMatVisualizerObjectSource";
                case "Emgu.CV.Mat" when assemblyName == "Emgu.CV" || assemblyName == "Emgu.CV.World" || assemblyName == "Emgu.CV.World.NetStandard" || assemblyName == "Emgu.CV.Platform.NetStandard":
                    return "EmguCvMatVisualizerObjectSource";
                case "Cressem.ImageModel.ImagePtr" when assemblyName == "Cressem.ImageModel":
                    return "ImagePtrVisualizerObjectSource";
                default:
                    return null;
            }
        }
    }
}
