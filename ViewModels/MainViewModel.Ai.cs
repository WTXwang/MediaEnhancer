using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaEnhancer.Models;
using MediaEnhancer.Services;

namespace MediaEnhancer.ViewModels;

/// <summary>
/// AI 对话与 AI 编辑相关逻辑（partial class）。
/// </summary>
partial class MainViewModel
{
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

        /// <summary>AI 上下文已选文件数量。</summary>
        [ObservableProperty]
        private int _selectedAiFileCount;

        /// <summary>刷新已选文件计数（文件下拉框打开/关闭时调用）。</summary>
        public void RefreshSelectedAiFileCount()
        {
            SelectedAiFileCount = MediaFilesList.Count(f => f.IsSelected);
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
        private async Task AiEditSave()
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

                // 入库 + 缩略图
                var info = new FileInfo(dialog.FileName);
                var mediaFile = new MediaFile
                {
                    Title = Path.GetFileNameWithoutExtension(dialog.FileName),
                    FilePath = dialog.FileName, Type = "图片",
                    FileFormat = Path.GetExtension(dialog.FileName).ToLowerInvariant(),
                    FileSize = info.Length, Description = $"AI 生成：{AiEditPrompt}",
                    IsFavorite = false, DateAdded = DateTime.Now, DateModified = info.LastWriteTime
                };
                await _dataService.AddMediaFileAsync(mediaFile);
                try { await GenerateThumbnailForFileAsync(mediaFile); } catch { }
                await LoadDataAsync();
                await LoadStatisticsAsync();

                AiEditStatus = $"✅ 已保存并入库：{dialog.FileName}";
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

}
