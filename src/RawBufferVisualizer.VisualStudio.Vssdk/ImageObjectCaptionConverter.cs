using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    public sealed class ImageObjectCaptionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 0) return DependencyProperty.UnsetValue;
            if (values[0] is string name && values.Length > 1 && values[1] is bool automatic && automatic && name.StartsWith("[Auto] ", StringComparison.Ordinal))
            {
                return name.Substring(7);
            }
            return values[0];
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
