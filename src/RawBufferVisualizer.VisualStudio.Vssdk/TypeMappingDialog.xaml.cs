using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.VisualStudio.ObjectSource;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    public partial class TypeMappingDialog : Window
    {
        private const string NoneItem = "(none)";

        private static readonly string[] DataNameCandidates = { "Ptr", "Buffer", "Data", "DataPointer", "_ptr", "_buffer", "_data" };
        private static readonly string[] WidthNameCandidates = { "Width", "_width" };
        private static readonly string[] HeightNameCandidates = { "Height", "_height" };
        private static readonly string[] StrideNameCandidates = { "Step", "Stride", "Pitch", "_step", "_stride", "_pitch" };
        private static readonly string[] PixelFormatNameCandidates = { "PixelFormat", "Format", "RawPixelFormat", "PixelType" };
        private static readonly string[] ValidBitsNameCandidates = { "BitDepth", "Depth", "ValidBits", "_bitDepth", "_depth" };
        private static readonly string[] BufferLengthNameCandidates = { "Length", "BufferLength", "Size", "ByteLength", "_length", "_size" };

        private readonly List<VisualizerMemberInventoryItem> _inventory;
        private readonly string _typeName;
        private readonly string _assemblyName;
        private readonly int _debuggeeProcessId;
        private readonly List<string> _enumValueNames = new List<string>();
        private readonly List<ComboBox> _enumValueBoxes = new List<ComboBox>();
        private readonly TypeMappingMembers? _initialMembers;
        private readonly TypeMappingMembers? _suggestedMembers;
        private readonly string _initialByteOrder;
        private readonly Dictionary<string, string> _existingPixelFormatMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _diagnosisCancellation;
        private BufferInterpretationCandidate? _selectedDiagnosisCandidate;
        private bool _applyingDiagnosisCandidate;

        public TypeMappingDialog(
            List<VisualizerMemberInventoryItem> inventory,
            string typeName,
            string assemblyName,
            int debuggeeProcessId,
            TypeMappingMembers? suggestedMembers = null,
            bool pixelFormatOnly = false)
        {
            InitializeComponent();
            Closed += delegate { CancelDiagnosis(); };
            _inventory = inventory ?? new List<VisualizerMemberInventoryItem>();
            _typeName = typeName ?? string.Empty;
            _assemblyName = assemblyName ?? string.Empty;
            _debuggeeProcessId = debuggeeProcessId;
            var existingMapping = TypeMappingStore.Default.FindMapping(_typeName, _assemblyName)
                ?? TypeMappingStore.Default.FindMappingByTypeNameOnly(_typeName);
            _suggestedMembers = suggestedMembers;
            _initialMembers = suggestedMembers ?? existingMapping?.Members;
            _initialByteOrder = existingMapping?.ByteOrder ?? RawByteOrder.LittleEndian.ToString();
            if (existingMapping?.PixelFormatMap != null)
            {
                foreach (var pair in existingMapping.PixelFormatMap)
                {
                    _existingPixelFormatMap[pair.Key] = pair.Value;
                }
            }

            TypeNameText.Text = string.Format(
                CultureInfo.InvariantCulture,
                "Connect buffer members of {0} ({1})",
                string.IsNullOrWhiteSpace(_typeName) ? "unknown type" : _typeName,
                string.IsNullOrWhiteSpace(_assemblyName) ? "unknown assembly" : _assemblyName);
            PopulateRoles();
            ByteOrderBox.ItemsSource = Enum.GetNames(typeof(RawByteOrder));
            ByteOrderBox.SelectedItem = Enum.TryParse(_initialByteOrder, true, out RawByteOrder initialByteOrder)
                ? initialByteOrder.ToString()
                : RawByteOrder.LittleEndian.ToString();
            if (pixelFormatOnly)
            {
                Title = "Confirm Pixel Format";
                AllRolesGrid.Visibility = Visibility.Collapsed;
                CopyTemplateButton.Visibility = Visibility.Collapsed;
                UseSuggestedRolesButton.Visibility = Visibility.Collapsed;
                MappingGuidanceText.Text = "Only the current pixel-format value needs confirmation. "
                    + "Data, dimensions, and stride were inferred and are already selected for the saved mapping.";
            }
            else
            {
                MappingGuidanceText.Text = "Review the inferred roles. Preview reads the current buffer; only Save Mapping persists changes.";
            }
        }

        private void PopulateRoles()
        {
            var names = new List<string>();
            for (var i = 0; i < _inventory.Count; i++)
            {
                names.Add(_inventory[i].Name);
            }

            FillBox(DataBox, names, false);
            FillBox(WidthBox, names, false);
            FillBox(HeightBox, names, false);
            FillBox(StrideBox, names, true);
            FillBox(PixelFormatBox, names, true);
            FillBox(BufferLengthBox, names, true);
            FillBox(ValidBitsBox, names, true);

            ApplyRoleSelections(_initialMembers);
        }

        private void ApplyRoleSelections(TypeMappingMembers? preferredMembers)
        {
            SelectRole(DataBox, FirstAvailable(preferredMembers?.Data, PreselectDataMember()));
            SelectRole(WidthBox, FirstAvailable(preferredMembers?.Width, PreselectByName(WidthNameCandidates, "width", "sizex")));
            SelectRole(HeightBox, FirstAvailable(preferredMembers?.Height, PreselectByName(HeightNameCandidates, "height", "sizey")));
            SelectRole(StrideBox, FirstAvailable(preferredMembers?.Stride, PreselectByName(StrideNameCandidates, "stride", "pitch", "step")));
            SelectRole(PixelFormatBox, FirstAvailable(preferredMembers?.PixelFormat, PreselectPixelFormatMember()));
            SelectRole(ValidBitsBox, FirstAvailable(preferredMembers?.ValidBits, PreselectByName(ValidBitsNameCandidates, "validbits", "bitdepth", "depth")));
            SelectRole(BufferLengthBox, FirstAvailable(preferredMembers?.BufferLength, PreselectByName(BufferLengthNameCandidates, "bufferlength", "bytelength", "length")));
            RebuildEnumMappingRows();
        }

        private void SelectRole(ComboBox box, string? memberName)
        {
            box.SelectedIndex = 0;
            Preselect(box, memberName);
        }

        private string? FirstAvailable(string? preferred, string? fallback)
        {
            return FindInventoryItem(preferred) == null ? fallback : preferred;
        }

        private static void FillBox(ComboBox box, List<string> names, bool includeNone)
        {
            var items = new List<string>();
            if (includeNone)
            {
                items.Add(NoneItem);
            }

            items.AddRange(names);
            box.ItemsSource = items;
            box.SelectedIndex = 0;
        }

        private void Preselect(ComboBox box, string? memberName)
        {
            if (!string.IsNullOrEmpty(memberName))
            {
                box.SelectedItem = memberName;
            }
        }

        private string? PreselectDataMember()
        {
            for (var i = 0; i < _inventory.Count; i++)
            {
                if (IsPointerTypeName(_inventory[i].TypeName))
                {
                    return _inventory[i].Name;
                }
            }

            for (var i = 0; i < _inventory.Count; i++)
            {
                var typeName = _inventory[i].TypeName;
                if (typeName == "Byte[]" || typeName == "UInt16[]" || typeName == "Single[]")
                {
                    return _inventory[i].Name;
                }
            }

            return PreselectByName(DataNameCandidates);
        }

        private string? PreselectPixelFormatMember()
        {
            for (var i = 0; i < _inventory.Count; i++)
            {
                var enumValues = _inventory[i].EnumValues;
                if (enumValues != null && enumValues.Count > 0)
                {
                    return _inventory[i].Name;
                }
            }

            return PreselectByName(PixelFormatNameCandidates, "pixelformat", "pixeltype", "format");
        }

        private string? PreselectByName(string[] exactCandidates, params string[] containsCandidates)
        {
            for (var c = 0; c < exactCandidates.Length; c++)
            {
                for (var i = 0; i < _inventory.Count; i++)
                {
                    if (string.Equals(_inventory[i].Name, exactCandidates[c], StringComparison.OrdinalIgnoreCase))
                    {
                        return _inventory[i].Name;
                    }
                }
            }

            for (var c = 0; c < containsCandidates.Length; c++)
            {
                for (var i = 0; i < _inventory.Count; i++)
                {
                    if (_inventory[i].Name.IndexOf(containsCandidates[c], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return _inventory[i].Name;
                    }
                }
            }

            return null;
        }

        private void PixelFormatBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RebuildEnumMappingRows();
        }

        private void RebuildEnumMappingRows()
        {
            EnumMappingRows.Children.Clear();
            _enumValueNames.Clear();
            _enumValueBoxes.Clear();
            var member = FindInventoryItem(PixelFormatBox.SelectedItem as string);
            if (member == null || member.EnumValues == null || member.EnumValues.Count == 0)
            {
                EnumMappingPanel.Visibility = Visibility.Collapsed;
                return;
            }

            EnumMappingPanel.Visibility = Visibility.Visible;
            for (var i = 0; i < member.EnumValues.Count; i++)
            {
                var valueName = member.EnumValues[i];
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
                var label = new TextBlock
                {
                    Text = valueName,
                    Width = 110,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var box = new ComboBox
                {
                    ItemsSource = Enum.GetNames(typeof(RawPixelFormat)),
                    Tag = valueName
                };
                AutomationProperties.SetAutomationId(box, "PixelFormatMapping_" + GetAutomationToken(valueName));
                AutomationProperties.SetName(box, valueName + " raw pixel format");
                RawPixelFormat parsed;
                string? existingFormat;
                box.SelectedItem = _existingPixelFormatMap.TryGetValue(valueName, out existingFormat)
                    && Enum.TryParse(existingFormat, true, out parsed)
                        ? parsed.ToString()
                        : (Enum.TryParse(valueName, true, out parsed) ? parsed : RawPixelFormat.Mono8).ToString();
                DockPanel.SetDock(label, Dock.Left);
                row.Children.Add(label);
                row.Children.Add(box);
                EnumMappingRows.Children.Add(row);
                _enumValueNames.Add(valueName);
                _enumValueBoxes.Add(box);
            }
        }

        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            PreviewImage.Source = null;
            RawImageDescriptor descriptor;
            VisualizerMemberInventoryItem? dataMember;
            string error;
            if (!TryBuildDescriptor(out descriptor, out dataMember, out error))
            {
                PreviewStatusText.Text = error;
                return;
            }

            var previewRendered = TryRenderPreview(descriptor, dataMember, out var previewError);
            PreviewStatusText.Text = previewRendered
                ? "Preview rendered from live debuggee memory."
                : "Preview unavailable: " + previewError;
            if (previewRendered)
            {
                PreviewImage.BringIntoView();
            }
        }

        private bool TryRenderPreview(
            RawImageDescriptor descriptor,
            VisualizerMemberInventoryItem? dataMember,
            out string error)
        {
            PreviewImage.Source = null;
            if (!TryCreateLiveSource(descriptor, dataMember, false, out var source, out error))
            {
                return false;
            }

            try
            {
                using (source)
                {
                    PreviewImage.Source = RawBufferToolWindowControl.CreateThumbnailSource(source, descriptor);
                }

                if (PreviewImage.Source == null)
                {
                    error = "mapping will be verified on the next scan.";
                    return false;
                }

                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private bool TryCreateLiveSource(
            RawImageDescriptor descriptor,
            VisualizerMemberInventoryItem? dataMember,
            bool includeFullStrideSpan,
            out RawImageSource source,
            out string error)
        {
            source = null!;
            long address;
            if (_debuggeeProcessId <= 0
                || dataMember == null
                || !IsPointerTypeName(dataMember.TypeName)
                || !TryParsePointer(dataMember.SampleValue, out address)
                || address == 0)
            {
                error = "the current mapping does not expose a readable pointer-backed buffer at this breakpoint.";
                return false;
            }

            if (!descriptor.TryGetRequiredByteCount(out var bufferLength))
            {
                error = "the selected descriptor exceeds the supported buffer range.";
                return false;
            }

            var lengthMember = FindInventoryItem(BufferLengthBox.SelectedItem as string);
            if (lengthMember != null)
            {
                if (!long.TryParse(lengthMember.SampleValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLength)
                    || parsedLength < bufferLength)
                {
                    error = "the reported buffer length is smaller than the selected descriptor requires.";
                    return false;
                }

                bufferLength = parsedLength;
            }
            else if (includeFullStrideSpan)
            {
                try
                {
                    bufferLength = Math.Max(bufferLength, checked((long)descriptor.Stride * descriptor.Height));
                }
                catch (OverflowException)
                {
                    error = "the selected stride and height exceed the supported buffer range.";
                    return false;
                }
            }

            try
            {
                source = RawImageSource.FromProcessMemory(
                    _debuggeeProcessId,
                    address,
                    bufferLength,
                    descriptor);
                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private bool TryBuildDescriptor(
            out RawImageDescriptor descriptor,
            out VisualizerMemberInventoryItem? dataMember,
            out string error)
        {
            descriptor = new RawImageDescriptor();
            dataMember = FindInventoryItem(DataBox.SelectedItem as string);
            var widthMember = FindInventoryItem(WidthBox.SelectedItem as string);
            var heightMember = FindInventoryItem(HeightBox.SelectedItem as string);
            if (dataMember == null || widthMember == null || heightMember == null)
            {
                error = "Data, Width, and Height members are required.";
                return false;
            }

            int width;
            int height;
            if (!TryParsePositiveInt(widthMember.SampleValue, out width)
                || !TryParsePositiveInt(heightMember.SampleValue, out height))
            {
                error = "Preview unavailable — width/height sample values are not readable.";
                return false;
            }

            var pixelFormat = RawPixelFormat.Mono8;
            var pixelFormatMember = FindInventoryItem(PixelFormatBox.SelectedItem as string);
            if (pixelFormatMember != null && !ResolvePixelFormat(pixelFormatMember, out pixelFormat))
            {
                error = "Preview unavailable — pixel format sample value is not mapped.";
                return false;
            }

            descriptor.Width = width;
            descriptor.Height = height;
            descriptor.PixelFormat = pixelFormat;
            RawByteOrder selectedByteOrder;
            descriptor.ByteOrder = Enum.TryParse(Convert.ToString(ByteOrderBox.SelectedItem, CultureInfo.InvariantCulture), true, out selectedByteOrder)
                ? selectedByteOrder
                : RawByteOrder.LittleEndian;
            var validBitsMember = FindInventoryItem(ValidBitsBox.SelectedItem as string);
            int validBits;
            descriptor.ValidBits = validBitsMember != null && TryParsePositiveInt(validBitsMember.SampleValue, out validBits)
                ? validBits
                : GetDefaultValidBits(pixelFormat);
            var strideMember = FindInventoryItem(StrideBox.SelectedItem as string);
            int stride;
            descriptor.Stride = strideMember != null && TryParsePositiveInt(strideMember.SampleValue, out stride)
                ? stride
                : descriptor.GetMinimumStride();
            error = string.Empty;
            return true;
        }

        private bool ResolvePixelFormat(VisualizerMemberInventoryItem pixelFormatMember, out RawPixelFormat pixelFormat)
        {
            var sample = pixelFormatMember.SampleValue;
            if (pixelFormatMember.EnumValues != null && pixelFormatMember.EnumValues.Count > 0)
            {
                for (var i = 0; i < _enumValueNames.Count; i++)
                {
                    if (string.Equals(_enumValueNames[i], sample, StringComparison.Ordinal))
                    {
                        RawPixelFormat mapped;
                        pixelFormat = Enum.TryParse(Convert.ToString(_enumValueBoxes[i].SelectedItem, CultureInfo.InvariantCulture), true, out mapped)
                            ? mapped
                            : RawPixelFormat.Mono8;
                        return true;
                    }
                }
            }

            return Enum.TryParse(sample, true, out pixelFormat);
        }

        private void DiagnoseInterpretation_Changed(object sender, RoutedEventArgs e)
        {
            if (DiagnoseInterpretationButton.IsChecked != true)
            {
                CancelDiagnosis();
                DiagnosisResultPanel.Visibility = Visibility.Collapsed;
                return;
            }

            CancelDiagnosis();
            DiagnosisResultPanel.Visibility = Visibility.Visible;
            DiagnosisCandidateList.ItemsSource = null;
            DiagnosisApplyStatusText.Text = string.Empty;
            StatusText.Text = string.Empty;
            DiagnosisStatusText.Text = "Diagnosing the current paused buffer...";

            if (!TryBuildDescriptor(out var descriptor, out var dataMember, out var descriptorError))
            {
                DiagnosisStatusText.Text = "Diagnosis unavailable: " + descriptorError;
                return;
            }

            if (!TryCreateLiveSource(descriptor, dataMember, true, out var source, out var sourceError))
            {
                DiagnosisStatusText.Text = "Diagnosis unavailable: " + sourceError;
                return;
            }

            var cancellation = new CancellationTokenSource();
            var diagnosisToken = cancellation.Token;
            _diagnosisCancellation = cancellation;
#pragma warning disable VSTHRD110
            Task.Run(
                delegate
                {
                    using (source)
                    {
                        return BufferDoctor.Diagnose(source, diagnosisToken);
                    }
                }, diagnosisToken).ContinueWith(
                task => CompleteDiagnosis(task, cancellation),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.FromCurrentSynchronizationContext());
#pragma warning restore VSTHRD110
        }

        private void CompleteDiagnosis(
            Task<BufferDiagnosisResult> task,
            CancellationTokenSource cancellation)
        {
            if (!ReferenceEquals(_diagnosisCancellation, cancellation))
            {
                return;
            }

            _diagnosisCancellation = null;
            cancellation.Dispose();
            if (task.IsCanceled)
            {
                return;
            }

            if (task.IsFaulted)
            {
                var failure = task.Exception == null ? null : task.Exception.GetBaseException();
                DiagnosisStatusText.Text = failure is RawImageSourceUnavailableException
                    ? "Live source unavailable. Pause at a valid breakpoint and diagnose again."
                    : "Diagnosis unavailable: " + (failure == null ? "unknown error." : failure.Message);
                return;
            }

#pragma warning disable VSTHRD002
            var result = task.Result;
#pragma warning restore VSTHRD002
            var rows = new List<BufferDiagnosisCandidateItem>(result.Candidates.Count);
            for (var i = 0; i < result.Candidates.Count; i++)
            {
                rows.Add(new BufferDiagnosisCandidateItem(result.Candidates[i]));
            }

            DiagnosisCandidateList.ItemsSource = rows;
            DiagnosisStatusText.Text = string.Format(
                CultureInfo.InvariantCulture,
                "{0} ranked interpretation(s). Select one to update only the visible draft and preview; Save Mapping is the persistence boundary.",
                rows.Count);
            DiagnosisResultPanel.BringIntoView();
        }

        private void DiagnosisCandidateList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_applyingDiagnosisCandidate)
            {
                return;
            }

            var item = DiagnosisCandidateList.SelectedItem as BufferDiagnosisCandidateItem;
            if (item == null)
            {
                return;
            }

            _selectedDiagnosisCandidate = item.Candidate;
            StatusText.Text = string.Empty;
            _applyingDiagnosisCandidate = true;
            bool draftMatches;
            string draftError;
            try
            {
                draftMatches = TryApplyCandidateToDraft(item.Candidate.Descriptor, out draftError);
            }
            finally
            {
                _applyingDiagnosisCandidate = false;
            }

            var dataMember = FindInventoryItem(DataBox.SelectedItem as string);
            var previewRendered = TryRenderPreview(item.Candidate.Descriptor, dataMember, out var previewError);
            PreviewStatusText.Text = previewRendered
                ? "Preview rendered from the selected candidate; mapping is unchanged until Save Mapping."
                : "Preview unavailable: " + previewError;

            var ambiguity = item.Candidate.IsAmbiguousWithGroup
                ? " This candidate belongs to an ambiguous tie group and requires your confirmation."
                : string.Empty;
            DiagnosisApplyStatusText.Text = draftMatches
                ? "Candidate applied to the visible draft and preview; nothing has been saved." + ambiguity
                : "Candidate preview applied, but the draft cannot persist it yet: " + draftError
                    + " Select matching role members before saving." + ambiguity;
            DiagnosisResultPanel.BringIntoView();
        }

        private bool TryApplyCandidateToDraft(RawImageDescriptor candidate, out string error)
        {
            TrySelectRequiredNumericRole(WidthBox, candidate.Width, WidthNameCandidates, "width", "sizex");
            TrySelectRequiredNumericRole(HeightBox, candidate.Height, HeightNameCandidates, "height", "sizey");
            ApplyCandidatePixelFormat(candidate.PixelFormat);
            TrySelectOptionalNumericRole(
                StrideBox,
                candidate.Stride,
                candidate.GetMinimumStride(),
                StrideNameCandidates,
                "stride",
                "pitch",
                "step");
            TrySelectOptionalNumericRole(
                ValidBitsBox,
                candidate.ValidBits,
                GetDefaultValidBits(candidate.PixelFormat),
                ValidBitsNameCandidates,
                "validbits",
                "bitdepth",
                "depth");
            ByteOrderBox.SelectedItem = candidate.ByteOrder.ToString();

            if (!TryBuildDescriptor(out var draft, out _, out error))
            {
                return false;
            }

            if (DescriptorsMatch(draft, candidate))
            {
                error = string.Empty;
                return true;
            }

            error = DescribeDescriptorMismatch(draft, candidate);
            return false;
        }

        private bool TrySelectRequiredNumericRole(
            ComboBox box,
            int expectedValue,
            string[] exactNames,
            params string[] containsNames)
        {
            var current = FindInventoryItem(box.SelectedItem as string);
            if (MemberHasPositiveIntValue(current, expectedValue))
            {
                return true;
            }

            var matchingName = FindNumericMemberName(expectedValue, exactNames, containsNames);
            if (matchingName == null)
            {
                return false;
            }

            box.SelectedItem = matchingName;
            return true;
        }

        private bool TrySelectOptionalNumericRole(
            ComboBox box,
            int expectedValue,
            int defaultValue,
            string[] exactNames,
            params string[] containsNames)
        {
            if (TrySelectRequiredNumericRole(box, expectedValue, exactNames, containsNames))
            {
                return true;
            }

            if (expectedValue != defaultValue)
            {
                return false;
            }

            box.SelectedItem = NoneItem;
            return true;
        }

        private string? FindNumericMemberName(
            int expectedValue,
            string[] exactNames,
            string[] containsNames)
        {
            for (var c = 0; c < exactNames.Length; c++)
            {
                for (var i = 0; i < _inventory.Count; i++)
                {
                    if (string.Equals(_inventory[i].Name, exactNames[c], StringComparison.OrdinalIgnoreCase)
                        && MemberHasPositiveIntValue(_inventory[i], expectedValue))
                    {
                        return _inventory[i].Name;
                    }
                }
            }

            for (var c = 0; c < containsNames.Length; c++)
            {
                for (var i = 0; i < _inventory.Count; i++)
                {
                    if (_inventory[i].Name.IndexOf(containsNames[c], StringComparison.OrdinalIgnoreCase) >= 0
                        && MemberHasPositiveIntValue(_inventory[i], expectedValue))
                    {
                        return _inventory[i].Name;
                    }
                }
            }

            string? uniqueMatch = null;
            for (var i = 0; i < _inventory.Count; i++)
            {
                if (!MemberHasPositiveIntValue(_inventory[i], expectedValue))
                {
                    continue;
                }

                if (uniqueMatch != null)
                {
                    return null;
                }

                uniqueMatch = _inventory[i].Name;
            }

            return uniqueMatch;
        }

        private bool ApplyCandidatePixelFormat(RawPixelFormat pixelFormat)
        {
            var member = FindInventoryItem(PixelFormatBox.SelectedItem as string);
            if (member == null)
            {
                return pixelFormat == RawPixelFormat.Mono8;
            }

            if (member.EnumValues != null && member.EnumValues.Count > 0)
            {
                for (var i = 0; i < _enumValueNames.Count; i++)
                {
                    if (!string.Equals(_enumValueNames[i], member.SampleValue, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    _enumValueBoxes[i].SelectedItem = pixelFormat.ToString();
                    return true;
                }

                return false;
            }

            return Enum.TryParse(member.SampleValue, true, out RawPixelFormat current)
                && current == pixelFormat;
        }

        private static bool MemberHasPositiveIntValue(
            VisualizerMemberInventoryItem? member,
            int expectedValue)
        {
            return member != null
                && TryParsePositiveInt(member.SampleValue, out var value)
                && value == expectedValue;
        }

        private static bool DescriptorsMatch(RawImageDescriptor left, RawImageDescriptor right)
        {
            return left.Width == right.Width
                && left.Height == right.Height
                && left.Stride == right.Stride
                && left.PixelFormat == right.PixelFormat
                && left.ValidBits == right.ValidBits
                && left.ByteOrder == right.ByteOrder;
        }

        private static string DescribeDescriptorMismatch(
            RawImageDescriptor draft,
            RawImageDescriptor candidate)
        {
            var fields = new List<string>();
            if (draft.Width != candidate.Width) fields.Add("width " + candidate.Width);
            if (draft.Height != candidate.Height) fields.Add("height " + candidate.Height);
            if (draft.Stride != candidate.Stride) fields.Add("stride " + candidate.Stride);
            if (draft.PixelFormat != candidate.PixelFormat) fields.Add("format " + candidate.PixelFormat);
            if (draft.ValidBits != candidate.ValidBits) fields.Add("valid bits " + candidate.ValidBits);
            if (draft.ByteOrder != candidate.ByteOrder) fields.Add("byte order " + candidate.ByteOrder);
            return fields.Count == 0 ? "the selected values do not match the candidate." : string.Join(", ", fields);
        }

        private void CancelDiagnosis()
        {
            if (_diagnosisCancellation == null)
            {
                return;
            }

            _diagnosisCancellation.Cancel();
            _diagnosisCancellation.Dispose();
            _diagnosisCancellation = null;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = string.Empty;
            if (_selectedDiagnosisCandidate != null
                && (!TryBuildDescriptor(out var draft, out _, out var draftError)
                    || !DescriptorsMatch(draft, _selectedDiagnosisCandidate.Descriptor)))
            {
                StatusText.Text = "Mapping was not saved. The visible draft does not reproduce the selected diagnosis. "
                    + (string.IsNullOrWhiteSpace(draftError)
                        ? DescribeDescriptorMismatch(draft, _selectedDiagnosisCandidate.Descriptor)
                        : draftError);
                return;
            }

            if (!TryCreateMapping(out var mapping, out var error))
            {
                StatusText.Text = error;
                return;
            }

            try
            {
                var store = new TypeMappingStore(null, TypeMappingStore.GetDefaultUserMappingPath());
                var file = store.LoadUserFile();
                var existingIndex = -1;
                for (var i = 0; i < file.Mappings.Count; i++)
                {
                    if (string.Equals(file.Mappings[i].TypeName, _typeName, StringComparison.Ordinal)
                        && string.Equals(file.Mappings[i].AssemblyName, _assemblyName, StringComparison.Ordinal))
                    {
                        existingIndex = i;
                        break;
                    }
                }

                if (existingIndex >= 0)
                {
                    var overwrite = MessageBox.Show(
                        this,
                        "A mapping for " + _typeName + " already exists. Overwrite it?",
                        "Connect Your Buffer",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    if (overwrite != MessageBoxResult.Yes)
                    {
                        return;
                    }

                    file.Mappings[existingIndex] = mapping;
                }
                else
                {
                    file.Mappings.Add(mapping);
                }

                store.Save(file);
            }
            catch (Exception ex)
            {
                StatusText.Text = "Mapping save failed: " + ex.Message;
                return;
            }

            StatusText.Text = "Mapping saved to " + TypeMappingStore.GetDefaultUserMappingPath()
                + ". Automatic Vision Inspector will apply it on the next scan.";
            DialogResult = true;
        }

        private void CopyTemplate_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = string.Empty;
            if (!TryCreateMapping(out var mapping, out var error))
            {
                StatusText.Text = error;
                return;
            }

            var dataMember = FindInventoryItem(mapping.Members.Data);
            if (dataMember == null || !IsPointerTypeName(dataMember.TypeName))
            {
                StatusText.Text = "RawBufferView templates require an IntPtr or UIntPtr data member. Save Mapping supports managed arrays without project code.";
                return;
            }

            var pixelFormat = RawPixelFormat.Mono8;
            var pixelFormatMember = FindInventoryItem(mapping.Members.PixelFormat);
            if (pixelFormatMember != null && !ResolvePixelFormat(pixelFormatMember, out pixelFormat))
            {
                StatusText.Text = "Select a Raw Buffer Visualizer format for the current pixel-format value before copying the template.";
                return;
            }

            var validBitsMember = FindInventoryItem(mapping.Members.ValidBits ?? mapping.Members.BitDepth);
            var bitDepth = validBitsMember != null && TryParsePositiveInt(validBitsMember.SampleValue, out var selectedValidBits)
                ? selectedValidBits
                : GetDefaultValidBits(pixelFormat);

            try
            {
                Clipboard.SetText(RawBufferViewTemplateGenerator.Create(
                    mapping,
                    pixelFormat,
                    bitDepth,
                    dataMember.TypeName == "UIntPtr"));
                StatusText.Text = "Template copied; mapping unchanged. Verify buffer lifetime and runtime pixel format.";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Template copy failed: " + ex.Message;
            }
        }

        private void UseSuggestedRoles_Click(object sender, RoutedEventArgs e)
        {
            _selectedDiagnosisCandidate = null;
            DiagnosisCandidateList.SelectedItem = null;
            DiagnosisApplyStatusText.Text = string.Empty;
            _existingPixelFormatMap.Clear();
            ApplyRoleSelections(_suggestedMembers);
            ByteOrderBox.SelectedItem = RawByteOrder.LittleEndian.ToString();
            PreviewImage.Source = null;
            PreviewStatusText.Text = string.Empty;
            StatusText.Text = "Suggested roles restored. Select Save Mapping to persist them.";
        }

        private bool TryCreateMapping(out TypeMapping mapping, out string error)
        {
            mapping = new TypeMapping();
            var dataName = DataBox.SelectedItem as string;
            var widthName = WidthBox.SelectedItem as string;
            var heightName = HeightBox.SelectedItem as string;
            if (string.IsNullOrEmpty(dataName) || string.IsNullOrEmpty(widthName) || string.IsNullOrEmpty(heightName))
            {
                error = "Data, Width, and Height members are required.";
                return false;
            }

            mapping = new TypeMapping
            {
                TypeName = _typeName,
                AssemblyName = _assemblyName,
                Members = new TypeMappingMembers
                {
                    Data = dataName,
                    Width = widthName,
                    Height = heightName,
                    Stride = OptionalMemberName(StrideBox),
                    BufferLength = OptionalMemberName(BufferLengthBox),
                    PixelFormat = OptionalMemberName(PixelFormatBox),
                    ValidBits = OptionalMemberName(ValidBitsBox)
                },
                ByteOrder = Convert.ToString(ByteOrderBox.SelectedItem, CultureInfo.InvariantCulture) ?? RawByteOrder.LittleEndian.ToString()
            };

            var pixelFormatMember = FindInventoryItem(mapping.Members.PixelFormat);
            if (pixelFormatMember != null && pixelFormatMember.EnumValues != null && pixelFormatMember.EnumValues.Count > 0)
            {
                var map = new Dictionary<string, string>(_existingPixelFormatMap, StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < _enumValueNames.Count; i++)
                {
                    map[_enumValueNames[i]] = Convert.ToString(_enumValueBoxes[i].SelectedItem, CultureInfo.InvariantCulture) ?? RawPixelFormat.Mono8.ToString();
                }

                mapping.PixelFormatMap = map;
            }

            error = string.Empty;
            return true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            CancelDiagnosis();
            DialogResult = false;
        }

        private string? OptionalMemberName(ComboBox box)
        {
            var name = box.SelectedItem as string;
            return string.IsNullOrEmpty(name) || name == NoneItem ? null : name;
        }

        private VisualizerMemberInventoryItem? FindInventoryItem(string? name)
        {
            if (string.IsNullOrEmpty(name) || name == NoneItem)
            {
                return null;
            }

            for (var i = 0; i < _inventory.Count; i++)
            {
                if (_inventory[i].Name == name)
                {
                    return _inventory[i];
                }
            }

            return null;
        }

        private static bool IsPointerTypeName(string typeName)
        {
            return typeName == "IntPtr" || typeName == "UIntPtr";
        }

        private static bool TryParsePositiveInt(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value > 0;
        }

        private static bool TryParsePointer(string text, out long address)
        {
            address = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return long.TryParse(
                    text.Substring(2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out address);
            }

            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out address);
        }

        private static int GetDefaultValidBits(RawPixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case RawPixelFormat.Mono16:
                    return 16;
                case RawPixelFormat.Float32:
                case RawPixelFormat.Int32:
                    return 32;
                case RawPixelFormat.Binary:
                    return 1;
                case RawPixelFormat.Mono10PackedLsb:
                    return 10;
                case RawPixelFormat.Mono12PackedLsb:
                    return 12;
                default:
                    return 8;
            }
        }

        private static string GetAutomationToken(string value)
        {
            var characters = new char[value.Length];
            var count = 0;
            for (var i = 0; i < value.Length; i++)
            {
                if (char.IsLetterOrDigit(value[i]))
                {
                    characters[count++] = value[i];
                }
            }

            return count == 0 ? "Value" : new string(characters, 0, count);
        }
    }
}
