using System;

namespace MediaEnhancer.Models
{
    /// <summary>
    /// 收藏记录表，记录用户收藏媒体文件的时间。
    /// 与 MediaFile.IsFavorite 字段配合使用，IsFavorite 为快速查询标志，
    /// 此表提供收藏时间的详细记录。
    /// </summary>
    public class Favorite
    {
        /// <summary>
        /// 收藏记录的唯一标识符。
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
        /// 收藏时间。
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}
