using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MediaEnhancer.Core
{
    /// <summary>
    /// 将布尔值转换为 Visibility 的转换器。
    /// true → Visible, false → Collapsed。
    /// 可配合 ConverterParameter="Invert" 使用反转逻辑。
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// 将布尔值转换为 Visibility。
        /// 如果 ConverterParameter 为 "Invert"，则 true → Collapsed, false → Visible。
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                bool invert = parameter is string param && param.Equals("Invert", StringComparison.OrdinalIgnoreCase);
                return (boolValue ^ invert) ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        /// <summary>
        /// 反向转换，本场景暂不需要。
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
