using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.ML.OnnxRuntime;

namespace MediaEnhancer.Core;

/// <summary>
/// Multinex Nano 低光照增强（ONNX 推理），超轻量 ~15K 参数。
/// LOL-v2 synthetic 预训练权重，比原版快约 3 倍，更适合实时场景。
/// </summary>
public class MultinexNanoMethod : IRealTimeEnhancer, IOnnxEnhancement
{
    private InferenceSession? _session;
    private readonly object _lock = new();

    public string Name => "Multinex Nano 低光增强";
    public string Description => "超轻量 Retinex 增强（15K 参数），适合实时场景";

    public List<EnhancementParameter> Parameters { get; } = new();

    // IRealTimeEnhancer
    public bool SupportsRealTime => true; // 15K 参数，仅有的可实时推理的 ONNX 方法
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
    public string ModelFileName => "multinex_nano_lolv2_syn.onnx";
    public int MaxInferenceSize => 0;

    public async Task<BitmapSource> EnhanceAsync(BitmapSource input)
    {
        EnsureSession();
        // 在 UI 线程读取像素，避免 Task.Run 内访问 WPF 对象
        int w = input.PixelWidth, h = input.PixelHeight, stride = w * 4;
        byte[] pixels = new byte[stride * h];
        input.CopyPixels(pixels, stride, 0);

        return await Task.Run(() =>
        {
            var (nchw, _, _, _, _, _) = OnnxModelHelper.Preprocess(pixels, w, h, stride, 0);
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
