using System;
using System.Globalization;
using System.Windows.Data;

namespace MediaEnhancer.Core
{
    /// <summary>
    /// 将布尔值转换为收藏图标文字。true → "⭐"，false → "☆"。
    /// 用于 DataGrid 收藏按钮的 Content 绑定。
    /// </summary>
    public class BoolToFavoriteConverter : IValueConverter
    {
        /// <summary>
        /// 布尔值转收藏图标。
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isFavorite)  // 判断是不是布尔值，isFavorite 是一种模式匹配的写法，等价于 bool isFavorite=(bool)value
                return isFavorite ? "⭐" : "☆";
            return "☆";
        }

        /// <summary>
        /// 反向转换，本场景不需要。
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
