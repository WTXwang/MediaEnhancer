using System;
using System.Globalization;
using System.Windows.Data;

namespace MediaEnhancer.Core;

/// <summary>
/// 判断 SelectedPageIndex 是否等于 ConverterParameter 指定的页面索引。
/// 用于导航按钮的高亮触发器：每个按钮用 ConverterParameter 声明自己对应的页索引，
/// 只有当当前页面索引一致时该按钮才显示高亮。
/// </summary>
public class PageEqConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int pageIndex && parameter != null
            && int.TryParse(parameter.ToString(), out int target))
            return pageIndex == target;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
