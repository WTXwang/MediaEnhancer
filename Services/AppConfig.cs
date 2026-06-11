using System.IO;
using System.Text.Json;

namespace MediaEnhancer.Services;

/// <summary>
/// 应用配置持久化——按用户隔离，每个用户拥有独立的 JSON 配置文件。
/// 文件命名：appsettings_{userId}.json（userId=0 为全局默认模板）。
/// </summary>
public class AppConfig
{
    private readonly int _userId;
    private string FilePath => System.IO.Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        _userId > 0 ? $"appsettings_{_userId}.json" : "appsettings.json");

    public AppConfig(int userId = 0)
    {
        _userId = userId;
    }

    public AppSettings Load()
    {
        try
        {
            var path = FilePath;
            // 如果用户配置文件不存在，从默认模板复制
            if (!File.Exists(path) && _userId > 0)
            {
                var defaultPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(defaultPath))
                {
                    File.Copy(defaultPath, path);
                    var copied = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new();
                    copied.ChatKey = SecureStorage.Unprotect(copied.ChatKey);
                    copied.EditKey = SecureStorage.Unprotect(copied.EditKey);
                    return copied;
                }
            }

            if (File.Exists(path))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new();
                s.ChatKey = SecureStorage.Unprotect(s.ChatKey);
                s.EditKey = SecureStorage.Unprotect(s.EditKey);
                return s;
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save(AppSettings s)
    {
        try
        {
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
            File.WriteAllText(FilePath, JsonSerializer.Serialize(toSave));
        }
        catch { }
    }
}

public class AppSettings
{
    public string ChatEndpoint { get; set; } = "https://dashscope.aliyuncs.com/compatible-mode/v1";
    public string ChatKey { get; set; } = "";
    public string ChatModel { get; set; } = "qwen-plus";

    public string EditEndpoint { get; set; } = "https://dashscope.aliyuncs.com/api/v1/services/aigc/image-generation/generation";
    public string EditKey { get; set; } = "";
    public string EditModel { get; set; } = "wanx2.0-t2i-turbo";
    public string EditFormat { get; set; } = "auto";

    public string RecordingPath { get; set; } = "";
    public string EnhancementPath { get; set; } = "";
    public string ThumbnailPath { get; set; } = "";
}
