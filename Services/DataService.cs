using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediaEnhancer.Data;
using MediaEnhancer.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaEnhancer.Services
{
    /// <summary>
    /// 数据服务实现，封装对 EF Core AppDbContext 的完整数据库操作。
    /// </summary>
    public class DataService : IDataService
    {
        private readonly AppDbContext _context;

        /// <summary>
        /// 构造函数，通过依赖注入获取数据库上下文。
        /// </summary>
        /// <param name="context">数据库上下文。</param>
        public DataService(AppDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // 查询
        // ============================================================

        /// <inheritdoc/>
        public async Task<List<MediaFile>> GetAllMediaFilesAsync()
        {
            return await _context.MediaFiles
                .OrderByDescending(m => m.DateAdded)
                .ToListAsync();
        }

        /// <inheritdoc/>
        public async Task<MediaFile?> GetMediaFileByIdAsync(int id)
        {
            return await _context.MediaFiles.FindAsync(id);
        }

        /// <inheritdoc/>
        public async Task<List<MediaFile>> SearchMediaFilesAsync(
            string? keyword, string? type, bool? favoritesOnly)
        {
            var query = _context.MediaFiles.AsQueryable();

            // 按关键词搜索（标题或路径）
            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(m =>
                    m.Title.Contains(keyword) || m.FilePath.Contains(keyword));

            // 按类型筛选（"全部"时不过滤）
            if (!string.IsNullOrWhiteSpace(type) && type != "全部")
                query = query.Where(m => m.Type == type);

            // 仅显示收藏
            if (favoritesOnly == true)
                query = query.Where(m => m.IsFavorite);

            return await query
                .OrderByDescending(m => m.DateAdded)
                .ToListAsync();
        }

        // ============================================================
        // 新增
        // ============================================================

        /// <inheritdoc/>
        public async Task<bool> AddMediaFileAsync(MediaFile mediaFile)
        {
            // 路径为空或已存在则跳过
            if (string.IsNullOrWhiteSpace(mediaFile.FilePath))
                return false;

            var exists = await _context.MediaFiles
                .AnyAsync(m => m.FilePath == mediaFile.FilePath);
            if (exists)
                return false;

            await _context.MediaFiles.AddAsync(mediaFile);
            await _context.SaveChangesAsync();
            return true;
        }

        /// <inheritdoc/>
        public async Task<int> AddMediaFilesAsync(List<MediaFile> mediaFiles)
        {
            if (mediaFiles == null || mediaFiles.Count == 0)
                return 0;

            // 查询数据库中已存在的路径
            var existingPaths = new HashSet<string>(
                await _context.MediaFiles
                    .Select(m => m.FilePath)
                    .ToListAsync(),
                StringComparer.OrdinalIgnoreCase);

            // 过滤出新文件
            var newFiles = mediaFiles
                .Where(m => !string.IsNullOrWhiteSpace(m.FilePath)
                         && !existingPaths.Contains(m.FilePath))
                .ToList();

            if (newFiles.Count == 0)
                return 0;

            // 批量插入（每 50 条一次提交）
            const int batchSize = 50;
            for (int i = 0; i < newFiles.Count; i += batchSize)
            {
                var batch = newFiles.Skip(i).Take(batchSize).ToList();
                _context.MediaFiles.AddRange(batch);
                await _context.SaveChangesAsync();
            }

            return newFiles.Count;
        }

        // ============================================================
        // 更新
        // ============================================================

        /// <inheritdoc/>
        public async Task UpdateMediaFileAsync(MediaFile mediaFile)
        {
            _context.MediaFiles.Update(mediaFile);
            await _context.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task ToggleFavoriteAsync(int id)
        {
            var file = await _context.MediaFiles.FindAsync(id);
            if (file != null)
            {
                file.IsFavorite = !file.IsFavorite;
                await _context.SaveChangesAsync();
                await SyncFavoriteRecordAsync(id, file.IsFavorite);
            }
        }

        /// <summary>
        /// 根据 IsFavorite 状态同步 Favorites 表：收藏时新增记录，取消时删除记录。
        /// 供批量收藏操作等场景直接调用。
        /// </summary>
        public async Task SyncFavoriteRecordAsync(int mediaFileId, bool isFavorite)
        {
            if (isFavorite)
            {
                var exists = await _context.Favorites
                    .AnyAsync(f => f.MediaFileId == mediaFileId);
                if (!exists)
                {
                    await _context.Favorites.AddAsync(new Favorite
                    {
                        MediaFileId = mediaFileId,
                        CreatedAt = DateTime.Now
                    });
                    await _context.SaveChangesAsync();
                }
            }
            else
            {
                var record = await _context.Favorites
                    .FirstOrDefaultAsync(f => f.MediaFileId == mediaFileId);
                if (record != null)
                {
                    _context.Favorites.Remove(record);
                    await _context.SaveChangesAsync();
                }
            }
        }

        // ============================================================
        // 播放记录
        // ============================================================

        /// <inheritdoc/>
        public async Task AddPlayHistoryAsync(int mediaFileId, double? progress = null)
        {
            var history = new PlayHistory
            {
                MediaFileId = mediaFileId,
                PlayedAt = DateTime.Now,
                PlayProgress = progress
            };
            await _context.PlayHistories.AddAsync(history);
            await _context.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task<List<PlayHistory>> GetRecentPlaysAsync(int count = 10)
        {
            // 单条 SQL：用关联子查询找出每个文件的最近播放，再取 top N
            var latestPlays = await _context.PlayHistories
                .Include(p => p.MediaFile)
                .Where(p => p.PlayedAt == _context.PlayHistories
                    .Where(x => x.MediaFileId == p.MediaFileId)
                    .Max(x => (DateTime?)x.PlayedAt))
                .OrderByDescending(p => p.PlayedAt)
                .Take(count)
                .ToListAsync();

            return latestPlays;
        }

        // ============================================================
        // 删除
        // ============================================================

        /// <inheritdoc/>
        public async Task DeleteMediaFileAsync(int id)
        {
            var file = await _context.MediaFiles.FindAsync(id);
            if (file != null)
            {
                _context.MediaFiles.Remove(file);
                await _context.SaveChangesAsync();
            }
        }

        /// <inheritdoc/>
        public async Task DeleteMediaFilesAsync(List<int> ids)
        {
            if (ids == null || ids.Count == 0) return;

            var files = await _context.MediaFiles
                .Where(m => ids.Contains(m.Id))
                .ToListAsync();

            _context.MediaFiles.RemoveRange(files);
            await _context.SaveChangesAsync();
        }

        // ============================================================
        // 辅助
        // ============================================================

        /// <inheritdoc/>
        public async Task<bool> FilePathExistsAsync(string filePath)
        {
            return await _context.MediaFiles
                .AnyAsync(m => m.FilePath == filePath);
        }

        /// <inheritdoc/>
        public async Task<int> GetTotalCountAsync()
        {
            return await _context.MediaFiles.CountAsync();
        }

        /// <inheritdoc/>
        public async Task<(int imageCount, int videoCount)> GetTypeCountAsync()
        {
            var imageCount = await _context.MediaFiles
                .CountAsync(m => m.Type == "图片");
            var videoCount = await _context.MediaFiles
                .CountAsync(m => m.Type == "视频");
            return (imageCount, videoCount);
        }

        /// <inheritdoc/>
        public async Task<int> GetEnhancementCountAsync()
        {
            return await _context.EnhancementLogs.CountAsync();
        }

        /// <inheritdoc/>
        public async Task<int> GetRecordingCountAsync()
        {
            return await _context.Recordings.CountAsync();
        }

        /// <inheritdoc/>
        public async Task<int> GetTotalPlayCountAsync()
        {
            return await _context.PlayHistories.CountAsync();
        }

        /// <inheritdoc/>
        public async Task<int> GetFavoriteCountAsync()
        {
            return await _context.MediaFiles.CountAsync(m => m.IsFavorite);
        }

        /// <inheritdoc/>
        public async Task<Dictionary<int, int>> GetPlayCountsAsync()
        {
            return await _context.PlayHistories
                .GroupBy(p => p.MediaFileId)
                .Select(g => new { MediaFileId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.MediaFileId, x => x.Count);
        }

        /// <inheritdoc/>
        public async Task AddEnhancementLogAsync(EnhancementLog log)
        {
            await _context.EnhancementLogs.AddAsync(log);
            await _context.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task AddRecordingAsync(int mediaFileId, string outputPath, double durationSec, bool isEnhanced)
        {
            var info = new System.IO.FileInfo(outputPath);
            var ts = TimeSpan.FromSeconds(durationSec);
            var durationStr = ts.Hours > 0
                ? $"{ts.Hours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes:D2}:{ts.Seconds:D2}";

            var recording = new Recording
            {
                MediaFileId = mediaFileId,
                Title = System.IO.Path.GetFileNameWithoutExtension(outputPath),
                FilePath = outputPath,
                Duration = durationStr,
                FileSize = info.Exists ? info.Length : 0,
                IsEnhanced = isEnhanced,
                AudioSource = "无",
                CreatedAt = DateTime.Now
            };
            await _context.Recordings.AddAsync(recording);
            await _context.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task<List<Recording>> GetRecordingsAsync(int count = 30)
        {
            return await _context.Recordings
                .Include(r => r.MediaFile)
                .OrderByDescending(r => r.CreatedAt)
                .Take(count)
                .ToListAsync();
        }
    }
}