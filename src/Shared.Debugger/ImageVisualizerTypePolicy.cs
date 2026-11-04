using System;
using System.Collections.Generic;

namespace RawBufferVisualizer.VisualStudio.Debugger
{
    internal static class ImageVisualizerTypePolicy
    {
        internal const string OpenCvSharpMatTypeName = "OpenCvSharp.Mat";
        internal static IReadOnlyList<(string TypeName, string AssemblyName, string ObjectSource, bool AllowReflectionByName)> Registrations { get; } =
            Array.AsReadOnly(new[]
            {
                ("RawBufferVisualizer.Sdk.RawBufferSnapshot", "RawBufferVisualizer.Sdk", "RawBufferSnapshotVisualizerObjectSource", false),
                ("RawBufferVisualizer.Sdk.RawBufferView", "RawBufferVisualizer.Sdk", "RawBufferViewVisualizerObjectSource", false),
                ("System.Drawing.Bitmap", "System.Drawing", "BitmapVisualizerObjectSource", true),
                ("System.Drawing.Bitmap", "System.Drawing.Common", "BitmapVisualizerObjectSource", true),
                (OpenCvSharpMatTypeName, "OpenCvSharp", "OpenCvSharpMatVisualizerObjectSource", true),
                ("OpenCvSharp.Mat`1", "OpenCvSharp", "OpenCvSharpMatVisualizerObjectSource", false),
                ("Emgu.CV.Mat", "Emgu.CV", "EmguCvMatVisualizerObjectSource", true),
                ("Emgu.CV.Mat", "Emgu.CV.World", "EmguCvMatVisualizerObjectSource", true),
                ("Emgu.CV.Mat", "Emgu.CV.World.NetStandard", "EmguCvMatVisualizerObjectSource", true),
                ("Emgu.CV.Mat", "Emgu.CV.Platform.NetStandard", "EmguCvMatVisualizerObjectSource", true),
                ("Cressem.ImageModel.ImagePtr", "Cressem.ImageModel", "ImagePtrVisualizerObjectSource", false)
            });

        internal static string? GetObjectSource(Type type, bool allowLegacyNameMatch)
        {
            var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
            return GetObjectSource(definition.FullName, definition.Assembly.GetName().Name, allowLegacyNameMatch);
        }

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

        internal static string? GetObjectSource(string? fullName, string? assemblyName, bool allowLegacyNameMatch = false)
        {
            for (var index = 0; index < Registrations.Count; index++)
            {
                var registration = Registrations[index];
                if (fullName == registration.TypeName
                    && (assemblyName == registration.AssemblyName || (allowLegacyNameMatch && registration.AllowReflectionByName)))
                    return registration.ObjectSource;
            }
            return null;
        }
    }
}
