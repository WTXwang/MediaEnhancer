using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using MediaEnhancer.Core;
using MediaEnhancer.Views;

namespace MediaEnhancer.Services;

/// <summary>
/// 屏幕录制器（纯视频，不含音频）：
///   1. 录制：逐帧保存原始 JPEG → outputDir/rec_{timestamp}_frames/
///   2. 后处理增强（可选）：停止录制后，逐帧增强已保存的 JPEG
///   3. 编码：ffmpeg 从帧目录合成 MP4
///   4. 成功：删帧目录，返回 MP4 路径
///   5. 失败：保留帧目录（用户可手动合成）
///
/// 增强在录制结束后执行，保证录制期间帧率不受影响。
/// 局限：不支持音频录制、不支持麦克风采集。
/// </summary>
public class ScreenRecorder : IDisposable
{
    private readonly int _fps;
    private int _screenW, _screenH;
    private int _screenLeft, _screenTop;      // 主显示器虚拟桌面坐标
    private readonly string _outputDir;
    private readonly IRealTimeEnhancer? _enhancer;
    private readonly IOnnxEnhancement? _offlineEnhancer;
    private readonly IReadOnlyDictionary<string, double>? _enhancerParams;

    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _enhanceCts;
    private Task? _recordingTask;
    private string? _framesDir;
    private DateTime _startTime;
    private DateTime _firstFrameTime;
    private DateTime _lastFrameTime;
    private int _frameCount;
    private string? _errorMsg;

    public bool IsRecording => _cts != null && !_cts.IsCancellationRequested;
    public double DurationSeconds { get; private set; }
    public string? OutputPath { get; private set; }
    public string? LastError => _errorMsg;
    public int FrameCount => _frameCount;

    public ScreenRecorder(int screenW, int screenH, string outputDir,
        IRealTimeEnhancer? enhancer = null,
        IOnnxEnhancement? offlineEnhancer = null,
        IReadOnlyDictionary<string, double>? enhancerParams = null,
        int fps = 15)
    {
        _screenW = screenW;
        _screenH = screenH;
        // 使用主显示器在虚拟桌面中的实际坐标（多显示器下原点可能不为 (0,0)）
        var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
        _screenLeft = primaryScreen.Bounds.X;
        _screenTop = primaryScreen.Bounds.Y;
        _outputDir = outputDir;
        _enhancer = enhancer;
        _offlineEnhancer = offlineEnhancer;
        _enhancerParams = enhancerParams;
        _fps = Math.Clamp(fps, 1, 60);
    }

    /// <summary>
    /// 开始录制：创建帧目录 → 后台启动捕获循环。
    /// 捕获循环在独立 Task 中运行，不阻塞 UI。
    /// </summary>
    public void Start()
    {
        if (IsRecording) return;
        _errorMsg = null;
        _frameCount = 0;
        _startTime = DateTime.UtcNow;

        // 创建输出目录和帧缓存目录（rec_{时间戳}_frames）
        Directory.CreateDirectory(_outputDir);
        _framesDir = Path.Combine(_outputDir, $"rec_{DateTime.Now:yyyyMMdd_HHmmss}_frames");
        Directory.CreateDirectory(_framesDir);

        _cts = new CancellationTokenSource();
        _recordingTask = Task.Run(() => RecordingLoop(_cts.Token));
    }

    public async Task<string?> StopAsync(IProgress<string>? status = null)
    {
        _cts?.Cancel();

        if (_recordingTask != null)
            try { await Task.WhenAny(_recordingTask, Task.Delay(5000)); } catch { }

        if (_frameCount == 0 || _framesDir == null)
        {
            _errorMsg = "未捕获到任何帧。";
            return null;
        }

        // ================================================================
        // 后处理增强：录屏结束后逐帧增强（不再实时增强，保证录制帧率）
        // ================================================================
        if (_offlineEnhancer != null || _enhancer != null)
        {
            try
            {
                _enhanceCts = new CancellationTokenSource();
                await Task.Run(() => EnhanceFrames(_framesDir, _enhanceCts.Token, status));
            }
            catch (Exception ex)
            {
                _errorMsg = $"逐帧增强失败: {ex.Message}";
                // 即使增强失败也继续编码（使用原始帧）
            }
            finally
            {
                _enhanceCts?.Dispose();
                _enhanceCts = null;
            }
        }

        double videoDuration = (_lastFrameTime - _firstFrameTime).TotalSeconds;
        double realFps = videoDuration > 0 && _frameCount > 1
            ? (_frameCount - 1) / videoDuration
            : _fps;

        status?.Report("正在编码视频...");
        var mp4Path = Path.Combine(_outputDir, Path.GetFileName(_framesDir).Replace("_frames", ".mp4"));

        if (await TryFfmpegAsync(_framesDir, mp4Path, realFps))
        {
            try { Directory.Delete(_framesDir, true); } catch { }
            OutputPath = mp4Path;
            return mp4Path;
        }

        OutputPath = _framesDir;
        return _framesDir;
    }

    /// <summary>
    /// 对已保存的 JPEG 帧逐张执行增强，原地覆盖。
    /// 优先使用离线 ONNX 方法（全分辨率、异步），回退实时方法（线性拉伸等）。
    /// 帧 I/O 全部使用 WPF BitmapImage/JpegBitmapEncoder——避免 GDI+ ExternalException。
    /// </summary>
    private void EnhanceFrames(string framesDir, CancellationToken ct,
        IProgress<string>? status = null)
    {
        var frameFiles = Directory.GetFiles(framesDir, "*.jpg").OrderBy(f => f).ToArray();
        int total = frameFiles.Length;
        if (total == 0) return;
        if (_offlineEnhancer == null && _enhancer == null) return;

        var methodName = _offlineEnhancer?.Name ?? _enhancer?.Name ?? "增强";

        for (int i = 0; i < total; i++)
        {
            var path = frameFiles[i];
            if (ct.IsCancellationRequested) break;

            status?.Report($"增强中... {i + 1}/{total}");

            try
            {
                // 优先使用离线 ONNX 方法
                if (_offlineEnhancer != null)
                {
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(path);
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();

                    var enhanced = _offlineEnhancer.EnhanceAsync(bmp).GetAwaiter().GetResult();

                    var encoder = new System.Windows.Media.Imaging.JpegBitmapEncoder { QualityLevel = 92 };
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(enhanced));
                    using var stream = new FileStream(path, FileMode.Create);
                    encoder.Save(stream);
                    continue;
                }

                // 回退实时方法（线性拉伸等）—— 用 WPF 避免 GDI+ ExternalException
                var bmp2 = new System.Windows.Media.Imaging.BitmapImage();
                bmp2.BeginInit();
                bmp2.UriSource = new Uri(path);
                bmp2.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp2.EndInit();
                bmp2.Freeze();

                int w2 = bmp2.PixelWidth, h2 = bmp2.PixelHeight, stride2 = w2 * 4;
                byte[] pixels2 = new byte[stride2 * h2];
                bmp2.CopyPixels(pixels2, stride2, 0);

                byte[] enhanced2 = _enhancer!.Enhance(pixels2, w2, h2, stride2, _enhancerParams);

                var result2 = System.Windows.Media.Imaging.BitmapSource.Create(
                    w2, h2, 96, 96,
                    System.Windows.Media.PixelFormats.Bgra32, null, enhanced2, stride2);
                var encoder2 = new System.Windows.Media.Imaging.JpegBitmapEncoder
                {
                    QualityLevel = 92
                };
                encoder2.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(result2));
                using var stream2 = new FileStream(path, FileMode.Create);
                encoder2.Save(stream2);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"录屏帧增强失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 录制循环（在后台 Task 中运行）。
    /// 每帧：截图 → 存 JPEG。录制期间不做增强，保证帧率稳定。
    /// </summary>
    private async Task RecordingLoop(CancellationToken ct)
    {
        // 帧间隔：如 15fps → 1000/15 ≈ 66.7ms/帧
        var frameInterval = TimeSpan.FromMilliseconds(1000.0 / _fps);

        // 优先 DXGI Desktop Duplication（GPU 零拷贝），失败回退 GDI
        using var capture = new DxgiScreenCapture();
        bool useDxgi = capture.Initialize();
        if (useDxgi) { _screenW = capture.Width; _screenH = capture.Height; }

        while (!ct.IsCancellationRequested)
        {
            var frameStart = DateTime.UtcNow;
            DurationSeconds = (DateTime.UtcNow - _startTime).TotalSeconds;

            try
            {
                byte[]? pixels = null;
                int stride = _screenW * 4;

                // DXGI 捕获（GPU 零拷贝，性能最优）
                if (useDxgi)
                    pixels = capture.CaptureFrame(maxWaitMs: (int)frameInterval.TotalMilliseconds);

                // GDI 回退：DXGI 不可用时（无 GPU、驱动不支持、桌面锁定等）
                // CopyFromScreen 从合成窗口缓冲区读取，兼容性好但性能较低
                if (pixels == null)
                {
                    using var bmp = new System.Drawing.Bitmap(_screenW, _screenH,
                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using var g = System.Drawing.Graphics.FromImage(bmp);
                    g.CopyFromScreen(_screenLeft, _screenTop, 0, 0,
                        new System.Drawing.Size(_screenW, _screenH));
                    var data = bmp.LockBits(
                        new System.Drawing.Rectangle(0, 0, _screenW, _screenH),
                        System.Drawing.Imaging.ImageLockMode.ReadOnly,
                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    stride = data.Stride;
                    pixels = new byte[stride * _screenH];
                    Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                    bmp.UnlockBits(data);
                }

                if (pixels == null) continue;

                // 录制时不再实时增强——增强移至 StopAsync 后处理阶段
                // 保证录制帧率不受增强算法影响

                var now = DateTime.UtcNow;
                if (_frameCount == 0) _firstFrameTime = now;
                _lastFrameTime = now;

                // 录制时不增强——增强移到停止后后处理阶段。
                // 这样即使 ONNX 推理耗时 > 帧间隔，也不会掉帧。
                SaveAsJpeg(pixels, _screenW, _screenH, stride,
                    Path.Combine(_framesDir!, $"{_frameCount:D8}.jpg"));
                _frameCount++;
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { Debug.WriteLine($"帧异常: {ex.Message}"); }

            // 帧率控制：计算剩余时间，>2ms 才 sleep（避免微秒级 sleep 的线程调度开销）
            var delay = frameInterval - (DateTime.UtcNow - frameStart);
            if (delay > TimeSpan.FromMilliseconds(2))
            {
                try { await Task.Delay(delay, ct); }
                catch { break; }
            }
        }
    }

    /// <summary>
    /// FFmpeg 编码帧序列为 MP4。
    /// 编码器五级降级链：NVENC → QSV → AMF → libx264 → mpeg4。
    /// 硬件编码优先（低延迟、低 CPU），失败回退软件编码。
    /// </summary>
    private async Task<bool> TryFfmpegAsync(string framesDir, string mp4Path, double realFps)
    {
        var ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
        if (!File.Exists(ffmpegPath)) { _errorMsg = "ffmpeg.exe 未找到。"; return false; }

        var framePattern = framesDir.Replace('\\', '/') + "/%08d.jpg";
        // 1.NVENC(NVIDIA) → 2.QSV(Intel) → 3.AMF(AMD) → 4.libx264(CPU,兼容最好) → 5.mpeg4(最后兜底)
        var codecs = new[] { "h264_nvenc", "h264_qsv", "h264_amf", "libx264", "mpeg4" };
        var fpsStr = realFps.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);

        foreach (var codec in codecs)
        {
            var args = new List<string>
            {
                "-y", "-hide_banner", "-loglevel", "error",
                "-framerate", fpsStr,
                "-start_number", "0",
                "-i", framePattern,
                "-c:v", codec
            };

            switch (codec)
            {
                case "libx264":   args.AddRange(["-preset", "ultrafast", "-crf", "23"]); break;
                case "h264_nvenc": args.AddRange(["-preset", "p1", "-qp", "23"]); break;
                case "h264_qsv":  args.AddRange(["-preset", "veryfast", "-global_quality", "23"]); break;
                case "h264_amf":  args.AddRange(["-usage", "lowlatency_high_quality", "-qp_i", "23", "-qp_p", "23"]); break;
                case "mpeg4":     args.AddRange(["-q:v", "3"]); break;
            }
            args.AddRange(["-pix_fmt", "yuv420p", "-movflags", "+faststart", mp4Path]);

            var argStr = string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));

            try
            {
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = argStr,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (proc == null) continue;

                await proc.StandardError.ReadToEndAsync();
                if (!await Task.Run(() => proc.WaitForExit(120_000))) { try { proc.Kill(); } catch { } continue; }

                if (proc.ExitCode != 0) continue;
                return true;
            }
            catch { continue; }
        }

        _errorMsg = "所有编码器均失败。";
        return false;
    }

    private static void SaveAsJpeg(byte[] pixels, int w, int h, int stride, string path)
    {
        using var bmp = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, w, h),
            System.Drawing.Imaging.ImageLockMode.WriteOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        for (int y = 0; y < h; y++)
            Marshal.Copy(pixels, y * stride, data.Scan0 + y * data.Stride, data.Stride);
        bmp.UnlockBits(data);
        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Jpeg);
    }

    /// <summary>
    /// 取消后处理增强（录制已停止，仅中断逐帧增强环节，直接进入编码）。
    /// </summary>
    public void CancelEnhancement()
    {
        _enhanceCts?.Cancel();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        GC.SuppressFinalize(this);
    }
}
