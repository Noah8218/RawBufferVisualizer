using System;
using System.Collections.Generic;
using System.Globalization;
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
        private readonly TypeMappingMembers? _suggestedMembers;
        private readonly Dictionary<string, string> _existingPixelFormatMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public TypeMappingDialog(
            List<VisualizerMemberInventoryItem> inventory,
            string typeName,
            string assemblyName,
            int debuggeeProcessId,
            TypeMappingMembers? suggestedMembers = null,
            bool pixelFormatOnly = false)
        {
            InitializeComponent();
            _inventory = inventory ?? new List<VisualizerMemberInventoryItem>();
            _typeName = typeName ?? string.Empty;
            _assemblyName = assemblyName ?? string.Empty;
            _debuggeeProcessId = debuggeeProcessId;
            var existingMapping = TypeMappingStore.Default.FindMapping(_typeName, _assemblyName)
                ?? TypeMappingStore.Default.FindMappingByTypeNameOnly(_typeName);
            _suggestedMembers = suggestedMembers ?? existingMapping?.Members;
            if (existingMapping?.PixelFormatMap != null)
            {
                foreach (var pair in existingMapping.PixelFormatMap)
                {
                    _existingPixelFormatMap[pair.Key] = pair.Value;
                }
            }

            TypeNameText.Text = string.Format(
                CultureInfo.InvariantCulture,
                "Map members of {0} ({1})",
                string.IsNullOrWhiteSpace(_typeName) ? "unknown type" : _typeName,
                string.IsNullOrWhiteSpace(_assemblyName) ? "unknown assembly" : _assemblyName);
            PopulateRoles();
            ByteOrderBox.ItemsSource = Enum.GetNames(typeof(RawByteOrder));
            ByteOrderBox.SelectedItem = RawByteOrder.LittleEndian.ToString();
            if (pixelFormatOnly)
            {
                Title = "Confirm Pixel Format";
                AllRolesGrid.Visibility = Visibility.Collapsed;
                MappingGuidanceText.Text = "Only the current pixel-format value needs confirmation. "
                    + "Data, dimensions, and stride were inferred and are already selected for the saved mapping.";
            }
            else
            {
                MappingGuidanceText.Text = "Review the inferred member roles, then save this mapping for future breaks.";
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

            Preselect(DataBox, FirstAvailable(_suggestedMembers?.Data, PreselectDataMember()));
            Preselect(WidthBox, FirstAvailable(_suggestedMembers?.Width, PreselectByName(WidthNameCandidates, "width", "sizex")));
            Preselect(HeightBox, FirstAvailable(_suggestedMembers?.Height, PreselectByName(HeightNameCandidates, "height", "sizey")));
            Preselect(StrideBox, FirstAvailable(_suggestedMembers?.Stride, PreselectByName(StrideNameCandidates, "stride", "pitch", "step")));
            Preselect(PixelFormatBox, FirstAvailable(_suggestedMembers?.PixelFormat, PreselectPixelFormatMember()));
            Preselect(ValidBitsBox, FirstAvailable(_suggestedMembers?.ValidBits, PreselectByName(ValidBitsNameCandidates, "validbits", "bitdepth", "depth")));
            Preselect(BufferLengthBox, FirstAvailable(_suggestedMembers?.BufferLength, PreselectByName(BufferLengthNameCandidates, "bufferlength", "bytelength", "length")));
            RebuildEnumMappingRows();
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

            long address;
            if (_debuggeeProcessId <= 0
                || dataMember == null
                || !IsPointerTypeName(dataMember.TypeName)
                || !TryParsePointer(dataMember.SampleValue, out address)
                || address == 0)
            {
                PreviewStatusText.Text = "Preview unavailable — mapping will be verified on the next scan.";
                return;
            }

            try
            {
                var bufferLength = descriptor.GetRequiredByteCount();
                var lengthMember = FindInventoryItem(BufferLengthBox.SelectedItem as string);
                long parsedLength;
                if (lengthMember != null
                    && long.TryParse(lengthMember.SampleValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedLength)
                    && parsedLength >= bufferLength)
                {
                    bufferLength = parsedLength;
                }

                using (var source = RawImageSource.FromProcessMemory(_debuggeeProcessId, address, bufferLength, descriptor))
                {
                    PreviewImage.Source = RawBufferToolWindowControl.CreateThumbnailSource(source, descriptor);
                }

                PreviewStatusText.Text = PreviewImage.Source == null
                    ? "Preview unavailable — mapping will be verified on the next scan."
                    : "Preview rendered from live debuggee memory.";
            }
            catch (Exception ex)
            {
                PreviewStatusText.Text = "Preview unavailable: " + ex.Message;
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

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = string.Empty;
            var dataName = DataBox.SelectedItem as string;
            var widthName = WidthBox.SelectedItem as string;
            var heightName = HeightBox.SelectedItem as string;
            if (string.IsNullOrEmpty(dataName) || string.IsNullOrEmpty(widthName) || string.IsNullOrEmpty(heightName))
            {
                StatusText.Text = "Data, Width, and Height members are required.";
                return;
            }

            var mapping = new TypeMapping
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

            var pixelFormatMember = FindInventoryItem(PixelFormatBox.SelectedItem as string);
            if (pixelFormatMember != null && pixelFormatMember.EnumValues != null && pixelFormatMember.EnumValues.Count > 0)
            {
                var map = new Dictionary<string, string>(_existingPixelFormatMap, StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < _enumValueNames.Count; i++)
                {
                    map[_enumValueNames[i]] = Convert.ToString(_enumValueBoxes[i].SelectedItem, CultureInfo.InvariantCulture) ?? RawPixelFormat.Mono8.ToString();
                }

                mapping.PixelFormatMap = map;
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
                        "Map This Type",
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

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
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
