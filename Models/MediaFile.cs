using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace MediaEnhancer.Models
{
    /// <summary>
    /// 表示一个媒体文件实体，对应 SQLite 中 MediaFiles 表。
    /// 包含文件基础信息、元数据、收藏状态等字段。
    /// </summary>
    public class MediaFile
    {
        /// <summary>
        /// 获取或设置媒体文件的唯一标识符（主键，自增）。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 获取或设置用户可见的文件标题（默认为文件名不含扩展名，可手动编辑）。
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// 获取或设置媒体文件的完整本地路径（唯一索引，用于去重）。
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// 获取或设置媒体类型（"图片" 或 "视频"）。
        /// 本项目不处理纯音频文件。
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 获取或设置文件扩展名（如 ".jpg"、".mp4"），便于筛选和显示。
        /// </summary>
        public string FileFormat { get; set; }

        /// <summary>
        /// 获取或设置文件大小（字节）。
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// 获取或设置画面宽度（像素）。图片可直接读取，视频可为 null。
        /// </summary>
        public int? Width { get; set; }

        /// <summary>
        /// 获取或设置画面高度（像素）。图片可直接读取，视频可为 null。
        /// </summary>
        public int? Height { get; set; }

        /// <summary>
        /// 获取或设置视频时长（字符串格式 "hh:mm:ss"），图片为 null。
        /// </summary>
        public string? Duration { get; set; }

        /// <summary>
        /// 增强来源文件 ID（由哪个文件增强生成，null 表示原始文件）。
        /// 与自身形成自引用关系，用于追溯文件增强历史。
        /// </summary>
        public int? SourceFileId { get; set; }

        /// <summary>
        /// 导航属性：增强来源文件。
        /// </summary>
        [System.ComponentModel.DataAnnotations.Schema.ForeignKey(nameof(SourceFileId))]
        public MediaFile? SourceFile { get; set; }

        /// <summary>
        /// 获取或设置该媒体文件是否已被用户收藏。
        /// </summary>
        public bool IsFavorite { get; set; }

        /// <summary>
        /// AI 面板中是否被选中（不持久化）。
        /// </summary>
        [NotMapped]
        public bool IsSelected { get; set; }

        /// <summary>
        /// 播放次数（不持久化到数据库，由程序运行时从播放记录统计填充）。
        /// </summary>
        [NotMapped]
        public int PlayCount { get; set; }

        /// <summary>
        /// 文件简介，初始值为文件类型名（"图片"/"视频"/"音频"），允许用户自由修改。
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 获取或设置缩略图/封面的缓存路径。
        /// 扫描时为空，由缩略图服务后续生成并填充。
        /// </summary>
        public string? ThumbnailPath { get; set; }

        /// <summary>
        /// 获取或设置文件首次导入系统的时间。
        /// </summary>
        public DateTime DateAdded { get; set; }

        /// <summary>
        /// 获取或设置文件的最后修改时间（来自文件系统的 LastWriteTime）。
        /// </summary>
        public DateTime DateModified { get; set; }
    }
}
