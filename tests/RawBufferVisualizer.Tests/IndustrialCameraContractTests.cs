using System;
using System.Collections.Generic;
using System.IO;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.VisualStudio.ObjectSource;

namespace RawBufferVisualizer.Tests
{
    internal static class IndustrialCameraContractTests
    {
        public static void RunAll()
        {
            SpinnakerManagedImageContractAutoOpens();
            BaslerGrabResultRequiresContiguousPayload();
            VimbaFramePrefersImageDataAndRejectsChunkOffset();
            IdsPeakImageRequiresContiguousSize();
            GenICamPfncAliasesResolveConservatively();
            MethodOnlyBufferContractsStayExplicit();
            MappedPointerTransfersRejectUnsafeLayouts();
        }

        private static void SpinnakerManagedImageContractAutoOpens()
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
                "SpinnakerNET.IManagedImage");

            Assert(inference.CanAutoOpen, "Spinnaker IManagedImage should auto-open when DataPtr and Stride are explicit.");
            Assert(inference.Members.Data == "DataPtr", "Spinnaker inference selected the wrong data pointer.");
            Assert(inference.Members.Stride == "Stride", "Spinnaker inference did not retain the explicit stride.");
            Assert(inference.PixelFormat == RawPixelFormat.Mono8, "Spinnaker PixelFormat_Mono8 did not resolve.");
        }

        private static void BaslerGrabResultRequiresContiguousPayload()
        {
            var contiguous = CreateBaslerInventory(0, 640L * 480L);
            var safe = VisionMemberInference.Infer(contiguous, "Basler.Pylon.IGrabResult");
            Assert(safe.CanAutoOpen, "A zero-padding Basler grab result with an exact payload should auto-open.");
            Assert(safe.Members.Data == "PixelDataPointer", "Basler inference must prefer PixelDataPointer.");
            Assert(safe.Members.BufferLength == "PayloadSize", "Basler inference did not retain PayloadSize.");

            var padded = VisionMemberInference.Infer(
                CreateBaslerInventory(16, (640L + 16L) * 480L),
                "Basler.Pylon.IGrabResult");
            Assert(padded.RequiresExplicitLayout, "Nonzero Basler PaddingX must require an explicit layout.");
            Assert(!padded.CanAutoOpen, "A padded Basler grab result must not auto-open without a stride.");

            var extraPayload = VisionMemberInference.Infer(
                CreateBaslerInventory(0, (640L * 480L) + 128L),
                "Basler.Pylon.IGrabResult");
            Assert(extraPayload.RequiresExplicitLayout, "A Basler payload larger than the contiguous image must remain explicit.");
            Assert(!extraPayload.CanAutoOpen, "An extra Basler payload must not be interpreted as contiguous pixels.");
        }

        private static void VimbaFramePrefersImageDataAndRejectsChunkOffset()
        {
            var safe = VisionMemberInference.Infer(
                CreateVimbaInventory("0x2000", "0x2000", 640L * 480L),
                "VmbNET.IFrame");
            Assert(safe.Members.Data == "ImageData", "Vimba inference must prefer ImageData over the whole Buffer.");
            Assert(safe.CanAutoOpen, "A Vimba frame with equal base/image pointers and exact size should auto-open.");

            var chunked = VisionMemberInference.Infer(
                CreateVimbaInventory("0x2000", "0x2100", (640L * 480L) + 256L),
                "VmbNET.IFrame");
            Assert(chunked.Members.Data == "ImageData", "Chunked Vimba inference must still select ImageData.");
            Assert(chunked.RequiresExplicitLayout, "A Vimba ImageData offset from Buffer must require an adapter.");
            Assert(!chunked.CanAutoOpen, "A chunk-prefixed Vimba frame must never auto-open as a flat image.");
        }

        private static void IdsPeakImageRequiresContiguousSize()
        {
            var safe = VisionMemberInference.Infer(
                CreateIdsPeakInventory(640L * 480L),
                "IDSImaging.Peak.ICV.Types.Image");
            Assert(safe.CanAutoOpen, "An IDS peak ICV image with exact SizeInBytes should auto-open.");
            Assert(safe.Members.BufferLength == "SizeInBytes", "IDS peak inference did not retain SizeInBytes.");

            var padded = VisionMemberInference.Infer(
                CreateIdsPeakInventory((640L * 480L) + 480L),
                "IDSImaging.Peak.ICV.Types.Image");
            Assert(padded.RequiresExplicitLayout, "A padded IDS peak ICV image must require explicit layout.");
            Assert(!padded.CanAutoOpen, "A padded IDS peak ICV image must not default to minimum stride.");
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
                "Euresys.EGrabber.ScopedBuffer");
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

        private static List<VisualizerMemberInventoryItem> CreateBaslerInventory(long paddingX, long payloadSize)
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

        private static List<VisualizerMemberInventoryItem> CreateVimbaInventory(
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

        private static List<VisualizerMemberInventoryItem> CreateIdsPeakInventory(long sizeInBytes)
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

        private static void AssertMappedFailure(object value, TypeMappingMembers members, string expectedError)
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
                    ByteOrder = "LittleEndian"
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
    }
}
