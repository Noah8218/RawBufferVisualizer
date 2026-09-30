using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using BindingFlags = Microsoft.VisualStudio.Debugger.Metadata.BindingFlags;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Clr;
using Microsoft.VisualStudio.Debugger.ComponentInterfaces;
using Microsoft.VisualStudio.Debugger.Evaluation;
using Microsoft.VisualStudio.Debugger.Evaluation.ClrCompilation;

namespace RawBufferVisualizer.VisualStudio.Debugger
{
    /// <summary>
    /// 역할: C# 이미지 평가 결과에 Classic 시각화 도우미와 확장 설치 경로를 연결한다.
    /// 흐름: 원래 평가 결과 유지 → 확장 위치의 이미지 후보 등록 → Classic 전송 경로 위임.
    /// 상태: 설치된 Classic DLL의 전체 이름을 캐시하고 원래 평가 결과를 보관하여 C# 공급자에 위임한다.
    /// 연관 클래스:
    /// - DkmSuccessEvaluationResult: 원래 결과와 교체 결과의 수명 및 호출 전달.
    /// - ImageVisualizerTypePolicy: 이미지 타입과 소유한 ObjectSource 판별.
    /// - RawBufferClassicDebuggerVisualizer: 단일 이미지의 Classic 진입점.
    /// - ImageCollectionClassicDebuggerVisualizer: 이미지 컬렉션의 Classic 진입점.
    /// </summary>
    public sealed class ImageVisualizerResultProvider : IDkmClrResultProvider, IDkmClrCustomVisualizerObjectProvider
    {
        private const string ClassicAssemblyName = "RawBufferVisualizer.VisualStudio.Classic";
        private const string SingleImageVisualizerTypeName = "RawBufferVisualizer.VisualStudio.Classic.RawBufferClassicDebuggerVisualizer";
        private const string CollectionVisualizerTypeName = "RawBufferVisualizer.VisualStudio.Classic.ImageCollectionClassicDebuggerVisualizer";
        private static readonly Lazy<string> ClassicAssemblyIdentity = new Lazy<string>(ReadClassicAssemblyIdentity);

        public void GetResult(DkmClrValue value, DkmWorkList workList, DkmClrType? declaredType,
            DkmClrCustomTypeInfo? customTypeInfo, DkmInspectionContext inspectionContext,
            ReadOnlyCollection<string>? formatSpecifiers, string resultName, string? resultFullName,
            DkmCompletionRoutine<DkmEvaluationAsyncResult> completionRoutine)
        {
            value.GetResult(workList, declaredType, customTypeInfo, inspectionContext, formatSpecifiers,
                resultName, resultFullName, result =>
                {
                    var success = result.Result as DkmSuccessEvaluationResult;
                    if (result.ErrorCode != 0 || success == null || value.IsNull || value.Type == null)
                    {
                        completionRoutine(result);
                        return;
                    }

                    DkmEvaluationResult replacement = success;
                    try
                    {
                        string? source = GetObjectSource(value, inspectionContext, out bool collection);
                        bool ownsCandidate = success.CustomUIVisualizers != null
                            && System.Linq.Enumerable.Any(success.CustomUIVisualizers, candidate => ImageVisualizerTypePolicy.IsOwnedObjectSource(candidate.DebuggeeSideVisualizerAssemblyName));
                        if ((source != null || ownsCandidate) && CanPrependVisualizer(success.CustomUIVisualizers))
                        {
                            var candidates = new List<DkmCustomUIVisualizerInfo>();
                            // CLR visualizer IDs must match the array indexes used for invocation.
                            if (source != null)
                            {
                                candidates.Add(DkmCustomUIVisualizerInfo.Create(0, "Raw Buffer Visualizer",
                                    "Open image in the docked Raw Buffer Visualizer", "ClrCustomVisualizerVSHost",
                                    collection ? CollectionVisualizerTypeName : SingleImageVisualizerTypeName,
                                    ClassicAssemblyIdentity.Value, DkmClrCustomVisualizerAssemblyLocation.Extension,
                                    "RawBufferVisualizer.VisualStudio.ObjectSource." + source,
                                    "RawBufferVisualizer.VisualStudio.ObjectSource"));
                            }
                            if (success.CustomUIVisualizers != null)
                            {
                                foreach (var candidate in success.CustomUIVisualizers)
                                {
                                    if (ImageVisualizerTypePolicy.IsOwnedObjectSource(candidate.DebuggeeSideVisualizerAssemblyName)) continue;
                                    candidates.Add(DkmCustomUIVisualizerInfo.Create((uint)candidates.Count,
                                        candidate.MenuName, candidate.Description, candidate.Metric,
                                        candidate.UISideVisualizerTypeName, candidate.UISideVisualizerAssemblyName,
                                        candidate.UISideVisualizerAssemblyLocation, candidate.DebuggeeSideVisualizerTypeName,
                                        candidate.DebuggeeSideVisualizerAssemblyName, candidate.ExtensionPartId));
                                }
                            }

                            replacement = DkmSuccessEvaluationResult.Create(success.InspectionContext, success.StackFrame,
                                success.Name, success.FullName, success.Flags, success.Value, success.EditableValue,
                                success.Type, success.Category, success.Access, success.StorageType, success.TypeModifierFlags,
                                success.Address, candidates.AsReadOnly(), success.ExternalModules, success.RefreshButtonText,
                                new OriginalResult(success));
                        }
                    }
                    catch (Exception ex) when (ex is DkmException || ex is ArgumentException || ex is InvalidOperationException || ex is OverflowException || ex is IOException || ex is BadImageFormatException)
                    {
                        // Missing/unloaded metadata must never break ordinary expression evaluation.
                        System.Diagnostics.Trace.WriteLine("Raw Buffer Visualizer candidate unavailable: " + ex.Message);
                    }

                    completionRoutine(new DkmEvaluationAsyncResult(replacement));
                });
        }

        private static string ReadClassicAssemblyIdentity()
        {
            string path = Path.Combine(Path.GetDirectoryName(typeof(ImageVisualizerResultProvider).Assembly.Location), ClassicAssemblyName + ".dll");
            // VS indexes extension visualizers by type + Assembly.FullName, including version/culture/token.
            return AssemblyName.GetAssemblyName(path).FullName;
        }

        private static bool CanPrependVisualizer(ReadOnlyCollection<DkmCustomUIVisualizerInfo>? candidates)
        {
            if (candidates == null) return true;
            foreach (var candidate in candidates)
            {
                // Other hosts may use IDs as commands rather than CLR array indexes.
                if (candidate.Metric != "ClrCustomVisualizerVSHost") return false;
            }
            return true;
        }

        private static string? GetObjectSource(DkmClrValue value, DkmInspectionContext context, out bool collection)
        {
            collection = false;
            if (value.Type == null) return null;
            var metadata = value.Type.GetLmrType();
            var imageType = metadata;
            string? containerName = null;
            if (metadata.IsArray && metadata.GetArrayRank() == 1)
            {
                collection = true;
                imageType = metadata.GetElementType();
            }
            else if (metadata.IsGenericType)
            {
                var definition = metadata.GetGenericTypeDefinition();
                int argument = ImageVisualizerTypePolicy.GetCollectionValueArgument(definition.FullName, definition.Assembly.GetName().Name);
                if (argument >= 0)
                {
                    collection = true;
                    imageType = metadata.GetGenericArguments()[argument];
                    containerName = definition.FullName;
                }
            }
            else if (ImageVisualizerTypePolicy.IsFrameworkCollectionAssembly(metadata.Assembly.GetName().Name)
                && (metadata.FullName == "System.Collections.ArrayList" || metadata.FullName == "System.Collections.Hashtable"))
            {
                collection = true;
                imageType = null;
                containerName = metadata.FullName;
            }

            string? source = imageType == null ? null : ImageVisualizerTypePolicy.GetObjectSource(imageType.FullName, imageType.Assembly.GetName().Name);
            if (source == null && collection && (imageType == null || (!imageType.IsValueType && !imageType.IsSealed))
                && ContainsImage(value, containerName, context)) source = "ImageCollectionVisualizerObjectSource";
            return source == null ? null : collection ? "ImageCollectionVisualizerObjectSource" : source;
        }

        private static bool ContainsImage(DkmClrValue value, string? containerName, DkmInspectionContext context)
        {
            // Inspect storage fields only: no getters, debug proxies or target-side enumeration.
            if (containerName == null) return ArrayContainsImage(value, 256, null, false, context);

            DkmClrValue? storage = null;
            DkmClrValue? tables = null;
            DkmClrValue? size = null;
            try
            {
                switch (containerName)
                {
                    case "System.Collections.Generic.List`1":
                    case "System.Collections.ArrayList":
                        size = ReadField(value, context, "_size");
                        if (!(size?.HostObjectValue is int count) || count <= 0) return false;
                        storage = ReadField(value, context, "_items");
                        return storage != null && ArrayContainsImage(storage, Math.Min(count, 256), null, false, context);
                    case "System.Collections.Generic.Dictionary`2":
                        storage = ReadField(value, context, "_entries", "entries");
                        return storage != null && ArrayContainsImage(storage, 4096, "value", false, context);
                    case "System.Collections.Hashtable":
                        storage = ReadField(value, context, "_buckets", "buckets");
                        return storage != null && ArrayContainsImage(storage, 4096, "val", false, context);
                    case "System.Collections.Concurrent.ConcurrentDictionary`2":
                        tables = ReadField(value, context, "_tables", "m_tables");
                        if (tables == null || tables.IsNull) return false;
                        storage = ReadField(tables, context, "_buckets", "m_buckets");
                        return storage != null && ArrayContainsImage(storage, 4096, null, true, context);
                    default:
                        return false;
                }
            }
            finally
            {
                storage?.Close();
                tables?.Close();
                size?.Close();
            }
        }

        private static bool ArrayContainsImage(DkmClrValue array, int maximumSlots, string? valueField, bool linkedBuckets, DkmInspectionContext context)
        {
            if (array.IsNull || array.ArrayDimensions == null || array.ArrayDimensions.Count != 1) return false;
            int count = Math.Min(array.ArrayDimensions[0], maximumSlots);
            int first = array.ArrayLowerBounds == null ? 0 : array.ArrayLowerBounds[0];
            int remaining = 256;
            // ponytail: cap sparse storage scans at 4096 slots and 256 values to keep Locals bounded.
            // Larger object-valued sparse tables retain the built-in viewer if no image is found.
            for (int offset = 0; offset < count && remaining > 0; offset++)
            {
                var item = array.GetArrayElement(new[] { first + offset }, context);
                DkmClrValue? field = null;
                try
                {
                    if (item.IsNull) continue;
                    if (linkedBuckets)
                    {
                        field = ReadField(item, context, "_node"); // .NET 8 VolatileNode; Framework has Node[].
                        if (NodeContainsImage(field ?? item, context, ref remaining)) return true;
                    }
                    else
                    {
                        field = valueField == null ? null : ReadField(item, context, valueField);
                        var candidate = valueField == null ? item : field;
                        if (candidate == null || candidate.IsNull) continue;
                        remaining--;
                        if (IsImageValue(candidate)) return true;
                    }
                }
                finally
                {
                    field?.Close();
                    item.Close();
                }
            }
            return false;
        }

        private static bool NodeContainsImage(DkmClrValue node, DkmInspectionContext context, ref int remaining)
        {
            if (node.IsNull || remaining-- <= 0) return false;
            DkmClrValue? value = null;
            DkmClrValue? next = null;
            try
            {
                value = ReadField(node, context, "_value", "m_value");
                if (value != null && IsImageValue(value)) return true;
                next = ReadField(node, context, "_next", "m_next");
                return next != null && NodeContainsImage(next, context, ref remaining);
            }
            finally
            {
                next?.Close();
                value?.Close();
            }
        }

        private static bool IsImageValue(DkmClrValue value)
        {
            if (value.IsNull || value.Type == null) return false;
            var type = value.Type.GetLmrType();
            return ImageVisualizerTypePolicy.GetObjectSource(type.FullName, type.Assembly.GetName().Name) != null;
        }

        private static DkmClrValue? ReadField(DkmClrValue value, DkmInspectionContext context, params string[] names)
        {
            if (value.IsNull || value.Type == null) return null;
            var type = value.Type.GetLmrType();
            foreach (string name in names)
            {
                if (type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null)
                    return value.GetMemberValue(name, (int)MemberTypes.Field, null, context);
            }
            return null;
        }

        public DkmClrValue? GetClrValue(DkmSuccessEvaluationResult result)
        {
            return (result.GetDataItem<OriginalResult>()?.Result ?? result).GetClrValue();
        }

        public void GetChildren(DkmEvaluationResult result, DkmWorkList workList, int initialRequestSize,
            DkmInspectionContext inspectionContext, DkmCompletionRoutine<DkmGetChildrenAsyncResult> completionRoutine)
        {
            (result.GetDataItem<OriginalResult>()?.Result ?? result).GetChildren(workList, initialRequestSize, inspectionContext, completionRoutine);
        }

        public void GetItems(DkmEvaluationResultEnumContext enumContext, DkmWorkList workList, int startIndex, int count,
            DkmCompletionRoutine<DkmEvaluationEnumAsyncResult> completionRoutine)
        {
            enumContext.GetItems(workList, startIndex, count, completionRoutine);
        }

        public string? GetUnderlyingString(DkmEvaluationResult result)
        {
            return (result.GetDataItem<OriginalResult>()?.Result ?? result).GetUnderlyingString();
        }

        public void ResolveAssembly(DkmSuccessEvaluationResult result, string assemblyName, out string? assemblyPath, out ReadOnlyCollection<byte>? assemblyBytes)
        {
            if (result.GetDataItem<OriginalResult>() != null)
            {
                string name = new AssemblyName(assemblyName).Name;
                string? relativePath = null;
                switch (name)
                {
                    case "RawBufferVisualizer.VisualStudio.Classic":
                    case "RawBufferVisualizer.VisualStudio":
                        relativePath = name + ".dll";
                        break;
                    case "RawBufferVisualizer.Core":
                    case "RawBufferVisualizer.Sdk":
                    case "RawBufferVisualizer.VisualStudio.ObjectSource":
                        relativePath = Path.Combine("netstandard2.0", name + ".dll");
                        break;
                }
                if (relativePath != null)
                {
                    string path = Path.Combine(Path.GetDirectoryName(typeof(ImageVisualizerResultProvider).Assembly.Location), relativePath);
                    if (File.Exists(path))
                    {
                        assemblyPath = path;
                        assemblyBytes = null;
                        return;
                    }
                }
            }
            result.ResolveAssembly(assemblyName, out assemblyPath, out assemblyBytes);
            assemblyBytes = CopyVisualizerBytes(assemblyBytes);
        }

        public void CreateDebuggeeSideVisualizerObject(DkmSuccessEvaluationResult result, uint selectedVisualizerIndex, out string? exceptionType, out string? exceptionStackTrace, out string? exceptionMessage)
        {
            result.CreateDebuggeeSideVisualizerObject(selectedVisualizerIndex, out exceptionType, out exceptionStackTrace, out exceptionMessage);
        }

        public bool DestroyDebuggeeSideVisualizerObject(DkmSuccessEvaluationResult result)
        {
            return result.DestroyDebuggeeSideVisualizerObject();
        }

        public ReadOnlyCollection<byte>? GetDataFromDebuggeeSideVisualizer(DkmSuccessEvaluationResult result, out string? exceptionType, out string? exceptionStackTrace, out string? exceptionMessage)
        {
            return CopyVisualizerBytes(result.GetDataFromDebuggeeSideVisualizer(out exceptionType, out exceptionStackTrace, out exceptionMessage));
        }

        public ReadOnlyCollection<byte>? TransferDataToDebuggeeSideVisualizer(DkmSuccessEvaluationResult result, byte[]? dataIn, out string? exceptionType, out string? exceptionStackTrace, out string? exceptionMessage)
        {
            return CopyVisualizerBytes(result.TransferDataToDebuggeeSideVisualizer(dataIn, out exceptionType, out exceptionStackTrace, out exceptionMessage));
        }

        public void CreateReplacementObjectOnDebuggeeSideVisualizer(DkmSuccessEvaluationResult result, byte[]? dataIn, out string? exceptionType, out string? exceptionStackTrace, out string? exceptionMessage)
        {
            result.CreateReplacementObjectOnDebuggeeSideVisualizer(dataIn, out exceptionType, out exceptionStackTrace, out exceptionMessage);
        }

        internal static ReadOnlyCollection<byte>? CopyVisualizerBytes(ReadOnlyCollection<byte>? bytes)
        {
            // VS17.14's incoming marshaller has no outgoing-call context. Forwarding its
            // native-backed collection hits a context-dependent fast path and crashes the IDE.
            return bytes == null ? null : new List<byte>(bytes).AsReadOnly();
        }

        private sealed class OriginalResult : DkmDataItem
        {
            internal readonly DkmSuccessEvaluationResult Result;

            internal OriginalResult(DkmSuccessEvaluationResult result) { Result = result; }

            protected override void OnClose() { Result.Close(); }
        }
    }
}
