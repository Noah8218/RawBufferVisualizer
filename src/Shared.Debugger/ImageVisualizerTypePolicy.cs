using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

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
                ("Emgu.CV.Mat", "Emgu.CV.Platform.NetStandard", "EmguCvMatVisualizerObjectSource", true)
            });

        // Exact identity: SHA-256(UTF-8(full type name + NUL + simple assembly name)).
        // DTE supplies only a display name. Its case-insensitive hash can suppress an automatic
        // scan, but cannot admit a debugger visualizer. Lengths avoid hashing unrelated locals.
        private static readonly (int TypeLength, int AssemblyLength, string IdentityHash, string ScanNameHash)[] PointerFrames =
        {
            (27, 18, "EC2hUpsgpEB3cmwnl6/EYEpRZsJ1gJvAXVXB9T1X03I=", "l2mZS4Pf90X/3qL5CM7lMeRo5HJakXDLVOzcLRgkI5k="),
            // Public sample; exercises the same lookup through root, child and collection results.
            (51, 38, "QbpYV3cBYLdFhlxwaymKlJBTgXj/ECSk+UCqq43bbf0=", "qaSEpKMSDWkcm4MTTqlLliHlGw/2SNpjvHRA7iNvgmw=")
        };

        internal static bool UsesPointerFrameVisualizer(string typeName)
        {
            foreach (var frame in PointerFrames)
            {
                if (typeName.Length == frame.TypeLength && Fingerprint(typeName.ToUpperInvariant()) == frame.ScanNameHash)
                    return true;
            }
            return false;
        }

        private static string Fingerprint(string identity)
        {
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(identity)));
        }

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
            foreach (var frame in PointerFrames)
            {
                if (fullName?.Length == frame.TypeLength && assemblyName?.Length == frame.AssemblyLength
                    && Fingerprint(fullName + "\0" + assemblyName) == frame.IdentityHash)
                    return "PointerFrameVisualizerObjectSource";
            }
            return null;
        }
    }
}
