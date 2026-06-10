using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MediaEnhancer.Core
{
    /// <summary>
    /// 将整型的索引转换为 Visibility 隐藏/显示的转换器。
    /// 当绑定的值等于 ConverterParameter 传入的值时，返回 Visible，否则返回 Collapsed。
    /// </summary>
    public class IndexToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int selectedIndex && parameter != null)
            {
                if (int.TryParse(parameter.ToString(), out int targetIndex))
                {
                    return selectedIndex == targetIndex ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}