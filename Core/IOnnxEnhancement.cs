using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MediaEnhancer.Core
{
    /// <summary>
    /// ONNX 深度学习模型增强接口——加载 .onnx 模型文件进行推理。
    /// 适用于去雾、去噪、超分辨率等低光照/画质增强等深度学习任务。
    /// 异步执行，避免阻塞 UI；支持 CPU/GPU 切换。
    ///
    /// 实现类只需提供模型文件名和推理逻辑，由调用方或基类管理 InferenceSession 生命周期。
    /// </summary>
    public interface IOnnxEnhancement : IEnhancementMethod
    {
        /// <summary>
        /// 模型文件名（如 "denoise.onnx"），不含路径。
        /// 模型文件位于运行目录的 OnnxModels/ 子目录下。
        /// </summary>
        string ModelFileName { get; }

        /// <summary>
        /// 推理尺寸上限（长边像素数）。超过此值的输入会先缩放到此尺寸，
        /// 推理后再放大回原始分辨率。0 表示不限制。
        /// 默认 480 适合实时场景，离线增强可设为 0 获得最佳画质。
        /// </summary>
        int MaxInferenceSize { get; }

        /// <summary>
        /// 对输入图像执行 ONNX 深度学习增强推理（使用内部缓存的模型）。
        /// 异步执行，适合离线文件处理。
        /// </summary>
        /// <param name="input">输入图像（任意分辨率）。</param>
        /// <returns>增强后的图像。</returns>
        Task<BitmapSource> EnhanceAsync(BitmapSource input);

        /// <summary>
        /// 使用指定路径的模型文件执行增强推理。
        /// 适合从外部加载模型或测试不同权重文件的场景。
        /// </summary>
        /// <param name="input">输入图像。</param>
        /// <param name="modelPath">模型文件的完整路径。</param>
        /// <returns>增强后的图像。</returns>
        Task<BitmapSource> EnhanceAsync(BitmapSource input, string modelPath);
    }
}
