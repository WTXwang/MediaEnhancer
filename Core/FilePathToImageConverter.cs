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
                    var bitmap = new BitmapImage();                 // 创建空文件
                    bitmap.BeginInit();                             // 进入配置模式
                    bitmap.UriSource = new Uri(filePath);           // 加载文件
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;  // 加载后立即释放文件
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache; //忽略缓存
                    bitmap.DecodePixelWidth = 400;  // 限制最大宽度，节省内存
                    bitmap.EndInit(); // 退出配置模式
                    bitmap.Freeze();  // 冻结图片，跨线程安全
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
