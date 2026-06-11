using System;
using System.Collections.Generic;
using System.IO;
using TagLib;

namespace MediaEnhancer.Core
{
    /// <summary>
    /// 媒体文件工具类，提供格式定义、类型判断、元数据提取等静态方法。
    /// </summary>
    public static class MediaFileUtils
    {
        /// <summary>
        /// 支持的图片扩展名集合（不区分大小写）。
        /// </summary>
        public static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"
        };

        /// <summary>
        /// 支持的视频扩展名集合（不区分大小写）。
        /// </summary>
        public static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm"
        };

        /// <summary>
        /// 支持的音频扩展名集合（不区分大小写）。
        /// </summary>
        public static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a"
        };

        /// <summary>
        /// 判断指定扩展名是否为支持的媒体文件。
        /// </summary>
        /// <param name="extension">文件扩展名（含点号，如 ".jpg"）。</param>
        /// <returns>是媒体文件返回 true。</returns>
        public static bool IsMediaFile(string extension)
        {
            return ImageExtensions.Contains(extension)
                || VideoExtensions.Contains(extension)
                || AudioExtensions.Contains(extension);
        }

        /// <summary>
        /// 根据扩展名返回媒体类型（"图片"、"视频" 或 "音频"）。
        /// </summary>
        /// <param name="extension">文件扩展名。</param>
        /// <returns>"图片"、"视频" 或 "音频"。</returns>
        public static string GetMediaType(string extension)
        {
            if (ImageExtensions.Contains(extension)) return "图片";
            if (VideoExtensions.Contains(extension)) return "视频";
            return "音频";
        }

        /// <summary>
        /// 从文件路径提取文件名（不含扩展名）作为默认标题。
        /// </summary>
        public static string GetTitleFromPath(string filePath)
        {
            return Path.GetFileNameWithoutExtension(filePath);
        }

        /// <summary>
        /// 获取图片文件的物理像素宽高（使用 System.Drawing.Image）。
        /// 注意：WPF BitmapImage.PixelWidth 返回的是设备无关像素（受图片内嵌 DPI 影响），
        /// 例如 72 DPI 的 1920×1080 照片会错误显示为 2560×1440。
        /// </summary>
        public static (int? width, int? height) GetImageDimensions(string filePath)
        {
            try
            {
                using var img = System.Drawing.Image.FromFile(filePath);
                return (img.Width, img.Height);
            }
            catch
            {
                return (null, null);
            }
        }

        /// <summary>
        /// 获取视频文件的时长（使用 TagLibSharp 库读取元数据）。
        /// 非视频文件或读取失败时返回 null。
        /// </summary>
        /// <param name="filePath">视频文件路径。</param>
        /// <returns>格式化后的时长字符串 "hh:mm:ss"，或 null。</returns>
        public static string? GetVideoDuration(string filePath)
        {
            try
            {
                using var tagFile = TagLib.File.Create(filePath);
                var duration = tagFile.Properties.Duration;
                if (duration.TotalSeconds > 0)
                {
                    // 格式化为 hh:mm:ss
                    return duration.TotalHours >= 1
                        ? duration.ToString(@"hh\:mm\:ss")
                        : duration.ToString(@"mm\:ss");
                }
            }
            catch
            {
                // 文件无法读取或损坏时静默跳过
            }
            return null;
        }
    }
}
