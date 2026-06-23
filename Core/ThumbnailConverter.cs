using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace MediaEnhancer.Core
{
    /// <summary>
    /// 缩略图路径转 BitmapImage 转换器。
    /// 优先使用缩略图缓存路径；不存在时对图片文件直接加载原图（限制尺寸）；
    /// 均失败时返回 null，UI 显示占位。
    /// </summary>
    public class ThumbnailConverter : IValueConverter
    {
        /// <summary>
        /// 值转换：ThumbnailPath → BitmapImage（不锁定文件）。
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string path && !string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    // 跟图片路径转换类似，只是尺寸变小了
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(path);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bitmap.DecodePixelWidth = 200;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        /// <summary>
        /// 反向转换，不需要。
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
