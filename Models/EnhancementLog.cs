using System;

namespace MediaEnhancer.Models
{
    /// <summary>
    /// 增强历史记录实体，记录每次文件增强操作。
    /// 关联源文件、增强方法、参数及输出结果，形成版本记录。
    /// </summary>
    public class EnhancementLog
    {
        /// <summary>
        /// 增强日志的唯一标识符。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 关联的源媒体文件 ID。
        /// </summary>
        public int MediaFileId { get; set; }

        /// <summary>
        /// 导航属性，关联的源媒体文件对象。
        /// </summary>
        public MediaFile MediaFile { get; set; } = null!;

        /// <summary>
        /// 使用的增强方法名称（如 "线性拉伸"、"CLAHE" 等）。
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// 增强后输出文件的完整路径。
        /// </summary>
        public string OutputPath { get; set; }

        /// <summary>
        /// 本次增强使用的参数（JSON 格式）。
        /// 例如 {"contrast":1.5,"brightness":10}，可用于复现历史增强效果。
        /// </summary>
        public string? ParametersJson { get; set; }

        /// <summary>
        /// 增强完成的时间。
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>归属用户 ID（外键）。</summary>
        public int UserId { get; set; }

        /// <summary>导航属性：归属用户。</summary>
        public User? User { get; set; }
    }
}
