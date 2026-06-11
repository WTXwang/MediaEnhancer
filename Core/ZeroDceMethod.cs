using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.ML.OnnxRuntime;

namespace MediaEnhancer.Core;

/// <summary>
/// Zero-DCE++ 低光照增强（ONNX 推理），80K 参数。
/// MaxInferenceSize=480 时全屏增强接近实时。
/// </summary>
public class ZeroDceMethod : IRealTimeEnhancer, IOnnxEnhancement
{
    private InferenceSession? _session;
    private readonly object _lock = new();

    public string Name => "Zero-DCE++ 低光增强";
    public string Description => "轻量级深度曲线估计，可降采样实时推理";

    public List<EnhancementParameter> Parameters { get; } = new();

    // IRealTimeEnhancer
    public bool SupportsRealTime => false;
    public IReadOnlyDictionary<string, ParameterMeta> GetParameters() =>
        new Dictionary<string, ParameterMeta>();

    public byte[] Enhance(byte[] pixels, int width, int height, int stride,
        IReadOnlyDictionary<string, double>? parameters = null)
    {
        EnsureSession();
        var (nchw, iw, ih, ow, oh, downscaled) =
            OnnxModelHelper.Preprocess(pixels, width, height, stride, 0);
        var result = OnnxModelHelper.RunInference(_session!, nchw, ih, iw);
        return OnnxModelHelper.Postprocess(result, ih, iw, ow, oh, stride, downscaled);
    }

    // IOnnxEnhancement
    public string ModelFileName => "zero_dce_plus_plus_s1.onnx";
    public int MaxInferenceSize => 480;

    public async Task<BitmapSource> EnhanceAsync(BitmapSource input)
    {
        EnsureSession();
        return await Task.Run(() =>
        {
            // 离线增强：全分辨率，不降采样
            int w = input.PixelWidth, h = input.PixelHeight, stride = w * 4;
            byte[] pixels = new byte[stride * h];
            input.CopyPixels(pixels, stride, 0);

            var nchw = OnnxModelHelper.Preprocess(input);
            var result = OnnxModelHelper.RunInference(_session!, nchw, h, w);
            return OnnxModelHelper.Postprocess(result, h, w);
        });
    }

    public async Task<BitmapSource> EnhanceAsync(BitmapSource input, string modelPath)
    {
        return await Task.Run(() =>
        {
            using var session = new InferenceSession(modelPath);
            var nchw = OnnxModelHelper.Preprocess(input);
            var result = OnnxModelHelper.RunInference(session, nchw,
                input.PixelHeight, input.PixelWidth);
            return OnnxModelHelper.Postprocess(result,
                input.PixelHeight, input.PixelWidth);
        });
    }

    private void EnsureSession()
    {
        if (_session != null) return;
        lock (_lock)
        {
            if (_session != null) return;
            _session = new InferenceSession(System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "OnnxModels", ModelFileName));
        }
    }

    public void Dispose()
    {
        lock (_lock) { _session?.Dispose(); _session = null; }
    }
}
