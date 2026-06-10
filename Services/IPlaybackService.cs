using System.Threading.Tasks;
using MediaEnhancer.Models;

namespace MediaEnhancer.Services
{
    /// <summary>
    /// 播放服务接口，负责调起对应类型的播放窗口并记录播放历史。
    /// </summary>
    public interface IPlaybackService
    {
        /// <summary>
        /// 播放指定的媒体文件（根据类型自动选择图片查看器或媒体播放器）。
        /// </summary>
        /// <param name="file">要播放的媒体文件。</param>
        /// <param name="owner">所属主窗口。</param>
        void Play(MediaFile file, System.Windows.Window owner);

        /// <summary>
        /// 播放媒体文件并指定播放列表（支持上一首/下一首）。
        /// </summary>
        /// <param name="files">播放列表。</param>
        /// <param name="startIndex">起始播放的文件索引。</param>
        /// <param name="owner">所属主窗口。</param>
        void PlayInList(System.Collections.Generic.List<MediaFile> files, int startIndex, System.Windows.Window owner);
    }
}
