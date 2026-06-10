using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediaEnhancer.Models;

namespace MediaEnhancer.Services
{
    /// <summary>
    /// 文件扫描服务接口，负责扫描本地文件夹中的媒体文件并导入数据库。
    /// </summary>
    public interface IFileScanService
    {
        /// <summary>
        /// 扫描指定文件夹及其子文件夹，提取元数据并批量入库。
        /// </summary>
        /// <param name="folderPath">要扫描的文件夹路径。</param>
        /// <param name="progress">进度报告，传递已处理的文件数量。</param>
        /// <returns>新扫描到的媒体文件列表（已在数据库中去重）。</returns>
        Task<List<MediaFile>> ScanFolderAsync(string folderPath, IProgress<int> progress);
    }
}
