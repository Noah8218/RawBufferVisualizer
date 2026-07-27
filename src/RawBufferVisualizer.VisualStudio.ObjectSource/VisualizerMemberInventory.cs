using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    public sealed class VisualizerMemberInventoryItem
    {
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string SampleValue { get; set; } = string.Empty;
        public List<string>? EnumValues { get; set; }
    }

    internal static class VisualizerMemberInventory
    {
        private const int MaxSampleValueLength = 64;

        public static List<VisualizerMemberInventoryItem> Create(object value)
        {
            var items = new List<VisualizerMemberInventoryItem>();
            if (value == null)
            {
                return items;
            }

            var type = value.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var fields = type.GetFields(flags);
            for (var i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                if (field.Name.IndexOf("k__BackingField", StringComparison.Ordinal) >= 0)
                {
                    continue;
                }

                var captured = field;
                items.Add(new VisualizerMemberInventoryItem
                {
                    Name = field.Name,
                    Kind = "Field",
                    TypeName = GetFriendlyTypeName(field.FieldType),
                    SampleValue = GetSampleValue(() => captured.GetValue(value), field.FieldType),
                    EnumValues = GetEnumValues(field.FieldType)
                });
            }

            var properties = type.GetProperties(flags);
            for (var i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                var captured = property;
                items.Add(new VisualizerMemberInventoryItem
                {
                    Name = property.Name,
                    Kind = "Property",
                    TypeName = GetFriendlyTypeName(property.PropertyType),
                    SampleValue = GetSampleValue(() => captured.GetValue(value), property.PropertyType),
                    EnumValues = GetEnumValues(property.PropertyType)
                });
            }

            return items;
        }

        private static string GetSampleValue(Func<object?> read, Type memberType)
        {
            object? value;
            try
            {
                value = read();
            }
            catch
            {
                return "<unreadable>";
            }

            if (value == null)
            {
                return "null";
            }

            var array = value as Array;
            if (array != null)
            {
                return GetFriendlyTypeName(memberType) + "[" + array.Length.ToString(CultureInfo.InvariantCulture) + "]";
            }

            if (value is IntPtr || value is UIntPtr)
            {
                var pointer = value is IntPtr ? ((IntPtr)value).ToInt64() : (long)((UIntPtr)value).ToUInt64();
                return "0x" + pointer.ToString("X", CultureInfo.InvariantCulture);
            }

            var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            if (text.Length > MaxSampleValueLength)
            {
                text = text.Substring(0, MaxSampleValueLength) + "...";
            }

            return text;
        }

        private static List<string>? GetEnumValues(Type type)
        {
            return type.IsEnum ? new List<string>(Enum.GetNames(type)) : null;
        }

        private static string GetFriendlyTypeName(Type type)
        {
            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                return GetFriendlyTypeName(elementType ?? typeof(object)) + "[]";
            }

            var name = type.Name;
            var tick = name.IndexOf('`');
            return tick > 0 ? name.Substring(0, tick) : name;
        }
    }
}
