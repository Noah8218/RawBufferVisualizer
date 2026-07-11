using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Microsoft.VisualStudio.DebuggerVisualizers;
using RawBufferVisualizer.Sdk;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    public sealed class ImageCollectionVisualizerObjectSource : VisualizerObjectSource
    {
        private object? _cachedTarget;
        private ImageCollectionVisualizerView? _cachedView;

        public override void GetData(object target, Stream outgoingData)
        {
            SerializeAsJson(outgoingData, GetView(target).Summary);
        }

        public override void TransferData(object target, Stream incomingData, Stream outgoingData)
        {
            var request = DeserializeFromJson<VisualizerCollectionItemRequest>(incomingData);
            if (request == null)
            {
                throw new InvalidDataException("Collection item request is required.");
            }

            var view = GetView(target);
            if (request.Operation == VisualizerCollectionOperation.Metadata)
            {
                SerializeAsJson(outgoingData, view.GetMetadata(request.Index));
                return;
            }

            if (request.Operation == VisualizerCollectionOperation.Chunk)
            {
                SerializeAsJson(
                    outgoingData,
                    view.GetChunk(
                        request.Index,
                        new VisualizerSnapshotChunkRequest
                        {
                            Offset = request.Offset,
                            Count = request.Count
                        }));
                return;
            }

            throw new InvalidDataException("Unsupported collection request operation.");
        }

        private ImageCollectionVisualizerView GetView(object target)
        {
            if (!ReferenceEquals(target, _cachedTarget))
            {
                _cachedTarget = target;
                _cachedView = ImageCollectionVisualizerTransfer.CreateView(target);
            }

            return _cachedView ?? throw new InvalidOperationException("Collection view was not created.");
        }
    }

    public enum VisualizerCollectionOperation
    {
        Metadata = 0,
        Chunk = 1
    }

    public sealed class VisualizerCollectionItemRequest
    {
        public VisualizerCollectionOperation Operation { get; set; }
        public int Index { get; set; }
        public long Offset { get; set; }
        public int Count { get; set; }
    }

    public sealed class VisualizerCollectionSummary
    {
        public int TotalCount { get; set; }
        public int ItemCount { get; set; }
        public string SourceType { get; set; } = string.Empty;
    }

    public sealed class VisualizerCollectionItemMetadata
    {
        public int Index { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public VisualizerSnapshotMetadata? Metadata { get; set; }
        public string Error { get; set; } = string.Empty;
    }

    public sealed class ImageCollectionVisualizerView
    {
        private readonly IReadOnlyList<ImageCollectionItem> _items;

        internal ImageCollectionVisualizerView(VisualizerCollectionSummary summary, IReadOnlyList<ImageCollectionItem> items)
        {
            Summary = summary;
            _items = items;
        }

        public VisualizerCollectionSummary Summary { get; }

        public VisualizerCollectionItemMetadata GetMetadata(int index)
        {
            var item = GetItem(index);
            item.EnsureTransfer();
            return new VisualizerCollectionItemMetadata
            {
                Index = index,
                DisplayName = item.DisplayName,
                Metadata = item.Transfer?.Metadata,
                Error = item.Error
            };
        }

        public VisualizerSnapshotChunk GetChunk(int index, VisualizerSnapshotChunkRequest request)
        {
            var item = GetItem(index);
            item.EnsureTransfer();
            if (item.Transfer == null)
            {
                throw new NotSupportedException(item.Error);
            }

            var chunk = item.Transfer.CreateChunk(request);
            if (chunk.Buffer.LongLength >= chunk.TotalLength - chunk.Offset)
            {
                item.ReleaseTransfer();
            }

            return chunk;
        }

        private ImageCollectionItem GetItem(int index)
        {
            if (index < 0 || index >= _items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _items[index];
        }
    }

    public static class ImageCollectionVisualizerTransfer
    {
        public const int MaximumItemsPerOpen = 256;

        public static ImageCollectionVisualizerView CreateView(object target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var items = new List<ImageCollectionItem>();
            int totalCount;
            var dictionary = target as IDictionary;
            if (dictionary != null)
            {
                totalCount = dictionary.Count;
                var enumerator = dictionary.GetEnumerator();
                while (items.Count < MaximumItemsPerOpen && enumerator.MoveNext())
                {
                    var key = Convert.ToString(enumerator.Key, CultureInfo.InvariantCulture);
                    items.Add(new ImageCollectionItem(
                        enumerator.Value,
                        "[" + (string.IsNullOrWhiteSpace(key) ? "null" : key) + "]"));
                }
            }
            else
            {
                var list = target as IList;
                if (list == null)
                {
                    throw new NotSupportedException("Only IList, arrays, and IDictionary collections are supported.");
                }

                totalCount = list.Count;
                var count = Math.Min(totalCount, MaximumItemsPerOpen);
                for (var index = 0; index < count; index++)
                {
                    items.Add(new ImageCollectionItem(
                        list[index],
                        "[" + index.ToString(CultureInfo.InvariantCulture) + "]"));
                }
            }

            var type = target.GetType();
            return new ImageCollectionVisualizerView(
                new VisualizerCollectionSummary
                {
                    TotalCount = totalCount,
                    ItemCount = items.Count,
                    SourceType = type.FullName ?? type.Name
                },
                items);
        }

        internal static ImageCollectionItemTransfer CreateItemTransfer(object value, string displayName)
        {
            var snapshot = value as RawBufferSnapshot;
            if (snapshot != null)
            {
                var metadata = VisualizerChunkedTransfer.CreateMetadata(
                    snapshot.Descriptor,
                    snapshot.Buffer.LongLength,
                    typeof(RawBufferSnapshot).FullName ?? nameof(RawBufferSnapshot),
                    displayName);
                return new ImageCollectionItemTransfer(
                    metadata,
                    request => VisualizerChunkedTransfer.CreateChunk(snapshot.Buffer, request));
            }

            var rawView = value as RawBufferView;
            if (rawView != null)
            {
                var metadata = VisualizerChunkedTransfer.CreateMetadata(
                    rawView.ToDescriptor(),
                    rawView.GetBufferLength(),
                    typeof(RawBufferView).FullName ?? nameof(RawBufferView),
                    displayName);
                return new ImageCollectionItemTransfer(
                    metadata,
                    request => RawBufferViewVisualizerTransfer.CreateChunk(rawView, request));
            }

            var type = value.GetType();
            switch (type.FullName)
            {
                case "System.Drawing.Bitmap":
                    var bitmapTransfer = BitmapVisualizerTransfer.CreateTransfer(value, displayName);
                    return new ImageCollectionItemTransfer(
                        VisualizerChunkedTransfer.CreateMetadata(bitmapTransfer),
                        request => VisualizerChunkedTransfer.CreateChunk(bitmapTransfer, request));

                case "OpenCvSharp.Mat":
                    var openCvView = OpenCvSharpMatVisualizerTransfer.CreateView(value, displayName);
                    return new ImageCollectionItemTransfer(
                        OpenCvSharpMatVisualizerTransfer.CreateMetadata(openCvView),
                        request => OpenCvSharpMatVisualizerTransfer.CreateChunk(openCvView, request));

                case "Emgu.CV.Mat":
                    var emguView = EmguCvMatVisualizerTransfer.CreateView(value, displayName);
                    return new ImageCollectionItemTransfer(
                        EmguCvMatVisualizerTransfer.CreateMetadata(emguView),
                        request => EmguCvMatVisualizerTransfer.CreateChunk(emguView, request));
            }

            if (LooksLikeImagePointer(type))
            {
                var pointerView = ImagePtrVisualizerTransfer.CreateView(value);
                pointerView.DisplayName = displayName;
                return new ImageCollectionItemTransfer(
                    ImagePtrVisualizerTransfer.CreateMetadata(pointerView),
                    request => ImagePtrVisualizerTransfer.CreateChunk(pointerView, request));
            }

            throw new NotSupportedException("Unsupported collection image type: " + (type.FullName ?? type.Name));
        }

        private static bool LooksLikeImagePointer(Type type)
        {
            return HasMember(type, "Ptr", "Buffer", "Data", "DataPointer", "_ptr", "_buffer", "_data")
                && HasMember(type, "Width", "_width")
                && HasMember(type, "Height", "_height");
        }

        private static bool HasMember(Type type, params string[] names)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var name in names)
            {
                if (type.GetProperty(name, flags) != null || type.GetField(name, flags) != null)
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal sealed class ImageCollectionItem
    {
        private readonly object? _value;
        private bool _initialized;

        public ImageCollectionItem(object? value, string displayName)
        {
            _value = value;
            DisplayName = displayName;
        }

        public string DisplayName { get; }
        public ImageCollectionItemTransfer? Transfer { get; private set; }
        public string Error { get; private set; } = string.Empty;

        public void EnsureTransfer()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            if (_value == null)
            {
                Error = "Collection item is null.";
                return;
            }

            try
            {
                Transfer = ImageCollectionVisualizerTransfer.CreateItemTransfer(_value, DisplayName);
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }
        }

        public void ReleaseTransfer()
        {
            Transfer = null;
            _initialized = false;
        }
    }

    internal sealed class ImageCollectionItemTransfer
    {
        private readonly Func<VisualizerSnapshotChunkRequest, VisualizerSnapshotChunk> _createChunk;

        public ImageCollectionItemTransfer(
            VisualizerSnapshotMetadata metadata,
            Func<VisualizerSnapshotChunkRequest, VisualizerSnapshotChunk> createChunk)
        {
            Metadata = metadata;
            _createChunk = createChunk;
        }

        public VisualizerSnapshotMetadata Metadata { get; }

        public VisualizerSnapshotChunk CreateChunk(VisualizerSnapshotChunkRequest request)
        {
            return _createChunk(request);
        }
    }
}
