using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.VisualStudio.DebuggerVisualizers;
using RawBufferVisualizer.Sdk;
using RawBufferVisualizer.VisualStudio.Debugger;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    public sealed class ImageCollectionVisualizerObjectSource : VisualizerObjectSource
    {
        private readonly VisualizerViewCache<ImageCollectionVisualizerView> _viewCache;
        private byte[]? _serializedChunkBuffer;

        public ImageCollectionVisualizerObjectSource()
        {
            _viewCache = new VisualizerViewCache<ImageCollectionVisualizerView>(target =>
            {
                var view = ImageCollectionVisualizerTransfer.CreateView(target);
                Interlocked.Exchange(ref _serializedChunkBuffer, null);
                return view;
            });
        }

        public override void GetData(object target, Stream outgoingData)
        {
            var summary = GetView(target).Summary;
            summary.ExpressionIdentityHash = RuntimeHelpers.GetHashCode(target);
            SerializeAsJson(outgoingData, summary);
        }

        public override void TransferData(object target, Stream incomingData, Stream outgoingData)
        {
            VisualizerTransferTrace.Write("target deserialize begin");
            var request = DeserializeFromJson<VisualizerCollectionItemRequest>(incomingData);
            if (request == null)
            {
                throw new InvalidDataException("Collection item request is required.");
            }
            VisualizerTransferTrace.Write("target request " + request.Operation + " index=" + request.Index + " offset=" + request.Offset + " count=" + request.Count);

            if (request.Operation == VisualizerCollectionOperation.ConfigureMappings)
            {
                var configuredView = _viewCache.Replace(target, value =>
                    ImageCollectionVisualizerTransfer.CreateView(value,
                        TypeMappingStore.FromSnapshot(request.Mappings ?? throw new InvalidDataException("Mapping context is required."))));
                Interlocked.Exchange(ref _serializedChunkBuffer, null);
                configuredView.Summary.ExpressionIdentityHash = RuntimeHelpers.GetHashCode(target);
                SerializeAsJson(outgoingData, configuredView.Summary);
                VisualizerTransferTrace.Write("target configure serialized");
                return;
            }

            var view = GetView(target);
            if (request.Operation == VisualizerCollectionOperation.Metadata)
            {
                var metadata = view.GetMetadata(request.Index);
                VisualizerTransferTrace.Write("target metadata ready index=" + request.Index);
                SerializeAsJson(outgoingData, metadata);
                VisualizerTransferTrace.Write("target metadata serialized index=" + request.Index);
                return;
            }

            if (request.Operation == VisualizerCollectionOperation.Chunk)
            {
                var reusableBuffer = Interlocked.Exchange(ref _serializedChunkBuffer, null);
                VisualizerSnapshotChunk? chunk = null;
                try
                {
                    VisualizerTransferTrace.Write("target chunk read begin index=" + request.Index);
                    chunk = view.GetChunk(
                        request.Index,
                        new VisualizerSnapshotChunkRequest
                        {
                            Offset = request.Offset,
                            Count = request.Count
                        }, reusableBuffer);
                    VisualizerTransferTrace.Write("target chunk read end index=" + request.Index + " bytes=" + chunk.Buffer.Length);
                    VisualizerChunkedTransfer.SerializeChunkAsJson(outgoingData, chunk);
                    VisualizerTransferTrace.Write("target chunk serialized index=" + request.Index);
                }
                finally
                {
                    var completed = chunk != null && request.Index == view.Summary.ItemCount - 1
                        && chunk.Buffer.LongLength >= chunk.TotalLength - chunk.Offset;
                    if (completed) Interlocked.Exchange(ref _serializedChunkBuffer, null);
                    else
                    {
                        var retained = chunk?.Buffer;
                        if (reusableBuffer != null && (retained == null || reusableBuffer.Length > retained.Length))
                            retained = reusableBuffer;
                        if (retained != null && retained.Length <= VisualizerChunkedTransfer.DefaultChunkSize)
                            Interlocked.CompareExchange(ref _serializedChunkBuffer, retained, null);
                    }
                }
                return;
            }

            if (request.Operation == VisualizerCollectionOperation.Preview)
            {
                SerializeAsJson(
                    outgoingData,
                    view.GetPreview(
                        request.Index,
                        new VisualizerSnapshotChunkRequest
                        {
                            Operation = VisualizerSnapshotOperation.Preview,
                            MaximumWidth = request.MaximumWidth,
                            MaximumHeight = request.MaximumHeight
                        }));
                return;
            }

            throw new InvalidDataException("Unsupported collection request operation.");
        }

        private ImageCollectionVisualizerView GetView(object target)
        {
            return _viewCache.GetOrCreate(target);
        }
    }

    public enum VisualizerCollectionOperation
    {
        Metadata = 0,
        Chunk = 1,
        Preview = 2,
        ConfigureMappings = 3
    }

    public sealed class VisualizerCollectionItemRequest
    {
        public VisualizerCollectionOperation Operation { get; set; }
        public TypeMappingSnapshot? Mappings { get; set; }
        public int Index { get; set; }
        public long Offset { get; set; }
        public int Count { get; set; }
        public int MaximumWidth { get; set; }
        public int MaximumHeight { get; set; }
    }

    public sealed class VisualizerCollectionSummary
    {
        public int TotalCount { get; set; }
        public int ItemCount { get; set; }
        public string SourceType { get; set; } = string.Empty;
        public int ExpressionIdentityHash { get; set; }
    }

    public sealed class VisualizerCollectionItemMetadata
    {
        public int Index { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public VisualizerSnapshotMetadata? Metadata { get; set; }
        public string Error { get; set; } = string.Empty;
        public List<VisualizerMemberInventoryItem>? MemberInventory { get; set; }
        public string ItemTypeName { get; set; } = string.Empty;
        public string ItemAssemblyName { get; set; } = string.Empty;
        public int DebuggeeProcessId { get; set; }
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
                Metadata = item.Metadata,
                Error = item.Error,
                MemberInventory = item.MemberInventory,
                ItemTypeName = item.ItemTypeName,
                ItemAssemblyName = item.ItemAssemblyName,
                DebuggeeProcessId = System.Diagnostics.Process.GetCurrentProcess().Id
            };
        }

        public VisualizerSnapshotChunk GetChunk(int index, VisualizerSnapshotChunkRequest request)
        {
            return GetChunk(index, request, null);
        }

        internal VisualizerSnapshotChunk GetChunk(int index, VisualizerSnapshotChunkRequest request, byte[]? reusableBuffer)
        {
            var item = GetItem(index);
            item.EnsureTransfer();
            if (item.Transfer == null)
            {
                throw new NotSupportedException(item.Error);
            }

            var chunk = item.Transfer.CreateChunk(request, reusableBuffer);
            if (chunk.Buffer.LongLength >= chunk.TotalLength - chunk.Offset)
            {
                item.ReleaseTransfer();
            }

            return chunk;
        }

        public VisualizerSnapshotTransfer GetPreview(int index, VisualizerSnapshotChunkRequest request)
        {
            var item = GetItem(index);
            item.EnsureTransfer();
            if (item.Transfer == null)
            {
                throw new NotSupportedException(item.Error);
            }

            return item.Transfer.CreatePreview(request);
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
            return CreateView(target, TypeMappingStore.Default);
        }

        public static ImageCollectionVisualizerView CreateView(object target, TypeMappingStore? mappingStore)
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
                        "[" + (string.IsNullOrWhiteSpace(key) ? "null" : key) + "]",
                        mappingStore));
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
                var array = list as Array;
                if (array != null && array.Rank != 1) throw new NotSupportedException("Only one-dimensional image arrays are supported.");
                var firstIndex = array?.GetLowerBound(0) ?? 0;
                for (var offset = 0; offset < count; offset++)
                {
                    var index = firstIndex + offset;
                    items.Add(new ImageCollectionItem(
                        array == null ? list[index] : array.GetValue(index),
                        "[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                        mappingStore));
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

        internal static ImageVisualizerTransfer CreateItemTransfer(object value, string displayName, TypeMappingStore? mappingStore)
        {
            var type = value.GetType();
            var source = ImageVisualizerTypePolicy.GetObjectSource(type, allowLegacyNameMatch: true);
            switch (source)
            {
                case nameof(RawBufferSnapshotVisualizerObjectSource) when value is RawBufferSnapshot snapshot:
                    return VisualizerChunkedTransfer.CreateSnapshotTransfer(snapshot, displayName);
                case nameof(RawBufferViewVisualizerObjectSource) when value is RawBufferView view:
                    return RawBufferViewVisualizerTransfer.CreateImageTransfer(view, displayName);
                case nameof(BitmapVisualizerObjectSource):
                    return BitmapVisualizerTransfer.CreateImageTransfer(value, displayName);
                case nameof(OpenCvSharpMatVisualizerObjectSource):
                    return OpenCvSharpMatVisualizerTransfer.CreateImageTransfer(value, displayName);
                case nameof(EmguCvMatVisualizerObjectSource):
                    return EmguCvMatVisualizerTransfer.CreateImageTransfer(value, displayName);
            }

            var mapping = mappingStore == null
                ? null
                : mappingStore.FindMapping(type.FullName ?? type.Name, type.Assembly.GetName().Name ?? string.Empty);
            if (mapping != null)
            {
                string mappedError;
                var mappedTransfer = MappedTypeVisualizerTransfer.TryCreateItemTransfer(value, displayName, mapping, out mappedError);
                if (mappedTransfer != null)
                {
                    return mappedTransfer;
                }

                throw new NotSupportedException(mappedError + " (type mapping for " + (type.FullName ?? type.Name) + ")");
            }

            if (ImagePtrVisualizerTransfer.HasImageLayout(type))
            {
                return ImagePtrVisualizerTransfer.CreateImageTransfer(value, displayName);
            }

            throw new NotSupportedException("Unsupported collection image type: " + (type.FullName ?? type.Name));
        }

    }

    internal sealed class ImageCollectionItem
    {
        private readonly object? _value;
        private readonly TypeMappingStore? _mappingStore;
        private bool _initialized;

        public ImageCollectionItem(object? value, string displayName, TypeMappingStore? mappingStore = null)
        {
            _value = value;
            DisplayName = displayName;
            _mappingStore = mappingStore;
        }

        public string DisplayName { get; }
        public ImageVisualizerTransfer? Transfer { get; private set; }
        public VisualizerSnapshotMetadata? Metadata { get; private set; }
        public string Error { get; private set; } = string.Empty;
        public List<VisualizerMemberInventoryItem>? MemberInventory { get; private set; }

        public string ItemTypeName
        {
            get
            {
                var type = _value == null ? null : _value.GetType();
                return type == null ? string.Empty : type.FullName ?? type.Name;
            }
        }

        public string ItemAssemblyName
        {
            get
            {
                var type = _value == null ? null : _value.GetType();
                return type == null ? string.Empty : type.Assembly.GetName().Name ?? string.Empty;
            }
        }

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
                var transfer = ImageCollectionVisualizerTransfer.CreateItemTransfer(_value, DisplayName, _mappingStore);
                Metadata = transfer.CreateMetadata();
                Transfer = transfer;
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                MemberInventory = VisualizerMemberInventory.Create(_value);
            }
        }

        public void ReleaseTransfer()
        {
            Transfer = null;
            Metadata = null;
            _initialized = false;
        }
    }
}
