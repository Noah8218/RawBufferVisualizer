using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.Sdk;
using RawBufferVisualizer.VisualStudio.ObjectSource;

namespace RawBufferVisualizer.Tests
{
    internal static class IndustrialCameraContractTests
    {
        public static void RunAll()
        {
            ExplicitStridePointerShapeAutoOpens();
            PaddingAndPayloadShapeRequiresContiguousLayout();
            ImageDataOffsetShapeRequiresExplicitLayout();
            SizedBufferShapeRequiresContiguousSize();
            GenICamPfncAliasesResolveConservatively();
            MethodOnlyBufferContractsStayExplicit();
            MappedSupportedCarriersPreserveLayoutAndBytes();
            MappedPointerTransfersRejectUnsafeLayouts();
            MappedCoreContractsFailClosed();
            RegisteredRawBufferViewContractsMatchMappedValidation();
            ValidBitsContractsAreStrictAndPreserved();
        }

        private static void ExplicitStridePointerShapeAutoOpens()
        {
            var inference = VisionMemberInference.Infer(
                new List<VisualizerMemberInventoryItem>
                {
                    Item("DataPtr", "IntPtr", "0x1000"),
                    Item("Width", "UInt64", "640"),
                    Item("Height", "UInt64", "480"),
                    Item("Stride", "UInt64", "640"),
                    Item("PixelFormat", "PixelFormatEnums", "PixelFormat_Mono8")
                },
                "Example.ExplicitStrideFrame");

            Assert(inference.CanAutoOpen, "A pointer shape should auto-open when DataPtr and Stride are explicit.");
            Assert(inference.Members.Data == "DataPtr", "Inference selected the wrong data pointer.");
            Assert(inference.Members.Stride == "Stride", "Inference did not retain the explicit stride.");
            Assert(inference.PixelFormat == RawPixelFormat.Mono8, "PixelFormat_Mono8 did not resolve.");
        }

        private static void PaddingAndPayloadShapeRequiresContiguousLayout()
        {
            var contiguous = CreatePaddingAwareInventory(0, 640L * 480L);
            var safe = VisionMemberInference.Infer(contiguous, "Example.PaddingAwareFrame");
            Assert(safe.CanAutoOpen, "A zero-padding frame with an exact payload should auto-open.");
            Assert(safe.Members.Data == "PixelDataPointer", "Inference must prefer PixelDataPointer.");
            Assert(safe.Members.BufferLength == "PayloadSize", "Inference did not retain PayloadSize.");

            var padded = VisionMemberInference.Infer(
                CreatePaddingAwareInventory(16, (640L + 16L) * 480L),
                "Example.PaddingAwareFrame");
            Assert(padded.RequiresExplicitLayout, "Nonzero PaddingX must require an explicit layout.");
            Assert(!padded.CanAutoOpen, "A padded frame must not auto-open without a stride.");

            var extraPayload = VisionMemberInference.Infer(
                CreatePaddingAwareInventory(0, (640L * 480L) + 128L),
                "Example.PaddingAwareFrame");
            Assert(extraPayload.RequiresExplicitLayout, "A payload larger than the contiguous image must remain explicit.");
            Assert(!extraPayload.CanAutoOpen, "An extra payload must not be interpreted as contiguous pixels.");
        }

        private static void ImageDataOffsetShapeRequiresExplicitLayout()
        {
            var safe = VisionMemberInference.Infer(
                CreateImageDataInventory("0x2000", "0x2000", 640L * 480L),
                "Example.ImageDataFrame");
            Assert(safe.Members.Data == "ImageData", "Inference must prefer ImageData over the whole Buffer.");
            Assert(safe.CanAutoOpen, "A frame with equal base/image pointers and exact size should auto-open.");

            var chunked = VisionMemberInference.Infer(
                CreateImageDataInventory("0x2000", "0x2100", (640L * 480L) + 256L),
                "Example.ImageDataFrame");
            Assert(chunked.Members.Data == "ImageData", "Chunked inference must still select ImageData.");
            Assert(chunked.RequiresExplicitLayout, "An ImageData offset from Buffer must require an explicit layout.");
            Assert(!chunked.CanAutoOpen, "A chunk-prefixed frame must never auto-open as a flat image.");
        }

        private static void SizedBufferShapeRequiresContiguousSize()
        {
            var safe = VisionMemberInference.Infer(
                CreateSizedBufferInventory(640L * 480L),
                "Example.SizedBufferImage");
            Assert(safe.CanAutoOpen, "An image with exact SizeInBytes should auto-open.");
            Assert(safe.Members.BufferLength == "SizeInBytes", "Inference did not retain SizeInBytes.");

            var padded = VisionMemberInference.Infer(
                CreateSizedBufferInventory((640L * 480L) + 480L),
                "Example.SizedBufferImage");
            Assert(padded.RequiresExplicitLayout, "A padded image must require explicit layout.");
            Assert(!padded.CanAutoOpen, "A padded image must not default to minimum stride.");
        }

        private static void GenICamPfncAliasesResolveConservatively()
        {
            AssertFormat("Mono10p", RawPixelFormat.Mono10PackedLsb);
            AssertFormat("PixelFormat_Mono12p", RawPixelFormat.Mono12PackedLsb);
            AssertFormat("BayerRG8", RawPixelFormat.BayerRGGB8);
            AssertFormat("BayerGR8", RawPixelFormat.BayerGRBG8);
            AssertFormat("BayerGB8", RawPixelFormat.BayerGBRG8);
            AssertFormat("BayerBG8", RawPixelFormat.BayerBGGR8);
            AssertFormat("RGB8Packed", RawPixelFormat.RGB24);
            AssertFormat("BGR8Packed", RawPixelFormat.BGR24);

            AssertUnsupported("Mono10Packed");
            AssertUnsupported("Mono12Packed");
            AssertUnsupported("YUV422_8");
            AssertUnsupported("RGB8_Planar");
            AssertUnsupported("Coord3D_ABC32f");
            AssertUnsupported("RGBA8");
        }

        private static void MethodOnlyBufferContractsStayExplicit()
        {
            var inference = VisionMemberInference.Infer(
                new List<VisualizerMemberInventoryItem>(),
                "Example.MethodOnlyScopedBuffer");
            Assert(!inference.CanAutoOpen, "A method-only frame-grabber buffer must not auto-open.");
            Assert(inference.MissingRoles.Contains("data"), "A method-only buffer must report missing property/field data.");
        }

        private static void MappedPointerTransfersRejectUnsafeLayouts()
        {
            AssertMappedFailure(
                new MappedPaddedFrame
                {
                    Data = new IntPtr(0x1000),
                    Width = 640,
                    Height = 480,
                    PaddingX = 16,
                    PayloadSize = (640L + 16L) * 480L
                },
                new TypeMappingMembers
                {
                    Data = "Data",
                    Width = "Width",
                    Height = "Height",
                    BufferLength = "PayloadSize"
                },
                "PaddingX");

            AssertMappedFailure(
                new MappedOffsetFrame
                {
                    Buffer = new IntPtr(0x2000),
                    BufferSize = (640L * 480L) + 256L,
                    ImageData = new IntPtr(0x2100),
                    Width = 640,
                    Height = 480
                },
                new TypeMappingMembers
                {
                    Data = "ImageData",
                    Width = "Width",
                    Height = "Height",
                    BufferLength = "BufferSize"
                },
                "offset from Buffer");

            AssertMappedFailure(
                new MappedExtraPayloadFrame
                {
                    Data = new IntPtr(0x3000),
                    BufferSize = (640L * 480L) + 64L,
                    Width = 640,
                    Height = 480
                },
                new TypeMappingMembers
                {
                    Data = "Data",
                    Width = "Width",
                    Height = "Height",
                    BufferLength = "BufferSize"
                },
                "does not match the contiguous image size");
        }

        private static void MappedSupportedCarriersPreserveLayoutAndBytes()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "RawBufferVisualizerIndustrialContractTests",
                Guid.NewGuid().ToString("N"));
            var pointerBytes = new byte[] { 1, 2, 3, 4, 90, 91, 5, 6, 7, 8, 92, 93 };
            var pointerHandle = GCHandle.Alloc(pointerBytes, GCHandleType.Pinned);
            Directory.CreateDirectory(directory);
            try
            {
                var mappingPath = Path.Combine(directory, "supported-carriers.json");
                var file = new TypeMappingFile();
                AddMapping(
                    file,
                    typeof(MappedPointerFrame),
                    CompleteMembers(),
                    RawByteOrder.LittleEndian);
                AddMapping(
                    file,
                    typeof(MappedContiguousPointerFrame),
                    ContiguousPointerMembers(),
                    RawByteOrder.LittleEndian);
                AddMapping(
                    file,
                    typeof(MappedArrayFrame<byte>),
                    ArrayMembers(),
                    RawByteOrder.LittleEndian);
                AddMapping(
                    file,
                    typeof(MappedArrayFrame<ushort>),
                    ArrayMembers(),
                    RawByteOrder.BigEndian);
                AddMapping(
                    file,
                    typeof(MappedArrayFrame<float>),
                    ArrayMembers(),
                    RawByteOrder.BigEndian);

                var writer = new TypeMappingStore(null, mappingPath);
                writer.Save(file);
                var store = new TypeMappingStore(null, mappingPath);

                AssertMappedSuccess(
                    new MappedPointerFrame
                    {
                        Data = pointerHandle.AddrOfPinnedObject(),
                        Width = 4,
                        Height = 2,
                        Stride = 6,
                        BufferLength = pointerBytes.Length,
                        PixelFormat = RawPixelFormat.Mono8,
                        ValidBits = 8
                    },
                    store,
                    RawPixelFormat.Mono8,
                    6,
                    8,
                    RawByteOrder.LittleEndian,
                    pointerBytes,
                    true);

                AssertMappedSuccess(
                    new MappedArrayFrame<byte>
                    {
                        Data = new byte[] { 11, 12, 13, 14 },
                        Width = 2,
                        Height = 2,
                        Stride = 2,
                        PixelFormat = RawPixelFormat.Mono8,
                        ValidBits = 8
                    },
                    store,
                    RawPixelFormat.Mono8,
                    2,
                    8,
                    RawByteOrder.LittleEndian,
                    new byte[] { 11, 12, 13, 14 },
                    false);

                AssertMappedSuccess(
                    new MappedContiguousPointerFrame
                    {
                        Data = pointerHandle.AddrOfPinnedObject(),
                        Width = 4,
                        Height = 1,
                        BufferLength = 4,
                        PixelFormat = RawPixelFormat.Mono8,
                        ValidBits = 8
                    },
                    store,
                    RawPixelFormat.Mono8,
                    4,
                    8,
                    RawByteOrder.LittleEndian,
                    new byte[] { 1, 2, 3, 4 },
                    true);

                foreach (var validBits in new[] { 10, 12, 14, 16 })
                {
                    AssertMappedSuccess(
                        new MappedArrayFrame<ushort>
                        {
                            Data = new ushort[] { 0x1234, 0xABCD },
                            Width = 2,
                            Height = 1,
                            Stride = 4,
                            PixelFormat = RawPixelFormat.Mono16,
                            ValidBits = validBits
                        },
                        store,
                        RawPixelFormat.Mono16,
                        4,
                        validBits,
                        RawByteOrder.BigEndian,
                        new byte[] { 0x12, 0x34, 0xAB, 0xCD },
                        false);
                }

                AssertMappedSuccess(
                    new MappedArrayFrame<float>
                    {
                        Data = new float[] { 1.0f, -2.5f },
                        Width = 2,
                        Height = 1,
                        Stride = 8,
                        PixelFormat = RawPixelFormat.Float32,
                        ValidBits = 32
                    },
                    store,
                    RawPixelFormat.Float32,
                    8,
                    32,
                    RawByteOrder.BigEndian,
                    new byte[] { 0x3F, 0x80, 0x00, 0x00, 0xC0, 0x20, 0x00, 0x00 },
                    false);
            }
            finally
            {
                pointerHandle.Free();
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void MappedCoreContractsFailClosed()
        {
            var complete = CompleteMembers();
            AssertMappedFailure(
                new MappedPointerFrame
                {
                    Data = IntPtr.Zero,
                    Width = 1,
                    Height = 1,
                    Stride = 1,
                    BufferLength = 1,
                    PixelFormat = RawPixelFormat.Mono8,
                    ValidBits = 8
                },
                complete,
                "null pointer");

            AssertMappedFailure(
                new MappedPointerFrame
                {
                    Data = new IntPtr(0x1000),
                    Width = 0,
                    Height = 1,
                    Stride = 1,
                    BufferLength = 1,
                    PixelFormat = RawPixelFormat.Mono8,
                    ValidBits = 8
                },
                complete,
                "Width must be greater than zero");

            AssertMappedFailure(
                new MappedPointerFrame
                {
                    Data = new IntPtr(0x1000),
                    Width = 1,
                    Height = 0,
                    Stride = 1,
                    BufferLength = 1,
                    PixelFormat = RawPixelFormat.Mono8,
                    ValidBits = 8
                },
                complete,
                "Height must be greater than zero");

            AssertMappedFailure(
                new MappedPointerFrame
                {
                    Data = new IntPtr(0x1000),
                    Width = -1,
                    Height = 1,
                    Stride = 1,
                    BufferLength = 1,
                    PixelFormat = RawPixelFormat.Mono8,
                    ValidBits = 8
                },
                complete,
                "Width must be greater than zero");

            AssertMappedFailure(
                new MappedPointerFrame
                {
                    Data = new IntPtr(0x1000),
                    Width = 2,
                    Height = 1,
                    Stride = 5,
                    BufferLength = 6,
                    PixelFormat = RawPixelFormat.BGR24,
                    ValidBits = 8
                },
                complete,
                "Stride is smaller than the pixel format requires");

            AssertMappedFailure(
                new MappedPointerFrame
                {
                    Data = new IntPtr(0x1000),
                    Width = 4,
                    Height = 2,
                    Stride = 6,
                    BufferLength = 9,
                    PixelFormat = RawPixelFormat.Mono8,
                    ValidBits = 8
                },
                complete,
                "Buffer is smaller than descriptor requires");

            AssertMappedFailure(
                new MappedPointerFrame
                {
                    Data = new IntPtr(0x1000),
                    Width = 1,
                    Height = 1,
                    Stride = 2,
                    BufferLength = 2,
                    PixelFormat = RawPixelFormat.Mono16,
                    ValidBits = 17
                },
                complete,
                "Mono16 valid bits must be between 1 and 16");

            AssertMappedFailure(
                new MappedPointerFrame
                {
                    Data = new IntPtr(0x1000),
                    Width = 4,
                    Height = 1,
                    Stride = 5,
                    BufferLength = 5,
                    PixelFormat = RawPixelFormat.Mono10PackedLsb,
                    ValidBits = 12
                },
                complete,
                "Mono10PackedLsb requires 10 valid bits per pixel");

            AssertMappedFailure(
                new MappedPointerFrame
                {
                    Data = new IntPtr(0x1000),
                    Width = 1,
                    Height = 1,
                    Stride = 1,
                    BufferLength = 1,
                    PixelFormat = RawPixelFormat.Mono8,
                    ValidBits = 8
                },
                complete,
                "byte order is not supported",
                "MiddleEndian");

            AssertMappedFailure(
                new MappedArrayFrame<int>
                {
                    Data = new[] { 1 },
                    Width = 1,
                    Height = 1,
                    Stride = 4,
                    PixelFormat = RawPixelFormat.Float32,
                    ValidBits = 32
                },
                ArrayMembers(),
                "expected IntPtr/UIntPtr or byte[]/ushort[]/float[]");
        }

        private static void RegisteredRawBufferViewContractsMatchMappedValidation()
        {
            AssertRawBufferViewFailure(
                RawBufferViewOf(0, 1, 1, 1, RawPixelFormat.Mono8, 8),
                "Width must be greater than zero");
            AssertRawBufferViewFailure(
                RawBufferViewOf(1, -1, 1, 1, RawPixelFormat.Mono8, 8),
                "Height must be greater than zero");
            AssertRawBufferViewFailure(
                RawBufferViewOf(2, 1, 5, 6, RawPixelFormat.BGR24, 8, 3),
                "Stride is smaller than the pixel format requires");
            AssertRawBufferViewFailure(
                RawBufferViewOf(4, 2, 6, 9, RawPixelFormat.Mono8, 8),
                "Buffer is smaller than descriptor requires");
        }

        private static void ValidBitsContractsAreStrictAndPreserved()
        {
            foreach (var validBits in new[] { 10, 12, 14, 16 })
            {
                var metadata = RawBufferViewVisualizerTransfer.CreateMetadata(
                    RawBufferViewOf(1, 1, 2, 2, RawPixelFormat.Mono16, validBits));
                Assert(metadata.Descriptor.ValidBits == validBits, "Valid Mono16 bit depth changed.");
            }

            var mono10 = RawBufferViewVisualizerTransfer.CreateMetadata(
                RawBufferViewOf(4, 1, 5, 5, RawPixelFormat.Mono10PackedLsb, 10));
            Assert(mono10.Descriptor.ValidBits == 10, "Valid Mono10PackedLsb bit depth changed.");

            var mono12 = RawBufferViewVisualizerTransfer.CreateMetadata(
                RawBufferViewOf(2, 1, 3, 3, RawPixelFormat.Mono12PackedLsb, 12));
            Assert(mono12.Descriptor.ValidBits == 12, "Valid Mono12PackedLsb bit depth changed.");

            AssertRawBufferViewFailure(
                RawBufferViewOf(1, 1, 2, 2, RawPixelFormat.Mono16, 17),
                "Mono16 valid bits must be between 1 and 16");
            AssertRawBufferViewFailure(
                RawBufferViewOf(4, 1, 5, 5, RawPixelFormat.Mono10PackedLsb, 12),
                "Mono10PackedLsb requires 10 valid bits per pixel");
            AssertRawBufferViewFailure(
                RawBufferViewOf(2, 1, 3, 3, RawPixelFormat.Mono12PackedLsb, 10),
                "Mono12PackedLsb requires 12 valid bits per pixel");
        }

        private static RawBufferView RawBufferViewOf(
            int width,
            int height,
            int stride,
            long bufferLength,
            RawPixelFormat pixelFormat,
            int validBits,
            int channels = 1)
        {
            return new RawBufferView
            {
                Buffer = new IntPtr(0x1000),
                BufferLength = bufferLength,
                Width = width,
                Height = height,
                Stride = stride,
                PixelFormat = pixelFormat,
                Channels = channels,
                BitDepth = validBits
            };
        }

        private static void AssertRawBufferViewFailure(RawBufferView view, string expectedError)
        {
            try
            {
                RawBufferViewVisualizerTransfer.CreateMetadata(view);
                throw new InvalidOperationException("Unsafe RawBufferView unexpectedly produced metadata.");
            }
            catch (ArgumentException ex)
            {
                Assert(
                    ex.Message.IndexOf(expectedError, StringComparison.OrdinalIgnoreCase) >= 0,
                    "Unsafe RawBufferView error should mention '" + expectedError + "' but was: " + ex.Message);
            }
        }

        private static List<VisualizerMemberInventoryItem> CreatePaddingAwareInventory(long paddingX, long payloadSize)
        {
            return new List<VisualizerMemberInventoryItem>
            {
                Item("PixelDataPointer", "IntPtr", "0x1000"),
                Item("Width", "Int64", "640"),
                Item("Height", "Int64", "480"),
                Item("PaddingX", "Int64", paddingX.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                Item("PayloadSize", "Int64", payloadSize.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                Item("PixelTypeValue", "PixelType", "Mono8")
            };
        }

        private static List<VisualizerMemberInventoryItem> CreateImageDataInventory(
            string bufferAddress,
            string imageDataAddress,
            long bufferSize)
        {
            return new List<VisualizerMemberInventoryItem>
            {
                Item("Buffer", "IntPtr", bufferAddress),
                Item("BufferSize", "UInt32", bufferSize.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                Item("ImageData", "IntPtr", imageDataAddress),
                Item("Width", "UInt32", "640"),
                Item("Height", "UInt32", "480"),
                Item("PixelFormat", "PixelFormat", "Mono8")
            };
        }

        private static List<VisualizerMemberInventoryItem> CreateSizedBufferInventory(long sizeInBytes)
        {
            return new List<VisualizerMemberInventoryItem>
            {
                Item("Data", "IntPtr", "0x3000"),
                Item("Width", "UInt32", "640"),
                Item("Height", "UInt32", "480"),
                Item("PixelFormat", "PixelFormat", "Mono8"),
                Item("SizeInBytes", "Int64", sizeInBytes.ToString(System.Globalization.CultureInfo.InvariantCulture))
            };
        }

        private static void AssertFormat(string value, RawPixelFormat expected)
        {
            RawPixelFormat actual;
            Assert(
                VisionMemberInference.TryResolvePixelFormat(value, out actual) && actual == expected,
                value + " should resolve to " + expected + ".");
        }

        private static void AssertUnsupported(string value)
        {
            RawPixelFormat ignored;
            Assert(
                !VisionMemberInference.TryResolvePixelFormat(value, out ignored),
                value + " must stay explicit because its memory layout is unsupported or ambiguous.");
        }

        private static void AssertMappedFailure(
            object value,
            TypeMappingMembers members,
            string expectedError,
            string byteOrder = "LittleEndian")
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "RawBufferVisualizerIndustrialContractTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var mappingPath = Path.Combine(directory, "type-mappings.json");
                var store = new TypeMappingStore(null, mappingPath);
                var file = new TypeMappingFile();
                file.Mappings.Add(new TypeMapping
                {
                    TypeName = value.GetType().FullName ?? value.GetType().Name,
                    AssemblyName = value.GetType().Assembly.GetName().Name ?? string.Empty,
                    Members = members,
                    ByteOrder = byteOrder
                });
                store.Save(file);

                var view = ImageCollectionVisualizerTransfer.CreateView(new[] { value }, store);
                var metadata = view.GetMetadata(0);
                Assert(metadata.Metadata == null, "An unsafe mapped pointer layout must not produce image metadata.");
                Assert(
                    metadata.Error.IndexOf(expectedError, StringComparison.OrdinalIgnoreCase) >= 0,
                    "Unsafe mapped pointer error should mention '" + expectedError + "' but was: " + metadata.Error);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static void AssertMappedSuccess(
            object value,
            TypeMappingStore store,
            RawPixelFormat expectedFormat,
            int expectedStride,
            int expectedValidBits,
            RawByteOrder expectedByteOrder,
            byte[] expectedBytes,
            bool expectedDirectMemory)
        {
            var view = ImageCollectionVisualizerTransfer.CreateView(new[] { value }, store);
            var item = view.GetMetadata(0);
            Assert(string.IsNullOrEmpty(item.Error), "Supported mapped carrier failed: " + item.Error);
            Assert(item.Metadata != null, "Supported mapped carrier did not produce metadata.");
            var metadata = item.Metadata!;
            Assert(metadata.Descriptor.PixelFormat == expectedFormat, "Mapped carrier pixel format changed.");
            Assert(metadata.Descriptor.Stride == expectedStride, "Mapped carrier stride changed.");
            Assert(metadata.Descriptor.ValidBits == expectedValidBits, "Mapped carrier valid bits changed.");
            Assert(metadata.Descriptor.ByteOrder == expectedByteOrder, "Mapped carrier byte order changed.");
            Assert(metadata.BufferLength == expectedBytes.LongLength, "Mapped carrier buffer length changed.");
            Assert(metadata.SupportsDirectMemory == expectedDirectMemory, "Mapped carrier direct-memory ownership changed.");
            if (expectedDirectMemory)
            {
                Assert(metadata.ProcessId == Process.GetCurrentProcess().Id, "Mapped pointer process ownership is missing.");
                Assert(metadata.BufferAddress != 0, "Mapped pointer address is missing.");
            }

            var chunk = view.GetChunk(
                0,
                new VisualizerSnapshotChunkRequest { Offset = 0, Count = expectedBytes.Length });
            AssertBytesEqual(expectedBytes, chunk.Buffer, "Mapped carrier bytes changed.");
        }

        private static void AddMapping(
            TypeMappingFile file,
            Type type,
            TypeMappingMembers members,
            RawByteOrder byteOrder)
        {
            file.Mappings.Add(new TypeMapping
            {
                TypeName = type.FullName ?? type.Name,
                AssemblyName = type.Assembly.GetName().Name ?? string.Empty,
                Members = members,
                ByteOrder = byteOrder.ToString()
            });
        }

        private static TypeMappingMembers CompleteMembers()
        {
            return new TypeMappingMembers
            {
                Data = "Data",
                Width = "Width",
                Height = "Height",
                Stride = "Stride",
                BufferLength = "BufferLength",
                PixelFormat = "PixelFormat",
                ValidBits = "ValidBits"
            };
        }

        private static TypeMappingMembers ArrayMembers()
        {
            var members = CompleteMembers();
            members.BufferLength = null;
            return members;
        }

        private static TypeMappingMembers ContiguousPointerMembers()
        {
            var members = CompleteMembers();
            members.Stride = null;
            return members;
        }

        private static void AssertBytesEqual(byte[] expected, byte[] actual, string message)
        {
            Assert(expected.Length == actual.Length, message + " Length differs.");
            for (var i = 0; i < expected.Length; i++)
            {
                Assert(expected[i] == actual[i], message + " Byte differs at " + i + ".");
            }
        }

        private static VisualizerMemberInventoryItem Item(string name, string typeName, string sampleValue)
        {
            return new VisualizerMemberInventoryItem
            {
                Name = name,
                Kind = "Property",
                TypeName = typeName,
                SampleValue = sampleValue
            };
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private sealed class MappedPaddedFrame
        {
            public IntPtr Data { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public int PaddingX { get; set; }
            public long PayloadSize { get; set; }
        }

        private sealed class MappedOffsetFrame
        {
            public IntPtr Buffer { get; set; }
            public long BufferSize { get; set; }
            public IntPtr ImageData { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }

        private sealed class MappedExtraPayloadFrame
        {
            public IntPtr Data { get; set; }
            public long BufferSize { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }

        private sealed class MappedPointerFrame
        {
            public IntPtr Data { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public int Stride { get; set; }
            public long BufferLength { get; set; }
            public RawPixelFormat PixelFormat { get; set; }
            public int ValidBits { get; set; }
        }

        private sealed class MappedContiguousPointerFrame
        {
            public IntPtr Data { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public long BufferLength { get; set; }
            public RawPixelFormat PixelFormat { get; set; }
            public int ValidBits { get; set; }
        }

        private sealed class MappedArrayFrame<T>
        {
            public T[] Data { get; set; } = new T[0];
            public int Width { get; set; }
            public int Height { get; set; }
            public int Stride { get; set; }
            public RawPixelFormat PixelFormat { get; set; }
            public int ValidBits { get; set; }
        }
    }
}
