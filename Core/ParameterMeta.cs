namespace MediaEnhancer.Core
{
    /// <summary>
    /// 增强算法参数的元数据——描述参数的名称、范围、默认值等信息。
    /// 与 EnhancementParameter 不同，此类只描述参数的"形状"而非"当前值"，
    /// 供 UI 自动生成滑块控件，以及方法自描述。
    /// </summary>
    public class ParameterMeta
    {
        /// <summary>
        /// 参数显示名称（如 "对比度强度"）。
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 参数键名，用于传递到算法实现（如 "contrast"）。
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// 默认值。
        /// </summary>
        public double DefaultValue { get; set; }

        /// <summary>
        /// 最小值。
        /// </summary>
        public double MinValue { get; set; }

        /// <summary>
        /// 最大值。
        /// </summary>
        public double MaxValue { get; set; }

        /// <summary>
        /// 滑块步进。
        /// </summary>
        public double Step { get; set; } = 1.0;

        /// <summary>
        /// 参数说明（tooltip）。
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }
}
