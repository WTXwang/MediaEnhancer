using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using MediaEnhancer.Models;

namespace MediaEnhancer.Views
{
    /// <summary>
    /// 文件详情窗口，展示媒体文件的缩略图及完整信息。
    /// </summary>
    public partial class FileDetailWindow : Window
    {
        /// <summary>
        /// 当前展示的媒体文件对象。
        /// </summary>
        private readonly MediaFile _mediaFile;

        /// <summary>
        /// 构造函数，接收要展示详情的媒体文件。
        /// </summary>
        /// <param name="mediaFile">媒体文件实体。</param>
        public FileDetailWindow(MediaFile mediaFile)
        {
            InitializeComponent();
            _mediaFile = mediaFile;
            LoadFileDetails();
        }

        /// <summary>
        /// 加载文件详情到界面控件。
        /// </summary>
        private void LoadFileDetails()
        {
            // 窗口标题
            Title = $"文件详情 - {_mediaFile.Title}";

            // 标题栏
            TitleText.Text = _mediaFile.Title;

            // 基本信息
            IdText.Text = _mediaFile.Id.ToString();
            TypeText.Text = _mediaFile.Type;
            FormatText.Text = _mediaFile.FileFormat ?? "未知";
            SizeText.Text = FormatFileSize(_mediaFile.FileSize);

            // 分辨率
            if (_mediaFile.Width.HasValue && _mediaFile.Height.HasValue)
                DimensionText.Text = $"{_mediaFile.Width} × {_mediaFile.Height}";
            else
                DimensionText.Text = "—";

            // 时长
            DurationText.Text = !string.IsNullOrEmpty(_mediaFile.Duration) ? _mediaFile.Duration : "—";

            // 收藏
            FavoriteText.Text = _mediaFile.IsFavorite ? "⭐ 已收藏" : "未收藏";

            // 时间
            AddedText.Text = _mediaFile.DateAdded.ToString("yyyy-MM-dd HH:mm");
            ModifiedText.Text = _mediaFile.DateModified.ToString("yyyy-MM-dd HH:mm");

            // 路径
            PathText.Text = _mediaFile.FilePath;
            ThumbPathText.Text = !string.IsNullOrEmpty(_mediaFile.ThumbnailPath)
                ? _mediaFile.ThumbnailPath
                : "（尚未生成缩略图）";

            // 加载缩略图
            LoadThumbnail();
        }

        /// <summary>
        /// 尝试加载缩略图或原图作为预览。
        /// </summary>
        private void LoadThumbnail()
        {
            // 优先使用缩略图路径，其次直接使用原文件（仅图片）
            var imagePath = !string.IsNullOrEmpty(_mediaFile.ThumbnailPath)
                ? _mediaFile.ThumbnailPath
                : (_mediaFile.Type == "图片" ? _mediaFile.FilePath : null);

            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imagePath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = 400; // 限制加载尺寸，节省内存
                    bitmap.EndInit();
                    bitmap.Freeze();

                    ThumbnailImage.Source = bitmap;
                    PlaceholderBorder.Visibility = Visibility.Collapsed;
                    return;
                }
                catch
                {
                    // 加载失败，回退到占位图
                }
            }

            // 无有效图片，显示占位
            ThumbnailImage.Source = null;
            PlaceholderBorder.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 将文件大小格式化为可读字符串。
        /// </summary>
        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F1} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }
}
