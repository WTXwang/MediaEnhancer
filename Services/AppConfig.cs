using System.IO;
using System.Text.Json;

namespace MediaEnhancer.Services;

/// <summary>
/// 应用配置持久化（单文件 JSON，启动加载，修改即存）。
/// </summary>
public static class AppConfig
{
    private static string Path => System.IO.Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path)) ?? new();
                // 解密 API 密钥供内存使用（磁盘上始终是密文）
                s.ChatKey = SecureStorage.Unprotect(s.ChatKey);
                s.EditKey = SecureStorage.Unprotect(s.EditKey);
                return s;
            }
        }
        catch { }
        return new AppSettings();
    }

    public static void Save(AppSettings s)
    {
        try
        {
            // 加密 API 密钥后再序列化到磁盘
            var toSave = new AppSettings
            {
                ChatEndpoint = s.ChatEndpoint,
                ChatKey = SecureStorage.Protect(s.ChatKey),
                ChatModel = s.ChatModel,
                EditEndpoint = s.EditEndpoint,
                EditKey = SecureStorage.Protect(s.EditKey),
                EditModel = s.EditModel,
                EditFormat = s.EditFormat,
                RecordingPath = s.RecordingPath,
                EnhancementPath = s.EnhancementPath,
                ThumbnailPath = s.ThumbnailPath
            };
            File.WriteAllText(Path, JsonSerializer.Serialize(toSave));
        }
        catch { }
    }
}

public class AppSettings
{
    // AI 对话
    public string ChatEndpoint { get; set; } = "https://dashscope.aliyuncs.com/compatible-mode/v1";
    public string ChatKey { get; set; } = "";
    public string ChatModel { get; set; } = "qwen-plus";

    // AI 编辑（生图）
    public string EditEndpoint { get; set; } = "https://dashscope.aliyuncs.com/api/v1/services/aigc/image-generation/generation";
    public string EditKey { get; set; } = "";
    public string EditModel { get; set; } = "wanx2.0-t2i-turbo";
    public string EditFormat { get; set; } = "auto"; // "openai" | "dashscope" | "auto"

    // 路径
    public string RecordingPath { get; set; } = "";
    public string EnhancementPath { get; set; } = "";
    public string ThumbnailPath { get; set; } = "";
}
