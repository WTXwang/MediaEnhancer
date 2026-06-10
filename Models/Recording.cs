using System;

namespace MediaEnhancer.Models
{
    /// <summary>
    /// 录屏文件记录实体，记录每次屏幕录制的结果。
    /// 录制文件自动入库，形成"获取→增强→管理→分享"闭环。
    /// </summary>
    public class Recording
    {
        /// <summary>
        /// 录制记录的唯一标识符。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 关联的媒体文件 ID（录制的 MP4 文件在影音库中的记录）。
        /// </summary>
        public int MediaFileId { get; set; }

        /// <summary>
        /// 导航属性，关联的媒体文件对象。
        /// </summary>
        public MediaFile MediaFile { get; set; } = null!;

        /// <summary>
        /// 录制文件的标题（默认为文件名不含扩展名）。
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 录制文件的完整路径。
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// 录制时长（字符串格式 "mm:ss"）。
        /// </summary>
        public string Duration { get; set; }

        /// <summary>
        /// 录制文件大小（字节）。
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// 录制时是否应用了增强效果。
        /// </summary>
        public bool IsEnhanced { get; set; }

        /// <summary>
        /// 音频来源描述（"系统"、"麦克风"、"混合"、"无"）。
        /// </summary>
        public string AudioSource { get; set; }

        /// <summary>
        /// 录制创建时间。
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}
