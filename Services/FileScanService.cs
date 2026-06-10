using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MediaEnhancer.Core;
using MediaEnhancer.Models;

namespace MediaEnhancer.Services
{
    /// <summary>
    /// 文件扫描服务实现，递归扫描文件夹中的媒体文件，
    /// 提取元数据并通过 DataService 批量写入数据库。
    /// </summary>
    public class FileScanService : IFileScanService
    {
        private readonly IDataService _dataService;

        /// <summary>
        /// 构造函数，通过依赖注入获取数据服务。
        /// </summary>
        /// <param name="dataService">数据服务接口。</param>
        public FileScanService(IDataService dataService)
        {
            _dataService = dataService;
        }

        /// <inheritdoc/>
        public async Task<List<MediaFile>> ScanFolderAsync(string folderPath, IProgress<int> progress)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"文件夹不存在：{folderPath}");

            // 递归获取所有文件
            var allFiles = Directory.EnumerateFiles(
                folderPath, "*", SearchOption.AllDirectories);

            var mediaFiles = new List<MediaFile>();
            int processedCount = 0;

            foreach (var filePath in allFiles)
            {
                var extension = Path.GetExtension(filePath);

                // 跳过非媒体文件
                if (!MediaFileUtils.IsMediaFile(extension))
                    continue;

                // 提取元数据
                var mediaFile = ExtractMetadata(filePath, extension);

                if (mediaFile != null)
                {
                    mediaFiles.Add(mediaFile);
                }

                processedCount++;
                progress?.Report(processedCount);
            }

            // 批量写入数据库（自动去重）
            var addedCount = await _dataService.AddMediaFilesAsync(mediaFiles);

            // 返回实际新增的文件
            var addedPaths = mediaFiles.Select(m => m.FilePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var result = await _dataService.SearchMediaFilesAsync(null, null, null);
            return result.Where(m => addedPaths.Contains(m.FilePath)).ToList();
        }

        /// <summary>
        /// 从文件路径提取元数据，构造 MediaFile 实体。
        /// </summary>
        private static MediaFile? ExtractMetadata(string filePath, string extension)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var type = MediaFileUtils.GetMediaType(extension);

                var mediaFile = new MediaFile
                {
                    Title = MediaFileUtils.GetTitleFromPath(filePath),
                    FilePath = filePath,
                    Type = type,
                    FileFormat = extension.ToLowerInvariant(),
                    FileSize = fileInfo.Length,
                    IsFavorite = false,
                    Description = type,  // 初始简介设为文件类型
                    ThumbnailPath = null,
                    DateAdded = DateTime.Now,
                    DateModified = fileInfo.LastWriteTime
                };

                // 图片文件读取宽高
                if (type == "图片")
                {
                    var (width, height) = MediaFileUtils.GetImageDimensions(filePath);
                    mediaFile.Width = width;
                    mediaFile.Height = height;
                }
                // 视频和音频文件读取时长
                else if (type != "图片")
                {
                    mediaFile.Duration = MediaFileUtils.GetVideoDuration(filePath);
                }

                return mediaFile;
            }
            catch
            {
                // 权限不足或文件损坏时跳过该文件
                return null;
            }
        }
    }
}
