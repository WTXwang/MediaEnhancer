using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MediaEnhancer.Core
{
    /// <summary>
    /// ONNX 深度学习模型增强接口——加载 .onnx 模型文件进行推理。
    /// 适用于去雾、去噪、超分辨率等深度学习增强任务。
    /// 异步执行，支持 CPU/GPU 切换。
    /// </summary>
    public interface IOnnxEnhancement : IEnhancementMethod
    {
        /// <summary>
        /// 模型文件名（如 "denoise.onnx"）。
        /// 模型文件应放置在运行目录的 models/ 文件夹下。
        /// </summary>
        string ModelFileName { get; }

        /// <summary>
        /// 使用指定模型对输入图像执行增强推理。
        /// </summary>
        /// <param name="input">输入图像。</param>
        /// <param name="modelPath">模型文件的完整路径。</param>
        /// <returns>增强后的图像。</returns>
        Task<BitmapSource> EnhanceAsync(BitmapSource input, string modelPath);
    }
}
