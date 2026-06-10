using System.Windows.Media.Imaging;

namespace MediaEnhancer.Core
{
    /// <summary>
    /// C# 原生增强算法接口——像素级直接操作，用于实时增强。
    /// 同步执行，适用于线性拉伸、直方图均衡化等传统图像算法。
    /// 典型性能：< 0.1ms/帧。
    /// </summary>
    public interface INativeEnhancement : IEnhancementMethod
    {
        /// <summary>
        /// 对输入图像执行增强，返回增强后的图像。
        /// </summary>
        /// <param name="input">输入图像。</param>
        /// <returns>增强后的图像。</returns>
        BitmapSource Enhance(BitmapSource input);
    }
}
