using System;
using System.Globalization;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.Sdk;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    internal static class MappedTypeVisualizerTransfer
    {
        public static ImageCollectionItemTransfer? TryCreateItemTransfer(
            object value,
            string displayName,
            TypeMapping mapping,
            out string error)
        {
            error = string.Empty;
            if (value == null)
            {
                error = "Mapped value is null.";
                return null;
            }

            if (mapping == null || mapping.Members == null)
            {
                error = "Type mapping is empty.";
                return null;
            }

            var type = value.GetType();
            var typeName = type.FullName ?? type.Name;
            var members = mapping.Members;

            int width;
            int height;
            if (!TryReadInt32(value, type, members.Width, "width", out width, out error)
                || !TryReadInt32(value, type, members.Height, "height", out height, out error))
            {
                return null;
            }

            var pixelFormat = RawPixelFormat.Mono8;
            if (!string.IsNullOrWhiteSpace(members.PixelFormat)
                && !TryReadPixelFormat(value, type, members.PixelFormat!, mapping, out pixelFormat, out error))
            {
                return null;
            }

            var validBits = 0;
            if (!string.IsNullOrWhiteSpace(members.ValidBits))
            {
                if (!TryReadInt32(value, type, members.ValidBits, "validBits", out validBits, out error))
                {
                    return null;
                }
            }
            else if (!string.IsNullOrWhiteSpace(members.BitDepth))
            {
                if (!TryReadInt32(value, type, members.BitDepth, "bitDepth", out validBits, out error))
                {
                    return null;
                }
            }

            RawByteOrder byteOrder;
            if (!Enum.TryParse(mapping.ByteOrder, true, out byteOrder))
            {
                error = "Mapping byte order is not supported: " + mapping.ByteOrder;
                return null;
            }

            var stride = 0;
            if (!string.IsNullOrWhiteSpace(members.Stride)
                && !TryReadInt32(value, type, members.Stride, "stride", out stride, out error))
            {
                return null;
            }

            var descriptor = new RawImageDescriptor
            {
                Width = width,
                Height = height,
                PixelFormat = pixelFormat,
                ValidBits = validBits > 0 ? validBits : ImagePtrVisualizerTransfer.GetDefaultValidBits(pixelFormat),
                ByteOrder = byteOrder
            };
            descriptor.Stride = stride > 0 ? stride : descriptor.GetMinimumStride();

            var dataValue = ReadMember(value, type, members.Data, "data", out error);
            if (error.Length > 0)
            {
                return null;
            }

            if (dataValue == null)
            {
                error = string.Format(CultureInfo.InvariantCulture, "Mapped data member '{0}' is null.", members.Data);
                return null;
            }

            if (dataValue is IntPtr || dataValue is UIntPtr)
            {
                return CreatePointerTransfer(value, type, members, descriptor, typeName, displayName, dataValue, out error);
            }

            return CreateArrayTransfer(dataValue, descriptor, typeName, displayName, members.Data ?? string.Empty, out error);
        }

        private static ImageCollectionItemTransfer? CreatePointerTransfer(
            object value,
            Type type,
            TypeMappingMembers members,
            RawImageDescriptor descriptor,
            string typeName,
            string displayName,
            object dataValue,
            out string error)
        {
            error = string.Empty;
            var pointer = ImagePtrVisualizerTransfer.ConvertValue<IntPtr>(dataValue);
            if (pointer == IntPtr.Zero)
            {
                error = string.Format(CultureInfo.InvariantCulture, "Mapped data member '{0}' is a null pointer.", members.Data);
                return null;
            }

            if (!TryValidatePointerLayoutSignals(value, type, members, pointer, out error))
            {
                return null;
            }

            long bufferLength;
            if (!string.IsNullOrWhiteSpace(members.BufferLength))
            {
                if (!TryReadInt64(value, type, members.BufferLength, "bufferLength", out bufferLength, out error))
                {
                    return null;
                }
            }
            else
            {
                bufferLength = descriptor.GetRequiredByteCount();
            }

            if (string.IsNullOrWhiteSpace(members.Stride)
                && bufferLength != descriptor.GetRequiredByteCount())
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    "Mapped buffer length {0} does not match the contiguous image size {1}; map an explicit stride or use an SDK adapter.",
                    bufferLength,
                    descriptor.GetRequiredByteCount());
                return null;
            }

            var diagnostics = RawBufferDiagnostics.AnalyzeLength(bufferLength, descriptor);
            if (RawBufferDiagnostics.HasErrors(diagnostics))
            {
                error = "Mapped descriptor is invalid for " + typeName + ": " + GetFirstError(diagnostics);
                return null;
            }

            var view = new ImagePtrView
            {
                Buffer = pointer,
                BufferLength = bufferLength,
                Descriptor = descriptor,
                SourceType = typeName,
                DisplayName = displayName
            };
            return new ImageCollectionItemTransfer(
                ImagePtrVisualizerTransfer.CreateMetadata(view),
                request => ImagePtrVisualizerTransfer.CreateChunk(view, request),
                request => ImagePtrVisualizerTransfer.CreatePreview(view, request));
        }

        private static bool TryValidatePointerLayoutSignals(
            object value,
            Type type,
            TypeMappingMembers members,
            IntPtr dataPointer,
            out string error)
        {
            error = string.Empty;
            if (string.Equals(GetLeafName(members.Data), "ImageData", StringComparison.OrdinalIgnoreCase))
            {
                var bufferMember = ImagePtrVisualizerTransfer.FindMember(type, "Buffer");
                if (bufferMember != null)
                {
                    try
                    {
                        var bufferValue = ImagePtrVisualizerTransfer.GetMemberValue(value, bufferMember);
                        if (bufferValue is IntPtr || bufferValue is UIntPtr)
                        {
                            var bufferPointer = ImagePtrVisualizerTransfer.ConvertValue<IntPtr>(bufferValue);
                            if (bufferPointer != dataPointer)
                            {
                                error = "Mapped ImageData is offset from Buffer; chunk-prefixed frames require an SDK adapter.";
                                return false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        error = "Mapped Buffer/ImageData relationship could not be validated: " + ex.Message;
                        return false;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(members.Stride))
            {
                return true;
            }

            var paddingNames = new[] { "PaddingX", "XPadding", "RowPadding", "LinePadding" };
            for (var i = 0; i < paddingNames.Length; i++)
            {
                var paddingMember = ImagePtrVisualizerTransfer.FindMember(type, paddingNames[i]);
                if (paddingMember == null)
                {
                    continue;
                }

                try
                {
                    var paddingValue = ImagePtrVisualizerTransfer.GetMemberValue(value, paddingMember);
                    if (paddingValue == null)
                    {
                        error = "Mapped row padding could not be validated: " + paddingNames[i] + " is null.";
                        return false;
                    }

                    var padding = ImagePtrVisualizerTransfer.ConvertValue<long>(paddingValue);
                    if (padding != 0)
                    {
                        error = string.Format(
                            CultureInfo.InvariantCulture,
                            "Mapped {0} is {1}, but no explicit stride was mapped.",
                            paddingNames[i],
                            padding);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    error = "Mapped row padding could not be validated: " + ex.Message;
                    return false;
                }
            }

            return true;
        }

        private static string GetLeafName(string? memberPath)
        {
            if (string.IsNullOrWhiteSpace(memberPath))
            {
                return string.Empty;
            }

            var dot = memberPath!.LastIndexOf('.');
            return dot >= 0 && dot + 1 < memberPath.Length
                ? memberPath.Substring(dot + 1)
                : memberPath;
        }

        private static ImageCollectionItemTransfer? CreateArrayTransfer(
            object dataValue,
            RawImageDescriptor descriptor,
            string typeName,
            string displayName,
            string dataMemberName,
            out string error)
        {
            error = string.Empty;
            byte[]? buffer = null;
            var bytes = dataValue as byte[];
            if (bytes != null)
            {
                buffer = bytes;
            }
            else
            {
                var ushorts = dataValue as ushort[];
                if (ushorts != null)
                {
                    buffer = RawBufferSnapshot.FromUInt16Array(ushorts, descriptor).Buffer;
                }
                else
                {
                    var floats = dataValue as float[];
                    if (floats != null)
                    {
                        buffer = RawBufferSnapshot.FromFloatArray(floats, descriptor).Buffer;
                    }
                }
            }

            if (buffer == null)
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    "Mapped data member '{0}' on {1} is {2}; expected IntPtr/UIntPtr or byte[]/ushort[]/float[].",
                    dataMemberName,
                    typeName,
                    dataValue.GetType().FullName);
                return null;
            }

            var diagnostics = RawBufferDiagnostics.AnalyzeLength(buffer.LongLength, descriptor);
            if (RawBufferDiagnostics.HasErrors(diagnostics))
            {
                error = "Mapped descriptor is invalid for " + typeName + ": " + GetFirstError(diagnostics);
                return null;
            }

            var metadata = VisualizerChunkedTransfer.CreateMetadata(descriptor, buffer.LongLength, typeName, displayName);
            return new ImageCollectionItemTransfer(
                metadata,
                request => VisualizerChunkedTransfer.CreateChunk(buffer, request),
                request => VisualizerChunkedTransfer.CreatePreview(buffer, descriptor, typeName, displayName, request));
        }

        private static bool TryReadPixelFormat(
            object value,
            Type type,
            string memberName,
            TypeMapping mapping,
            out RawPixelFormat pixelFormat,
            out string error)
        {
            var memberValue = ReadMember(value, type, memberName, "pixelFormat", out error);
            if (error.Length > 0)
            {
                pixelFormat = RawPixelFormat.Mono8;
                return false;
            }

            if (memberValue == null)
            {
                error = string.Format(CultureInfo.InvariantCulture, "Mapped pixelFormat member '{0}' is null.", memberName);
                pixelFormat = RawPixelFormat.Mono8;
                return false;
            }

            if (memberValue is RawPixelFormat rawPixelFormat)
            {
                pixelFormat = rawPixelFormat;
                return true;
            }

            var text = Convert.ToString(memberValue, CultureInfo.InvariantCulture) ?? string.Empty;
            var formatName = text;
            string? mappedName;
            if (mapping.PixelFormatMap != null
                && mapping.PixelFormatMap.TryGetValue(text, out mappedName)
                && !string.IsNullOrWhiteSpace(mappedName))
            {
                formatName = mappedName!;
            }

            if (!Enum.TryParse(formatName, true, out pixelFormat))
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    "Mapped pixelFormat member '{0}' value '{1}' is not a known RawPixelFormat.",
                    memberName,
                    text);
                return false;
            }

            return true;
        }

        private static bool TryReadInt32(object value, Type type, string? memberName, string role, out int result, out string error)
        {
            var memberValue = ReadMember(value, type, memberName, role, out error);
            if (error.Length > 0)
            {
                result = 0;
                return false;
            }

            return TryConvert(memberValue, memberName, role, out result, out error);
        }

        private static bool TryReadInt64(object value, Type type, string? memberName, string role, out long result, out string error)
        {
            var memberValue = ReadMember(value, type, memberName, role, out error);
            if (error.Length > 0)
            {
                result = 0;
                return false;
            }

            return TryConvert(memberValue, memberName, role, out result, out error);
        }

        private static bool TryConvert<T>(object? memberValue, string? memberName, string role, out T result, out string error)
        {
            if (memberValue == null)
            {
                error = string.Format(CultureInfo.InvariantCulture, "Mapped {0} member '{1}' is null.", role, memberName);
                result = default!;
                return false;
            }

            try
            {
                result = ImagePtrVisualizerTransfer.ConvertValue<T>(memberValue);
                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    "Mapped {0} member '{1}' is not numeric: {2}",
                    role,
                    memberName,
                    ex.Message);
                result = default!;
                return false;
            }
        }

        private static object? ReadMember(object value, Type type, string? memberName, string role, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(memberName))
            {
                error = "Mapping does not name a " + role + " member.";
                return null;
            }

            var path = memberName!.Split('.');
            if (path.Length > 2)
            {
                error = string.Format(
                    CultureInfo.InvariantCulture,
                    "Mapped {0} member path '{1}' exceeds the supported one-level nesting limit.",
                    role,
                    memberName);
                return null;
            }

            object? current = value;
            var currentType = type;
            for (var i = 0; i < path.Length; i++)
            {
                var segment = path[i].Trim();
                if (segment.Length == 0 || current == null)
                {
                    error = string.Format(
                        CultureInfo.InvariantCulture,
                        "Mapped {0} member path '{1}' reached a null or empty segment.",
                        role,
                        memberName);
                    return null;
                }

                var member = ImagePtrVisualizerTransfer.FindMember(currentType, segment);
                if (member == null)
                {
                    error = string.Format(
                        CultureInfo.InvariantCulture,
                        "Mapped {0} member '{1}' was not found on type {2}.",
                        role,
                        segment,
                        currentType.FullName);
                    return null;
                }

                try
                {
                    current = ImagePtrVisualizerTransfer.GetMemberValue(current, member);
                    if (current != null)
                    {
                        currentType = current.GetType();
                    }
                }
                catch (Exception ex)
                {
                    error = string.Format(
                        CultureInfo.InvariantCulture,
                        "Mapped {0} member '{1}' could not be read: {2}",
                        role,
                        memberName,
                        ex.Message);
                    return null;
                }
            }

            return current;
        }

        private static string GetFirstError(System.Collections.Generic.IReadOnlyList<RawDiagnostic> diagnostics)
        {
            for (var i = 0; i < diagnostics.Count; i++)
            {
                if (diagnostics[i].Severity == RawDiagnosticSeverity.Error)
                {
                    return diagnostics[i].Message;
                }
            }

            return "unknown validation error.";
        }
    }
}
