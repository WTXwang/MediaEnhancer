using System.Threading.Tasks;
using MediaEnhancer.Models;

namespace MediaEnhancer.Services
{
    /// <summary>
    /// 缩略图服务接口，负责生成、缓存和清理媒体文件缩略图。
    /// </summary>
    public interface IThumbnailService
    {
        /// <summary>
        /// 为指定媒体文件生成缩略图，返回缓存文件路径。
        /// 图片：缩放到 200×200 保持比例；视频/音频：使用默认图标。
        /// </summary>
        Task<string?> GenerateThumbnailAsync(MediaFile file);

        /// <summary>
        /// 批量生成缩略图。
        /// </summary>
        Task GenerateThumbnailsAsync(System.Collections.Generic.IEnumerable<MediaFile> files);

        /// <summary>
        /// 清理已失效的缩略图缓存（源文件不存在的）。
        /// </summary>
        Task CleanupOrphanedThumbnailsAsync();

        /// <summary>
        /// 获取或设置缩略图缓存目录。
        /// </summary>
        string CacheDirectory { get; set; }
    }
}
