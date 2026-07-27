using System;
using System.Collections.Generic;
using RawBufferVisualizer.Core;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    public sealed class VisionMemberInferenceResult
    {
        public TypeMappingMembers Members { get; } = new TypeMappingMembers();
        public int ConfidenceScore { get; internal set; }
        public RawPixelFormat? PixelFormat { get; internal set; }
        public bool RequiresPixelFormatMapping { get; internal set; }
        public List<string> MissingRoles { get; } = new List<string>();
        public List<string> Reasons { get; } = new List<string>();

        public bool HasRequiredMembers
        {
            get
            {
                return !string.IsNullOrWhiteSpace(Members.Data)
                    && !string.IsNullOrWhiteSpace(Members.Width)
                    && !string.IsNullOrWhiteSpace(Members.Height);
            }
        }

        public bool CanAutoOpen
        {
            get
            {
                return HasRequiredMembers
                    && PixelFormat.HasValue
                    && !RequiresPixelFormatMapping
                    && ConfidenceScore >= 90;
            }
        }
    }

    public static class VisionMemberInference
    {
        private static readonly string[] DataNames =
        {
            "data", "buffer", "ptr", "pointer", "imageaddress", "datapointer", "pixeldata", "imagebuffer", "bytes"
        };

        private static readonly string[] WidthNames =
        {
            "width", "sizex", "cols", "columns", "imagewidth"
        };

        private static readonly string[] HeightNames =
        {
            "height", "sizey", "rows", "imageheight"
        };

        private static readonly string[] StrideNames =
        {
            "stride", "step", "pitch", "bytesperline", "rowbytes", "linesize"
        };

        private static readonly string[] PixelFormatNames =
        {
            "pixelformat", "pixeltype", "rawpixelformat", "format", "imageformat"
        };

        private static readonly string[] BufferLengthNames =
        {
            "bufferlength", "bytelength", "datasize", "imagesize", "payloadsize", "size", "length"
        };

        private static readonly string[] ValidBitsNames =
        {
            "validbits", "bitdepth", "bitsperpixel", "depth"
        };

        public static VisionMemberInferenceResult Infer(
            IReadOnlyList<VisualizerMemberInventoryItem> inventory,
            string? runtimeTypeName)
        {
            var result = new VisionMemberInferenceResult();
            if (inventory == null || inventory.Count == 0)
            {
                result.MissingRoles.Add("data");
                result.MissingRoles.Add("width");
                result.MissingRoles.Add("height");
                result.Reasons.Add("No readable fields or property getters were found.");
                return result;
            }

            var data = FindBest(inventory, DataNames, IsSupportedDataType, true);
            var width = FindBest(inventory, WidthNames, IsIntegerType, false);
            var height = FindBest(inventory, HeightNames, IsIntegerType, false);
            var stride = FindBest(inventory, StrideNames, IsIntegerType, false);
            var pixelFormat = FindBest(inventory, PixelFormatNames, null, false);
            var bufferLength = FindBest(inventory, BufferLengthNames, IsIntegerType, false);
            var validBits = FindBest(inventory, ValidBitsNames, IsIntegerType, false);

            result.Members.Data = NameOf(data);
            result.Members.Width = NameOf(width);
            result.Members.Height = NameOf(height);
            result.Members.Stride = NameOf(stride);
            result.Members.PixelFormat = NameOf(pixelFormat);
            result.Members.BufferLength = NameOf(bufferLength);
            result.Members.ValidBits = NameOf(validBits);

            AddRequiredRole(result, data, "data", 30);
            AddRequiredRole(result, width, "width", 20);
            AddRequiredRole(result, height, "height", 20);
            AddOptionalRole(result, stride, "stride", 8);
            AddOptionalRole(result, bufferLength, "buffer length", 4);
            AddOptionalRole(result, validBits, "valid bits", 2);

            RawPixelFormat resolvedFormat;
            if (pixelFormat != null)
            {
                if (TryResolvePixelFormat(pixelFormat.SampleValue, out resolvedFormat))
                {
                    result.PixelFormat = resolvedFormat;
                    result.ConfidenceScore += 12;
                    result.Reasons.Add("Pixel format resolved from " + pixelFormat.Name + ".");
                }
                else
                {
                    result.RequiresPixelFormatMapping = true;
                    result.ConfidenceScore += 6;
                    result.Reasons.Add("Pixel format member was found, but its current value needs one explicit mapping.");
                }
            }
            else if (TryResolveFormatFromDataType(data == null ? null : data.TypeName, out resolvedFormat))
            {
                result.PixelFormat = resolvedFormat;
                result.ConfidenceScore += 10;
                result.Reasons.Add("Pixel format inferred from the managed array element type.");
            }
            else
            {
                result.MissingRoles.Add("pixel format");
                result.Reasons.Add("Pixel format could not be inferred safely.");
            }

            if (LooksLikeVisionType(runtimeTypeName))
            {
                result.ConfidenceScore += 4;
                result.Reasons.Add("Runtime type name is image/frame/buffer-like.");
            }

            result.ConfidenceScore = Math.Min(100, result.ConfidenceScore);
            return result;
        }

        public static bool TryResolvePixelFormat(string? value, out RawPixelFormat pixelFormat)
        {
            pixelFormat = RawPixelFormat.Mono8;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var text = value!.Trim().Trim('{', '}');
            var equals = text.LastIndexOf('=');
            if (equals >= 0 && equals + 1 < text.Length)
            {
                text = text.Substring(equals + 1).Trim();
            }

            var dot = text.LastIndexOf('.');
            if (dot >= 0 && dot + 1 < text.Length)
            {
                text = text.Substring(dot + 1);
            }

            if (Enum.TryParse(text, true, out pixelFormat))
            {
                return true;
            }

            var normalized = Normalize(text);
            switch (normalized)
            {
                case "GRAY8":
                case "GREY8":
                case "Y8":
                case "MONO8":
                    pixelFormat = RawPixelFormat.Mono8;
                    return true;
                case "GRAY16":
                case "GREY16":
                case "Y16":
                case "MONO16":
                    pixelFormat = RawPixelFormat.Mono16;
                    return true;
                case "FLOAT":
                case "FLOAT32":
                case "SINGLE":
                    pixelFormat = RawPixelFormat.Float32;
                    return true;
                case "BGR":
                case "BGR8":
                case "BGR24":
                    pixelFormat = RawPixelFormat.BGR24;
                    return true;
                case "RGB":
                case "RGB8":
                case "RGB24":
                    pixelFormat = RawPixelFormat.RGB24;
                    return true;
                case "BGRA":
                case "BGRA8":
                case "BGRA32":
                    pixelFormat = RawPixelFormat.BGRA32;
                    return true;
                default:
                    return false;
            }
        }

        private static VisualizerMemberInventoryItem? FindBest(
            IReadOnlyList<VisualizerMemberInventoryItem> inventory,
            string[] roleNames,
            Func<string?, bool>? typePredicate,
            bool allowTypeOnly)
        {
            VisualizerMemberInventoryItem? best = null;
            var bestScore = int.MinValue;
            for (var i = 0; i < inventory.Count; i++)
            {
                var item = inventory[i];
                var typeMatches = typePredicate == null || typePredicate(item.TypeName);
                if (typePredicate != null && !typeMatches)
                {
                    continue;
                }

                var leafName = GetLeafName(item.Name);
                var normalized = Normalize(leafName);
                var nameScore = MatchName(normalized, roleNames);
                if (nameScore <= 0 && !allowTypeOnly)
                {
                    continue;
                }

                var score = nameScore + (typeMatches ? 35 : 0) - (item.Name.IndexOf('.') >= 0 ? 3 : 0);
                if (score > bestScore)
                {
                    best = item;
                    bestScore = score;
                }
            }

            return bestScore >= 35 ? best : null;
        }

        private static int MatchName(string normalizedName, string[] roleNames)
        {
            for (var i = 0; i < roleNames.Length; i++)
            {
                var candidate = Normalize(roleNames[i]);
                if (normalizedName == candidate)
                {
                    return 70 - i;
                }
            }

            for (var i = 0; i < roleNames.Length; i++)
            {
                var candidate = Normalize(roleNames[i]);
                if (normalizedName.IndexOf(candidate, StringComparison.Ordinal) >= 0)
                {
                    return 40 - Math.Min(i, 20);
                }
            }

            return 0;
        }

        private static void AddRequiredRole(
            VisionMemberInferenceResult result,
            VisualizerMemberInventoryItem? item,
            string role,
            int score)
        {
            if (item == null)
            {
                result.MissingRoles.Add(role);
                result.Reasons.Add("Required " + role + " member was not found.");
                return;
            }

            result.ConfidenceScore += score;
            result.Reasons.Add("Inferred " + role + " from " + item.Name + ".");
        }

        private static void AddOptionalRole(
            VisionMemberInferenceResult result,
            VisualizerMemberInventoryItem? item,
            string role,
            int score)
        {
            if (item == null)
            {
                return;
            }

            result.ConfidenceScore += score;
            result.Reasons.Add("Inferred " + role + " from " + item.Name + ".");
        }

        private static string? NameOf(VisualizerMemberInventoryItem? item)
        {
            return item == null ? null : item.Name;
        }

        private static bool IsSupportedDataType(string? typeName)
        {
            var normalized = NormalizeType(typeName);
            return normalized == "intptr"
                || normalized == "uintptr"
                || normalized == "byte[]"
                || normalized == "uint16[]"
                || normalized == "ushort[]"
                || normalized == "single[]"
                || normalized == "float[]";
        }

        private static bool IsIntegerType(string? typeName)
        {
            var normalized = NormalizeType(typeName);
            return normalized == "byte"
                || normalized == "sbyte"
                || normalized == "int16"
                || normalized == "short"
                || normalized == "uint16"
                || normalized == "ushort"
                || normalized == "int32"
                || normalized == "int"
                || normalized == "uint32"
                || normalized == "uint"
                || normalized == "int64"
                || normalized == "long"
                || normalized == "uint64"
                || normalized == "ulong";
        }

        private static bool TryResolveFormatFromDataType(string? typeName, out RawPixelFormat pixelFormat)
        {
            var normalized = NormalizeType(typeName);
            if (normalized == "uint16[]" || normalized == "ushort[]")
            {
                pixelFormat = RawPixelFormat.Mono16;
                return true;
            }

            if (normalized == "single[]" || normalized == "float[]")
            {
                pixelFormat = RawPixelFormat.Float32;
                return true;
            }

            pixelFormat = RawPixelFormat.Mono8;
            return false;
        }

        private static string NormalizeType(string? typeName)
        {
            var normalized = (typeName ?? string.Empty).Trim().ToLowerInvariant();
            return normalized.StartsWith("system.", StringComparison.Ordinal)
                ? normalized.Substring("system.".Length)
                : normalized;
        }

        private static string GetLeafName(string name)
        {
            var dot = name.LastIndexOf('.');
            return dot >= 0 && dot + 1 < name.Length ? name.Substring(dot + 1) : name;
        }

        private static string Normalize(string value)
        {
            var chars = new char[value.Length];
            var count = 0;
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                if (char.IsLetterOrDigit(ch))
                {
                    chars[count++] = char.ToUpperInvariant(ch);
                }
            }

            return new string(chars, 0, count);
        }

        private static bool LooksLikeVisionType(string? typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return false;
            }

            return typeName!.IndexOf("image", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("frame", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("bitmap", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("buffer", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("mat", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
