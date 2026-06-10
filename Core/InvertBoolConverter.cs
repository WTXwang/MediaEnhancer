using System;
using System.Globalization;
using System.Windows.Data;

namespace MediaEnhancer.Core;

/// <summary>布尔值取反转换器。</summary>
public class InvertBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b ? !b : value;
}
