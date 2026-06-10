using System.Collections.Generic;

namespace MediaEnhancer.Core
{
    /// <summary>
    /// 实时增强器接口——用于逐帧屏幕增强的字节数组级操作。
    ///
    /// 设计原则：
    ///   1. 操作原始 BGRA32 字节数组，避免 WPF 对象创建开销，保证逐帧性能。
    ///   2. 方法自描述参数元数据，UI 可据此自动生成调节滑块。
    ///   3. SupportsRealTime 标志区分"能跟上帧率"的方法和"离线专用"的方法。
    ///   4. 消费者（如 FullscreenEnhanceWindow）只依赖此接口，不依赖具体算法。
    ///
    /// 新增算法只需实现此接口并注册到 EnhancementRegistry 即可，
    /// 无需修改任何窗口或 ViewModel 代码。
    /// </summary>
    public interface IRealTimeEnhancer : IEnhancementMethod
    {
        /// <summary>
        /// 增强方法名称（如 "线性拉伸"、"直方图均衡化"）。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 方法简要描述。
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 是否支持实时处理（延迟低于每帧预算，通常 &lt; 33ms 即可达 30 FPS）。
        /// false 的方法不会出现在实时增强的方法列表中。
        /// </summary>
        bool SupportsRealTime { get; }

        /// <summary>
        /// 对 BGRA32 格式的像素数据执行增强。
        /// </summary>
        /// <param name="pixels">输入像素（BGRA32，每像素 4 字节，B G R A 顺序）。</param>
        /// <param name="width">图像宽度（像素）。</param>
        /// <param name="height">图像高度（像素）。</param>
        /// <param name="stride">每行字节数（通常为 width * 4，可能因对齐而更大）。</param>
        /// <param name="parameters">可选参数字典（键为参数 Key，值为当前设置）。为 null 时使用默认值。</param>
        /// <returns>增强后的像素数据（相同格式和尺寸）。</returns>
        byte[] Enhance(byte[] pixels, int width, int height, int stride,
                       IReadOnlyDictionary<string, double>? parameters = null);

        /// <summary>
        /// 获取该方法可调参数的元数据。
        /// 返回字典的键为参数 Key（如 "contrast"），值为参数描述。
        /// </summary>
        IReadOnlyDictionary<string, ParameterMeta> GetParameters();
    }
}
