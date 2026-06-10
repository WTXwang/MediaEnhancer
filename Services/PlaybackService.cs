using System.Collections.Generic;
using System.Windows;
using MediaEnhancer.Models;
using MediaEnhancer.Views;

namespace MediaEnhancer.Services
{
    /// <summary>
    /// 播放服务实现，根据媒体文件类型调起对应的播放窗口。
    /// 图片 → ImageViewerWindow；视频/音频 → MediaPlayerWindow。
    /// </summary>
    public class PlaybackService : IPlaybackService
    {
        private readonly IDataService _dataService;

        /// <summary>
        /// 构造函数，通过依赖注入获取数据服务。
        /// </summary>
        public PlaybackService(IDataService dataService)
        {
            _dataService = dataService;
        }

        /// <inheritdoc/>
        public void Play(MediaFile file, Window owner)
        {
            if (file == null) return;

            // 记录播放历史
            _ = RecordPlayAsync(file.Id);

            if (file.Type == "图片")
            {
                var viewer = new ImageViewerWindow(file);
                viewer.Owner = owner;
                viewer.ShowDialog();
            }
            else
            {
                // 视频或音频，使用媒体播放器
                var player = new MediaPlayerWindow(file);
                player.Owner = owner;
                player.ShowDialog();
            }
        }

        /// <inheritdoc/>
        public void PlayInList(List<MediaFile> files, int startIndex, Window owner)
        {
            if (files == null || files.Count == 0) return;

            var file = files[startIndex];
            _ = RecordPlayAsync(file.Id);

            if (file.Type == "图片")
            {
                var viewer = new ImageViewerWindow(file);
                viewer.Owner = owner;
                viewer.ShowDialog();
            }
            else
            {
                var player = new MediaPlayerWindow(file, files, startIndex);
                player.Owner = owner;
                player.ShowDialog();
            }
        }

        /// <summary>
        /// 异步记录播放历史。
        /// </summary>
        private async Task RecordPlayAsync(int mediaFileId)
        {
            try
            {
                await _dataService.AddPlayHistoryAsync(mediaFileId);
            }
            catch
            {
                // 播放记录写入失败不影响播放
            }
        }
    }
}
