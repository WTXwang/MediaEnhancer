using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaEnhancer.Models;
using MediaEnhancer.Services;

namespace MediaEnhancer.ViewModels;

/// <summary>
/// 系统设置面板相关逻辑（partial class）。
/// </summary>
partial class MainViewModel
{
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
            var appConfig = new AppConfig(_authService.CurrentUser?.Id ?? 0);
            var cfg = appConfig.Load();
            cfg.RecordingPath = _recordingSavePath;
            cfg.EnhancementPath = _enhancementSavePath;
            cfg.ThumbnailPath = _thumbnailSavePath;
            appConfig.Save(cfg);
        }

        [RelayCommand]
        private void BrowseRecordingPath()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "选择录屏文件保存目录" };
            if (dlg.ShowDialog() == true) RecordingSavePath = dlg.FolderName;
        }

        [RelayCommand]
        private void BrowseEnhancementPath()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "选择增强结果保存目录" };
            if (dlg.ShowDialog() == true) EnhancementSavePath = dlg.FolderName;
        }

        [RelayCommand]
        private void BrowseThumbnailPath()
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "选择缩略图缓存目录" };
            if (dlg.ShowDialog() == true) ThumbnailSavePath = dlg.FolderName;
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
        /// 测试 API 密钥有效性：发送一个简短对话请求验证连接。
        /// </summary>
        [RelayCommand]
        private async Task TestApiKey()
        {
            if (!ChatConfigured)
            {
                MessageBox.Show("请先填写 API 密钥和端点。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var reply = await _aiService.ChatAsync(
                    new List<ChatMessage> { new() { Role = "user", Content = "hello" } },
                    null, "你是一个助手，请只回复'OK'。");

                if (_aiService.LastCallFallback)
                    MessageBox.Show("API 调用失败，已使用本地降级。\n请检查密钥和网络。",
                        "测试结果", MessageBoxButton.OK, MessageBoxImage.Warning);
                else
                    MessageBox.Show($"API 连接成功！模型: {ChatModelName}",
                        "测试结果", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"API 测试失败: {ex.Message}",
                    "测试结果", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 重置所有设置到默认值：清空 API 配置和路径。
        /// </summary>
        [RelayCommand]
        private void ResetAllSettings()
        {
            var result = MessageBox.Show("确定要重置所有设置到默认值吗？\n\n将清空 API 密钥和自定义路径。",
                "重置确认", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            ChatApiEndpoint = "https://dashscope.aliyuncs.com/compatible-mode/v1";
            ChatApiKey = "";
            ChatModelName = "qwen3-vl-flash";
            EditApiEndpoint = "https://dashscope.aliyuncs.com/api/v1/services/aigc/multimodal-generation/generation";
            EditApiKey = "";
            EditModelName = "wan2.7-image";
            EditFormat = "auto";
            RecordingSavePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Recordings");
            EnhancementSavePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Enhancements");
            ThumbnailSavePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Thumbnails");

            UpdateChatConfig();
            UpdateEditConfig();
            SavePathConfig();
            _aiService.ConfigureChat("", ChatApiEndpoint, ChatModelName);
            _aiService.ConfigureEdit("", EditApiEndpoint, EditModelName, EditFormat);

            MessageBox.Show("所有设置已重置为默认值。", "完成",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

}
