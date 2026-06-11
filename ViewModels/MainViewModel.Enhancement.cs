using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaEnhancer.Core;
using MediaEnhancer.Models;
using MediaEnhancer.Services;

namespace MediaEnhancer.ViewModels;

/// <summary>
/// 实时增强与屏幕录制相关逻辑（partial class）。
/// </summary>
partial class MainViewModel
{
        // ============================================================
        // 实时增强面板 — 方法选择（仅限实时方法）
        // ============================================================

        /// <summary>
        /// 实时增强选中方法（全屏覆盖用，仅限 SupportsRealTime=true 的方法）。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsLinearStretchSelected))]
        [NotifyPropertyChangedFor(nameof(IsRealTimeMethodSelected))]
        private string _selectedMethod = "线性拉伸";

        /// <summary>
        /// 是否开启实时增强。
        /// </summary>
        [ObservableProperty]
        private bool _isEnhancementEnabled = false;

        /// <summary>
        /// 实时增强可用方法列表（仅 SupportsRealTime=true）。
        /// </summary>
        public List<string> EnhancementMethods => _registry.RealTimeMethodNames.ToList();

        /// <summary>
        /// 当前是否选择了带可调参数的方法（控制参数滑块区的显隐）。
        /// </summary>
        public bool IsLinearStretchSelected => _selectedMethod == "线性拉伸";

        /// <summary>
        /// 当前实时方法是否可用（始终为 true，因为下拉只列实时方法）。
        /// </summary>
        public bool IsRealTimeMethodSelected => _registry.Current?.SupportsRealTime == true;

        partial void OnSelectedMethodChanged(string value)
        {
            _registry.SetCurrent(value);
            RefreshPreview();
        }

        /// <summary>
        /// 根据当前选中的实时方法刷新预览图。
        /// </summary>
        private void RefreshPreview()
        {
            if (OriginalPreview == null) return;
            if (_registry.Current is IOnnxEnhancement onnx)
                _ = onnx.EnhanceAsync(OriginalPreview).ContinueWith(t =>
                {
                    if (t.IsCompletedSuccessfully)
                        EnhancedPreview = t.Result;
                }, TaskScheduler.FromCurrentSynchronizationContext());
            else if (_registry.Current != null)
                EnhancedPreview = LinearStretch.Enhance(OriginalPreview);
        }

        // ============================================================
        // 离线增强方法选择（文件右键增强 / 批量增强 / 视频增强用）
        // ============================================================

        /// <summary>
        /// 离线增强选中的方法名称（下拉框绑定，含全部方法）。
        /// </summary>
        [ObservableProperty]
        private string _selectedOfflineMethodName = "线性拉伸";

        /// <summary>
        /// 离线增强可选方法列表（实时 + ONNX，全部可选）。
        /// </summary>
        public List<string> OfflineEnhancementMethods => _registry.MethodNames.ToList();

        /// <summary>
        /// 根据 SelectedOfflineMethodName 获取对应的增强方法实例。
        /// 优先找离线注册表，其次实时注册表，最后回退到线性拉伸。
        /// </summary>
        public IEnhancementMethod? GetSelectedOfflineMethod()
        {
            var name = SelectedOfflineMethodName;
            return _registry.GetOfflineMethod(name)
                ?? (IEnhancementMethod?)_registry.GetMethod(name);
        }

        /// <summary>
        /// 线性拉伸算法实例（C# 原生实现）。
        /// </summary>
        public LinearStretchMethod LinearStretch { get; } = new();

        /// <summary>
        /// 将当前增强参数序列化为 JSON，用于存入增强日志。
        /// </summary>
        private string BuildEnhancementParamsJson()
        {
            var dict = new Dictionary<string, double>();
            foreach (var p in LinearStretch.Parameters)
                dict[p.Key] = p.Value;
            return System.Text.Json.JsonSerializer.Serialize(dict);
        }

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
            if (method == null || !method.SupportsRealTime)
            {
                System.Windows.MessageBox.Show(
                    method == null
                        ? "没有可用的增强方法。"
                        : $"「{method.Name}」不支持实时增强，请切换到支持实时的方法。",
                    "无法启动全屏增强", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    var method = _registry.Current
                        ?? (IRealTimeEnhancer)LinearStretch; // fallback
                    var eParams = new Dictionary<string, double>();
                    foreach (var p in method.Parameters)
                        eParams[p.Key] = p.Value;
                    var enhancer = new VideoEnhancer(method, eParams);
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
                        SourceFileId = file.Id,
                        IsFavorite = false,
                        DateAdded = DateTime.Now,
                        DateModified = DateTime.Now
                    };
                    await _dataService.AddMediaFileAsync(newFile);
                    try { await _dataService.AddEnhancementLogAsync(new EnhancementLog { MediaFileId = file.Id, MethodName = method.Name, OutputPath = outputPath, ParametersJson = BuildEnhancementParamsJson(), CreatedAt = DateTime.Now }); } catch { }
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

            // 图片增强 — 根据选中的增强方法调用对应算法
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(file.FilePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                // 使用离线增强方法选择器中的方法
                var offlineMethod = GetSelectedOfflineMethod();
                var methodName = offlineMethod?.Name ?? "线性拉伸";
                BitmapSource enhanced;
                if (offlineMethod is IOnnxEnhancement onnx)
                    enhanced = await onnx.EnhanceAsync(bitmap);
                else
                    enhanced = LinearStretch.Enhance(bitmap);

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
                        SourceFileId = file.Id,
                    IsFavorite = false,
                    DateAdded = DateTime.Now,
                    DateModified = DateTime.Now
                };
                await _dataService.AddMediaFileAsync(newFile);
                try { await GenerateThumbnailForFileAsync(newFile); } catch { }
                try { await _dataService.AddEnhancementLogAsync(new EnhancementLog { MediaFileId = file.Id, MethodName = methodName, OutputPath = filePath, ParametersJson = BuildEnhancementParamsJson(), CreatedAt = DateTime.Now }); } catch { }
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

            // 使用 WPF 原生 DPI 系统计算物理像素尺寸
            // SystemParameters 返回 WPF 逻辑像素，乘以 DPI 缩放 = 物理像素
            var mainWindow = System.Windows.Application.Current.MainWindow;
            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(mainWindow);
            var screenW = (int)(System.Windows.SystemParameters.PrimaryScreenWidth * dpi.DpiScaleX);
            var screenH = (int)(System.Windows.SystemParameters.PrimaryScreenHeight * dpi.DpiScaleY);

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

}
