using System.Collections.Generic;
using System.Threading.Tasks;
using MediaEnhancer.Models;

namespace MediaEnhancer.Services
{
    /// <summary>
    /// 数据访问服务接口，提供媒体文件的全部数据库操作。
    /// 包括查询、新增、更新、删除及辅助统计功能。
    /// </summary>
    public interface IDataService
    {
        // ─── 查询 ───

        /// <summary>
        /// 异步获取所有媒体文件，按导入时间降序排列。
        /// </summary>
        Task<List<MediaFile>> GetAllMediaFilesAsync();

        /// <summary>
        /// 根据 ID 获取单个媒体文件，不存在时返回 null。
        /// </summary>
        Task<MediaFile?> GetMediaFileByIdAsync(int id);

        /// <summary>
        /// 按关键词、类型、收藏状态筛选媒体文件。
        /// 所有参数为可选，传 null 表示不过滤该条件。
        /// </summary>
        /// <param name="keyword">标题或路径关键词。</param>
        /// <param name="type">媒体类型（"图片"/"视频"/null 表示全部）。</param>
        /// <param name="favoritesOnly">仅显示收藏。</param>
        Task<List<MediaFile>> SearchMediaFilesAsync(string? keyword, string? type, bool? favoritesOnly);

        // ─── 新增 ───

        /// <summary>
        /// 添加单个媒体文件。如果文件路径已存在则跳过，返回 false。
        /// </summary>
        Task<bool> AddMediaFileAsync(MediaFile mediaFile);

        /// <summary>
        /// 批量添加媒体文件，自动跳过已存在的路径。
        /// 返回实际新增的文件数量。
        /// </summary>
        Task<int> AddMediaFilesAsync(List<MediaFile> mediaFiles);

        // ─── 更新 ───

        /// <summary>
        /// 更新媒体文件信息（标题、收藏状态等）。
        /// </summary>
        Task UpdateMediaFileAsync(MediaFile mediaFile);

        /// <summary>
        /// 切换指定媒体文件的收藏状态。
        /// </summary>
        Task ToggleFavoriteAsync(int id);

        /// <summary>
        /// 根据收藏状态同步 Favorites 表记录（新增或删除）。
        /// </summary>
        Task SyncFavoriteRecordAsync(int mediaFileId, bool isFavorite);

        // ─── 删除 ───

        /// <summary>
        /// 删除指定 ID 的媒体文件记录。
        /// </summary>
        Task DeleteMediaFileAsync(int id);

        /// <summary>
        /// 批量删除指定 ID 列表的媒体文件记录。
        /// </summary>
        Task DeleteMediaFilesAsync(List<int> ids);

        // ─── 播放记录 ───

        /// <summary>
        /// 添加播放记录。
        /// </summary>
        /// <param name="mediaFileId">媒体文件 ID。</param>
        /// <param name="progress">播放进度（百分比或秒数，可选）。</param>
        Task AddPlayHistoryAsync(int mediaFileId, double? progress = null);

        /// <summary>
        /// 获取最近播放记录（含关联的 MediaFile 导航属性）。
        /// </summary>
        /// <param name="count">获取条数，默认 10。</param>
        Task<List<PlayHistory>> GetRecentPlaysAsync(int count = 10);

        // ─── 辅助 ───

        /// <summary>
        /// 判断文件路径是否已存在数据库中。
        /// </summary>
        Task<bool> FilePathExistsAsync(string filePath);

        /// <summary>
        /// 根据文件路径获取媒体文件（用于导入后查找 ID 等场景）。
        /// </summary>
        Task<MediaFile?> GetMediaFileByPathAsync(string filePath);

        /// <summary>
        /// 获取媒体文件总数。
        /// </summary>
        Task<int> GetTotalCountAsync();

        /// <summary>
        /// 获取图片、视频、音频各自的数量。
        /// </summary>
        Task<(int imageCount, int videoCount, int audioCount)> GetTypeCountAsync();

        /// <summary>
        /// 获取增强日志总数。
        /// </summary>
        Task<int> GetEnhancementCountAsync();

        /// <summary>
        /// 获取录屏记录总数。
        /// </summary>
        Task<int> GetRecordingCountAsync();

        /// <summary>
        /// 获取总播放次数。
        /// </summary>
        Task<int> GetTotalPlayCountAsync();

        /// <summary>
        /// 获取已收藏的文件数量。
        /// </summary>
        Task<int> GetFavoriteCountAsync();

        /// <summary>
        /// 获取多个文件的播放次数（字典：MediaFileId → 播放次数）。
        /// </summary>
        Task<Dictionary<int, int>> GetPlayCountsAsync();

        /// <summary>
        /// 添加增强日志记录。
        /// </summary>
        Task AddEnhancementLogAsync(EnhancementLog log);

        /// <summary>
        /// 添加录制记录（将录制文件关联到媒体库并记录录屏信息）。
        /// </summary>
        /// <param name="mediaFileId">录制 MP4 文件对应的媒体文件 ID。</param>
        /// <param name="outputPath">录制输出文件路径。</param>
        Task AddRecordingAsync(int mediaFileId, string outputPath, double durationSec, bool isEnhanced);

        /// <summary>
        /// 获取录制历史列表。
        /// </summary>
        Task<List<Recording>> GetRecordingsAsync(int count = 30);

        /// <summary>
        /// 获取当前用户的增强日志列表（含源文件导航属性）。
        /// </summary>
        Task<List<EnhancementLog>> GetEnhancementLogsAsync(int count = 50);

        /// <summary>
        /// 获取当前用户的实时增强会话记录（按启动时间倒序）。
        /// </summary>
        Task<List<RealtimeSession>> GetRealtimeSessionsAsync(int count = 50);

        /// <summary>
        /// 新增实时增强会话记录。
        /// </summary>
        Task<RealtimeSession> AddRealtimeSessionAsync(RealtimeSession session);

        /// <summary>
        /// 更新实时增强会话记录（写入结束时间）。
        /// </summary>
        Task UpdateRealtimeSessionAsync(RealtimeSession session);

        /// <summary>
        /// 获取当前用户的实时增强会话次数。
        /// </summary>
        Task<int> GetRealtimeSessionCountAsync();
    }
}