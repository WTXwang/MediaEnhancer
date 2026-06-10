using System.Diagnostics;
using System.IO;
using MediaEnhancer.Core;

namespace MediaEnhancer.Services;

/// <summary>
/// 视频增强器：解帧 → 逐帧增强 → 合帧（含音轨）→ 输出 MP4。
/// 支持 CancellationToken 取消。
/// </summary>
public class VideoEnhancer
{
    private readonly IEnhancementMethod _method;

    public VideoEnhancer(IEnhancementMethod method) { _method = method; }

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

            // 3. 逐帧增强
            var total = frameFiles.Count;
            var realTime = _method as IRealTimeEnhancer;

            await Task.Run(() =>
            {
                for (int i = 0; i < total; i++)
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        using var src = new System.Drawing.Bitmap(frameFiles[i]);
                        int w = src.Width, h = src.Height;
                        var data = src.LockBits(
                            new System.Drawing.Rectangle(0, 0, w, h),
                            System.Drawing.Imaging.ImageLockMode.ReadOnly,
                            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                        int stride = data.Stride;
                        byte[] pixels = new byte[stride * h];
                        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                        src.UnlockBits(data);

                        byte[] enhanced = realTime?.Enhance(pixels, w, h, stride, null) ?? pixels;

                        using var outBmp = new System.Drawing.Bitmap(w, h,
                            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                        var outData = outBmp.LockBits(
                            new System.Drawing.Rectangle(0, 0, w, h),
                            System.Drawing.Imaging.ImageLockMode.WriteOnly,
                            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                        for (int y = 0; y < h; y++)
                            System.Runtime.InteropServices.Marshal.Copy(enhanced, y * stride,
                                outData.Scan0 + y * outData.Stride, outData.Stride);
                        outBmp.UnlockBits(outData);
                        outBmp.Save(frameFiles[i], System.Drawing.Imaging.ImageFormat.Jpeg);
                    }
                    catch { }

                    progress?.Report((i + 1, total));
                }
            }, ct);

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

            try { Directory.Delete(tempDir, true); } catch { }
            return outputPath;
        }
        catch
        {
            return null;
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
