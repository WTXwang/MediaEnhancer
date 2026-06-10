using System;

namespace MediaEnhancer.Models
{
    /// <summary>
    /// 缩略图表，管理媒体文件的缩略图缓存记录。
    /// 每个媒体文件最多对应一条缩略图记录。
    /// </summary>
    public class Thumbnail
    {
        /// <summary>
        /// 缩略图记录的唯一标识符。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 关联的媒体文件 ID。
        /// </summary>
        public int MediaFileId { get; set; }

        /// <summary>
        /// 导航属性，关联的媒体文件对象。
        /// </summary>
        public MediaFile MediaFile { get; set; } = null!;

        /// <summary>
        /// 缩略图文件在磁盘上的缓存路径。
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// 缩略图生成时间。
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 缩略图最后被访问的时间（用于清理策略）。
        /// </summary>
        public DateTime LastAccessAt { get; set; }
    }
}
