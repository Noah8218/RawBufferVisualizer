using System;
using System.Collections.Generic;
using EnvDTE;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    internal sealed class ImageCandidate
    {
        public string RootExpression { get; set; } = string.Empty;
        public string RuntimeTypeName { get; set; } = string.Empty;
        public string DataExpression { get; set; } = string.Empty;
        public string WidthExpression { get; set; } = string.Empty;
        public string HeightExpression { get; set; } = string.Empty;
        public string? StrideExpression { get; set; }
        public string? PixelFormatExpression { get; set; }
        public string? BufferLengthExpression { get; set; }
        public string? ValidBitsExpression { get; set; }
        public int ConfidenceScore { get; set; }
        public bool RequiresMapping { get; set; }
        public string Summary { get; set; } = string.Empty;
    }

    internal sealed class ImageTypeRecognizer
    {
        private static readonly string[] DataAliases =
        {
            "data", "buffer", "imagebuffer", "pixeldata", "address",
            "imageaddress", "pointer", "ptr", "scan0", "_data", "_buffer",
            "datapointer", "pixeldata"
        };

        private static readonly string[] WidthAliases =
        {
            "width", "sizex", "columns", "cols", "imagewidth", "_width", "w",
            "col"
        };

        private static readonly string[] HeightAliases =
        {
            "height", "sizey", "rows", "imageheight", "_height", "h",
            "row"
        };

        private static readonly string[] StrideAliases =
        {
            "stride", "pitch", "linepitch", "step", "bytesperline",
            "_stride", "_pitch", "_step"
        };

        private static readonly string[] PixelFormatAliases =
        {
            "pixelformat", "format", "rawpixelformat", "pixeltype",
            "pixel_format", "_pixelformat", "_format", "type", "depth"
        };

        private static readonly string[] ValidBitsAliases =
        {
            "validbits", "bitdepth", "depth", "bits", "_validbits",
            "_bitdepth", "_depth"
        };

        private static readonly string[] BufferLengthAliases =
        {
            "length", "bufferlength", "size", "bytelength",
            "buffersize", "_length", "_size", "_buffersize"
        };

        public ImageCandidate? TryRecognize(Expression root)
        {
            if (root == null)
            {
                return null;
            }

            string typeName = root.Type ?? string.Empty;

            // 1. Known type
            if (KnownImageType.IsKnownType(typeName))
            {
                return new ImageCandidate
                {
                    RootExpression = root.Name,
                    RuntimeTypeName = typeName,
                    ConfidenceScore = 100,
                    RequiresMapping = false,
                    Summary = "Known image type"
                };
            }

            // 2. Unsupported collection (e.g. List<CompanyFrame>)
            if (KnownImageType.IsSupportedCollection(typeName))
            {
                return new ImageCandidate
                {
                    RootExpression = root.Name,
                    RuntimeTypeName = typeName,
                    ConfidenceScore = 85,
                    RequiresMapping = true,
                    Summary = "Unsupported collection — check inner type"
                };
            }

            // 3. Structure-based recognition
            Expressions members;
            try
            {
                members = root.DataMembers;
            }
            catch
            {
                return null;
            }

            if (members == null || members.Count == 0)
            {
                return null;
            }

            var data = FindMember(members, DataAliases, IsBufferType);
            var width = FindMember(members, WidthAliases, IsIntegerType);
            var height = FindMember(members, HeightAliases, IsIntegerType);
            var stride = FindMember(members, StrideAliases, IsIntegerType);
            var pixelFormat = FindMember(members, PixelFormatAliases, IsEnumType);
            var bufferLength = FindMember(members, BufferLengthAliases, IsIntegerType);
            var validBits = FindMember(members, ValidBitsAliases, IsIntegerType);

            if (data == null || width == null || height == null)
            {
                return null;
            }

            int score = 0;
            if (IsBufferType(data.TypeName))
            {
                score += 35;
            }

            if (IsIntegerType(width.TypeName))
            {
                score += 15;
            }

            if (IsIntegerType(height.TypeName))
            {
                score += 15;
            }

            if (stride != null && IsIntegerType(stride.TypeName))
            {
                score += 10;
            }

            if (pixelFormat != null)
            {
                score += 10;
            }

            if (ContainsImageHint(typeName))
            {
                score += 5;
            }

            return new ImageCandidate
            {
                RootExpression = root.Name,
                RuntimeTypeName = typeName,
                DataExpression = root.Name + "." + data.Name,
                WidthExpression = root.Name + "." + width.Name,
                HeightExpression = root.Name + "." + height.Name,
                StrideExpression = stride == null ? null : root.Name + "." + stride.Name,
                PixelFormatExpression = pixelFormat == null ? null : root.Name + "." + pixelFormat.Name,
                BufferLengthExpression = bufferLength == null ? null : root.Name + "." + bufferLength.Name,
                ValidBitsExpression = validBits == null ? null : root.Name + "." + validBits.Name,
                ConfidenceScore = score,
                RequiresMapping = score < 80,
                Summary = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "Buffer: {0}, Width: {1}, Height: {2}, Stride: {3}",
                    data.TypeName,
                    width.TypeName,
                    height.TypeName,
                    stride == null ? "none" : stride.TypeName)
            };
        }

        private static MemberInfo? FindMember(
            Expressions members,
            string[] aliases,
            Func<string, bool> typePredicate)
        {
            for (int i = 1; i <= members.Count; i++)
            {
                Expression member;
                try
                {
                    member = members.Item(i);
                }
                catch
                {
                    continue;
                }

                if (member == null || !member.IsValidValue)
                {
                    continue;
                }

                string memberName = member.Name ?? string.Empty;
                string memberType = member.Type ?? string.Empty;

                for (int a = 0; a < aliases.Length; a++)
                {
                    if (memberName.IndexOf(aliases[a], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (typePredicate(memberType))
                        {
                            return new MemberInfo(memberName, memberType);
                        }
                    }
                }
            }

            return null;
        }

        private static bool IsBufferType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return false;
            }

            return typeName.IndexOf("IntPtr", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("UIntPtr", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("Byte[]", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("UInt16[]", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("Single[]", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsIntegerType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return false;
            }

            return typeName == "int"
                || typeName == "System.Int32"
                || typeName == "uint"
                || typeName == "System.UInt32"
                || typeName == "long"
                || typeName == "System.Int64"
                || typeName == "short"
                || typeName == "System.Int16";
        }

        private static bool IsEnumType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return false;
            }

            return typeName.IndexOf("Type", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("Format", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("PixelType", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsImageHint(string typeName)
        {
            return typeName.IndexOf("image", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("frame", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("buffer", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private sealed class MemberInfo
        {
            public string Name { get; }
            public string TypeName { get; }

            public MemberInfo(string name, string typeName)
            {
                Name = name;
                TypeName = typeName;
            }
        }
    }
}
