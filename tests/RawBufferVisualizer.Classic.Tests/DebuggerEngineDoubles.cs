// Test-only Concord boundary. The linked production provider runs unchanged against these
// deterministic callbacks; the extension build separately checks the real SDK signatures.
// These doubles do not emulate native Concord dispatch, CLR evaluation or installed IDE UI.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using Microsoft.VisualStudio.Debugger.Clr;
using Microsoft.VisualStudio.Debugger.Evaluation;

namespace Microsoft.VisualStudio.Debugger
{
    public delegate void DkmCompletionRoutine<T>(T result);
    public sealed class DkmWorkList { public bool IsCanceled { get; set; } }
    public sealed class DkmException : Exception { }
    public abstract class DkmDataItem
    {
        protected virtual void OnClose() { }
        internal void Close() { OnClose(); }
    }
}

namespace Microsoft.VisualStudio.Debugger.ComponentInterfaces
{
    public interface IDkmClrResultProvider { }
    public interface IDkmClrCustomVisualizerObjectProvider { }
}

namespace Microsoft.VisualStudio.Debugger.Metadata
{
    [Flags]
    public enum BindingFlags { Instance = 4, Public = 16, NonPublic = 32 }

    public sealed class Type
    {
        private readonly System.Type _type;
        public Type(System.Type type) { _type = type; }
        public string? FullName => _type.FullName;
        public Assembly Assembly => _type.Assembly;
        public bool IsArray => _type.IsArray;
        public bool IsGenericType => _type.IsGenericType;
        public bool IsValueType => _type.IsValueType;
        public bool IsSealed => _type.IsSealed;
        public int GetArrayRank() => _type.GetArrayRank();
        public Type? GetElementType() => _type.GetElementType() is System.Type element ? new Type(element) : null;
        public Type GetGenericTypeDefinition() => new Type(_type.GetGenericTypeDefinition());
        public Type[] GetGenericArguments() => Array.ConvertAll(_type.GetGenericArguments(), type => new Type(type));
        public FieldInfo? GetField(string name, BindingFlags flags) => _type.GetField(name, (System.Reflection.BindingFlags)flags);
    }
}

namespace Microsoft.VisualStudio.Debugger.Clr
{
    public enum DkmClrCustomVisualizerAssemblyLocation { Debuggee, Extension }
    public sealed class DkmClrType
    {
        public DkmClrType(System.Type type) { Metadata = new Metadata.Type(type); }
        public Metadata.Type Metadata { get; }
        public Metadata.Type GetLmrType() => Metadata;
    }

    public sealed class DkmClrValue
    {
        private readonly object? _value;
        public DkmClrValue(object? value) { _value = value; Type = value == null ? null : new DkmClrType(value.GetType()); }
        public DkmClrType? Type { get; set; }
        public bool IsNull => _value == null;
        public object? HostObjectValue => _value;
        public ReadOnlyCollection<int>? ArrayDimensions => _value is Array array ? Array.AsReadOnly(new[] { array.Length }) : null;
        public ReadOnlyCollection<int>? ArrayLowerBounds => _value is Array array ? Array.AsReadOnly(new[] { array.GetLowerBound(0) }) : null;
        public DkmEvaluationAsyncResult Evaluation { get; set; }
        public int CloseCount { get; private set; }
        public DkmClrValue GetArrayElement(int[] indices, DkmInspectionContext context) => new DkmClrValue(((Array)_value!).GetValue(indices));
        public DkmClrValue GetMemberValue(string name, int kind, object? unused, DkmInspectionContext context)
        {
            var field = _value!.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            return new DkmClrValue(field!.GetValue(_value));
        }
        public void GetResult(DkmWorkList workList, DkmClrType? declaredType, object? customTypeInfo, DkmInspectionContext context,
            ReadOnlyCollection<string>? formatSpecifiers, string name, string? fullName, DkmCompletionRoutine<DkmEvaluationAsyncResult> completion)
        {
            completion(Evaluation);
        }
        public void Close() { CloseCount++; }
    }
}

namespace Microsoft.VisualStudio.Debugger.Evaluation.ClrCompilation
{
    public sealed class DkmClrCustomTypeInfo { }
}

namespace Microsoft.VisualStudio.Debugger.Evaluation
{
    public sealed class DkmInspectionContext { }

    public class DkmEvaluationResult
    {
        internal DkmDataItem? DataItem { get; set; }
        public int CloseCount { get; private set; }
        public DkmGetChildrenAsyncResult Children { get; set; } = new DkmGetChildrenAsyncResult(Array.Empty<DkmEvaluationResult>(), new DkmEvaluationResultEnumContext());
        public int GetChildrenCount { get; private set; }
        public DkmInspectionContext? RequestedContext { get; private set; }
        public string? UnderlyingString { get; set; }
        public T? GetDataItem<T>() where T : DkmDataItem => DataItem as T;
        public void Close() { CloseCount++; DataItem?.Close(); }
        public void GetChildren(DkmWorkList workList, int count, DkmInspectionContext context, DkmCompletionRoutine<DkmGetChildrenAsyncResult> completion)
        {
            GetChildrenCount++;
            RequestedContext = context;
            completion(Children);
        }
        public string? GetUnderlyingString() => UnderlyingString;
    }

    public sealed class DkmSuccessEvaluationResult : DkmEvaluationResult
    {
        public DkmInspectionContext InspectionContext { get; set; } = new DkmInspectionContext();
        public object StackFrame { get; set; } = new object();
        public string Name { get; set; } = "image";
        public string? FullName { get; set; }
        public object Flags { get; set; } = new object();
        public string? Value { get; set; }
        public string? EditableValue { get; set; }
        public string? Type { get; set; }
        public object Category { get; set; } = new object();
        public object Access { get; set; } = new object();
        public object StorageType { get; set; } = new object();
        public object TypeModifierFlags { get; set; } = new object();
        public object Address { get; set; } = new object();
        public ReadOnlyCollection<DkmCustomUIVisualizerInfo>? CustomUIVisualizers { get; set; }
        public object ExternalModules { get; set; } = new object();
        public string? RefreshButtonText { get; set; }
        public DkmClrValue? ClrValue { get; set; }
        public Exception? ClrValueError { get; set; }
        public int ClrValueReads { get; private set; }
        public DkmClrValue? GetClrValue()
        {
            ClrValueReads++;
            if (ClrValueError != null) throw ClrValueError;
            return ClrValue;
        }
        public static DkmSuccessEvaluationResult Create(DkmInspectionContext context, object frame, string name, string? fullName,
            object flags, string? value, string? editableValue, string? type, object category, object access, object storage,
            object modifiers, object address, ReadOnlyCollection<DkmCustomUIVisualizerInfo>? visualizers, object modules,
            string? refresh, DkmDataItem item)
        {
            return new DkmSuccessEvaluationResult
            {
                InspectionContext = context, StackFrame = frame, Name = name, FullName = fullName, Flags = flags, Value = value,
                EditableValue = editableValue, Type = type, Category = category, Access = access, StorageType = storage,
                TypeModifierFlags = modifiers, Address = address, CustomUIVisualizers = visualizers, ExternalModules = modules,
                RefreshButtonText = refresh, DataItem = item
            };
        }
        public void ResolveAssembly(string name, out string? path, out ReadOnlyCollection<byte>? bytes) { path = null; bytes = null; }
        public void CreateDebuggeeSideVisualizerObject(uint index, out string? type, out string? stack, out string? message) { type = stack = message = null; }
        public bool DestroyDebuggeeSideVisualizerObject() => true;
        public ReadOnlyCollection<byte>? GetDataFromDebuggeeSideVisualizer(out string? type, out string? stack, out string? message) { type = stack = message = null; return null; }
        public ReadOnlyCollection<byte>? TransferDataToDebuggeeSideVisualizer(byte[]? data, out string? type, out string? stack, out string? message) { type = stack = message = null; return null; }
        public void CreateReplacementObjectOnDebuggeeSideVisualizer(byte[]? data, out string? type, out string? stack, out string? message) { type = stack = message = null; }
    }

    public sealed class DkmCustomUIVisualizerInfo
    {
        public uint Id { get; private set; }
        public string MenuName { get; private set; } = "";
        public string Description { get; private set; } = "";
        public string Metric { get; private set; } = "";
        public string UISideVisualizerTypeName { get; private set; } = "";
        public string UISideVisualizerAssemblyName { get; private set; } = "";
        public DkmClrCustomVisualizerAssemblyLocation UISideVisualizerAssemblyLocation { get; private set; }
        public string DebuggeeSideVisualizerTypeName { get; private set; } = "";
        public string DebuggeeSideVisualizerAssemblyName { get; private set; } = "";
        public Guid ExtensionPartId { get; private set; }
        public static DkmCustomUIVisualizerInfo Create(uint id, string menu, string description, string metric, string uiType, string uiAssembly,
            DkmClrCustomVisualizerAssemblyLocation location, string sourceType, string sourceAssembly, Guid extensionPartId = default)
        {
            return new DkmCustomUIVisualizerInfo
            {
                Id = id, MenuName = menu, Description = description, Metric = metric, UISideVisualizerTypeName = uiType,
                UISideVisualizerAssemblyName = uiAssembly, UISideVisualizerAssemblyLocation = location,
                DebuggeeSideVisualizerTypeName = sourceType, DebuggeeSideVisualizerAssemblyName = sourceAssembly, ExtensionPartId = extensionPartId
            };
        }
    }

    public sealed class DkmEvaluationResultEnumContext
    {
        public DkmInspectionContext InspectionContext { get; set; } = new DkmInspectionContext();
        public DkmEvaluationEnumAsyncResult Page { get; set; }
        public int StartIndex { get; private set; }
        public int Count { get; private set; }
        public Action? BeforeCompletion { get; set; }
        public void GetItems(DkmWorkList workList, int start, int count, DkmCompletionRoutine<DkmEvaluationEnumAsyncResult> completion)
        {
            StartIndex = start;
            Count = count;
            BeforeCompletion?.Invoke();
            completion(Page);
        }
    }

    public readonly struct DkmEvaluationAsyncResult
    {
        public DkmEvaluationAsyncResult(DkmEvaluationResult result, int error = 0) { Result = result; ErrorCode = error; }
        public DkmEvaluationResult Result { get; }
        public int ErrorCode { get; }
    }
    public readonly struct DkmGetChildrenAsyncResult
    {
        public DkmGetChildrenAsyncResult(DkmEvaluationResult[] children, DkmEvaluationResultEnumContext context, int error = 0)
        {
            InitialChildren = children; EnumContext = context; ErrorCode = error;
        }
        public DkmEvaluationResult[] InitialChildren { get; }
        public DkmEvaluationResultEnumContext EnumContext { get; }
        public int ErrorCode { get; }
    }
    public readonly struct DkmEvaluationEnumAsyncResult
    {
        public DkmEvaluationEnumAsyncResult(DkmEvaluationResult[] items, int error = 0) { Items = items; ErrorCode = error; }
        public DkmEvaluationResult[] Items { get; }
        public int ErrorCode { get; }
    }
}
