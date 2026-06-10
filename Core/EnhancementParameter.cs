namespace MediaEnhancer.Core
{
    /// <summary>
    /// 增强算法参数模型，定义参数的名称、范围、步进和当前值。
    /// 用于 UI 滑块绑定和算法参数传递。
    /// </summary>
    public class EnhancementParameter
    {
        /// <summary>
        /// 参数显示名称（如 "对比度强度"）。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 参数键名，用于传递到算法实现（如 "contrast"）。
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// 当前值。
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// 最小值。
        /// </summary>
        public double Min { get; set; }

        /// <summary>
        /// 最大值。
        /// </summary>
        public double Max { get; set; }

        /// <summary>
        /// 滑块步进。
        /// </summary>
        public double Step { get; set; }
    }
}
