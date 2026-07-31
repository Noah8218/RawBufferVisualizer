using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.VisualStudio.Shell;
using RawBufferVisualizer.VisualStudio;
using RawBufferVisualizer.VisualStudio.ObjectSource;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    internal sealed class AutomaticVisionScanResult
    {
        public string FrameDisplayName { get; set; } = string.Empty;
        public int LocalExpressionCount { get; set; }
        public int ArgumentExpressionCount { get; set; }
        public int DuplicateExpressionCount { get; set; }
        public int SkippedCollectionRootCount { get; set; }
        public List<AutomaticVisionInspection> Inspections { get; } = new List<AutomaticVisionInspection>();
        public List<AutomaticCollectionScanSummary> CollectionSummaries { get; } =
            new List<AutomaticCollectionScanSummary>();

        public AutomaticCollectionScanSummary? FindCollectionSummary(string rootExpression)
        {
            if (string.IsNullOrWhiteSpace(rootExpression))
            {
                return null;
            }

            for (var index = 0; index < CollectionSummaries.Count; index++)
            {
                if (string.Equals(
                    CollectionSummaries[index].RootExpression,
                    rootExpression,
                    StringComparison.Ordinal))
                {
                    return CollectionSummaries[index];
                }
            }

            return null;
        }
    }

    internal sealed class AutomaticCollectionScanSummary
    {
        public string RootExpression { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public int ScheduledCount { get; set; }
        public int TruncatedCount { get; set; }
        public string ScanError { get; set; } = string.Empty;
        public int OpenedCount { get; set; }
        public int MappingCount { get; set; }
        public int FailedCount { get; set; }
    }

    internal sealed class AutomaticVisionInspection
    {
        public string RootExpression { get; set; } = string.Empty;
        public string RuntimeTypeName { get; set; } = string.Empty;
        public string AssemblyName { get; set; } = string.Empty;
        public List<VisualizerMemberInventoryItem> Inventory { get; set; } = new List<VisualizerMemberInventoryItem>();
        public VisionMemberInferenceResult Inference { get; set; } = new VisionMemberInferenceResult();
        public TypeMapping? Mapping { get; set; }
        public bool UsesSavedMapping { get; set; }
        public AutomaticKnownImageKind KnownImageKind { get; set; }
        public string CollectionRootExpression { get; set; } = string.Empty;
        public string PreflightError { get; set; } = string.Empty;

        public string StableKey
        {
            get { return "automatic-vision-inspector:" + RootExpression; }
        }

        public TypeMapping CreateTransientMapping()
        {
            var mapping = new TypeMapping
            {
                TypeName = RuntimeTypeName,
                AssemblyName = AssemblyName,
                Members = Inference.Members,
                ByteOrder = "LittleEndian"
            };

            if (!string.IsNullOrWhiteSpace(Inference.Members.PixelFormat) && Inference.PixelFormat.HasValue)
            {
                var item = FindInventoryItem(Inference.Members.PixelFormat);
                if (item != null && !string.IsNullOrWhiteSpace(item.SampleValue))
                {
                    var rawValue = item.SampleValue.Trim();
                    var normalized = NormalizeEnumValue(rawValue);
                    mapping.PixelFormatMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [rawValue] = Inference.PixelFormat.Value.ToString(),
                        [normalized] = Inference.PixelFormat.Value.ToString()
                    };
                }
            }

            return mapping;
        }

        public string GetDataTypeName()
        {
            var memberName = Mapping == null ? Inference.Members.Data : Mapping.Members.Data;
            var item = FindInventoryItem(memberName);
            return item == null ? string.Empty : item.TypeName;
        }

        public string GetMembersSummary()
        {
            if (KnownImageKind == AutomaticKnownImageKind.OpenCvSharpMat)
            {
                return "Data=Data\nWidth=Cols, Height=Rows\nStride=Step(), Format=Depth()/Channels()";
            }

            if (KnownImageKind == AutomaticKnownImageKind.EmguCvMat)
            {
                return "Data=DataPointer\nWidth=Cols, Height=Rows\nStride=Step, Format=Depth/NumberOfChannels";
            }

            var members = Mapping == null ? Inference.Members : Mapping.Members;
            return string.Format(
                CultureInfo.InvariantCulture,
                "Data={0}\nWidth={1}, Height={2}\nStride={3}, Format={4}",
                members.Data ?? "?",
                members.Width ?? "?",
                members.Height ?? "?",
                members.Stride ?? "(minimum)",
                members.PixelFormat ?? "(unknown)");
        }

        private VisualizerMemberInventoryItem? FindInventoryItem(string? memberName)
        {
            if (string.IsNullOrWhiteSpace(memberName))
            {
                return null;
            }

            for (var i = 0; i < Inventory.Count; i++)
            {
                if (string.Equals(Inventory[i].Name, memberName, StringComparison.Ordinal))
                {
                    return Inventory[i];
                }
            }

            return null;
        }

        private static string NormalizeEnumValue(string value)
        {
            var dot = value.LastIndexOf('.');
            return dot >= 0 && dot + 1 < value.Length ? value.Substring(dot + 1) : value;
        }
    }

    internal sealed class AutomaticVisionInspector
    {
        private const int MaxMembersPerLocal = 128;
        private const int MaxNestedMembersPerLocal = 64;
        private const int CollectionExpressionTimeoutMilliseconds = 500;

        private sealed class AutomaticCollectionScanBudget
        {
            public int RemainingItems { get; set; } =
                AutomaticImageCollectionPolicy.MaximumItemsPerScan;

            public int RemainingRoots { get; set; } =
                AutomaticImageCollectionPolicy.MaximumCollectionRootsPerScan;
        }

        public AutomaticVisionScanResult Scan(EnvDTE.Debugger debugger)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return Scan(debugger, false);
        }

        public AutomaticVisionScanResult Scan(
            EnvDTE.Debugger debugger,
            bool includeImageCollections)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var result = new AutomaticVisionScanResult();
            if (debugger == null)
            {
                return result;
            }

            EnvDTE.StackFrame? frame;
            try
            {
                frame = debugger.CurrentStackFrame;
            }
            catch
            {
                return result;
            }

            if (frame == null)
            {
                return result;
            }

            result.FrameDisplayName = GetFrameDisplayName(frame);
            var seenExpressions = new HashSet<string>(StringComparer.Ordinal);
            var collectionBudget = new AutomaticCollectionScanBudget();
            EnvDTE.Expressions? locals = null;
            try
            {
                locals = frame.Locals;
            }
            catch
            {
                // A debugger engine may temporarily withhold one expression collection.
            }

            if (locals != null)
            {
                result.LocalExpressionCount = GetExpressionCount(locals);
                ScanExpressions(
                    debugger,
                    result,
                    locals,
                    result.LocalExpressionCount,
                    seenExpressions,
                    includeImageCollections,
                    collectionBudget);
            }

            EnvDTE.Expressions? arguments = null;
            try
            {
                arguments = frame.Arguments;
            }
            catch
            {
                // Locals can still be useful when argument enumeration is unavailable.
            }

            if (arguments != null)
            {
                result.ArgumentExpressionCount = GetExpressionCount(arguments);
                ScanExpressions(
                    debugger,
                    result,
                    arguments,
                    result.ArgumentExpressionCount,
                    seenExpressions,
                    includeImageCollections,
                    collectionBudget);
            }

            return result;
        }

        private static void ScanExpressions(
            EnvDTE.Debugger debugger,
            AutomaticVisionScanResult result,
            EnvDTE.Expressions expressions,
            int expressionCount,
            HashSet<string> seenExpressions,
            bool includeImageCollections,
            AutomaticCollectionScanBudget collectionBudget)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            for (var i = 1; i <= expressionCount; i++)
            {
                EnvDTE.Expression expression;
                try
                {
                    expression = expressions.Item(i);
                }
                catch
                {
                    continue;
                }

                if (expression == null || !IsValid(expression))
                {
                    continue;
                }

                var rootExpression = ReadExpressionName(expression);
                var runtimeTypeName = ReadExpressionType(expression);
                if (string.IsNullOrWhiteSpace(rootExpression) || string.IsNullOrWhiteSpace(runtimeTypeName))
                {
                    continue;
                }

                if (!seenExpressions.Add(rootExpression))
                {
                    result.DuplicateExpressionCount++;
                    continue;
                }

                AutomaticImageCollectionDescriptor collectionDescriptor;
                if (AutomaticImageCollectionPolicy.TryDescribe(
                    runtimeTypeName,
                    out collectionDescriptor))
                {
                    if (includeImageCollections)
                    {
                        ScanKnownImageCollection(
                            debugger,
                            result,
                            expression,
                            rootExpression,
                            runtimeTypeName,
                            collectionDescriptor,
                            collectionBudget);
                    }

                    continue;
                }

                if (ShouldSkipRootType(runtimeTypeName))
                {
                    continue;
                }

                var knownImageKind = KnownImageType.GetAutomaticCaptureKind(runtimeTypeName);
                if (KnownImageType.UsesRegisteredVisualizerPath(runtimeTypeName)
                    && knownImageKind == AutomaticKnownImageKind.None)
                {
                    RawBufferVisualizerPackageLog.Write(
                        "Automatic scan skipped registered visualizer type " + rootExpression + " (" + runtimeTypeName + ")");
                    continue;
                }

                if (KnownImageType.IsMetadataOnlyType(runtimeTypeName))
                {
                    RawBufferVisualizerPackageLog.Write(
                        "Automatic scan skipped metadata-only type " + rootExpression + " (" + runtimeTypeName + ")");
                    continue;
                }

                RawBufferVisualizerPackageLog.Write(
                    "Automatic scan inspecting " + rootExpression + " (" + runtimeTypeName + ")");
                if (knownImageKind != AutomaticKnownImageKind.None)
                {
                    if (IsNullOrUnavailable(expression))
                    {
                        RawBufferVisualizerPackageLog.Write(
                            "Automatic scan skipped uninitialized registered image " + rootExpression);
                        continue;
                    }

                    result.Inspections.Add(new AutomaticVisionInspection
                    {
                        RootExpression = rootExpression,
                        RuntimeTypeName = runtimeTypeName,
                        KnownImageKind = knownImageKind
                    });
                    continue;
                }

                var inventory = BuildInventory(expression);
                var mapping = TypeMappingStore.Default.FindMappingByTypeNameOnly(runtimeTypeName);
                var inference = VisionMemberInference.Infer(inventory, runtimeTypeName);
                if (mapping == null && inference.ConfidenceScore < 40)
                {
                    continue;
                }

                result.Inspections.Add(new AutomaticVisionInspection
                {
                    RootExpression = rootExpression,
                    RuntimeTypeName = runtimeTypeName,
                    Inventory = inventory,
                    Inference = inference,
                    Mapping = mapping,
                    UsesSavedMapping = mapping != null
                });
            }
        }

        private static void ScanKnownImageCollection(
            EnvDTE.Debugger debugger,
            AutomaticVisionScanResult result,
            EnvDTE.Expression expression,
            string rootExpression,
            string runtimeTypeName,
            AutomaticImageCollectionDescriptor descriptor,
            AutomaticCollectionScanBudget budget)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (budget.RemainingRoots <= 0)
            {
                result.SkippedCollectionRootCount++;
                return;
            }

            budget.RemainingRoots--;
            var summary = new AutomaticCollectionScanSummary
            {
                RootExpression = rootExpression
            };
            result.CollectionSummaries.Add(summary);

            if (IsNullOrUnavailable(expression))
            {
                AddCollectionPreflightFailure(
                    result,
                    summary,
                    rootExpression,
                    runtimeTypeName,
                    "The image collection is null or unavailable.");
                return;
            }

            int totalCount;
            string countError;
            if (!TryReadCollectionCount(
                debugger,
                rootExpression,
                descriptor.Kind,
                out totalCount,
                out countError))
            {
                AddCollectionPreflightFailure(
                    result,
                    summary,
                    rootExpression,
                    runtimeTypeName,
                    countError);
                return;
            }

            summary.TotalCount = totalCount;
            var scheduledCount = AutomaticImageCollectionPolicy.GetScheduledItemCount(
                totalCount,
                budget.RemainingItems);
            summary.ScheduledCount = scheduledCount;
            summary.TruncatedCount = Math.Max(0, totalCount - scheduledCount);
            budget.RemainingItems -= scheduledCount;

            var knownImageKind = KnownImageType.GetAutomaticCaptureKind(
                descriptor.ElementTypeName);
            if (knownImageKind == AutomaticKnownImageKind.None)
            {
                AddCollectionPreflightFailure(
                    result,
                    summary,
                    rootExpression,
                    runtimeTypeName,
                    "The collection element type is not supported by the automatic capture path.");
                return;
            }

            RawBufferVisualizerPackageLog.Write(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Automatic scan expanding collection {0} ({1}); {2}/{3} item(s) scheduled",
                    rootExpression,
                    runtimeTypeName,
                    scheduledCount,
                    totalCount));
            for (var index = 0; index < scheduledCount; index++)
            {
                result.Inspections.Add(new AutomaticVisionInspection
                {
                    RootExpression = AutomaticImageCollectionPolicy.CreateElementExpression(
                        rootExpression,
                        index),
                    RuntimeTypeName = descriptor.ElementTypeName,
                    KnownImageKind = knownImageKind,
                    CollectionRootExpression = rootExpression
                });
            }
        }

        private static void AddCollectionPreflightFailure(
            AutomaticVisionScanResult result,
            AutomaticCollectionScanSummary summary,
            string rootExpression,
            string runtimeTypeName,
            string error)
        {
            summary.ScanError = error;
            result.Inspections.Add(new AutomaticVisionInspection
            {
                RootExpression = rootExpression,
                RuntimeTypeName = runtimeTypeName,
                CollectionRootExpression = rootExpression,
                PreflightError = error
            });
            RawBufferVisualizerPackageLog.Write(
                "Automatic collection scan failed " + rootExpression + ": " + error);
        }

#pragma warning disable VSTHRD010
        private static bool TryReadCollectionCount(
            EnvDTE.Debugger debugger,
            string rootExpression,
            AutomaticImageCollectionKind collectionKind,
            out int count,
            out string error)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            count = 0;
            error = string.Empty;
            var memberName = collectionKind == AutomaticImageCollectionKind.OneDimensionalArray
                ? "Length"
                : "Count";
            var countExpression = rootExpression + "." + memberName;
            try
            {
                var expression = debugger.GetExpression(
                    countExpression,
                    true,
                    CollectionExpressionTimeoutMilliseconds);
                if (expression == null || !expression.IsValidValue)
                {
                    error = "Debugger could not evaluate " + countExpression + ".";
                    return false;
                }

                var value = ReadExpressionValue(expression).Trim().Trim('{', '}');
                var equalsIndex = value.LastIndexOf('=');
                if (equalsIndex >= 0 && equalsIndex + 1 < value.Length)
                {
                    value = value.Substring(equalsIndex + 1).Trim();
                }

                var separatorIndex = value.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
                if (separatorIndex >= 0)
                {
                    value = value.Substring(0, separatorIndex);
                }

                if (!int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out count)
                    || count < 0)
                {
                    error = countExpression + " did not return a non-negative integer.";
                    count = 0;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = "Debugger evaluation failed for " + countExpression + ": " + ex.Message;
                return false;
            }
        }
#pragma warning restore VSTHRD010

        private static int GetExpressionCount(EnvDTE.Expressions expressions)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                return Math.Max(0, expressions.Count);
            }
            catch
            {
                return 0;
            }
        }

        private static bool ShouldSkipRootType(string runtimeTypeName)
        {
            var typeName = runtimeTypeName.Trim();
            if (typeName.EndsWith("[]", StringComparison.Ordinal)
                || typeName.StartsWith("System.Collections.", StringComparison.Ordinal)
                || typeName.StartsWith("System.Collections.Generic.", StringComparison.Ordinal))
            {
                return true;
            }

            switch (typeName)
            {
                case "bool":
                case "byte":
                case "sbyte":
                case "short":
                case "ushort":
                case "int":
                case "uint":
                case "long":
                case "ulong":
                case "float":
                case "double":
                case "decimal":
                case "char":
                case "string":
                case "System.Boolean":
                case "System.Byte":
                case "System.SByte":
                case "System.Int16":
                case "System.UInt16":
                case "System.Int32":
                case "System.UInt32":
                case "System.Int64":
                case "System.UInt64":
                case "System.Single":
                case "System.Double":
                case "System.Decimal":
                case "System.Char":
                case "System.String":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsNullOrUnavailable(EnvDTE.Expression expression)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var value = ReadExpressionValue(expression).Trim();
            return string.Equals(value, "null", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Nothing", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "<unreadable>", StringComparison.OrdinalIgnoreCase)
                || value.IndexOf("not available", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("not exist", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static List<VisualizerMemberInventoryItem> BuildInventory(EnvDTE.Expression expression)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var inventory = new List<VisualizerMemberInventoryItem>();
            var directMembers = GetDataMembers(expression);
            if (directMembers == null)
            {
                return inventory;
            }

            for (var i = 1; i <= directMembers.Count && inventory.Count < MaxMembersPerLocal; i++)
            {
                EnvDTE.Expression member;
                try
                {
                    member = directMembers.Item(i);
                }
                catch
                {
                    continue;
                }

                if (member == null)
                {
                    continue;
                }

                var directItem = CreateInventoryItem(member, null);
                if (directItem == null)
                {
                    continue;
                }

                inventory.Add(directItem);
                if (!ShouldInspectNested(directItem) || !IsValid(member))
                {
                    continue;
                }

                var nestedMembers = GetDataMembers(member);
                if (nestedMembers == null)
                {
                    continue;
                }

                var nestedCount = 0;
                for (var n = 1;
                     n <= nestedMembers.Count
                         && nestedCount < MaxNestedMembersPerLocal
                         && inventory.Count < MaxMembersPerLocal;
                     n++)
                {
                    EnvDTE.Expression nestedMember;
                    try
                    {
                        nestedMember = nestedMembers.Item(n);
                    }
                    catch
                    {
                        continue;
                    }

                    var nestedItem = CreateInventoryItem(nestedMember, directItem.Name);
                    if (nestedItem != null)
                    {
                        inventory.Add(nestedItem);
                        nestedCount++;
                    }
                }
            }

            return inventory;
        }

        private static VisualizerMemberInventoryItem? CreateInventoryItem(EnvDTE.Expression expression, string? prefix)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (expression == null)
            {
                return null;
            }

            var name = ReadExpressionName(expression);
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            name = GetLeafName(name);
            if (!IsSupportedMemberSegment(name))
            {
                return null;
            }

            var typeName = ReadExpressionType(expression);
            var value = ReadExpressionValue(expression);
            if (value.Length > 64)
            {
                value = value.Substring(0, 64) + "...";
            }

            var item = new VisualizerMemberInventoryItem
            {
                Name = string.IsNullOrWhiteSpace(prefix) ? name : prefix + "." + name,
                Kind = "Member",
                TypeName = ShortenTypeName(typeName),
                SampleValue = value
            };

            if (LooksLikePixelFormat(item.Name, item.TypeName))
            {
                var enumValue = NormalizeEnumValue(value);
                if (!string.IsNullOrWhiteSpace(enumValue)
                    && !string.Equals(enumValue, "<unreadable>", StringComparison.OrdinalIgnoreCase))
                {
                    item.EnumValues = new List<string> { enumValue };
                }
            }

            return item;
        }

        private static bool IsSupportedMemberSegment(string name)
        {
            if (string.IsNullOrWhiteSpace(name)
                || !(char.IsLetter(name[0]) || name[0] == '_'))
            {
                return false;
            }

            for (var i = 1; i < name.Length; i++)
            {
                if (!(char.IsLetterOrDigit(name[i]) || name[i] == '_'))
                {
                    return false;
                }
            }

            return true;
        }

        private static EnvDTE.Expressions? GetDataMembers(EnvDTE.Expression expression)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                return expression.DataMembers;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsValid(EnvDTE.Expression expression)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                return expression.IsValidValue;
            }
            catch
            {
                return false;
            }
        }

        private static bool ShouldInspectNested(VisualizerMemberInventoryItem item)
        {
            var type = item.TypeName;
            if (string.IsNullOrWhiteSpace(type)
                || type.EndsWith("[]", StringComparison.Ordinal)
                || type == "String"
                || type == "IntPtr"
                || type == "UIntPtr")
            {
                return false;
            }

            switch (type)
            {
                case "Byte":
                case "SByte":
                case "Int16":
                case "UInt16":
                case "Int32":
                case "UInt32":
                case "Int64":
                case "UInt64":
                case "Single":
                case "Double":
                case "Decimal":
                case "Boolean":
                case "Char":
                    return false;
                default:
                    return !LooksLikePixelFormat(item.Name, item.TypeName);
            }
        }

        private static string GetFrameDisplayName(EnvDTE.StackFrame frame)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string functionName;
            try
            {
                functionName = frame.FunctionName;
            }
            catch
            {
                functionName = string.Empty;
            }

            return string.IsNullOrWhiteSpace(functionName) ? "Current stack frame" : functionName;
        }

        private static bool LooksLikePixelFormat(string name, string typeName)
        {
            return name.IndexOf("format", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("pixeltype", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("format", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("pixeltype", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string NormalizeEnumValue(string value)
        {
            var text = value.Trim().Trim('{', '}');
            var equals = text.LastIndexOf('=');
            if (equals >= 0 && equals + 1 < text.Length)
            {
                text = text.Substring(equals + 1).Trim();
            }

            var dot = text.LastIndexOf('.');
            return dot >= 0 && dot + 1 < text.Length ? text.Substring(dot + 1) : text;
        }

        private static string GetLeafName(string name)
        {
            var dot = name.LastIndexOf('.');
            return dot >= 0 && dot + 1 < name.Length ? name.Substring(dot + 1) : name;
        }

        private static string ShortenTypeName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return string.Empty;
            }

            var arraySuffix = string.Empty;
            var result = typeName.Trim();
            if (result.EndsWith("[]", StringComparison.Ordinal))
            {
                arraySuffix = "[]";
                result = result.Substring(0, result.Length - 2);
            }

            var dot = result.LastIndexOf('.');
            if (dot >= 0 && dot + 1 < result.Length)
            {
                result = result.Substring(dot + 1);
            }

            return result + arraySuffix;
        }

        private static string ReadExpressionName(EnvDTE.Expression expression)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                return expression.Name ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ReadExpressionType(EnvDTE.Expression expression)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                return expression.Type ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ReadExpressionValue(EnvDTE.Expression expression)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                return expression.Value ?? string.Empty;
            }
            catch
            {
                return "<unreadable>";
            }
        }
    }
}
