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
    /// 数据服务实现——封装对 EF Core AppDbContext 的完整数据库操作，
    /// 自动按当前登录用户隔离数据。
    /// </summary>
    public class DataService : IDataService
    {
        private readonly AppDbContext _context;
        private readonly int _userId;

        public DataService(AppDbContext context, IAuthService authService)
        {
            _context = context;
            _userId = authService.CurrentUser?.Id ?? 0;
        }

        private int EnsureUserId()
        {
            if (_userId <= 0)
                throw new InvalidOperationException("未登录用户不能执行数据操作。");
            return _userId;
        }

        // ============================================================
        // 查询
        // ============================================================

        public async Task<List<MediaFile>> GetAllMediaFilesAsync()
        {
            return await _context.MediaFiles
                .Where(m => m.UserId == _userId)
                .OrderByDescending(m => m.DateAdded)
                .ToListAsync();
        }

        public async Task<MediaFile?> GetMediaFileByIdAsync(int id)
        {
            return await _context.MediaFiles
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == _userId);
        }

        public async Task<List<MediaFile>> SearchMediaFilesAsync(
            string? keyword, string? type, bool? favoritesOnly)
        {
            var query = _context.MediaFiles.Where(m => m.UserId == _userId);

            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(m =>
                    m.Title.Contains(keyword) || m.FilePath.Contains(keyword));

            if (!string.IsNullOrWhiteSpace(type) && type != "全部")
                query = query.Where(m => m.Type == type);

            if (favoritesOnly == true)
                query = query.Where(m => m.IsFavorite);

            return await query
                .OrderByDescending(m => m.DateAdded)
                .ToListAsync();
        }

        // ============================================================
        // 新增
        // ============================================================

        public async Task<bool> AddMediaFileAsync(MediaFile mediaFile)
        {
            if (string.IsNullOrWhiteSpace(mediaFile.FilePath))
                return false;

            var exists = await _context.MediaFiles
                .AnyAsync(m => m.FilePath == mediaFile.FilePath && m.UserId == _userId);
            if (exists)
                return false;

            mediaFile.UserId = EnsureUserId();
            await _context.MediaFiles.AddAsync(mediaFile);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<int> AddMediaFilesAsync(List<MediaFile> mediaFiles)
        {
            if (mediaFiles == null || mediaFiles.Count == 0)
                return 0;

            var uid = EnsureUserId();
            var existingPaths = new HashSet<string>(
                await _context.MediaFiles
                    .Where(m => m.UserId == uid)
                    .Select(m => m.FilePath)
                    .ToListAsync(),
                StringComparer.OrdinalIgnoreCase);

            var newFiles = mediaFiles
                .Where(m => !string.IsNullOrWhiteSpace(m.FilePath)
                         && !existingPaths.Contains(m.FilePath))
                .ToList();

            if (newFiles.Count == 0)
                return 0;

            foreach (var f in newFiles) f.UserId = uid;

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

        public async Task UpdateMediaFileAsync(MediaFile mediaFile)
        {
            if (mediaFile.UserId != _userId)
                mediaFile.UserId = _userId;
            _context.MediaFiles.Update(mediaFile);
            await _context.SaveChangesAsync();
        }

        public async Task ToggleFavoriteAsync(int id)
        {
            var file = await _context.MediaFiles
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == _userId);
            if (file != null)
            {
                file.IsFavorite = !file.IsFavorite;
                await _context.SaveChangesAsync();
                await SyncFavoriteRecordAsync(id, file.IsFavorite);
            }
        }

        public async Task SyncFavoriteRecordAsync(int mediaFileId, bool isFavorite)
        {
            var uid = EnsureUserId();
            if (isFavorite)
            {
                var exists = await _context.Favorites
                    .AnyAsync(f => f.MediaFileId == mediaFileId && f.UserId == uid);
                if (!exists)
                {
                    await _context.Favorites.AddAsync(new Favorite
                    {
                        MediaFileId = mediaFileId,
                        UserId = uid,
                        CreatedAt = DateTime.Now
                    });
                    await _context.SaveChangesAsync();
                }
            }
            else
            {
                var record = await _context.Favorites
                    .FirstOrDefaultAsync(f => f.MediaFileId == mediaFileId && f.UserId == uid);
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

        public async Task AddPlayHistoryAsync(int mediaFileId, double? progress = null)
        {
            var history = new PlayHistory
            {
                MediaFileId = mediaFileId,
                UserId = EnsureUserId(),
                PlayedAt = DateTime.Now,
                PlayProgress = progress
            };
            await _context.PlayHistories.AddAsync(history);
            await _context.SaveChangesAsync();
        }

        public async Task<List<PlayHistory>> GetRecentPlaysAsync(int count = 10)
        {
            var latestPlays = await _context.PlayHistories
                .Include(p => p.MediaFile)
                .Where(p => p.UserId == _userId
                    && p.PlayedAt == _context.PlayHistories
                        .Where(x => x.MediaFileId == p.MediaFileId && x.UserId == _userId)
                        .Max(x => (DateTime?)x.PlayedAt))
                .OrderByDescending(p => p.PlayedAt)
                .Take(count)
                .ToListAsync();

            return latestPlays;
        }

        // ============================================================
        // 删除
        // ============================================================

        public async Task DeleteMediaFileAsync(int id)
        {
            var file = await _context.MediaFiles
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == _userId);
            if (file != null)
            {
                _context.MediaFiles.Remove(file);
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteMediaFilesAsync(List<int> ids)
        {
            if (ids == null || ids.Count == 0) return;

            var files = await _context.MediaFiles
                .Where(m => ids.Contains(m.Id) && m.UserId == _userId)
                .ToListAsync();

            _context.MediaFiles.RemoveRange(files);
            await _context.SaveChangesAsync();
        }

        // ============================================================
        // 辅助统计
        // ============================================================

        public async Task<bool> FilePathExistsAsync(string filePath)
        {
            return await _context.MediaFiles
                .AnyAsync(m => m.FilePath == filePath && m.UserId == _userId);
        }

        public async Task<MediaFile?> GetMediaFileByPathAsync(string filePath)
        {
            return await _context.MediaFiles
                .FirstOrDefaultAsync(m => m.FilePath == filePath && m.UserId == _userId);
        }

        public async Task<int> GetTotalCountAsync()
        {
            return await _context.MediaFiles.CountAsync(m => m.UserId == _userId);
        }

        public async Task<(int imageCount, int videoCount, int audioCount)> GetTypeCountAsync()
        {
            var imageCount = await _context.MediaFiles.CountAsync(m => m.Type == "图片" && m.UserId == _userId);
            var videoCount = await _context.MediaFiles.CountAsync(m => m.Type == "视频" && m.UserId == _userId);
            var audioCount = await _context.MediaFiles.CountAsync(m => m.Type == "音频" && m.UserId == _userId);
            return (imageCount, videoCount, audioCount);
        }

        public async Task<int> GetEnhancementCountAsync()
        {
            return await _context.EnhancementLogs.CountAsync(e => e.UserId == _userId);
        }

        public async Task<int> GetRecordingCountAsync()
        {
            return await _context.Recordings.CountAsync(r => r.UserId == _userId);
        }

        public async Task<int> GetTotalPlayCountAsync()
        {
            return await _context.PlayHistories.CountAsync(p => p.UserId == _userId);
        }

        public async Task<int> GetFavoriteCountAsync()
        {
            return await _context.MediaFiles.CountAsync(m => m.IsFavorite && m.UserId == _userId);
        }

        public async Task<Dictionary<int, int>> GetPlayCountsAsync()
        {
            return await _context.PlayHistories
                .Where(p => p.UserId == _userId)
                .GroupBy(p => p.MediaFileId)
                .Select(g => new { MediaFileId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.MediaFileId, x => x.Count);
        }

        public async Task AddEnhancementLogAsync(EnhancementLog log)
        {
            log.UserId = EnsureUserId();
            await _context.EnhancementLogs.AddAsync(log);
            await _context.SaveChangesAsync();
        }

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
                UserId = EnsureUserId(),
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

        public async Task<List<Recording>> GetRecordingsAsync(int count = 30)
        {
            return await _context.Recordings
                .Include(r => r.MediaFile)
                .Where(r => r.UserId == _userId)
                .OrderByDescending(r => r.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<EnhancementLog>> GetEnhancementLogsAsync(int count = 50)
        {
            return await _context.EnhancementLogs
                .Include(e => e.MediaFile)
                .Where(e => e.UserId == _userId)
                .OrderByDescending(e => e.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<RealtimeSession>> GetRealtimeSessionsAsync(int count = 50)
        {
            return await _context.RealtimeSessions
                .Where(r => r.UserId == _userId)
                .OrderByDescending(r => r.StartedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<RealtimeSession> AddRealtimeSessionAsync(RealtimeSession session)
        {
            session.UserId = EnsureUserId();
            await _context.RealtimeSessions.AddAsync(session);
            await _context.SaveChangesAsync();
            return session;
        }

        public async Task UpdateRealtimeSessionAsync(RealtimeSession session)
        {
            _context.RealtimeSessions.Update(session);
            await _context.SaveChangesAsync();
        }

        public async Task<int> GetRealtimeSessionCountAsync()
        {
            return await _context.RealtimeSessions
                .CountAsync(r => r.UserId == _userId);
        }
    }
}
