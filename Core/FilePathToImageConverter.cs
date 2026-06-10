using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace MediaEnhancer.Core
{
    /// <summary>
    /// 将文件路径转换为 BitmapImage，加载完成后立即释放文件锁定。
    /// 避免 Image 控件长期占用文件导致无法删除。
    /// </summary>
    public class FilePathToImageConverter : IValueConverter
    {
        /// <summary>
        /// 文件路径 → BitmapImage（不锁定源文件）。
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string filePath && !string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(filePath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;  // 加载后立即释放文件
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bitmap.DecodePixelWidth = 400;  // 限制最大宽度，节省内存
                    bitmap.EndInit();
                    bitmap.Freeze();  // 跨线程安全
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
        /// 反向转换，本场景不需要。
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
