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

            // 构造时初始化加载媒体文件列表、统计数据、最近播放和预设
            LoadDataAsync().Wait();
            LoadStatisticsAsync().Wait();
            LoadRecordingsAsync().Wait();
            LoadRecentPlaysAsync().Wait();
        }

        /// <summary>
        /// 从数据库中异步加载媒体文件到 ObservableCollection。
        /// 根据当前搜索关键词、类型筛选和收藏状态进行过滤。
        /// </summary>
        private async Task LoadDataAsync()
        {
            var keyword = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText;
            var type = FilterType == "全部" ? null : FilterType;
            bool? favoritesOnly = FilterFavoritesOnly ? true : null;

            var data = await _dataService.SearchMediaFilesAsync(keyword, type, favoritesOnly);
            _playCountMap = await _dataService.GetPlayCountsAsync();
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
        /// 0:数据统计, 1:文件管理, 2:实时增强, 3:屏幕录制, 4:AI对话, 5:AI编辑, 6:系统设置
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
        partial void OnSearchTextChanged(string value) => _ = LoadDataAsync();

        /// <summary>
        /// 当前选中的类型筛选条件。
        /// </summary>
        [ObservableProperty]
        private string _filterType = "全部";

        /// <summary>
        /// 类型筛选变化时自动刷新列表。
        /// </summary>
        partial void OnFilterTypeChanged(string value) => _ = LoadDataAsync();

        /// <summary>
        /// 是否只显示收藏的文件。
        /// </summary>
        [ObservableProperty]
        private bool _filterFavoritesOnly = false;

        /// <summary>
        /// 收藏筛选变化时自动刷新列表。
        /// </summary>
        partial void OnFilterFavoritesOnlyChanged(bool value) => _ = LoadDataAsync();

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
            // CheckBox 已更新本地 IsFavorite，直接持久化
            await _dataService.UpdateMediaFileAsync(file);
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
            foreach (var file in files)
            {
                file.IsFavorite = !file.IsFavorite;
                await _dataService.UpdateMediaFileAsync(file);
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

                    var enhanced = LinearStretch.Enhance(bitmap);

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

        // ============================================================
        // 实时增强面板
        // ============================================================

        /// <summary>
        /// 当前选中的增强方法名称。
        /// </summary>
        [ObservableProperty]
        private string _selectedMethod = "线性拉伸";

        /// <summary>
        /// 当前是否选择了线性拉伸（控制参数区的显隐）。
        /// </summary>
        public bool IsLinearStretchSelected => _selectedMethod == "线性拉伸";

        partial void OnSelectedMethodChanged(string value)
        {
            _registry.SetCurrent(value);
            OnPropertyChanged(nameof(IsLinearStretchSelected));
            // 刷新增强预览
            if (OriginalPreview != null)
                EnhancedPreview = LinearStretch.Enhance(OriginalPreview);
        }

        /// <summary>
        /// 是否开启实时增强。
        /// </summary>
        [ObservableProperty]
        private bool _isEnhancementEnabled = false;

        /// <summary>
        /// 可用的增强方法列表（从注册中心动态获取）。
        /// </summary>
        public List<string> EnhancementMethods => _registry.MethodNames.ToList();

        /// <summary>
        /// 线性拉伸算法实例（C# 原生实现）。
        /// </summary>
        public LinearStretchMethod LinearStretch { get; } = new();

        /// <summary>
        /// 原始预览图的 BitmapSource。
        /// </summary>
        [ObservableProperty]
        private BitmapSource? _originalPreview = null;

        /// <summary>
        /// 增强后的预览图的 BitmapSource。
        /// </summary>
        [ObservableProperty]
        private BitmapSource? _enhancedPreview = null;

        /// <summary>
        /// 是否已有增强预览图（控制对比区域的显隐）。
        /// </summary>
        public bool HasPreview => EnhancedPreview != null;

        /// <summary>
        /// 开启/关闭实时增强（旧版内嵌预览，保留向后兼容）。
        /// </summary>
        [RelayCommand]
        private void ToggleEnhancement()
        {
            IsEnhancementEnabled = !IsEnhancementEnabled;
            if (IsEnhancementEnabled)
            {
                if (OriginalPreview != null)
                {
                    EnhancedPreview = LinearStretch.Enhance(OriginalPreview);
                }
            }
        }

        // ============================================================
        // 全屏实时增强
        // ============================================================

        private Views.FullscreenEnhanceWindow? _fullscreenWindow;

        /// <summary>
        /// 启动全屏实时增强——以透明覆盖窗口形式增强整个屏幕画面。
        /// </summary>
        [RelayCommand]
        private void StartFullscreenEnhance()
        {
            if (_fullscreenWindow != null) return;

            var method = _registry.Current;
            if (method == null)
            {
                System.Windows.MessageBox.Show("没有可用的增强方法，请先在设置中注册。",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                _fullscreenWindow?.Close();
                _fullscreenWindow = new Views.FullscreenEnhanceWindow();
                _fullscreenWindow.Stopped += OnFullscreenStopped;

                // 从当前参数构建参数字典
                var parameters = new Dictionary<string, double>();
                foreach (var p in LinearStretch.Parameters)
                    parameters[p.Key] = p.Value;

                _fullscreenWindow.Start(method, parameters);

                if (_fullscreenWindow.LastError != null)
                {
                    System.Windows.MessageBox.Show(
                        $"全屏增强启动失败：{_fullscreenWindow.LastError}",
                        "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    _fullscreenWindow.Close();
                    _fullscreenWindow = null;
                    return;
                }

                IsEnhancementEnabled = true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"全屏增强启动失败：{ex.Message}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                _fullscreenWindow?.Close();
                _fullscreenWindow = null;
            }
        }

        private void OnFullscreenStopped()
        {
            _fullscreenWindow = null;
            IsEnhancementEnabled = false;
        }

        /// <summary>
        /// 手动停止全屏增强。
        /// </summary>
        [RelayCommand]
        private void StopFullscreenEnhance()
        {
            var w = _fullscreenWindow;
            if (w == null) return;
            w.Stopped -= OnFullscreenStopped;
            w.Stop();
            w.Close();
            OnFullscreenStopped();
        }

        /// <summary>
        /// 选择并预览增强效果。
        /// </summary>
        [RelayCommand]
        private async Task PreviewEnhancement()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Title = "选择一张图片进行增强预览";
            dialog.Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp";
            if (dialog.ShowDialog() != true) return;

            try
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(dialog.FileName);
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                OriginalPreview = bitmap;
                EnhancedPreview = LinearStretch.Enhance(bitmap);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"加载图片失败：{ex.Message}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 重置增强参数到默认值并刷新预览。
        /// </summary>
        [RelayCommand]
        private void ResetEnhancement()
        {
            LinearStretch.Contrast = 1.0;
            LinearStretch.Brightness = 0;
            if (OriginalPreview != null)
                EnhancedPreview = LinearStretch.Enhance(OriginalPreview);
        }

        /// <summary>增强进度文本。</summary>
        [ObservableProperty]
        private string _enhanceProgress = "";
        public bool HasEnhanceProgress => !string.IsNullOrEmpty(EnhanceProgress);
        partial void OnEnhanceProgressChanged(string value) => OnPropertyChanged(nameof(HasEnhanceProgress));

        /// <summary>是否正在增强（禁止并发）。</summary>
        [ObservableProperty]
        private bool _isEnhancing = false;

        private CancellationTokenSource? _enhanceCts;

        [RelayCommand]
        private void CancelEnhance()
        {
            _enhanceCts?.Cancel();
            EnhanceProgress = "已取消";
        }

        [RelayCommand]
        private async Task EnhanceFile(MediaFile? file)
        {
            if (file == null) return;

            if (file.Type == "视频")
            {
                if (_isEnhancing)
                {
                    MessageBox.Show("已有视频正在增强，请等待完成或取消。", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (!File.Exists(file.FilePath))
                {
                    MessageBox.Show("源文件不存在。", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                IsEnhancing = true;
                _enhanceCts = new CancellationTokenSource();

                try
                {
                    var enhancer = new VideoEnhancer(LinearStretch);
                    var saveDir = EnhancementSavePath;
                    Directory.CreateDirectory(saveDir);

                    EnhanceProgress = "正在增强视频...";
                    var progress = new Progress<(int current, int total)>(p =>
                        EnhanceProgress = $"正在增强视频... {p.current}/{p.total} 帧");

                    var outputPath = await enhancer.EnhanceAsync(file.FilePath, saveDir, progress,
                        _enhanceCts.Token);

                    if (_enhanceCts.IsCancellationRequested)
                    {
                        EnhanceProgress = "";
                        return;
                    }

                    EnhanceProgress = "";

                    if (outputPath == null)
                    {
                        MessageBox.Show("视频增强失败，请确保 ffmpeg.exe 已下载。",
                            "增强失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var info = new FileInfo(outputPath);
                    var newFile = new MediaFile
                    {
                        Title = Path.GetFileNameWithoutExtension(outputPath),
                        FilePath = outputPath,
                        Type = "视频",
                        FileFormat = ".mp4",
                        FileSize = info.Length,
                        Description = $"由「{file.Title}」增强生成",
                        IsFavorite = false,
                        DateAdded = DateTime.Now,
                        DateModified = DateTime.Now
                    };
                    await _dataService.AddMediaFileAsync(newFile);
                    try { await _dataService.AddEnhancementLogAsync(new EnhancementLog { MediaFileId = file.Id, MethodName = "线性拉伸", OutputPath = outputPath, CreatedAt = DateTime.Now }); } catch { }
                    await LoadDataAsync();
                    await LoadStatisticsAsync();

                    MessageBox.Show($"视频增强完成！\n已保存并入库。\n{outputPath}",
                        "增强成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                finally
                {
                    IsEnhancing = false;
                    _enhanceCts?.Dispose();
                    _enhanceCts = null;
                }
                return;
            }

            if (file.Type != "图片")
            {
                MessageBox.Show("当前仅支持图片和视频文件的增强。",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 图片增强（原有逻辑）
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(file.FilePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                var enhanced = LinearStretch.Enhance(bitmap);

                var saveDir = EnhancementSavePath;
                Directory.CreateDirectory(saveDir);
                var fileName = $"enhanced_{file.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                var filePath = Path.Combine(saveDir, fileName);

                var encoder = new JpegBitmapEncoder { QualityLevel = 92 };
                encoder.Frames.Add(BitmapFrame.Create(enhanced));
                using var stream = new FileStream(filePath, FileMode.Create);
                encoder.Save(stream);

                var newFile = new MediaFile
                {
                    Title = Path.GetFileNameWithoutExtension(fileName),
                    FilePath = filePath,
                    Type = "图片",
                    FileFormat = ".jpg",
                    FileSize = new FileInfo(filePath).Length,
                    Description = $"由「{file.Title}」增强生成",
                    IsFavorite = false,
                    DateAdded = DateTime.Now,
                    DateModified = DateTime.Now
                };
                await _dataService.AddMediaFileAsync(newFile);
                try { await GenerateThumbnailForFileAsync(newFile); } catch { }
                try { await _dataService.AddEnhancementLogAsync(new EnhancementLog { MediaFileId = file.Id, MethodName = "线性拉伸", OutputPath = filePath, CreatedAt = DateTime.Now }); } catch { }
                await LoadDataAsync();

                MessageBox.Show($"增强完成！\n已保存并导入到影音库。\n{filePath}",
                    "增强成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"增强失败：{ex.Message}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 导出增强后的图像到文件。
        /// </summary>
        [RelayCommand]
        private async Task ExportEnhanced()
        {
            if (EnhancedPreview == null)
            {
                System.Windows.MessageBox.Show("请先生成增强预览图。",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveDir = EnhancementSavePath;
            System.IO.Directory.CreateDirectory(saveDir);
            var fileName = $"enhanced_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            var filePath = System.IO.Path.Combine(saveDir, fileName);

            try
            {
                var encoder = new System.Windows.Media.Imaging.JpegBitmapEncoder();
                encoder.QualityLevel = 92;
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(EnhancedPreview));
                using var stream = new System.IO.FileStream(filePath, System.IO.FileMode.Create);
                encoder.Save(stream);

                // 自动导入影音库
                var info = new System.IO.FileInfo(filePath);
                var newFile = new Models.MediaFile
                {
                    Title = System.IO.Path.GetFileNameWithoutExtension(fileName),
                    FilePath = filePath,
                    Type = "图片",
                    FileFormat = ".jpg",
                    FileSize = info.Length,
                    Description = "增强图片",
                    IsFavorite = false,
                    DateAdded = DateTime.Now,
                    DateModified = info.LastWriteTime
                };
                await _dataService.AddMediaFileAsync(newFile);

                // 生成缩略图
                try { await GenerateThumbnailForFileAsync(newFile); } catch { }

                await LoadDataAsync();

                System.Windows.MessageBox.Show($"增强图片已保存并导入影音库：\n{filePath}",
                    "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"导出失败：{ex.Message}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============================================================
        // 屏幕录制面板
        // ============================================================

        private ScreenRecorder? _recorder;

        /// <summary>
        /// 录制历史列表。
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<Recording> _recordings = new();

        // 录制来源仅全屏（窗口/自定义区域留待未来版本）

        /// <summary>
        /// 是否正在录制中。
        /// </summary>
        [ObservableProperty]
        private bool _isRecording = false;

        /// <summary>
        /// 录制时长显示文本。
        /// </summary>
        [ObservableProperty]
        private string _recordingDuration = "00:00";

        /// <summary>
        /// 录制状态文本。
        /// </summary>
        [ObservableProperty]
        private string _recordingStatus = "就绪";

        /// <summary>
        /// 是否启用增强录制（录制时同步增强画面）。
        /// </summary>
        [ObservableProperty]
        private bool _enhancedRecording = true;

        [RelayCommand]
        private void StartRecording()
        {
            if (_recorder?.IsRecording == true) return;

            var outputDir = RecordingSavePath;
            Directory.CreateDirectory(outputDir);

            var screenW = (int)SystemParameters.PrimaryScreenWidth;
            var screenH = (int)SystemParameters.PrimaryScreenHeight;

            IRealTimeEnhancer? enhancer = EnhancedRecording ? _registry.Current : null;
            IReadOnlyDictionary<string, double>? eParams = null;
            if (enhancer != null)
            {
                var dict = new Dictionary<string, double>();
                foreach (var p in LinearStretch.Parameters) dict[p.Key] = p.Value;
                eParams = dict;
            }

            _recorder = new ScreenRecorder(screenW, screenH, outputDir,
                enhancer, eParams, fps: 15);

            _recorder.Start();

            if (_recorder.LastError != null)
            {
                RecordingStatus = $"启动失败: {_recorder.LastError}";
                _recorder.Dispose(); _recorder = null;
                return;
            }

            IsRecording = true;
            RecordingStatus = "⏺ 录制中...";
            _ = UpdateRecordingDurationAsync();
        }

        [RelayCommand]
        private async Task StopRecording()
        {
            if (_recorder == null) return;

            IsRecording = false;
            RecordingStatus = "正在编码视频...";

            var outputPath = await _recorder.StopAsync();
            var lastErr = _recorder.LastError;
            var frameCount = _recorder.FrameCount;
            var durationSec = _recorder.DurationSeconds;
            _recorder.Dispose();
            _recorder = null;

            if (outputPath != null && outputPath.EndsWith(".mp4"))
            {
                // 编码成功
                try
                {
                    var info = new FileInfo(outputPath);
                    var mediaFile = new MediaFile
                    {
                        Title = Path.GetFileNameWithoutExtension(outputPath),
                        FilePath = outputPath, Type = "视频", FileFormat = ".mp4",
                        FileSize = info.Length, Description = "屏幕录制",
                        IsFavorite = false, DateAdded = DateTime.Now,
                        DateModified = info.LastWriteTime
                    };
                    await _dataService.AddMediaFileAsync(mediaFile);
                    try { await _dataService.AddRecordingAsync(mediaFile.Id, outputPath, durationSec, EnhancedRecording); } catch { }
                    await LoadRecordingsAsync();
                    await LoadDataAsync();
                    await LoadStatisticsAsync();
                }
                catch (Exception ex) { Debug.WriteLine($"入库失败: {ex.Message}"); }

                RecordingStatus = "✅ 录制完成";
                MessageBox.Show($"录制完成！\n共 {frameCount} 帧\n{outputPath}",
                    "录制完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (outputPath != null)
            {
                // 帧序列（ffmpeg 编码失败但帧已保存）
                RecordingStatus = "⚠ 帧序列已保存";
                MessageBox.Show($"已保存帧序列。\n\n{outputPath}\n\n" +
                    $"ffmpeg 编码失败：{lastErr}\n\n" +
                    $"请确保 ffmpeg.exe 已下载（数据统计页→检查依赖），\n" +
                    $"或手动运行：ffmpeg -framerate 15 -i \"{outputPath.Replace('\\','/')}/%08d.jpg\" output.mp4",
                    "录制完成(帧序列)", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                RecordingStatus = "录制失败";
                MessageBox.Show(lastErr ?? "未知错误", "录制失败",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private void PauseRecording() => ShowNotImplementedMsg("暂停录制");

        [RelayCommand]
        private void OpenRecordingFolder()
        {
            var path = RecordingSavePath;
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe", Arguments = $"\"{path}\"",
                UseShellExecute = false
            });
        }

        private async Task UpdateRecordingDurationAsync()
        {
            while (_recorder?.IsRecording == true)
            {
                var ts = TimeSpan.FromSeconds(_recorder.DurationSeconds);
                RecordingDuration = $"{ts.Minutes:D2}:{ts.Seconds:D2}";
                await Task.Delay(500);
            }
            RecordingDuration = "00:00";
        }

        // ============================================================
        // AI 对话面板
        // ============================================================

        /// <summary>对话消息列表。</summary>
        public ObservableCollection<ChatMessage> AiMessages { get; } = new()
        {
            new ChatMessage
            {
                Role = "assistant",
                Content = "你好！我是影音智增强管理系统的 AI 助手。\n\n你可以：\n• 从左侧勾选文件\n• 点击快捷提示按钮\n• 或直接输入问题\n\n未配置 API 时将使用本地模板分析。"
            }
        };

        /// <summary>输入框文本。</summary>
        [ObservableProperty]
        private string _aiInputText = "";

        // ---- AI 对话配置 ----

        [ObservableProperty]
        private string _chatApiEndpoint = "https://dashscope.aliyuncs.com/compatible-mode/v1";

        public List<string> ApiEndpointPresets { get; } = new()
        {
            "https://dashscope.aliyuncs.com/compatible-mode/v1",
            "https://api.openai.com/v1",
            "https://api.deepseek.com",
            "https://open.bigmodel.cn",
            "http://localhost:11434"
        };

        [ObservableProperty]
        private string _chatApiKey = "";

        [ObservableProperty]
        private string _chatModelName = "qwen-plus";

        [ObservableProperty]
        private bool _chatConfigured = false;

        [ObservableProperty]
        private string _chatStatusText = "○ 未配置";

        partial void OnChatApiEndpointChanged(string value) => UpdateChatConfig();
        partial void OnChatApiKeyChanged(string value) => UpdateChatConfig();
        partial void OnChatModelNameChanged(string value) => UpdateChatConfig();

        private void UpdateChatConfig()
        {
            _aiService.ConfigureChat(ChatApiKey, ChatApiEndpoint, ChatModelName);
            ChatConfigured = _aiService.IsChatConfigured;
            ChatStatusText = ChatConfigured ? $"● 已连接  {ChatModelName}" : "○ 未配置";
        }

        // ---- AI 编辑配置 ----

        [ObservableProperty]
        private string _editApiEndpoint = "https://dashscope.aliyuncs.com/api/v1/services/aigc/image-generation/generation";

        [ObservableProperty]
        private string _editApiKey = "";

        [ObservableProperty]
        private string _editModelName = "wanx2.0-t2i-turbo";

        [ObservableProperty]
        private string _editFormat = "auto";

        [ObservableProperty]
        private bool _editConfigured = false;

        [ObservableProperty]
        private string _editStatusText = "○ 未配置";

        public List<string> EditFormatOptions { get; } = new() { "auto", "openai", "dashscope" };

        partial void OnEditApiEndpointChanged(string value) => UpdateEditConfig();
        partial void OnEditApiKeyChanged(string value) => UpdateEditConfig();
        partial void OnEditModelNameChanged(string value) => UpdateEditConfig();
        partial void OnEditFormatChanged(string value) => UpdateEditConfig();

        private void UpdateEditConfig()
        {
            _aiService.ConfigureEdit(EditApiKey, EditApiEndpoint, EditModelName, EditFormat);
            EditConfigured = _aiService.IsEditConfigured;
            EditStatusText = EditConfigured ? $"● 已连接  {EditModelName}" : "○ 未配置";
        }

        private List<MediaFile> GetSelectedAiFiles() =>
            MediaFilesList.Where(f => f.IsSelected).ToList();

        [RelayCommand]
        private async Task SendAiMessage()
        {
            if (string.IsNullOrWhiteSpace(AiInputText)) return;

            var userMsg = new ChatMessage { Role = "user", Content = AiInputText };
            AiMessages.Add(userMsg);
            AiInputText = "";

            var files = GetSelectedAiFiles();
            var thinking = new ChatMessage { Role = "thinking", Content = "正在处理，请稍候..." };
            AiMessages.Add(thinking);

            var reply = await _aiService.ChatAsync(AiMessages.Take(AiMessages.Count - 1).ToList(), files);
            AiMessages.Remove(thinking);
            AiMessages.Add(new ChatMessage { Role = "assistant", Content = reply });
        }

        [RelayCommand]
        private async Task ApplyAiPreset(string preset)
        {
            var files = GetSelectedAiFiles();

            switch (preset)
            {
                case "简介":
                    AiMessages.Add(new ChatMessage { Role = "user", Content = "📝 请为选中的文件生成简介和标签。" });
                    break;

                case "数据":
                    AiMessages.Add(new ChatMessage { Role = "user", Content = "📊 请生成选中文件的统计摘要。" });
                    break;

                default: return;
            }

            var thinking = new ChatMessage { Role = "thinking", Content = "正在处理，请稍候..." };
            AiMessages.Add(thinking);

            var prompt = preset switch
            {
                "简介" => AiService.DescriptionPrompt(),
                "数据" => AiService.DataSummaryPrompt(),
                _ => null
            };

            var reply = await _aiService.ChatAsync(AiMessages.Take(AiMessages.Count - 1).ToList(), files, prompt);
            AiMessages.Remove(thinking);
            AiMessages.Add(new ChatMessage { Role = "assistant", Content = reply });
        }

        [RelayCommand]
        private void ClearAiChat()
        {
            AiMessages.Clear();
            AiMessages.Add(new ChatMessage
            {
                Role = "assistant",
                Content = "对话已清空。选择文件后点击快捷提示或直接输入问题。"
            });
        }

        // ============================================================
        // AI 编辑面板
        // ============================================================

        /// <summary>编辑选中的图片文件。</summary>
        public ObservableCollection<MediaFile> AiEditFiles { get; } = new();

        /// <summary>编辑提示词。</summary>
        [ObservableProperty]
        private string _aiEditPrompt = "";

        /// <summary>原图预览。</summary>
        [ObservableProperty]
        private BitmapSource? _aiEditOriginal;

        /// <summary>结果图。</summary>
        [ObservableProperty]
        private BitmapSource? _aiEditResult;

        /// <summary>是否正在生成。</summary>
        [ObservableProperty]
        private bool _aiEditGenerating = false;

        /// <summary>生成状态文本。</summary>
        [ObservableProperty]
        private string _aiEditStatus = "";

        [RelayCommand]
        private async Task AiEditSelectFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择要编辑的图片",
                Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.webp"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(dialog.FileName);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                AiEditOriginal = bitmap;
                AiEditResult = null;
                AiEditStatus = "";
                AiEditFiles.Clear();
                OnPropertyChanged(nameof(HasEditImage));
                AiEditFiles.Add(new MediaFile
                {
                    Title = System.IO.Path.GetFileName(dialog.FileName),
                    FilePath = dialog.FileName,
                    Type = "图片"
                });
            }
            catch { }
        }

        public bool HasEditImage => AiEditOriginal != null;
        public bool HasEditResult => AiEditResult != null;

        [RelayCommand]
        private void AiEditClearFile()
        {
            AiEditFiles.Clear();
            AiEditOriginal = null;
            AiEditResult = null;
            AiEditStatus = "";
            OnPropertyChanged(nameof(HasEditImage));
        }

        [RelayCommand]
        private void AiEditSave()
        {
            if (AiEditResult == null) { AiEditStatus = "⚠ 暂无生成结果，请先生成图片。"; return; }
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "保存生成的图片",
                Filter = "PNG|*.png|JPEG|*.jpg",
                FileName = $"ai_gen_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                var encoder = dialog.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                    ? (BitmapEncoder)new JpegBitmapEncoder { QualityLevel = 95 }
                    : new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(AiEditResult));
                using var stream = new FileStream(dialog.FileName, FileMode.Create);
                encoder.Save(stream);
                AiEditStatus = $"✅ 已保存到 {dialog.FileName}";
            }
            catch (Exception ex) { AiEditStatus = $"保存失败: {ex.Message}"; }
        }

        [RelayCommand]
        private async Task AiEditGenerate()
        {
            if (!_aiService.IsEditConfigured)
            {
                AiEditStatus = "⚠ 请先在系统设置中配置 API（阿里百炼）。";
                return;
            }
            if (string.IsNullOrWhiteSpace(AiEditPrompt))
            {
                AiEditStatus = "⚠ 请输入增强/美化描述。";
                return;
            }
            // 无图也允许生成（纯文本生图）

            AiEditGenerating = true;
            AiEditStatus = "正在生成，通常需要 10-30 秒...";
            AiEditResult = null;

            try
            {
                string? imageB64 = null;
                var filePath = AiEditFiles.FirstOrDefault()?.FilePath;
                if (filePath != null && File.Exists(filePath))
                    imageB64 = Convert.ToBase64String(await File.ReadAllBytesAsync(filePath));

                var (resultBytes, error) = await _aiService.GenerateImageAsync(AiEditPrompt, imageB64);
                if (resultBytes != null)
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = new MemoryStream(resultBytes);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    AiEditResult = bitmap;
                    OnPropertyChanged(nameof(HasEditResult));
                    AiEditStatus = "✅ 生成完成";
                }
                else
                {
                    AiEditStatus = $"❌ {error ?? "未知错误"}";
                }
            }
            catch (Exception ex)
            {
                AiEditStatus = $"❌ 错误: {ex.Message}";
            }
            finally
            {
                AiEditGenerating = false;
            }
        }

        // ============================================================
        // 系统设置面板
        // ============================================================

        /// <summary>
        /// 选择的运算设备（当前仅支持 CPU）。
        /// </summary>
        [ObservableProperty]
        private string _selectedDevice = "CPU";

        /// <summary>
        /// 可选运算设备（仅 CPU）。
        /// </summary>
        public List<string> DeviceOptions { get; } = new()
        {
            "CPU"
        };

        /// <summary>
        /// 录屏文件保存目录，默认为项目下的 Recordings 文件夹。
        /// </summary>
        [ObservableProperty]
        private string _recordingSavePath = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Recordings");

        /// <summary>
        /// 增强结果文件保存目录，默认为项目下的 Enhancements 文件夹。
        /// </summary>
        [ObservableProperty]
        private string _enhancementSavePath = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Enhancements");

        /// <summary>
        /// 缩略图缓存文件保存目录。
        /// </summary>
        [ObservableProperty]
        private string _thumbnailSavePath = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Thumbnails");

        /// <summary>
        /// 缩略图路径变化时同步到缩略图服务。
        /// </summary>
        partial void OnRecordingSavePathChanged(string value) => SavePathConfig();
        partial void OnEnhancementSavePathChanged(string value) => SavePathConfig();
        partial void OnThumbnailSavePathChanged(string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) _thumbnailService.CacheDirectory = value;
            SavePathConfig();
        }

        private void SavePathConfig()
        {
            var cfg = AppConfig.Load();
            cfg.RecordingPath = _recordingSavePath;
            cfg.EnhancementPath = _enhancementSavePath;
            cfg.ThumbnailPath = _thumbnailSavePath;
            AppConfig.Save(cfg);
        }

        // ============================================================
        /// 应用版本号。
        /// </summary>
        public string AppVersion => "v1.0.0.0";

        /// <summary>
        /// 保存设置命令（待实现）。
        /// </summary>
        [RelayCommand]
        private void SaveSettings()
        {
            var msg = "✅ 当前配置：\n\n";
            msg += $"对话 API: {(ChatConfigured ? $"已连接 ({ChatModelName})" : "未配置")}\n";
            msg += $"编辑 API: {(EditConfigured ? $"已连接 ({EditModelName})" : "未配置")}\n";
            msg += $"录屏目录: {RecordingSavePath}\n";
            msg += $"增强目录: {EnhancementSavePath}\n";
            msg += $"缩略图目录: {ThumbnailSavePath}\n";
            msg += "\n所有设置已实时生效。";
            MessageBox.Show(msg, "系统设置", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 测试 API 密钥有效性（待实现）。
        /// </summary>
        [RelayCommand]
        private void TestApiKey() => ShowNotImplementedMsg("测试 API 密钥");

        /// <summary>
        /// 重置所有设置到默认值（待实现）。
        /// </summary>
        [RelayCommand]
        private void ResetAllSettings() => ShowNotImplementedMsg("重置所有设置");

        // ============================================================
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
            var (imageCount, videoCount) = await _dataService.GetTypeCountAsync();
            ImageCount = imageCount;
            VideoCount = videoCount;
            EnhancementCount = await _dataService.GetEnhancementCountAsync();
            RecordingCount = await _dataService.GetRecordingCountAsync();
            TotalPlayCount = await _dataService.GetTotalPlayCountAsync();
            FavoriteCount = await _dataService.GetFavoriteCountAsync();
            // 实时增强使用次数暂未实现对应数据表，保留为 0
            RealtimeEnhanceCount = 0;
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
            SelectedPageIndex = 4;
        }
    }
}