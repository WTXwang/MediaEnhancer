using System;

namespace MediaEnhancer.Models
{
    /// <summary>
    /// 播放记录实体，记录每次播放的时间与进度。
    /// 用于"最近播放"快速入口和播放次数统计。
    /// </summary>
    public class PlayHistory
    {
        /// <summary>
        /// 播放记录的唯一标识符。
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
        /// 播放开始时间。
        /// </summary>
        public DateTime PlayedAt { get; set; }

        /// <summary>
        /// 归属用户 ID（外键）。
        /// </summary>
        public int UserId { get; set; }

        /// <summary>
        /// 导航属性：归属用户。
        /// </summary>
        public User? User { get; set; }
    }
}
