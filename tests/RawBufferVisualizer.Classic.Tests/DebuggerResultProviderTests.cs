using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Clr;
using Microsoft.VisualStudio.Debugger.Evaluation;
using RawBufferVisualizer.VisualStudio.Classic;
using RawBufferVisualizer.VisualStudio.Debugger;

internal static class DebuggerResultProviderTests
{
    private static readonly ImageVisualizerResultProvider Provider = new ImageVisualizerResultProvider();
    private static int _checks;

    internal static void RunAll()
    {
        var contracts = new[]
        {
            new[] { "RawBufferVisualizer.Sdk.RawBufferSnapshot", "RawBufferVisualizer.Sdk", "RawBufferSnapshotVisualizerObjectSource" },
            new[] { "RawBufferVisualizer.Sdk.RawBufferView", "RawBufferVisualizer.Sdk", "RawBufferViewVisualizerObjectSource" },
            new[] { "System.Drawing.Bitmap", "System.Drawing", "BitmapVisualizerObjectSource" },
            new[] { "System.Drawing.Bitmap", "System.Drawing.Common", "BitmapVisualizerObjectSource" },
            new[] { "OpenCvSharp.Mat", "OpenCvSharp", "OpenCvSharpMatVisualizerObjectSource" },
            new[] { "OpenCvSharp.Mat`1", "OpenCvSharp", "OpenCvSharpMatVisualizerObjectSource" },
            new[] { "Emgu.CV.Mat", "Emgu.CV", "EmguCvMatVisualizerObjectSource" },
            new[] { "Emgu.CV.Mat", "Emgu.CV.World", "EmguCvMatVisualizerObjectSource" },
            new[] { "Emgu.CV.Mat", "Emgu.CV.World.NetStandard", "EmguCvMatVisualizerObjectSource" },
            new[] { "Emgu.CV.Mat", "Emgu.CV.Platform.NetStandard", "EmguCvMatVisualizerObjectSource" },
            new[] { "Cressem.ImageModel.ImagePtr", "Cressem.ImageModel", "ImagePtrVisualizerObjectSource" }
        };
        var registered = ImageVisualizerTypePolicy.Registrations.Select(item => (item.TypeName, item.AssemblyName, item.ObjectSource)).ToArray();
        var expected = new HashSet<(string, string, string)>(contracts.Select(item => (item[0], item[1], item[2])));
        Require(registered.Length == expected.Count && expected.SetEquals(registered),
            "Every registered type/assembly must have a routing case. Keep established identities when updating the matrix.");
        foreach (var contract in contracts)
        {
            var image = Activator.CreateInstance(CreateType(contract[0], contract[1]))!;
            foreach (var entry in new[] { "root", "children", "items" })
            {
                AssertCandidate(Route(Result(image, "owners[0].Storage.Image"), entry), contract[2], false);
                var array = Array.CreateInstance(image.GetType(), 1);
                array.SetValue(image, 0);
                AssertCandidate(Route(Result(array, "owner.Images"), entry), "ImageCollectionVisualizerObjectSource", true);
                foreach (var definition in new[] { typeof(List<>), typeof(Dictionary<,>), typeof(ConcurrentDictionary<,>) })
                {
                    var type = definition.MakeGenericType(definition == typeof(List<>) ? new[] { image.GetType() } : new[] { typeof(string), image.GetType() });
                    AssertCandidate(Route(Result(Activator.CreateInstance(type)!, "owner.Images"), entry), "ImageCollectionVisualizerObjectSource", true);
                }
                foreach (object collection in new object[]
                {
                    new object[] { null!, image }, new List<object> { image }, new ArrayList { image },
                    new Dictionary<string, object> { ["frame"] = image }, new Hashtable { ["frame"] = image },
                    new ConcurrentDictionary<string, object>(new[] { new KeyValuePair<string, object>("frame", image) })
                })
                    AssertCandidate(Route(Result(collection, "owner.Images"), entry), "ImageCollectionVisualizerObjectSource", true);
            }
        }
        VerifyNestedOwners();
        VerifyPreservationAndLifetime();
        VerifyFailuresAndCancellation();
        VerifyBatchIsolation();
        Console.WriteLine("PASS: " + _checks + " debugger result checks; all " + registered.Length + " registered identities, root/initial-child/paged-item callbacks, typed and object-valued collections, arbitrary owners, preservation and recovery.");
        Console.WriteLine("Boundary: linked production provider with deterministic engine doubles and metadata-only image fixtures; real SDK build/loader and installed-IDE interaction are separate checks.");
    }

    private static void VerifyNestedOwners()
    {
        var image = Activator.CreateInstance(CreateType("OpenCvSharp.Mat", "OpenCvSharp"))!;
        var imageResult = Result(image, "owners[0].Storage.Image");
        var storage = Result(new Storage(), "owners[0].Storage");
        storage.Children = new DkmGetChildrenAsyncResult(new[] { imageResult }, new DkmEvaluationResultEnumContext());
        var owner = Result(new Owner(), "owners[0]");
        owner.Children = new DkmGetChildrenAsyncResult(new[] { storage }, new DkmEvaluationResultEnumContext());
        var array = Result(new[] { new Owner() }, "owners");
        array.Children = new DkmGetChildrenAsyncResult(new[] { owner }, new DkmEvaluationResultEnumContext());
        var first = Children(array).InitialChildren.Single();
        Require(ReferenceEquals(first, owner), "Unregistered parent must keep its original result.");
        var second = Children(first).InitialChildren.Single();
        Require(ReferenceEquals(second, storage), "Arbitrary nested owner must remain expandable without registration.");
        AssertCandidate(Children(second).InitialChildren.Single(), "OpenCvSharpMatVisualizerObjectSource", false);
        Require(imageResult.FullName == "owners[0].Storage.Image", "Nested expression identity changed.");
    }

    private static void VerifyPreservationAndLifetime()
    {
        var image = Activator.CreateInstance(CreateType("OpenCvSharp.Mat", "OpenCvSharp"))!;
        var original = Result(image, "dictionary[\"frame\"].Value.Image");
        var other = Candidate("Other.Source", "Other.Extension", "ClrCustomVisualizerVSHost");
        original.CustomUIVisualizers = Array.AsReadOnly(new[] { Candidate("Old.Source", "RawBufferVisualizer.VisualStudio.ObjectSource", "ClrCustomVisualizerVSHost"), other });
        original.Value = "display";
        original.EditableValue = "edit";
        original.RefreshButtonText = "refresh";
        original.UnderlyingString = "underlying";
        var result = (DkmSuccessEvaluationResult)Route(original, "items");
        AssertCandidate(result, "OpenCvSharpMatVisualizerObjectSource", false);
        Require(result.CustomUIVisualizers!.Count == 2, "Owned candidates must be replaced once, other candidates retained.");
        var retained = result.CustomUIVisualizers[1];
        Require(retained.Id == 1 && retained.MenuName == other.MenuName && retained.Description == other.Description
            && retained.Metric == other.Metric && retained.UISideVisualizerTypeName == other.UISideVisualizerTypeName
            && retained.UISideVisualizerAssemblyName == other.UISideVisualizerAssemblyName
            && retained.UISideVisualizerAssemblyLocation == other.UISideVisualizerAssemblyLocation
            && retained.DebuggeeSideVisualizerTypeName == other.DebuggeeSideVisualizerTypeName
            && retained.DebuggeeSideVisualizerAssemblyName == other.DebuggeeSideVisualizerAssemblyName
            && retained.ExtensionPartId == other.ExtensionPartId, "Other visualizer metadata or invocation index changed.");
        Require(result.Name == original.Name && result.FullName == original.FullName && result.Type == original.Type
            && result.Value == original.Value && result.EditableValue == original.EditableValue && result.RefreshButtonText == original.RefreshButtonText
            && ReferenceEquals(result.Flags, original.Flags) && ReferenceEquals(result.StackFrame, original.StackFrame)
            && ReferenceEquals(result.InspectionContext, original.InspectionContext) && ReferenceEquals(result.Category, original.Category)
            && ReferenceEquals(result.Access, original.Access) && ReferenceEquals(result.StorageType, original.StorageType)
            && ReferenceEquals(result.TypeModifierFlags, original.TypeModifierFlags) && ReferenceEquals(result.Address, original.Address)
            && ReferenceEquals(result.ExternalModules, original.ExternalModules), "Debugger result metadata changed.");
        Require(ReferenceEquals(Provider.GetClrValue(result), original.ClrValue), "Wrapped value must delegate to the original.");
        Require(Provider.GetUnderlyingString(result) == "underlying", "Underlying-string delegation changed.");
        var context = new DkmInspectionContext();
        Provider.GetChildren(result, new DkmWorkList(), 0, context, _ => { });
        Require(original.GetChildrenCount == 1 && ReferenceEquals(original.RequestedContext, context), "Wrapped expansion must delegate once with the requested context.");
        Require(ReferenceEquals(Route(result, "children"), result), "Already routed results must not gain another ownership wrapper.");
        Require(original.CloseCount == 0 && original.ClrValue!.CloseCount == 0, "Routing must not close borrowed result/value.");
        result.Close();
        Require(original.CloseCount == 1 && original.ClrValue!.CloseCount == 0, "Wrapper must release its original once; borrowed CLR value is not independently owned.");
        var foreignHost = Result(image, "image");
        foreignHost.CustomUIVisualizers = Array.AsReadOnly(new[] { Candidate("Other.Source", "Other.Extension", "OtherHost") });
        Require(ReferenceEquals(Route(foreignHost, "children"), foreignHost), "Foreign-host command IDs must be preserved.");
    }

    private static void VerifyFailuresAndCancellation()
    {
        var image = Activator.CreateInstance(CreateType("OpenCvSharp.Mat", "OpenCvSharp"))!;
        foreach (var entry in new[] { "root", "children", "items" })
        {
            foreach (object? ordinary in new object?[] { null, 42, "text", new Owner(), new[] { new Owner() }, new List<Owner>() })
            {
                var original = Result(ordinary, "owner");
                Require(ReferenceEquals(Route(original, entry), original), "Ordinary/null owner must keep the original visualizer.");
            }
            var canceled = Result(image, "image");
            Require(ReferenceEquals(Route(canceled, entry, new DkmWorkList { IsCanceled = true }), canceled), "Canceled work must not decorate images.");
            Require(canceled.ClrValueReads == 0, "Canceled work must not request child CLR values.");
            var unavailable = new DkmSuccessEvaluationResult();
            Require(ReferenceEquals(Route(unavailable, entry), unavailable), "Synthetic nodes without CLR values must survive.");
            AssertCandidate(Route(Result(image, "recovery"), entry), "OpenCvSharpMatVisualizerObjectSource", false);
        }
        foreach (var entry in new[] { "children", "items" })
        {
            var broken = Result(image, "unavailable");
            broken.ClrValueError = new DkmException();
            Require(ReferenceEquals(Route(broken, entry), broken), "A metadata/value failure must retain its original result.");
            var synthetic = new DkmEvaluationResult();
            Require(ReferenceEquals(Route(synthetic, entry), synthetic), "Failed/non-value nodes must not become images.");
        }
        var failedImage = Result(image, "error");
        var parent = new DkmEvaluationResult { Children = new DkmGetChildrenAsyncResult(new[] { failedImage }, new DkmEvaluationResultEnumContext(), -17) };
        Require(Children(parent).ErrorCode == -17 && failedImage.ClrValueReads == 0, "Child errors must be forwarded unchanged without reads.");
        var work = new DkmWorkList();
        var page = new DkmEvaluationResultEnumContext { Page = new DkmEvaluationEnumAsyncResult(new[] { failedImage }, -19) };
        Provider.GetItems(page, work, 512, 64, result => Require(result.ErrorCode == -19, "Page error changed."));
        page.Page = new DkmEvaluationEnumAsyncResult(new[] { failedImage });
        page.BeforeCompletion = () => work.IsCanceled = true;
        Provider.GetItems(page, work, 512, 64, result => Require(ReferenceEquals(result.Items[0], failedImage), "Cancellation before page completion must retain originals."));
        Require(failedImage.ClrValueReads == 0, "Failed/canceled pages must not fetch CLR values.");

        failedImage.ClrValue!.Evaluation = new DkmEvaluationAsyncResult(failedImage, -23);
        Provider.GetResult(failedImage.ClrValue, new DkmWorkList(), null, null, failedImage.InspectionContext, null, "error", "error",
            result => Require(result.ErrorCode == -23 && ReferenceEquals(result.Result, failedImage), "Root evaluation error must be forwarded unchanged."));
        AssertCandidate(Route(Result(image, "recovery"), "root"), "OpenCvSharpMatVisualizerObjectSource", false);
    }

    private static void VerifyBatchIsolation()
    {
        var image = Activator.CreateInstance(CreateType("OpenCvSharp.Mat", "OpenCvSharp"))!;
        var broken = Result(image, "owners[0].Image");
        broken.ClrValueError = new DkmException();
        var healthy = Result(image, "owners[1].Image");
        var ordinary = Result(42, "owners[2].Count");
        var input = new DkmEvaluationResult[] { broken, healthy, ordinary, new DkmEvaluationResult() };
        var page = new DkmEvaluationResultEnumContext { Page = new DkmEvaluationEnumAsyncResult(input) };
        Provider.GetItems(page, new DkmWorkList(), 64, 4, result =>
        {
            Require(result.Items.Length == input.Length && !ReferenceEquals(result.Items, input), "Changed page must own its result array.");
            Require(ReferenceEquals(result.Items[0], broken) && ReferenceEquals(result.Items[2], ordinary)
                && ReferenceEquals(result.Items[3], input[3]), "One unavailable item must not discard or reorder its siblings.");
            AssertCandidate(result.Items[1], "OpenCvSharpMatVisualizerObjectSource", false);
        });
        Require(ReferenceEquals(input[1], healthy) && healthy.CustomUIVisualizers == null, "Routing must not mutate the evaluator's input array/result.");
        page.Page = new DkmEvaluationEnumAsyncResult(Array.Empty<DkmEvaluationResult>());
        Provider.GetItems(page, new DkmWorkList(), 0, 0, result => Require(result.Items.Length == 0, "Empty pages must remain empty."));
        var parent = new DkmEvaluationResult { Children = new DkmGetChildrenAsyncResult(input, page) };
        var expanded = Children(parent);
        Require(ReferenceEquals(expanded.EnumContext, page) && ReferenceEquals(expanded.InitialChildren[0], broken), "Expansion must preserve its context and failed sibling.");
        AssertCandidate(expanded.InitialChildren[1], "OpenCvSharpMatVisualizerObjectSource", false);
    }

    private static DkmEvaluationResult Route(DkmEvaluationResult original, string entry, DkmWorkList? work = null)
    {
        work = work ?? new DkmWorkList();
        DkmEvaluationResult? routed = null;
        int completions = 0;
        if (entry == "root")
        {
            var success = (DkmSuccessEvaluationResult)original;
            var value = success.ClrValue ?? new DkmClrValue(null);
            value.Evaluation = new DkmEvaluationAsyncResult(original);
            Provider.GetResult(value, work, new DkmClrType(typeof(object)), null, success.InspectionContext, null, success.Name, success.FullName,
                result => { routed = result.Result; completions++; });
        }
        else if (entry == "children")
        {
            var context = new DkmEvaluationResultEnumContext();
            var parent = new DkmEvaluationResult { Children = new DkmGetChildrenAsyncResult(new[] { original }, context) };
            Provider.GetChildren(parent, work, 1, new DkmInspectionContext(), result =>
            {
                Require(ReferenceEquals(result.EnumContext, context), "Child enumeration context was replaced.");
                routed = result.InitialChildren.Single(); completions++;
            });
        }
        else
        {
            var context = new DkmEvaluationResultEnumContext { Page = new DkmEvaluationEnumAsyncResult(new[] { original }) };
            Provider.GetItems(context, work, 512, 64, result => { routed = result.Items.Single(); completions++; });
            Require(context.StartIndex == 512 && context.Count == 64, "Requested page changed.");
        }
        Require(completions == 1 && routed != null, "Each request must complete exactly once.");
        return routed!;
    }

    private static DkmGetChildrenAsyncResult Children(DkmEvaluationResult parent)
    {
        DkmGetChildrenAsyncResult output = default;
        Provider.GetChildren(parent, new DkmWorkList(), 16, new DkmInspectionContext(), result => output = result);
        return output;
    }

    private static DkmSuccessEvaluationResult Result(object? image, string expression)
    {
        return new DkmSuccessEvaluationResult { ClrValue = new DkmClrValue(image), FullName = expression, Name = expression, Type = image?.GetType().FullName };
    }

    internal static DkmCustomUIVisualizerInfo GetCandidate(object value, string entry)
    {
        var routed = (DkmSuccessEvaluationResult)Route(Result(value, "owner.Images[0]"), entry);
        return routed.CustomUIVisualizers?.Single()
            ?? throw new InvalidOperationException("Missing " + entry + " visualizer for " + value.GetType());
    }

    private static void AssertCandidate(DkmEvaluationResult result, string source, bool collection)
    {
        var candidates = ((DkmSuccessEvaluationResult)result).CustomUIVisualizers;
        Require(candidates != null && candidates.Count > 0, "Missing image visualizer for " + ((DkmSuccessEvaluationResult)result).FullName);
        var candidate = candidates![0];
        Require(candidate.Id == 0 && candidate.DebuggeeSideVisualizerTypeName == "RawBufferVisualizer.VisualStudio.ObjectSource." + source,
            "Image routed to wrong source or invocation index.");
        Require(candidate.UISideVisualizerAssemblyLocation == DkmClrCustomVisualizerAssemblyLocation.Extension
            && candidate.UISideVisualizerAssemblyName == typeof(RawBufferClassicDebuggerVisualizer).Assembly.FullName,
            "Image must use the adjacent Classic assembly's full identity and extension location.");
        Require(candidate.UISideVisualizerTypeName.EndsWith(collection ? ".ImageCollectionClassicDebuggerVisualizer" : ".RawBufferClassicDebuggerVisualizer"), "Wrong Classic entry.");
    }

    private static DkmCustomUIVisualizerInfo Candidate(string type, string assembly, string metric)
    {
        return DkmCustomUIVisualizerInfo.Create(0, "Other", "Description", metric, "Other.UI", "Other.UI.Assembly",
            DkmClrCustomVisualizerAssemblyLocation.Debuggee, type, assembly, Guid.NewGuid());
    }

    private static Type CreateType(string name, string assembly)
    {
        // Metadata fixtures deliberately contain no native SDK implementation or pixel data.
        var builder = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(assembly), AssemblyBuilderAccess.Run)
            .DefineDynamicModule(assembly).DefineType(name, TypeAttributes.Public | TypeAttributes.Sealed);
        if (name.EndsWith("`1", StringComparison.Ordinal)) builder.DefineGenericParameters("T");
        var type = builder.CreateType();
        return type.IsGenericTypeDefinition ? type.MakeGenericType(typeof(byte)) : type;
    }

    private static void Require(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Owner { }
    private sealed class Storage { }
}
