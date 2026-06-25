using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.ML.OnnxRuntime;

namespace MediaEnhancer.Core;

/// <summary>
/// Multinex Nano 低光照增强——基于 ONNX Runtime 的 Retinex 深度学习模型。
///
/// 模型参数 ~15K，预训练于 LOL-v2 synthetic 数据集。
/// 输入/输出格式：NCHW float32 [0,1] RGB，任意分辨率。
///
/// 同时实现 IRealTimeEnhancer 和 IOnnxEnhancement 双接口：
///   - IRealTimeEnhancer.Enhance(byte[])  → 全屏增强、视频增强调用（全分辨率，每帧 ~30-150ms CPU）
///   - IOnnxEnhancement.EnhanceAsync()     → 单图/批量离线增强调用（全分辨率，异步）
///
/// ONNX Session 延迟加载至首次调用，双重检查锁定保证线程安全。
/// </summary>
public class MultinexNanoMethod : IRealTimeEnhancer, IOnnxEnhancement
{
    /// <summary>ONNX 推理会话，延迟初始化，首次调用时才加载模型文件。</summary>
    private InferenceSession? _session;

    /// <summary>双重检查锁定的同步对象。</summary>
    private readonly object _lock = new();

    public string Name => "Multinex Nano 低光增强";
    public string Description => "超轻量 Retinex 增强（15K 参数），适合实时场景";

    /// <summary>无可调参数——ONNX 模型的增强效果完全由训练权重决定。</summary>
    public List<EnhancementParameter> Parameters { get; } = new();

    // ================================================================
    // IRealTimeEnhancer — 实时 byte[] 路径
    // ================================================================

    /// <summary>15K 参数，CPU 推理约 30ms/帧，勉强实时。</summary>
    public bool SupportsRealTime => true;

    /// <summary>
    /// 实时逐帧增强（全屏覆盖/视频增强）。
    /// 流程：BGRA32 bytes → NCHW float32 → ONNX 推理 → NCHW float32 → BGRA32 bytes。
    /// MaxInferenceSize=0 表示全分辨率推理，不做降采样。
    /// </summary>
    public byte[] Enhance(byte[] pixels, int width, int height, int stride,
        IReadOnlyDictionary<string, double>? parameters = null)
    {
        EnsureSession();
        // BGRA32 → NCHW（maxSize=0 全分辨率，不做降采样）
        var (nchw, iw, ih, ow, oh, downscaled) =
            OnnxModelHelper.Preprocess(pixels, width, height, stride, 0);
        // ONNX 推理：输入 [1,3,H,W] → 输出 [1,3,H,W]
        var result = OnnxModelHelper.RunInference(_session!, nchw, ih, iw);
        // NCHW → BGRA32
        return OnnxModelHelper.Postprocess(result, ih, iw, ow, oh, stride, downscaled);
    }

    // ================================================================
    // IOnnxEnhancement — 离线 BitmapSource 异步路径
    // ================================================================

    public string ModelFileName => "multinex_nano_lolv2_syn.onnx";

    /// <summary>离线增强全分辨率推理，不做降采样。</summary>
    public int MaxInferenceSize => 0;

    /// <summary>
    /// 离线图片增强（使用内部缓存的 ONNX Session）。
    /// 像素读取在 UI 线程完成（WPF 要求），推理计算在 Task.Run 中异步执行。
    /// </summary>
    public async Task<BitmapSource> EnhanceAsync(BitmapSource input)
    {
        EnsureSession();
        // WPF BitmapSource 的像素操作必须在 UI 线程 —— 在外层完成读取
        int w = input.PixelWidth, h = input.PixelHeight, stride = w * 4;
        byte[] pixels = new byte[stride * h];
        input.CopyPixels(pixels, stride, 0);

        // 推理计算扔到线程池，避免阻塞 UI
        return await Task.Run(() =>
        {
            var (nchw, _, _, _, _, _) = OnnxModelHelper.Preprocess(pixels, w, h, stride, 0);
            var result = OnnxModelHelper.RunInference(_session!, nchw, h, w);
            return OnnxModelHelper.Postprocess(result, h, w);
        });
    }

    /// <summary>
    /// 使用外部模型文件路径执行增强（每次新建 Session，用后即弃）。
    /// 适合测试不同权重文件或加载用户自备模型的场景。
    /// </summary>
    public async Task<BitmapSource> EnhanceAsync(BitmapSource input, string modelPath)
    {
        return await Task.Run(() =>
        {
            // 每次调用新建独立 Session，用完即释放 —— 不做缓存
            using var session = new InferenceSession(modelPath);
            var nchw = OnnxModelHelper.Preprocess(input);
            var result = OnnxModelHelper.RunInference(session, nchw,
                input.PixelHeight, input.PixelWidth);
            return OnnxModelHelper.Postprocess(result,
                input.PixelHeight, input.PixelWidth);
        });
    }

    // ================================================================
    // 内部
    // ================================================================

    /// <summary>
    /// 确保 ONNX Session 已加载（双重检查锁定模式）。
    /// 首次调用时从 OnnxModels/ 目录加载模型文件，后续调用复用缓存的 Session。
    /// 线程安全：多线程同时首次调用时，只有一个线程创建 Session，其他线程等待后复用。
    /// </summary>
    private void EnsureSession()
    {
        if (_session != null) return;          // 快速路径：已加载
        lock (_lock)
        {
            if (_session != null) return;      // 双重检查：锁内再次确认
            _session = new InferenceSession(System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "OnnxModels", ModelFileName));
        }
    }

    /// <summary>
    /// 释放 ONNX Session。加锁保护，避免与 EnsureSession 并发访问。
    /// </summary>
    public void Dispose()
    {
        lock (_lock) { _session?.Dispose(); _session = null; }
    }
}
