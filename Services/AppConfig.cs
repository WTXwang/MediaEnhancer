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
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path)) ?? new();
        }
        catch { }
        return new AppSettings();
    }

    public static void Save(AppSettings s)
    {
        try { File.WriteAllText(Path, JsonSerializer.Serialize(s)); } catch { }
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
