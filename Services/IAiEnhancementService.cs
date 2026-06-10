using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MediaEnhancer.Services
{
    /// <summary>
    /// AI 增强服务接口（预留）。
    ///
    /// 未来计划：接入深度学习模型（如 MAXIM、ESRGAN 等）实现
    /// 去噪、去雾、超分辨率等高级增强功能。
    ///
    /// 当前版本暂不实现，调用时将提示"功能未实现"。
    /// </summary>
    public interface IAiEnhancementService
    {
        /// <summary>
        /// 对图像执行 AI 增强。
        /// </summary>
        /// <param name="input">输入图像。</param>
        /// <param name="modelName">模型名称（如 "MAXIM"、"ESRGAN" 等）。</param>
        /// <returns>增强后的图像。</returns>
        Task<BitmapSource> AiEnhanceAsync(BitmapSource input, string modelName);
    }
}
