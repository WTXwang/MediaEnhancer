using System.Collections.Generic;

namespace MediaEnhancer.Core
{
    /// <summary>
    /// 增强算法根接口——所有增强方法的共同契约。
    /// 包含方法基本信息和参数定义，执行入口由子接口定义。
    /// </summary>
    public interface IEnhancementMethod
    {
        /// <summary>
        /// 增强方法名称（如 "线性拉伸"）。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 方法描述。
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 可调参数列表。
        /// </summary>
        List<EnhancementParameter> Parameters { get; }
    }
}
