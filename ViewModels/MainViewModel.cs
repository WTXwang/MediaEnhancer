using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaEnhancer.Core;
using MediaEnhancer.Models;
using MediaEnhancer.Services;

namespace MediaEnhancer.ViewModels
{
    /// <summary>
    /// 主界面的视图模型，控制着媒体文件的展现和基础交互逻辑。
    /// 采用 CommunityToolkit.Mvvm 源生成器自动生成属性变更通知和命令。
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        // ============================================================
        // 依赖注入
        // ============================================================

        private readonly IDataService _dataService;
        private readonly IFileScanService _fileScanService;
        private readonly IPlaybackService _playbackService;
        private readonly IThumbnailService _thumbnailService;
        private readonly EnhancementRegistry _registry;
        private readonly AiService _aiService;

        // ============================================================
        // 构造函数
        // ============================================================

        /// <summary>
        /// 构造函数，通过依赖注入获取数据服务、文件扫描服务和增强方法注册中心。
        /// </summary>
        /// <param name="dataService">数据服务接口。</param>
        /// <param name="fileScanService">文件扫描服务接口。</param>
        /// <param name="playbackService">播放服务接口。</param>
        /// <param name="thumbnailService">缩略图服务接口。</param>
        /// <param name="registry">增强方法注册中心。</param>
        /// <param name="aiService">AI 服务。</param>
        public MainViewModel(IDataService dataService, IFileScanService fileScanService,
            IPlaybackService playbackService, IThumbnailService thumbnailService,
            EnhancementRegistry registry, AiService aiService)
        {
            _dataService = dataService;
            _fileScanService = fileScanService;
            _playbackService = playbackService;
            _thumbnailService = thumbnailService;
            _registry = registry;
            _aiService = aiService;

            // 回填已保存的配置
            var cfg = AppConfig.Load();
            _chatApiEndpoint = cfg.ChatEndpoint;
            _chatModelName = cfg.ChatModel;
            _editApiEndpoint = cfg.EditEndpoint;
            _editModelName = cfg.EditModel;
            _editFormat = cfg.EditFormat;
            if (!string.IsNullOrEmpty(cfg.RecordingPath)) _recordingSavePath = cfg.RecordingPath;
            if (!string.IsNullOrEmpty(cfg.EnhancementPath)) _enhancementSavePath = cfg.EnhancementPath;
            if (!string.IsNullOrEmpty(cfg.ThumbnailPath)) _thumbnailSavePath = cfg.ThumbnailPath;
            _thumbnailService.CacheDirectory = _thumbnailSavePath;
            UpdateChatConfig();

            // 构造时初始化加载。通过 Task.Run 将异步操作调度到线程池，
            // 避免在 UI 线程上 .Wait() 触发 SynchronizationContext 死锁。
            Task.Run(async () =>
            {
                await LoadDataAsync();
                await LoadStatisticsAsync();
                await LoadRecordingsAsync();
                await LoadRecentPlaysAsync();
            }).GetAwaiter().GetResult();
        }

        // 用于取消上一次搜索/筛选的异步加载，防止快速连续输入时旧结果覆盖新结果
        private CancellationTokenSource? _loadCts;

        /// <summary>
        /// 从数据库中异步加载媒体文件到 ObservableCollection。
        /// 根据当前搜索关键词、类型筛选和收藏状态进行过滤。
        /// </summary>
        /// <param name="ct">取消令牌，用于中断已过期的加载请求。</param>
        private async Task LoadDataAsync(CancellationToken ct = default)
        {
            var keyword = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText;
            var type = FilterType == "全部" ? null : FilterType;
            bool? favoritesOnly = FilterFavoritesOnly ? true : null;

            var data = await _dataService.SearchMediaFilesAsync(keyword, type, favoritesOnly);
            if (ct.IsCancellationRequested) return;

            _playCountMap = await _dataService.GetPlayCountsAsync();
            if (ct.IsCancellationRequested) return;

            foreach (var item in data)
            {
                _playCountMap.TryGetValue(item.Id, out var count);
                item.PlayCount = count;
            }
            MediaFilesList.Clear();
            foreach (var item in data)
            {
                MediaFilesList.Add(item);
            }
        }

        // ============================================================
        // 页面导航
        // ============================================================

        /// <summary>
        /// 控制右侧展示哪个面板的索引。
        /// 0:数据统计, 1:文件管理, 2:离线增强, 3:实时增强, 4:屏幕录制, 5:AI对话, 6:AI编辑, 7:系统设置
        /// </summary>
        [ObservableProperty]
        private int _selectedPageIndex = 0;

        /// <summary>
        /// 切换页面的命令，由侧边栏导航按钮触发。
        /// </summary>
        /// <param name="pageIndexStr">目标页面索引的字符串形式。</param>
        [RelayCommand]
        private void ChangePage(string pageIndexStr)
        {
            if (int.TryParse(pageIndexStr, out int index))
            {
                SelectedPageIndex = index;
            }
        }

        // ============================================================
        // 通用提示命令（所有未实现功能均调用此方法）
        // ============================================================

        /// <summary>
        /// 弹窗提示功能未实现，用于所有暂未开发的界面按钮。
        /// </summary>
        /// <param name="featureName">功能名称，显示在弹窗中。</param>
        [RelayCommand]
        private void ShowNotImplementedMsg(string featureName)
        {
            System.Windows.MessageBox.Show(
                $"{featureName} 功能暂未实现，敬请期待！",
                "提示",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        // ============================================================
        // 文件管理面板
        // ============================================================

        /// <summary>
        /// 媒体文件的可观察集合，与 DataGrid 绑定。
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<MediaFile> _mediaFilesList = new();

        /// <summary>
        /// DataGrid 中当前选中的文件，用于右侧详情面板绑定。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelectedFile))]
        [NotifyPropertyChangedFor(nameof(IsFileMissing))]
        private MediaFile? _selectedFile = null;

        /// <summary>
        /// 是否有选中的文件（用于控制详情面板的显示/隐藏）。
        /// </summary>
        public bool HasSelectedFile => SelectedFile != null;

        /// <summary>
        /// 选中的文件是否在磁盘上不存在。
        /// </summary>
        public bool IsFileMissing => SelectedFile != null && !System.IO.File.Exists(SelectedFile.FilePath);

        /// <summary>
        /// 是否显示浮层详情面板（双击 DataGrid 时打开）。
        /// </summary>
        [ObservableProperty]
        private bool _showDetailPanel = false;

        /// <summary>
        /// 关闭浮层详情面板。
        /// </summary>
        [RelayCommand]
        private void CloseDetailPanel()
        {
            ShowDetailPanel = false;
        }

        /// <summary>
        /// 当前在 DataGrid 中选中的多个文件（用于批量操作）。
        /// </summary>
        public ObservableCollection<MediaFile> SelectedFiles { get; } = new();

        /// <summary>
        /// 搜索关键词，与搜索框绑定。输入即搜索。
        /// </summary>
        [ObservableProperty]
        private string _searchText = "";

        /// <summary>
        /// 搜索文本变化时自动刷新列表。
        /// </summary>
        partial void OnSearchTextChanged(string value) => LoadWithCancel();

        /// <summary>
        /// 当前选中的类型筛选条件。
        /// </summary>
        [ObservableProperty]
        private string _filterType = "全部";

        /// <summary>
        /// 类型筛选变化时自动刷新列表。
        /// </summary>
        partial void OnFilterTypeChanged(string value) => LoadWithCancel();

        /// <summary>
        /// 是否只显示收藏的文件。
        /// </summary>
        [ObservableProperty]
        private bool _filterFavoritesOnly = false;

        /// <summary>
        /// 收藏筛选变化时自动刷新列表。
        /// </summary>
        partial void OnFilterFavoritesOnlyChanged(bool value) => LoadWithCancel();

        /// <summary>
        /// 取消上一个未完成的加载请求，然后启动新的加载。
        /// 防止快速连续输入/切换筛选时旧结果覆盖新结果。
        /// </summary>
        private void LoadWithCancel()
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = new CancellationTokenSource();
            _ = LoadDataAsync(_loadCts.Token);
        }

        // 按播放次数排序功能已移除 UI 开关，预留内部使用

        /// <summary>
        /// 类型筛选选项列表。
        /// </summary>
        public List<string> TypeFilters { get; } = new() { "全部", "图片", "视频", "音频" };

        /// <summary>
        /// 刷新文件列表命令（已实现）。
        /// </summary>
        [RelayCommand]
        private async Task Refresh()
        {
            await LoadDataAsync();
        }

        /// <summary>
        /// 是否正在扫描中（用于 UI 按钮禁用和进度提示）。
        /// </summary>
        [ObservableProperty]
        private bool _isScanning = false;

        /// <summary>
        /// 扫描进度文本，实时显示已处理的文件数。
        /// </summary>
        [ObservableProperty]
        private string _scanProgress = "";

        /// <summary>
        /// 选择文件夹并扫描媒体文件的命令。
        /// 弹出文件夹选择对话框 → 递归扫描 → 批量入库 → 刷新列表。
        /// </summary>
        [RelayCommand]
        private async Task SelectFolder()
        {
            // 弹出文件夹选择对话框（WPF 原生）
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            dialog.Title = "请选择包含媒体文件的文件夹";

            if (dialog.ShowDialog() != true)
                return;

            IsScanning = true;
            ScanProgress = "正在扫描...";

            try
            {
                var progress = new Progress<int>(count =>
                {
                    ScanProgress = $"正在扫描... 已处理 {count} 个文件";
                });

                var newFiles = await _fileScanService.ScanFolderAsync(dialog.FolderName, progress);

                // 刷新列表、统计和缩略图
                await LoadDataAsync();
                await LoadStatisticsAsync();
                _ = GenerateThumbnailsAfterImportAsync();

                System.Windows.MessageBox.Show(
                    $"扫描完成！共导入 {newFiles.Count} 个新文件。",
                    "导入结果",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"扫描出错：{ex.Message}",
                    "错误",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsScanning = false;
                ScanProgress = "";
            }
        }

        /// <summary>
        /// 导入单个或多个媒体文件到影音库。
        /// 弹出文件选择对话框，支持多选，自动提取元数据后入库。
        /// </summary>
        [RelayCommand]
        private async Task ImportFiles()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Title = "选择要导入的媒体文件";
            dialog.Multiselect = true;
            dialog.Filter = "媒体文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp;*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.flv;*.webm;*.mp3;*.wav;*.flac;*.aac;*.ogg;*.wma;*.m4a|所有文件|*.*";

            if (dialog.ShowDialog() != true)
                return;

            var mediaFiles = new List<Models.MediaFile>();
            int skippedCount = 0;

            foreach (var filePath in dialog.FileNames)
            {
                var extension = System.IO.Path.GetExtension(filePath);
                if (!Core.MediaFileUtils.IsMediaFile(extension))
                {
                    skippedCount++;
                    continue;
                }

                var fileInfo = new System.IO.FileInfo(filePath);
                var type = Core.MediaFileUtils.GetMediaType(extension);

                var (width, height) = type == "图片"
                    ? Core.MediaFileUtils.GetImageDimensions(filePath)
                    : (null, null);
                var duration = type != "图片"
                    ? Core.MediaFileUtils.GetVideoDuration(filePath)
                    : null;

                mediaFiles.Add(new Models.MediaFile
                {
                    Title = Core.MediaFileUtils.GetTitleFromPath(filePath),
                    FilePath = filePath,
                    Type = type,
                    FileFormat = extension.ToLowerInvariant(),
                    FileSize = fileInfo.Length,
                    Width = width,
                    Height = height,
                    Duration = duration,
                    Description = type,
                    IsFavorite = false,
                    DateAdded = DateTime.Now,
                    DateModified = fileInfo.LastWriteTime
                });
            }

            if (mediaFiles.Count == 0)
            {
                System.Windows.MessageBox.Show("未发现可导入的媒体文件。", "提示",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            var addedCount = await _dataService.AddMediaFilesAsync(mediaFiles);

            await LoadDataAsync();
            await LoadStatisticsAsync();
            _ = GenerateThumbnailsAfterImportAsync();

            var msg = addedCount > 0
                ? $"导入完成！成功导入 {addedCount} 个文件。"
                : "所选文件已在库中，无需重复导入。";

            if (skippedCount > 0)
                msg += $"\n（{skippedCount} 个文件因格式不支持已跳过）";

            System.Windows.MessageBox.Show(msg, "导入结果",
                System.Windows.MessageBoxButton.OK,
                addedCount > 0 ? System.Windows.MessageBoxImage.Information : System.Windows.MessageBoxImage.Asterisk);
        }

        /// <summary>
        /// 删除指定文件。弹出确认对话框，提供取消/仅删记录/删源文件三种选项。
        /// </summary>
        [RelayCommand]
        private async Task DeleteFile(MediaFile? file)
        {
            if (file == null) return;

            var dialog = new Views.DeleteConfirmDialog(file);
            dialog.Owner = System.Windows.Application.Current.MainWindow;
            dialog.ShowDialog();

            if (dialog.SelectedAction == 0)
                return; // 取消

            try
            {
                // 先清除选中状态，释放 Image 控件对文件的锁定
                SelectedFile = null;

                if (dialog.SelectedAction == 2)
                {
                    // 删除源文件前校验是否存在
                    if (!await EnsureFileExistsAsync(file, "删除"))
                    {
                        // 用户已选择删除记录或取消，不再重复删除
                        return;
                    }
                    System.IO.File.Delete(file.FilePath);
                }

                // 删除缩略图缓存文件
                if (!string.IsNullOrEmpty(file.ThumbnailPath))
                {
                    try { if (System.IO.File.Exists(file.ThumbnailPath)) System.IO.File.Delete(file.ThumbnailPath); } catch { }
                }

                // 删除数据库记录（Thumbnail 表等关联表由级联删除自动处理）
                await _dataService.DeleteMediaFileAsync(file.Id);

                await LoadDataAsync();
                await LoadStatisticsAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"删除失败：{ex.Message}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 切换指定文件的收藏状态。
        /// </summary>
        [RelayCommand]
        private async Task ToggleFavorite(MediaFile? file)
        {
            if (file == null) return;
            // CheckBox 已更新本地 IsFavorite，持久化并同步 Favorites 表
            await _dataService.UpdateMediaFileAsync(file);
            await _dataService.SyncFavoriteRecordAsync(file.Id, file.IsFavorite);
            await LoadDataAsync();
            // 重新选中同 ID 的文件（保持详情面板不消失）
            SelectedFile = MediaFilesList.FirstOrDefault(f => f.Id == file.Id);
        }

        /// <summary>
        /// 校验文件是否存在。如果不存在，提供删除记录或手动定位选项。
        /// </summary>
        /// <param name="file">要校验的媒体文件。</param>
        /// <param name="operationName">当前操作的名称（用于提示）。</param>
        /// <returns>true 表示可继续操作（文件存在或已重新定位），false 应中止操作。</returns>
        private async Task<bool> EnsureFileExistsAsync(MediaFile file, string operationName)
        {
            if (System.IO.File.Exists(file.FilePath))
                return true;

            var result = System.Windows.MessageBox.Show(
                $"无法{operationName}：源文件不存在。\n\n路径：{file.FilePath}\n\n该文件可能已被移动或删除。\n\n" +
                "选择「是」→ 从库中删除该记录\n" +
                "选择「否」→ 手动定位文件的新位置\n" +
                "选择「取消」→ 取消当前操作",
                "文件丢失",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // 从数据库中删除记录
                await _dataService.DeleteMediaFileAsync(file.Id);
                await LoadDataAsync();
                await LoadStatisticsAsync();
                return false;
            }
            else if (result == MessageBoxResult.No)
            {
                // 让用户手动定位文件
                var openDialog = new Microsoft.Win32.OpenFileDialog();
                openDialog.Title = $"请定位「{file.Title}」的新位置";
                openDialog.Filter = "媒体文件|*.*";

                if (openDialog.ShowDialog() == true)
                {
                    file.FilePath = openDialog.FileName;
                    file.Title = System.IO.Path.GetFileNameWithoutExtension(openDialog.FileName);
                    await _dataService.UpdateMediaFileAsync(file);
                    await LoadDataAsync();
                    return true;
                }
                return false;
            }

            return false; // 取消
        }

        /// <summary>
        /// 查看文件详情命令（待实现）。
        /// </summary>
        [RelayCommand]
        private void ViewFileDetail() => ShowNotImplementedMsg("文件详情");

        /// <summary>
        /// 重命名选中的文件。弹出输入对话框 → 修改文件系统名称 → 更新数据库。
        /// 重命名前自动校验源文件是否存在。
        /// </summary>
        [RelayCommand]
        private async Task RenameFile(MediaFile? file)
        {
            if (file == null) return;

            // 校验源文件是否存在
            if (!await EnsureFileExistsAsync(file, "重命名"))
                return;

            var oldPath = file.FilePath;
            var dir = System.IO.Path.GetDirectoryName(oldPath);
            var ext = System.IO.Path.GetExtension(oldPath);

            var dialog = new Views.InputDialog("重命名文件", "请输入新文件名（不含扩展名）：", file.Title);
            dialog.Owner = System.Windows.Application.Current.MainWindow;

            if (dialog.ShowDialog() != true)
                return;

            var newName = dialog.InputText.Trim();
            if (string.IsNullOrWhiteSpace(newName)) return;

            var newPath = System.IO.Path.Combine(dir!, newName + ext);

            try
            {
                // 检查新路径是否已存在
                if (System.IO.File.Exists(newPath))
                {
                    System.Windows.MessageBox.Show($"目标文件名已存在：{newName}{ext}",
                        "重命名失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 执行文件系统重命名
                System.IO.File.Move(oldPath, newPath);

                // 更新数据库
                file.Title = newName;
                file.FilePath = newPath;
                await _dataService.UpdateMediaFileAsync(file);

                await LoadDataAsync();

                System.Windows.MessageBox.Show($"文件已重命名为：{newName}{ext}",
                    "重命名成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"重命名失败：{ex.Message}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============================================================
        // 播放功能
        // ============================================================

        /// <summary>
        /// 最近播放列表，用于"最近播放"快速访问。
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<PlayHistory> _recentPlays = new();

        /// <summary>
        /// 加载最近播放记录。
        /// </summary>
        private async Task LoadRecentPlaysAsync()
        {
            var data = await _dataService.GetRecentPlaysAsync(10);
            RecentPlays.Clear();
            foreach (var item in data)
            {
                RecentPlays.Add(item);
            }
            OnPropertyChanged(nameof(HasRecentPlays));
        }

        /// <summary>
        /// 是否有最近播放记录。
        /// </summary>
        public bool HasRecentPlays => RecentPlays.Count > 0;

        /// <summary>
        /// 播放选中的媒体文件（图片/视频/音频）。
        /// </summary>
        [RelayCommand]
        private async Task PlayFile(MediaFile? file)
        {
            if (file == null) return;
            _playbackService.Play(file, System.Windows.Application.Current.MainWindow);
            await LoadRecentPlaysAsync();
        }

        // ============================================================
        // 简介保存
        // ============================================================

        /// <summary>
        /// 保存选中文件的简介到数据库。
        /// </summary>
        [RelayCommand]
        private async Task SaveDescription(MediaFile? file)
        {
            if (file == null) return;
            await _dataService.UpdateMediaFileAsync(file);
        }

        // ============================================================
        // 缩略图服务
        // ============================================================

        /// <summary>
        /// 为指定文件生成缩略图并更新数据库。
        /// </summary>
        private async Task<string?> GenerateThumbnailForFileAsync(MediaFile file)
        {
            var thumbPath = await _thumbnailService.GenerateThumbnailAsync(file);
            if (thumbPath != null)
            {
                file.ThumbnailPath = thumbPath;
                await _dataService.UpdateMediaFileAsync(file);
            }
            return thumbPath;
        }

        /// <summary>
        /// 为列表中所有缺少缩略图的文件批量生成缩略图。
        /// </summary>
        private async Task GenerateMissingThumbnailsAsync()
        {
            var missing = MediaFilesList.Where(f => string.IsNullOrEmpty(f.ThumbnailPath)).ToList();
            foreach (var file in missing)
            {
                var thumbPath = await _thumbnailService.GenerateThumbnailAsync(file);
                if (thumbPath != null)
                {
                    file.ThumbnailPath = thumbPath;
                    await _dataService.UpdateMediaFileAsync(file);
                }
            }
        }

        /// <summary>
        /// 为选中文件设置/重新生成缩略图。
        /// </summary>
        [RelayCommand]
        private async Task SetThumbnail(MediaFile? file)
        {
            if (file == null) return;

            var result = System.Windows.MessageBox.Show(
                "请选择缩略图生成方式：\n\n「是」→ 自动生成\n「否」→ 手动选择图片\n「取消」→ 取消操作",
                "设置缩略图",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
                return;

            if (result == MessageBoxResult.Yes)
            {
                var thumbPath = await GenerateThumbnailForFileAsync(file);
                if (thumbPath != null)
                {
                    await LoadDataAsync();
                    System.Windows.MessageBox.Show("缩略图生成成功！", "成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show("缩略图生成失败，请检查文件是否存在。", "失败",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else if (result == MessageBoxResult.No)
            {
                var dialog = new Microsoft.Win32.OpenFileDialog();
                dialog.Title = "选择一张图片作为缩略图";
                dialog.Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp";
                if (dialog.ShowDialog() == true)
                {
                    // 缩放用户选的图片到 200px，保存到配置的缓存目录
                    var cacheDir = _thumbnailService.CacheDirectory;
                    System.IO.Directory.CreateDirectory(cacheDir);
                    var destPath = System.IO.Path.Combine(cacheDir, $"manual_{file.Id}.jpg");

                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(dialog.FileName);
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = 200;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    var encoder = new System.Windows.Media.Imaging.JpegBitmapEncoder();
                    encoder.QualityLevel = 85;
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    using (var stream = new System.IO.FileStream(destPath, System.IO.FileMode.Create))
                        encoder.Save(stream);

                    file.ThumbnailPath = destPath;
                    await _dataService.UpdateMediaFileAsync(file);

                    await LoadDataAsync();
                    System.Windows.MessageBox.Show("缩略图设置成功！", "成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        /// <summary>
        /// 在扫描/导入后为新增文件生成缩略图。
        /// </summary>
        private async Task GenerateThumbnailsAfterImportAsync()
        {
            await GenerateMissingThumbnailsAsync();
            // 清除选中状态，强制详情面板刷新
            SelectedFile = null;
            await LoadDataAsync();
        }

        // ============================================================
        // 批量操作
        // ============================================================

        /// <summary>
        /// 是否有选中多个文件（控制批量工具栏显隐）。
        /// </summary>
        public bool HasMultipleSelected => SelectedFiles.Count > 1;

        /// <summary>
        /// 通知 HasMultipleSelected 属性变化（由 MainWindow 的 SelectionChanged 触发）。
        /// </summary>
        public void NotifyHasMultipleSelected() => OnPropertyChanged(nameof(HasMultipleSelected));

        /// <summary>
        /// 批量收藏/取消收藏。
        /// </summary>
        [RelayCommand]
        private async Task BatchToggleFavorite()
        {
            var files = SelectedFiles.ToList();
            if (files.Count == 0) return;
            // 多数决定：未收藏的多 → 全部收藏，已收藏的多 → 全部取消
            bool targetState = files.Count(f => !f.IsFavorite) >= files.Count(f => f.IsFavorite);
            foreach (var file in files)
            {
                file.IsFavorite = targetState;
                await _dataService.UpdateMediaFileAsync(file);
                await _dataService.SyncFavoriteRecordAsync(file.Id, file.IsFavorite);
            }
            await LoadDataAsync();
            await LoadStatisticsAsync();
            System.Windows.MessageBox.Show($"已切换 {files.Count} 个文件的收藏状态。",
                "批量收藏完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 批量删除（先确认再操作）。
        /// </summary>
        [RelayCommand]
        private async Task BatchDelete()
        {
            var files = SelectedFiles.ToList();
            if (files.Count == 0) return;

            var result = System.Windows.MessageBox.Show(
                $"确定要删除选中的 {files.Count} 个文件记录吗？\n\n选择「是」→ 仅删除记录\n选择「否」→ 删除源文件+记录\n选择「取消」→ 取消",
                "批量删除",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Cancel) return;
            bool deleteSource = result == MessageBoxResult.No;

            try
            {
                SelectedFile = null;
                var ids = new List<int>(files.Count);
                foreach (var file in files)
                {
                    // 删除缩略图缓存
                    if (!string.IsNullOrEmpty(file.ThumbnailPath))
                    {
                        try { if (System.IO.File.Exists(file.ThumbnailPath)) System.IO.File.Delete(file.ThumbnailPath); } catch { }
                    }
                    // 删除源文件
                    if (deleteSource)
                    {
                        try { if (System.IO.File.Exists(file.FilePath)) System.IO.File.Delete(file.FilePath); } catch { }
                    }
                    ids.Add(file.Id);
                }

                await _dataService.DeleteMediaFilesAsync(ids);
                await LoadDataAsync();
                await LoadStatisticsAsync();
                System.Windows.MessageBox.Show($"已删除 {ids.Count} 个文件记录。",
                    "批量删除完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"批量删除失败：{ex.Message}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 批量生成缩略图。
        /// </summary>
        [RelayCommand]
        private async Task BatchGenerateThumbnails()
        {
            var files = SelectedFiles.ToList();
            if (files.Count == 0) return;
            int success = 0;
            foreach (var file in files)
            {
                var thumbPath = await _thumbnailService.GenerateThumbnailAsync(file);
                if (thumbPath != null)
                {
                    file.ThumbnailPath = thumbPath;
                    await _dataService.UpdateMediaFileAsync(file);
                    success++;
                }
            }
            await LoadDataAsync();
            System.Windows.MessageBox.Show($"已为 {success}/{files.Count} 个文件生成缩略图。",
                "批量缩略图完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 批量增强（待实现）。
        /// </summary>
        [RelayCommand]
        private async Task BatchEnhance()
        {
            var allSelected = SelectedFiles.ToList();
            var videoCount = allSelected.Count(f => f.Type == "视频");
            var files = allSelected.Where(f => f.Type == "图片").ToList();

            if (files.Count == 0)
            {
                var msg = "请选择至少一张图片文件。";
                if (videoCount > 0) msg += $"\n\n已跳过 {videoCount} 个视频文件（视频增强不支持批量处理，请单独增强）。";
                MessageBox.Show(msg, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (videoCount > 0)
            {
                var result = MessageBox.Show(
                    $"选中了 {videoCount} 个视频文件将被跳过（视频增强耗时较长，请单独处理）。\n\n是否继续批量增强 {files.Count} 张图片？",
                    "批量增强", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;
            }

            var saveDir = EnhancementSavePath;
            System.IO.Directory.CreateDirectory(saveDir);
            int success = 0;

            foreach (var file in files)
            {
                try
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(file.FilePath);
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    BitmapSource enhanced;
                    var offlineMethod = GetSelectedOfflineMethod();
                    if (offlineMethod is IOnnxEnhancement onnx)
                        enhanced = await onnx.EnhanceAsync(bitmap);
                    else
                        enhanced = LinearStretch.Enhance(bitmap);

                    var fileName = $"enhanced_{file.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                    var filePath = System.IO.Path.Combine(saveDir, fileName);

                    var encoder = new System.Windows.Media.Imaging.JpegBitmapEncoder();
                    encoder.QualityLevel = 92;
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(enhanced));
                    using var stream = new System.IO.FileStream(filePath, System.IO.FileMode.Create);
                    encoder.Save(stream);

                    // 入库
                    var info = new System.IO.FileInfo(filePath);
                    var newFile = new Models.MediaFile
                    {
                        Title = System.IO.Path.GetFileNameWithoutExtension(fileName),
                        FilePath = filePath,
                        Type = "图片",
                        FileFormat = ".jpg",
                        FileSize = info.Length,
                        Description = $"由「{file.Title}」增强生成",
                        SourceFileId = file.Id,
                        IsFavorite = false,
                        DateAdded = DateTime.Now,
                        DateModified = info.LastWriteTime
                    };
                    await _dataService.AddMediaFileAsync(newFile);

                    // 缩略图
                    try { await GenerateThumbnailForFileAsync(newFile); } catch { }

                    success++;
                }
                catch { }
            }

            await LoadDataAsync();
            await LoadStatisticsAsync();

            System.Windows.MessageBox.Show(
                $"批量增强完成！\n成功处理 {success}/{files.Count} 张图片。\n保存位置：{saveDir}",
                "批量增强", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 清空所有缩略图缓存文件，并清除数据库中所有记录的 ThumbnailPath。
        /// </summary>
        [RelayCommand]
        private async Task ClearThumbnailCache()
        {
            var result = System.Windows.MessageBox.Show(
                "确定要清空所有缩略图缓存吗？\n\n缓存文件将被删除，下次查看时重新生成。",
                "清空缩略图缓存",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                // 清空缓存目录
                var cacheDir = _thumbnailService.CacheDirectory;
                if (System.IO.Directory.Exists(cacheDir))
                {
                    foreach (var file in System.IO.Directory.GetFiles(cacheDir, "*.jpg"))
                    {
                        try { System.IO.File.Delete(file); } catch { }
                    }
                }

                // 清除数据库中的 ThumbnailPath
                var allFiles = await _dataService.GetAllMediaFilesAsync();
                foreach (var f in allFiles)
                {
                    if (!string.IsNullOrEmpty(f.ThumbnailPath))
                    {
                        f.ThumbnailPath = null;
                        await _dataService.UpdateMediaFileAsync(f);
                    }
                }

                await LoadDataAsync();

                System.Windows.MessageBox.Show("缩略图缓存已清空！\n下次查看文件时将自动重新生成。",
                    "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"清空缓存失败：{ex.Message}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}