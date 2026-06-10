using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MediaEnhancer.Models;

namespace MediaEnhancer.Services
{
    /// <summary>
    /// 缩略图服务实现，生成图片缩略图，视频/音频使用默认图标。
    /// 缓存目录：{AppData}/MediaEnhancer/thumbnails/
    /// </summary>
    public class ThumbnailService : IThumbnailService
    {
        private string _cacheDir;
        private const int ThumbnailSize = 200;

        /// <summary>
        /// 初始化缩略图缓存目录。
        /// </summary>
        public ThumbnailService()
        {
            _cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Thumbnails");
            Directory.CreateDirectory(_cacheDir);
        }

        /// <summary>
        /// 获取或设置缩略图缓存目录。设置时会自动创建目录。
        /// </summary>
        public string CacheDirectory
        {
            get => _cacheDir;
            set
            {
                _cacheDir = value;
                Directory.CreateDirectory(_cacheDir);
            }
        }

        /// <inheritdoc/>
        public async Task<string?> GenerateThumbnailAsync(MediaFile file)
        {
            try
            {
                if (!File.Exists(file.FilePath))
                    return null;

                var cacheKey = ComputeCacheKey(file);
                var cachePath = Path.Combine(_cacheDir, cacheKey + ".jpg");

                // 缓存已存在则直接返回
                if (File.Exists(cachePath))
                    return cachePath;

                if (file.Type == "图片")
                {
                    // 图片缩略图依赖 WPF BitmapImage，必须在 UI 线程生成
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                        GenerateImageThumbnail(file.FilePath, cachePath));
                }
                else if (file.Type == "视频")
                {
                    // 视频：ffmpeg 提取封面帧（可在后台线程），失败时回退默认图标（需 UI 线程）
                    var frameOk = await Task.Run(() =>
                        CaptureVideoFrameAsync(file.FilePath, cachePath));
                    if (!frameOk)
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                            GenerateDefaultIcon(file.Type, cachePath));
                    }
                }
                else
                {
                    // 音频默认图标依赖 WPF DrawingVisual，必须在 UI 线程生成
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                        GenerateDefaultIcon(file.Type, cachePath));
                }

                return File.Exists(cachePath) ? cachePath : null;
            }
            catch
            {
                return null;
            }
        }

        /// <inheritdoc/>
        public async Task GenerateThumbnailsAsync(IEnumerable<MediaFile> files)
        {
            foreach (var file in files)
            {
                await GenerateThumbnailAsync(file);
            }
        }

        /// <inheritdoc/>
        public Task CleanupOrphanedThumbnailsAsync()
        {
            return Task.Run(() =>
            {
                if (!Directory.Exists(_cacheDir)) return;

                foreach (var cacheFile in Directory.GetFiles(_cacheDir, "*.jpg"))
                {
                    try
                    {
                        var fileName = Path.GetFileNameWithoutExtension(cacheFile);
                        // 缓存文件名格式：{hash}_{width}x{height}
                        // 我们只按生成时间清理，简单策略：删除30天前的缓存
                        var fileInfo = new FileInfo(cacheFile);
                        if (fileInfo.LastAccessTime < DateTime.Now.AddDays(-30))
                            fileInfo.Delete();
                    }
                    catch
                    {
                        // 跳过无法删除的
                    }
                }
            });
        }

        /// <summary>
        /// 为图片生成缩略图（缩放到 200px 宽，保持比例，JPEG 编码）。
        /// </summary>
        private static void GenerateImageThumbnail(string sourcePath, string cachePath)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(sourcePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.DecodePixelWidth = ThumbnailSize; // 限制解码尺寸，节省内存
            bitmap.EndInit();
            bitmap.Freeze();

            // 编码为 JPEG 写入缓存
            var encoder = new JpegBitmapEncoder();
            encoder.QualityLevel = 85;
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            using var stream = new FileStream(cachePath, FileMode.Create);
            encoder.Save(stream);
        }

        /// <summary>
        /// 用 ffmpeg.exe 提取视频帧作为缩略图。
        /// 先将视频复制到临时文件（英文路径），避免中文路径问题。
        /// </summary>
        private static async Task<bool> CaptureVideoFrameAsync(string videoPath, string cachePath)
        {
            var tempVideo = "";
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var ffmpegExe = Path.Combine(baseDir, "ffmpeg.exe");
                var ffprobeExe = Path.Combine(baseDir, "ffprobe.exe");
                if (!File.Exists(ffmpegExe) || !File.Exists(ffprobeExe))
                    return false;

                tempVideo = Path.Combine(Path.GetTempPath(), "media_temp_" + Guid.NewGuid().ToString("N") + ".mp4");
                File.Copy(videoPath, tempVideo, true);

                var duration = await GetVideoDurationAsync(ffprobeExe, tempVideo);

                var psi = new System.Diagnostics.ProcessStartInfo(ffmpegExe)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                if (duration >= 2.0) { psi.ArgumentList.Add("-ss"); psi.ArgumentList.Add("2"); }
                psi.ArgumentList.Add("-i"); psi.ArgumentList.Add(tempVideo);
                psi.ArgumentList.Add("-vframes"); psi.ArgumentList.Add("1");
                psi.ArgumentList.Add("-q:v"); psi.ArgumentList.Add("2");
                psi.ArgumentList.Add("-y"); psi.ArgumentList.Add(cachePath);

                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc == null) return false;
                proc.WaitForExit(15000);

                return proc.ExitCode == 0 && File.Exists(cachePath);
            }
            catch { return false; }
            finally
            {
                try { if (!string.IsNullOrEmpty(tempVideo) && File.Exists(tempVideo)) File.Delete(tempVideo); } catch { }
            }
        }

        /// <summary>
        /// 用 ffprobe 读取视频时长（秒）。
        /// </summary>
        private static async Task<double> GetVideoDurationAsync(string ffprobeExe, string videoPath)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(ffprobeExe)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };
                psi.ArgumentList.Add("-v"); psi.ArgumentList.Add("error");
                psi.ArgumentList.Add("-show_entries"); psi.ArgumentList.Add("format=duration");
                psi.ArgumentList.Add("-of"); psi.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
                psi.ArgumentList.Add(videoPath);

                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc == null) return 0;
                var output = await proc.StandardOutput.ReadToEndAsync();
                proc.WaitForExit(5000);
                if (proc.ExitCode == 0 && double.TryParse(output.Trim(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var seconds))
                    return seconds;
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// 生成默认类型图标（纯色背景 + 类型文字）。
        /// </summary>
        private static void GenerateDefaultIcon(string type, string cachePath)
        {
            var size = ThumbnailSize;
            var dpi = 96;

            // 创建绘制表面
            var drawingVisual = new DrawingVisual();
            using (var context = drawingVisual.RenderOpen())
            {
                // 背景色
                var bgColor = type == "视频" ? Color.FromRgb(37, 99, 235) : Color.FromRgb(16, 185, 129);
                context.DrawRectangle(
                    new SolidColorBrush(bgColor),
                    null,
                    new System.Windows.Rect(0, 0, size, size));

                // 类型图标文字
                var icon = type == "视频" ? "🎬" : "🎵";
                var typeText = type == "视频" ? "视频" : "音频";

                // 居中绘制
                context.DrawText(
                    new FormattedText(icon,
                        System.Globalization.CultureInfo.CurrentCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        new Typeface("Microsoft YaHei"),
                        64, Brushes.White, dpi),
                    new System.Windows.Point(size / 2 - 32, size / 2 - 48));

                context.DrawText(
                    new FormattedText(typeText,
                        System.Globalization.CultureInfo.CurrentCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        new Typeface("Microsoft YaHei"),
                        14, Brushes.White, dpi),
                    new System.Windows.Point(size / 2 - 14, size / 2 + 16));
            }

            // 渲染为位图
            var renderBitmap = new RenderTargetBitmap(size, size, dpi, dpi, PixelFormats.Pbgra32);
            renderBitmap.Render(drawingVisual);

            var encoder = new JpegBitmapEncoder();
            encoder.QualityLevel = 85;
            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

            using var stream = new FileStream(cachePath, FileMode.Create);
            encoder.Save(stream);
        }

        /// <summary>
        /// 根据文件路径和修改时间计算缓存键（纯英文 hash，不含中文）。
        /// </summary>
        private static string ComputeCacheKey(MediaFile file)
        {
            var input = $"{file.FilePath}_{file.DateModified.Ticks}_{ThumbnailSize}";
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()[..16];
        }
    }
}
