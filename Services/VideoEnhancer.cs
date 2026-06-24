using System.Diagnostics;
using System.IO;
using MediaEnhancer.Core;

namespace MediaEnhancer.Services;

/// <summary>
/// 视频增强器——四阶段管线：
///   1. FFmpeg 解帧：视频 → JPEG 帧序列
///   2. 提取音轨：从原视频提取 AAC 音频流
///   3. 逐帧增强：WPF 加载 JPEG → 增强算法处理 → WPF 写回 JPEG
///   4. FFmpeg 合帧：增强后的帧 + 原音轨 → 合成 MP4
///
/// 帧 I/O 使用 WPF（BitmapImage + JpegBitmapEncoder），避免 GDI+ ExternalException。
/// 支持 CancellationToken 随时取消。
/// </summary>
public class VideoEnhancer
{
    private readonly IRealTimeEnhancer _enhancer;
    private readonly IReadOnlyDictionary<string, double>? _parameters;

    public VideoEnhancer(IRealTimeEnhancer enhancer,
        IReadOnlyDictionary<string, double>? parameters = null)
    {
        _enhancer = enhancer;
        _parameters = parameters;
    }

    public async Task<string?> EnhanceAsync(string inputPath, string outputDir,
        IProgress<(int current, int total)>? progress = null,
        CancellationToken ct = default)
    {
        var ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
        if (!File.Exists(ffmpegPath)) return null;

        var tempDir = Path.Combine(Path.GetTempPath(), "VideoEnhance", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(outputDir);

        try
        {
            if (ct.IsCancellationRequested) return null;

            var outputName = $"enhanced_{Path.GetFileNameWithoutExtension(inputPath)}_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
            var outputPath = Path.Combine(outputDir, outputName);

            var fps = DetectFrameRate(ffmpegPath, inputPath);
            var fpsStr = fps > 0 ? fps.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "24";

            var framesDir = Path.Combine(tempDir, "frames");
            Directory.CreateDirectory(framesDir);
            var framePattern = Path.Combine(framesDir, "%08d.jpg");

            // 1. 解帧
            if (!await RunFfmpegAsync(ffmpegPath,
                $"-y -hide_banner -loglevel error -i \"{inputPath}\" -q:v 2 \"{framePattern}\"",
                120_000, ct))
                return null;

            var frameFiles = Directory.GetFiles(framesDir, "*.jpg").OrderBy(f => f).ToList();
            if (frameFiles.Count == 0) return null;
            if (ct.IsCancellationRequested) return null;

            // 2. 提音轨
            var audioPath = Path.Combine(tempDir, "audio.aac");
            bool hasAudio = await RunFfmpegAsync(ffmpegPath,
                $"-y -hide_banner -loglevel error -i \"{inputPath}\" -vn -acodec copy \"{audioPath}\"",
                30_000, ct);
            if (!hasAudio || !File.Exists(audioPath) || new FileInfo(audioPath).Length < 100)
            {
                hasAudio = false;
                try { File.Delete(audioPath); } catch { }
            }

            // 3. 逐帧增强（WPF 原生解码/编码，不用 GDI+避免 ExternalException）
            var total = frameFiles.Count;
            string? firstError = null;

            for (int i = 0; i < total; i++)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    // WPF 加载 JPEG（UI 线程安全）
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(frameFiles[i]);
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();

                    int w = bmp.PixelWidth, h = bmp.PixelHeight, stride = w * 4;
                    byte[] pixels = new byte[stride * h];
                    bmp.CopyPixels(pixels, stride, 0);

                    // 增强计算扔到线程池
                    byte[] enhanced = await Task.Run(() =>
                        _enhancer.Enhance(pixels, w, h, stride, _parameters), ct);

                    // WPF 保存 JPEG
                    var result = System.Windows.Media.Imaging.BitmapSource.Create(
                        w, h, 96, 96,
                        System.Windows.Media.PixelFormats.Bgra32, null, enhanced, stride);
                    var encoder = new System.Windows.Media.Imaging.JpegBitmapEncoder
                    {
                        QualityLevel = 92
                    };
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(result));
                    using var stream = new FileStream(frameFiles[i], FileMode.Create);
                    encoder.Save(stream);
                }
                catch (Exception ex)
                {
                    firstError ??= ex.Message;
                    Debug.WriteLine($"视频帧 {i} 增强失败: {ex.Message}");
                    progress?.Report((-(i + 1), total)); // 负值表示失败，让 UI 显示错误
                }

                progress?.Report((i + 1, total));
            }

            if (firstError != null)
            {
                Debug.WriteLine($"有帧增强失败，但继续编码（已成功帧已保存）: {firstError}");
            }

            if (ct.IsCancellationRequested) return null;

            // 4. 合帧 + 音轨
            var codecs = new[] { "h264_nvenc", "h264_qsv", "h264_amf", "libx264", "mpeg4" };
            bool success = false;

            foreach (var codec in codecs)
            {
                if (ct.IsCancellationRequested) return null;

                var args = $"-y -hide_banner -loglevel error -framerate {fpsStr} -start_number 0 -i \"{framePattern}\"";
                if (hasAudio) args += $" -i \"{audioPath}\"";
                args += $" -c:v {codec}";
                switch (codec)
                {
                    case "libx264": args += " -preset ultrafast -crf 23"; break;
                    case "h264_nvenc": args += " -preset p1 -qp 23"; break;
                    case "h264_qsv": args += " -preset veryfast -global_quality 23"; break;
                    case "h264_amf": args += " -usage lowlatency_high_quality -qp_i 23 -qp_p 23"; break;
                    case "mpeg4": args += " -q:v 3"; break;
                }
                args += " -pix_fmt yuv420p";
                if (hasAudio) args += " -c:a copy -shortest";
                args += $" -movflags +faststart \"{outputPath}\"";

                if (await RunFfmpegAsync(ffmpegPath, args, 300_000, ct))
                {
                    success = true;
                    break;
                }
            }

            if (ct.IsCancellationRequested || !success) return null;

            return outputPath;
        }
        catch
        {
            return null;
        }
        finally
        {
            // 无论成功或失败都清理临时帧目录，避免 %TEMP% 堆积
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private static double DetectFrameRate(string ffmpegPath, string videoPath)
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-i \"{videoPath}\"",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (proc == null) return 0;
            var stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit(3000);

            var tokens = stderr.Split(',', ' ', '\t');
            for (int i = 1; i < tokens.Length; i++)
            {
                if (tokens[i].Trim() == "fps" &&
                    double.TryParse(tokens[i - 1].Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var f))
                    return f;
            }
        }
        catch { }
        return 0;
    }

    private static async Task<bool> RunFfmpegAsync(string ffmpegPath, string args,
        int timeoutMs, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = args,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return false;

            var stderrTask = proc.StandardError.ReadToEndAsync();
            var exited = await Task.Run(() => proc.WaitForExit(timeoutMs), ct);
            if (!exited) { try { proc.Kill(); } catch { } return false; }

            await stderrTask;
            return proc.ExitCode == 0;
        }
        catch (OperationCanceledException) { return false; }
        catch { return false; }
    }
}
