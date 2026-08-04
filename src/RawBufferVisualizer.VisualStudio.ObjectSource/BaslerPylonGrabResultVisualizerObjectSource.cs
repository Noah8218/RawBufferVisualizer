using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.DebuggerVisualizers;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.Sdk;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    public sealed class BaslerPylonGrabResultVisualizerObjectSource : VisualizerObjectSource
    {
        private object? _cachedTarget;
        private BaslerPylonGrabResultView? _cachedView;

        public override void GetData(object target, Stream outgoingData)
        {
            SerializeAsJson(outgoingData, BaslerPylonGrabResultVisualizerTransfer.CreateMetadata(GetView(target)));
        }

        public override void TransferData(object target, Stream incomingData, Stream outgoingData)
        {
            var request = DeserializeFromJson<VisualizerSnapshotChunkRequest>(incomingData);
            if (request == null)
            {
                throw new InvalidDataException("Chunk request is required.");
            }

            if (request.Operation == VisualizerSnapshotOperation.Preview)
            {
                SerializeAsJson(
                    outgoingData,
                    BaslerPylonGrabResultVisualizerTransfer.CreatePreview(GetView(target), request));
                return;
            }

            SerializeAsJson(
                outgoingData,
                BaslerPylonGrabResultVisualizerTransfer.CreateChunk(GetView(target), request));
        }

        private BaslerPylonGrabResultView GetView(object target)
        {
            if (!ReferenceEquals(target, _cachedTarget))
            {
                _cachedTarget = target;
                _cachedView = BaslerPylonGrabResultVisualizerTransfer.CreateView(target);
            }

            return _cachedView ?? throw new InvalidOperationException("Basler pylon grab-result view was not created.");
        }
    }

    public sealed class BaslerPylonGrabResultView
    {
        public RawBufferView BufferView { get; set; } = new RawBufferView();
        public string SourceType { get; set; } = string.Empty;
    }

    public static class BaslerPylonGrabResultVisualizerTransfer
    {
        private const string GrabResultInterfaceName = "Basler.Pylon.IGrabResult";
        private const string ImageInterfaceName = "Basler.Pylon.IImage";
        private const string ImageExtensionsTypeName = "Basler.Pylon.IImageExtensions";
        private const string DisplayName = "Basler IGrabResult";

        public static BaslerPylonGrabResultView CreateView(object target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var runtimeType = target.GetType();
            var grabResultContract = FindInterface(runtimeType, GrabResultInterfaceName);
            if (grabResultContract == null)
            {
                throw new NotSupportedException(
                    "Only objects implementing exact Basler.Pylon.IGrabResult are supported by this adapter.");
            }

            if (!Read<bool>(target, grabResultContract, "GrabSucceeded"))
            {
                var description = ReadOptionalText(target, grabResultContract, "ErrorDescription");
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(description)
                        ? "Basler pylon grab did not succeed."
                        : "Basler pylon grab did not succeed: " + description);
            }

            if (!Read<bool>(target, grabResultContract, "IsValid"))
            {
                throw new InvalidOperationException("Basler pylon grab result is not a valid image.");
            }

            RequireEnumValue(target, grabResultContract, "PayloadTypeValue", "Image", "Only 2D image payloads are supported");
            RequireEnumValue(target, grabResultContract, "Orientation", "TopDown", "Only top-down Basler images are supported");

            var pointer = Read<IntPtr>(target, grabResultContract, "PixelDataPointer");
            if (pointer == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "Basler pylon pixel pointer is empty. Stop before the IGrabResult is disposed or its callback returns.");
            }

            var width = ReadPositiveInt(target, grabResultContract, "Width");
            var height = ReadPositiveInt(target, grabResultContract, "Height");
            var pixelType = ReadRequiredValue(target, grabResultContract, "PixelTypeValue");
            RawPixelFormat pixelFormat;
            int validBits;
            int channels;
            if (!TryMapPixelType(Convert.ToString(pixelType, CultureInfo.InvariantCulture), out pixelFormat, out validBits, out channels))
            {
                throw new NotSupportedException(
                    "Unsupported Basler pylon 2D pixel type: "
                    + (Convert.ToString(pixelType, CultureInfo.InvariantCulture) ?? "<null>")
                    + ". Convert it in the debugged application or expose a supported RawBufferView.");
            }

            var stride = ComputeStride(target, runtimeType);
            if (!stride.HasValue || stride.Value <= 0)
            {
                throw new NotSupportedException(
                    "Basler pylon ComputeStride() could not produce a byte-aligned 2D stride for this image.");
            }

            var imageLength = checked((long)stride.Value * height);
            var payloadSize = Read<long>(target, grabResultContract, "PayloadSize");
            var paddingY = Read<int>(target, grabResultContract, "PaddingY");
            if (payloadSize < imageLength || paddingY < 0 || payloadSize < checked(imageLength + paddingY))
            {
                throw new InvalidOperationException(
                    "Basler pylon payload is smaller than the computed 2D image and trailing padding require.");
            }

            return new BaslerPylonGrabResultView
            {
                BufferView = new RawBufferView
                {
                    Buffer = pointer,
                    BufferLength = imageLength,
                    Width = width,
                    Height = height,
                    Stride = stride.Value,
                    PixelFormat = pixelFormat,
                    Channels = channels,
                    BitDepth = validBits,
                    ByteOrder = RawByteOrder.LittleEndian,
                    Name = DisplayName
                },
                SourceType = runtimeType.FullName ?? GrabResultInterfaceName
            };
        }

        public static VisualizerSnapshotMetadata CreateMetadata(BaslerPylonGrabResultView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            var bufferView = view.BufferView;
            return VisualizerChunkedTransfer.CreatePointerMetadata(
                bufferView.ToDescriptor(),
                bufferView.GetBufferLength(),
                bufferView.Buffer,
                view.SourceType,
                DisplayName);
        }

        public static VisualizerSnapshotChunk CreateChunk(
            BaslerPylonGrabResultView view,
            VisualizerSnapshotChunkRequest request)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            return RawBufferViewVisualizerTransfer.CreateChunk(view.BufferView, request);
        }

        public static VisualizerSnapshotTransfer CreatePreview(
            BaslerPylonGrabResultView view,
            VisualizerSnapshotChunkRequest request)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var bufferView = view.BufferView;
            return VisualizerSampledPreview.Create(
                bufferView.Buffer,
                bufferView.GetBufferLength(),
                bufferView.ToDescriptor(),
                view.SourceType,
                DisplayName,
                request.MaximumWidth,
                request.MaximumHeight);
        }

        public static bool TryMapPixelType(
            string? value,
            out RawPixelFormat pixelFormat,
            out int validBits,
            out int channels)
        {
            var normalized = Normalize(value);
            switch (normalized)
            {
                case "MONO8":
                    return SetFormat(RawPixelFormat.Mono8, 8, 1, out pixelFormat, out validBits, out channels);
                case "MONO10":
                    return SetFormat(RawPixelFormat.Mono16, 10, 1, out pixelFormat, out validBits, out channels);
                case "MONO12":
                    return SetFormat(RawPixelFormat.Mono16, 12, 1, out pixelFormat, out validBits, out channels);
                case "MONO16":
                    return SetFormat(RawPixelFormat.Mono16, 16, 1, out pixelFormat, out validBits, out channels);
                case "MONO10P":
                    return SetFormat(RawPixelFormat.Mono10PackedLsb, 10, 1, out pixelFormat, out validBits, out channels);
                case "MONO12P":
                    return SetFormat(RawPixelFormat.Mono12PackedLsb, 12, 1, out pixelFormat, out validBits, out channels);
                case "BAYERRG8":
                    return SetFormat(RawPixelFormat.BayerRGGB8, 8, 1, out pixelFormat, out validBits, out channels);
                case "BAYERGR8":
                    return SetFormat(RawPixelFormat.BayerGRBG8, 8, 1, out pixelFormat, out validBits, out channels);
                case "BAYERGB8":
                    return SetFormat(RawPixelFormat.BayerGBRG8, 8, 1, out pixelFormat, out validBits, out channels);
                case "BAYERBG8":
                    return SetFormat(RawPixelFormat.BayerBGGR8, 8, 1, out pixelFormat, out validBits, out channels);
                case "RGB8PACKED":
                    return SetFormat(RawPixelFormat.RGB24, 8, 3, out pixelFormat, out validBits, out channels);
                case "BGR8PACKED":
                    return SetFormat(RawPixelFormat.BGR24, 8, 3, out pixelFormat, out validBits, out channels);
                case "BGRA8PACKED":
                    return SetFormat(RawPixelFormat.BGRA32, 8, 4, out pixelFormat, out validBits, out channels);
                default:
                    pixelFormat = RawPixelFormat.Mono8;
                    validBits = 0;
                    channels = 0;
                    return false;
            }
        }

        private static int? ComputeStride(object target, Type runtimeType)
        {
            var imageInterface = FindInterface(runtimeType, ImageInterfaceName);
            if (imageInterface == null)
            {
                throw new MissingMemberException(runtimeType.FullName, ImageInterfaceName);
            }

            var extensionsType = imageInterface.Assembly.GetType(ImageExtensionsTypeName, false);
            if (extensionsType == null)
            {
                throw new MissingMemberException(imageInterface.Assembly.FullName, ImageExtensionsTypeName);
            }

            MethodInfo? computeStride = null;
            var methods = extensionsType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (var i = 0; i < methods.Length; i++)
            {
                var candidate = methods[i];
                var parameters = candidate.GetParameters();
                if (candidate.Name == "ComputeStride"
                    && parameters.Length == 1
                    && parameters[0].ParameterType.FullName == ImageInterfaceName)
                {
                    computeStride = candidate;
                    break;
                }
            }

            if (computeStride == null)
            {
                throw new MissingMethodException(extensionsType.FullName, "ComputeStride(Basler.Pylon.IImage)");
            }

            var result = computeStride.Invoke(null, new[] { target });
            return result == null ? (int?)null : Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        private static void RequireEnumValue(
            object target,
            Type contract,
            string propertyName,
            string expected,
            string errorPrefix)
        {
            var actual = Convert.ToString(ReadRequiredValue(target, contract, propertyName), CultureInfo.InvariantCulture);
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new NotSupportedException(errorPrefix + ": " + (actual ?? "<null>") + ".");
            }
        }

        private static int ReadPositiveInt(object target, Type contract, string propertyName)
        {
            var value = Read<long>(target, contract, propertyName);
            if (value <= 0 || value > int.MaxValue)
            {
                throw new InvalidOperationException(
                    "Basler pylon " + propertyName + " must be between 1 and " + int.MaxValue.ToString(CultureInfo.InvariantCulture) + ".");
            }

            return checked((int)value);
        }

        private static T Read<T>(object target, Type contract, string propertyName)
        {
            return ImagePtrVisualizerTransfer.ConvertValue<T>(ReadRequiredValue(target, contract, propertyName));
        }

        private static object ReadRequiredValue(object target, Type contract, string propertyName)
        {
            var property = FindProperty(contract, propertyName);
            if (property == null)
            {
                throw new MissingMemberException(contract.FullName, propertyName);
            }

            return property.GetValue(target) ?? throw new InvalidOperationException(
                "Basler pylon " + propertyName + " returned null.");
        }

        private static string? ReadOptionalText(object target, Type contract, string propertyName)
        {
            var property = FindProperty(contract, propertyName);
            return property == null
                ? null
                : Convert.ToString(property.GetValue(target), CultureInfo.InvariantCulture);
        }

        private static PropertyInfo? FindProperty(Type contract, string propertyName)
        {
            var property = contract.GetProperty(propertyName);
            if (property != null)
            {
                return property;
            }

            var inherited = contract.GetInterfaces();
            for (var i = 0; i < inherited.Length; i++)
            {
                property = FindProperty(inherited[i], propertyName);
                if (property != null)
                {
                    return property;
                }
            }

            return null;
        }

        private static Type? FindInterface(Type runtimeType, string fullName)
        {
            if (runtimeType.IsInterface && runtimeType.FullName == fullName)
            {
                return runtimeType;
            }

            var interfaces = runtimeType.GetInterfaces();
            for (var i = 0; i < interfaces.Length; i++)
            {
                if (interfaces[i].FullName == fullName)
                {
                    return interfaces[i];
                }
            }

            return null;
        }

        private static bool SetFormat(
            RawPixelFormat format,
            int bits,
            int channelCount,
            out RawPixelFormat pixelFormat,
            out int validBits,
            out int channels)
        {
            pixelFormat = format;
            validBits = bits;
            channels = channelCount;
            return true;
        }

        private static string Normalize(string? value)
        {
            var text = value ?? string.Empty;
            var chars = new char[text.Length];
            var count = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (char.IsLetterOrDigit(text[i]))
                {
                    chars[count++] = char.ToUpperInvariant(text[i]);
                }
            }

            return new string(chars, 0, count);
        }
    }
}
