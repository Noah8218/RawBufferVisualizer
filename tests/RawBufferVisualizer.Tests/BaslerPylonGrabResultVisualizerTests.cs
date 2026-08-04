using System;
using System.Runtime.InteropServices;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.VisualStudio.ObjectSource;

namespace RawBufferVisualizer.Tests
{
    internal static class BaslerPylonGrabResultVisualizerTests
    {
        public static void RunAll()
        {
            PaddedMono8UsesOfficialStrideAndTransfersPixels();
            SupportedPixelTypesMapExactly();
            UnsafePayloadsAndLifetimeStatesFailClearly();
        }

        private static void PaddedMono8UsesOfficialStrideAndTransfersPixels()
        {
            var buffer = new byte[]
            {
                10, 20, 0, 0,
                30, 40, 0, 0,
                99, 98
            };
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            var grabResult = new FakeGrabResult
            {
                PixelDataPointer = handle.AddrOfPinnedObject(),
                Width = 2,
                Height = 2,
                PaddingX = 2,
                PaddingY = 2,
                PayloadSize = buffer.Length,
                PixelTypeValue = Basler.Pylon.PixelType.Mono8
            };

            try
            {
                var view = BaslerPylonGrabResultVisualizerTransfer.CreateView(grabResult);
                var descriptor = view.BufferView.ToDescriptor();
                var metadata = BaslerPylonGrabResultVisualizerTransfer.CreateMetadata(view);
                var preview = BaslerPylonGrabResultVisualizerTransfer.CreatePreview(
                    view,
                    new VisualizerSnapshotChunkRequest
                    {
                        Operation = VisualizerSnapshotOperation.Preview,
                        MaximumWidth = 2,
                        MaximumHeight = 2
                    });
                var chunk = BaslerPylonGrabResultVisualizerTransfer.CreateChunk(
                    view,
                    new VisualizerSnapshotChunkRequest
                    {
                        Offset = 2,
                        Count = 4
                    });

                Assert(descriptor.Stride == 4, "Basler ComputeStride result was not used.");
                Assert(descriptor.PixelFormat == RawPixelFormat.Mono8, "Basler Mono8 format mapping failed.");
                Assert(view.BufferView.BufferLength == 8, "Basler trailing PaddingY must not be exposed as image pixels.");
                Assert(metadata.SupportsDirectMemory, "Basler pointer metadata should support direct debugger memory.");
                Assert(metadata.SourceType == typeof(FakeGrabResult).FullName, "Basler runtime source type was not retained.");
                Assert(preview.Buffer[0] == 10 && preview.Buffer[4] == 20, "Basler first preview row failed.");
                Assert(preview.Buffer[8] == 30 && preview.Buffer[12] == 40, "Basler padded second preview row failed.");
                Assert(
                    chunk.Buffer.Length == 4
                    && chunk.Buffer[0] == 0
                    && chunk.Buffer[1] == 0
                    && chunk.Buffer[2] == 30
                    && chunk.Buffer[3] == 40,
                    "Basler pointer chunk failed.");
                Assert(!grabResult.CloneCalled, "The visualizer must not clone an application-owned Basler grab result.");
                Assert(!grabResult.DisposeCalled, "The visualizer must not dispose an application-owned Basler grab result.");
            }
            finally
            {
                handle.Free();
            }
        }

        private static void SupportedPixelTypesMapExactly()
        {
            AssertFormat("Mono10", RawPixelFormat.Mono16, 10, 1);
            AssertFormat("Mono12", RawPixelFormat.Mono16, 12, 1);
            AssertFormat("Mono10p", RawPixelFormat.Mono10PackedLsb, 10, 1);
            AssertFormat("Mono12p", RawPixelFormat.Mono12PackedLsb, 12, 1);
            AssertFormat("BayerRG8", RawPixelFormat.BayerRGGB8, 8, 1);
            AssertFormat("RGB8packed", RawPixelFormat.RGB24, 8, 3);
            AssertFormat("BGR8packed", RawPixelFormat.BGR24, 8, 3);
            AssertFormat("BGRA8packed", RawPixelFormat.BGRA32, 8, 4);

            AssertUnsupported("Mono10packed");
            AssertUnsupported("Mono12packed");
            AssertUnsupported("YUV422packed");
            AssertUnsupported("Coord3D_ABC32f");
        }

        private static void UnsafePayloadsAndLifetimeStatesFailClearly()
        {
            AssertFailure(
                CreateValidResult(new IntPtr(1), payloadSize: 1),
                "payload is smaller");

            var disposed = CreateValidResult(IntPtr.Zero, payloadSize: 4);
            AssertFailure(disposed, "disposed");

            var genDc = CreateValidResult(new IntPtr(1), payloadSize: 4);
            genDc.PayloadTypeValue = Basler.Pylon.PayloadType.GenDC;
            AssertFailure(genDc, "Only 2D image payloads");

            var bottomUp = CreateValidResult(new IntPtr(1), payloadSize: 4);
            bottomUp.Orientation = Basler.Pylon.ImageOrientation.BottomUp;
            AssertFailure(bottomUp, "Only top-down");

            var failedGrab = CreateValidResult(new IntPtr(1), payloadSize: 4);
            failedGrab.GrabSucceeded = false;
            failedGrab.ErrorDescription = "simulated transport failure";
            AssertFailure(failedGrab, "simulated transport failure");
        }

        private static FakeGrabResult CreateValidResult(IntPtr pointer, long payloadSize)
        {
            return new FakeGrabResult
            {
                PixelDataPointer = pointer,
                Width = 2,
                Height = 2,
                PayloadSize = payloadSize,
                PixelTypeValue = Basler.Pylon.PixelType.Mono8
            };
        }

        private static void AssertFormat(
            string value,
            RawPixelFormat expectedFormat,
            int expectedBits,
            int expectedChannels)
        {
            RawPixelFormat format;
            int bits;
            int channels;
            Assert(
                BaslerPylonGrabResultVisualizerTransfer.TryMapPixelType(value, out format, out bits, out channels)
                && format == expectedFormat
                && bits == expectedBits
                && channels == expectedChannels,
                "Unexpected Basler pixel mapping for " + value + ".");
        }

        private static void AssertUnsupported(string value)
        {
            RawPixelFormat format;
            int bits;
            int channels;
            Assert(
                !BaslerPylonGrabResultVisualizerTransfer.TryMapPixelType(value, out format, out bits, out channels),
                value + " must remain unsupported.");
        }

        private static void AssertFailure(FakeGrabResult grabResult, string expectedText)
        {
            Exception? failure = null;
            try
            {
                BaslerPylonGrabResultVisualizerTransfer.CreateView(grabResult);
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            Assert(failure != null, "Expected Basler adapter failure containing: " + expectedText);
            Assert(
                failure!.Message.IndexOf(expectedText, StringComparison.OrdinalIgnoreCase) >= 0,
                "Expected Basler failure containing '" + expectedText + "' but was: " + failure.Message);
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private sealed class FakeGrabResult : Basler.Pylon.IGrabResult
        {
            public bool GrabSucceeded { get; set; } = true;
            public string ErrorDescription { get; set; } = string.Empty;
            public bool IsValid { get; set; } = true;
            public IntPtr PixelDataPointer { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public int PaddingX { get; set; }
            public int PaddingY { get; set; }
            public long PayloadSize { get; set; }
            public Basler.Pylon.PixelType PixelTypeValue { get; set; }
            public Basler.Pylon.PayloadType PayloadTypeValue { get; set; } = Basler.Pylon.PayloadType.Image;
            public Basler.Pylon.ImageOrientation Orientation { get; set; } = Basler.Pylon.ImageOrientation.TopDown;
            public bool CloneCalled { get; private set; }
            public bool DisposeCalled { get; private set; }

            public Basler.Pylon.IGrabResult Clone()
            {
                CloneCalled = true;
                return this;
            }

            public void Dispose()
            {
                DisposeCalled = true;
            }
        }
    }
}

namespace Basler.Pylon
{
    public enum PayloadType
    {
        Undefined = -1,
        Image = 0,
        RawData = 1,
        ChunkData = 3,
        GenDC = 4
    }

    public enum ImageOrientation
    {
        TopDown = 0,
        BottomUp = 1
    }

    public enum PixelType
    {
        Mono8,
        Mono10,
        Mono10packed,
        Mono10p,
        Mono12,
        Mono12packed,
        Mono12p,
        Mono16,
        BayerRG8,
        BayerGR8,
        BayerGB8,
        BayerBG8,
        RGB8packed,
        BGR8packed,
        BGRA8packed,
        YUV422packed,
        Coord3D_ABC32f
    }

    public interface IImage
    {
        bool IsValid { get; }
        IntPtr PixelDataPointer { get; }
        int Width { get; }
        int Height { get; }
        int PaddingX { get; }
        PixelType PixelTypeValue { get; }
        ImageOrientation Orientation { get; }
    }

    public interface IGrabResult : IImage, IDisposable
    {
        bool GrabSucceeded { get; }
        string ErrorDescription { get; }
        int PaddingY { get; }
        long PayloadSize { get; }
        PayloadType PayloadTypeValue { get; }
        IGrabResult Clone();
    }

    public static class IImageExtensions
    {
        public static int? ComputeStride(IImage image)
        {
            int bitsPerPixel;
            switch (image.PixelTypeValue)
            {
                case PixelType.Mono8:
                case PixelType.BayerRG8:
                case PixelType.BayerGR8:
                case PixelType.BayerGB8:
                case PixelType.BayerBG8:
                    bitsPerPixel = 8;
                    break;
                case PixelType.Mono10:
                case PixelType.Mono12:
                case PixelType.Mono16:
                    bitsPerPixel = 16;
                    break;
                case PixelType.Mono10p:
                    bitsPerPixel = 10;
                    break;
                case PixelType.Mono12p:
                    bitsPerPixel = 12;
                    break;
                case PixelType.RGB8packed:
                case PixelType.BGR8packed:
                    bitsPerPixel = 24;
                    break;
                case PixelType.BGRA8packed:
                    bitsPerPixel = 32;
                    break;
                default:
                    return null;
            }

            var rowBits = checked(image.Width * bitsPerPixel);
            if ((rowBits % 8) != 0 && image.PaddingX == 0)
            {
                return null;
            }

            return checked(((rowBits + 7) / 8) + image.PaddingX);
        }
    }
}
