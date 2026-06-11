using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaEnhancer.Models;
using MediaEnhancer.Services;

namespace MediaEnhancer.ViewModels;

/// <summary>
/// 数据统计面板相关逻辑（partial class）。
/// </summary>
partial class MainViewModel
{
        // 数据统计面板
        // ============================================================

        /// <summary>
        /// 媒体文件总数。
        /// </summary>
        [ObservableProperty]
        private int _totalFileCount = 0;

        /// <summary>
        /// 图片文件数量。
        /// </summary>
        [ObservableProperty]
        private int _imageCount = 0;

        /// <summary>
        /// 视频文件数量。
        /// </summary>
        [ObservableProperty]
        private int _videoCount = 0;

        /// <summary>
        /// 音频文件数量。
        /// </summary>
        [ObservableProperty]
        private int _audioCount = 0;

        /// <summary>
        /// 已执行的文件增强次数。
        /// </summary>
        [ObservableProperty]
        private int _enhancementCount = 0;

        /// <summary>
        /// 录屏记录数量。
        /// </summary>
        [ObservableProperty]
        private int _recordingCount = 0;

        /// <summary>
        /// 实时增强功能的使用次数。
        /// </summary>
        [ObservableProperty]
        private int _realtimeEnhanceCount = 0;

        /// <summary>
        /// 总播放次数。
        /// </summary>
        [ObservableProperty]
        private int _totalPlayCount = 0;

        /// <summary>
        /// 已收藏的文件数量。
        /// </summary>
        [ObservableProperty]
        private int _favoriteCount = 0;

        /// <summary>
        /// 各文件的播放次数映射表（MediaFileId → 次数），用于 DataGrid 显示。
        /// </summary>
        private Dictionary<int, int> _playCountMap = new();

        /// <summary>
        /// 从数据库加载统计数据，更新仪表盘各卡片数值。
        /// </summary>
        private async Task LoadStatisticsAsync()
        {
            TotalFileCount = await _dataService.GetTotalCountAsync();
            var (imageCount, videoCount, audioCount) = await _dataService.GetTypeCountAsync();
            ImageCount = imageCount;
            VideoCount = videoCount;
            AudioCount = audioCount;
            EnhancementCount = await _dataService.GetEnhancementCountAsync();
            RecordingCount = await _dataService.GetRecordingCountAsync();
            TotalPlayCount = await _dataService.GetTotalPlayCountAsync();
            FavoriteCount = await _dataService.GetFavoriteCountAsync();
            RealtimeEnhanceCount = await _dataService.GetRealtimeSessionCountAsync();
        }

        /// <summary>
        /// 从数据库加载录制历史。
        /// </summary>
        private async Task LoadRecordingsAsync()
        {
            var data = await _dataService.GetRecordingsAsync();
            Recordings.Clear();
            foreach (var r in data) Recordings.Add(r);
        }

        /// <summary>
        /// 刷新统计数据命令。
        /// </summary>
        [RelayCommand]
        private async Task RefreshStatistics()
        {
            await LoadStatisticsAsync();
        }

        /// <summary>
        /// 批量校验所有媒体文件的源文件是否存在。
        /// 对缺失文件提示用户删除记录或手动定位。
        /// </summary>
        [RelayCommand]
        private async Task ValidateFiles()
        {
            var allFiles = await _dataService.GetAllMediaFilesAsync();
            var missingCount = 0;
            var repairedCount = 0;
            var deletedCount = 0;

            foreach (var file in allFiles)
            {
                if (!System.IO.File.Exists(file.FilePath))
                {
                    missingCount++;

                    var result = System.Windows.MessageBox.Show(
                        $"文件不存在：{file.Title}\n路径：{file.FilePath}\n\n" +
                        "选择「是」→ 从库中删除该记录\n" +
                        "选择「否」→ 手动定位文件的新位置\n" +
                        "选择「取消」→ 跳过该文件",
                        $"文件丢失（{missingCount}/{allFiles.Count}）",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        await _dataService.DeleteMediaFileAsync(file.Id);
                        deletedCount++;
                    }
                    else if (result == MessageBoxResult.No)
                    {
                        var openDialog = new Microsoft.Win32.OpenFileDialog();
                        openDialog.Title = $"请定位「{file.Title}」的新位置";
                        openDialog.Filter = "媒体文件|*.*";
                        if (openDialog.ShowDialog() == true)
                        {
                            file.FilePath = openDialog.FileName;
                            file.Title = System.IO.Path.GetFileNameWithoutExtension(openDialog.FileName);
                            await _dataService.UpdateMediaFileAsync(file);
                            repairedCount++;
                        }
                    }
                }
            }

            await LoadDataAsync();
            await LoadStatisticsAsync();

            if (missingCount == 0)
            {
                System.Windows.MessageBox.Show("所有文件记录完好，未发现缺失。",
                    "校验完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show(
                    $"校验完成。\n共发现 {missingCount} 个缺失文件：\n已删除记录：{deletedCount} 个\n已重新定位：{repairedCount} 个\n已跳过：{missingCount - deletedCount - repairedCount} 个",
                    "校验结果", MessageBoxButton.OK,
                    deletedCount + repairedCount > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 检查并下载项目依赖项（如 FFmpeg）。
        /// </summary>
        [RelayCommand]
        private async Task CheckDependencies()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var ffmpegExe = System.IO.Path.Combine(baseDir, "ffmpeg.exe");
            var messages = new System.Text.StringBuilder();
            messages.AppendLine("📋 依赖项检查结果：\n");

            // 检查 FFmpeg
            if (System.IO.File.Exists(ffmpegExe))
            {
                messages.AppendLine("✅ FFmpeg：已就绪");
            }
            else
            {
                messages.AppendLine("⏳ FFmpeg：未找到，正在下载...");
                try
                {
                    var ffmpegDir = baseDir;
                    Xabe.FFmpeg.FFmpeg.SetExecutablesPath(ffmpegDir);
                    await Xabe.FFmpeg.Downloader.FFmpegDownloader.GetLatestVersion(
                        Xabe.FFmpeg.Downloader.FFmpegVersion.Official, ffmpegDir);

                    if (System.IO.File.Exists(ffmpegExe))
                        messages.AppendLine("✅ FFmpeg：下载成功");
                    else
                        messages.AppendLine("❌ FFmpeg：下载失败，请检查网络后重试");
                }
                catch (Exception ex)
                {
                    messages.AppendLine($"❌ FFmpeg：下载出错 - {ex.Message}");
                }
            }

            System.Windows.MessageBox.Show(messages.ToString(), "依赖项检查",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 跳转到 AI 总结页面。
        /// </summary>
        [RelayCommand]
        private void GoToAISummary()
        {
            SelectedPageIndex = 5;
        }
}
