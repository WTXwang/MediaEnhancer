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
                    // 新建空bitmap
                    var bitmap = new BitmapImage();
                    // 开始编辑
                    bitmap.BeginInit();
                    // 从路径path中获取图像
                    bitmap.UriSource = new Uri(path);
                    // 把整个图片加载到内存，不锁文件
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    // 跳过旧的缓存，防止改过的缩略图不可见
                    bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    //限制缩略图尺寸
                    bitmap.DecodePixelWidth = 200;
                    bitmap.DecodePixelHeight = 200;
                    // 结束编辑
                    bitmap.EndInit();
                    // 保证跨线程安全
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
