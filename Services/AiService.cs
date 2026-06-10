using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using MediaEnhancer.Models;

namespace MediaEnhancer.Services;

/// <summary>
/// AI 服务：OpenAI 兼容大模型 + Whisper 语音转文字 + 模板化降级。
///
/// 用法：
///   ai.Configure(endpoint, key, model);
///   var reply = await ai.ChatAsync(messages, fileContext);
///   var text = await ai.SpeechToTextAsync(audioPath);
///
/// 降级逻辑：
///   API 未配置 或 调用失败 → 自动走模板化兜底，对话区会显示降级提示。
/// </summary>
public class AiService
{
    private readonly HttpClient _httpClient;

    // 对话配置
    private string _chatKey = "";
    private string _chatEndpoint = "https://dashscope.aliyuncs.com/compatible-mode/v1";
    private string _chatModel = "qwen-plus";

    // 编辑（生图）配置
    private string _editKey = "";
    private string _editEndpoint = "https://dashscope.aliyuncs.com/api/v1/services/aigc/image-generation/generation";
    private string _editModel = "wanx2.0-t2i-turbo";
    private string _editFormat = "auto";

    public bool IsChatConfigured => !string.IsNullOrEmpty(_chatKey);
    public bool IsEditConfigured => !string.IsNullOrEmpty(_editKey);
    public string CurrentChatModel => _chatModel;
    public string CurrentEditModel => _editModel;
    public bool LastCallFallback { get; private set; }

    public AiService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var cfg = AppConfig.Load();
        _chatEndpoint = cfg.ChatEndpoint;
        _chatKey = cfg.ChatKey;
        _chatModel = cfg.ChatModel;
        _editEndpoint = cfg.EditEndpoint;
        _editKey = cfg.EditKey;
        _editModel = cfg.EditModel;
        _editFormat = cfg.EditFormat;
    }

    public void ConfigureChat(string apiKey, string endpoint, string model)
    {
        if (!string.IsNullOrWhiteSpace(apiKey)) _chatKey = apiKey;
        if (!string.IsNullOrWhiteSpace(endpoint)) _chatEndpoint = endpoint.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(model)) _chatModel = model;

        var cfg = AppConfig.Load();
        cfg.ChatEndpoint = _chatEndpoint;
        cfg.ChatKey = _chatKey;
        cfg.ChatModel = _chatModel;
        AppConfig.Save(cfg);
    }

    public void ConfigureEdit(string apiKey, string endpoint, string model, string format)
    {
        if (!string.IsNullOrWhiteSpace(apiKey)) _editKey = apiKey;
        if (!string.IsNullOrWhiteSpace(endpoint)) _editEndpoint = endpoint.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(model)) _editModel = model;
        if (!string.IsNullOrWhiteSpace(format)) _editFormat = format;

        var cfg = AppConfig.Load();
        cfg.EditEndpoint = _editEndpoint;
        cfg.EditKey = _editKey;
        cfg.EditModel = _editModel;
        cfg.EditFormat = _editFormat;
        AppConfig.Save(cfg);
    }

    /// <summary>获取已保存的对话配置（用于 UI 回填）。</summary>
    public (string endpoint, string model, bool hasKey) GetSavedChatConfig()
    {
        var cfg = AppConfig.Load();
        return (cfg.ChatEndpoint, cfg.ChatModel, !string.IsNullOrEmpty(cfg.ChatKey));
    }

    /// <summary>获取已保存的编辑配置。</summary>
    public (string endpoint, string model, bool hasKey) GetSavedEditConfig()
    {
        var cfg = AppConfig.Load();
        return (cfg.EditEndpoint, cfg.EditModel, !string.IsNullOrEmpty(cfg.EditKey));
    }

    // ================================================================
    // 对话（多模态：文本 + 图片 base64）
    // ================================================================

    public async Task<string> ChatAsync(
        List<ChatMessage> history,
        List<MediaFile>? selectedFiles = null,
        string? systemPrompt = null)
    {
        if (!IsChatConfigured)
            return "🤖 对话 API 未配置，以下为本地生成结果：\n\n" + FallbackChat(history, selectedFiles, systemPrompt);

        try
        {
            var messages = await BuildMessagesAsync(history, selectedFiles, systemPrompt);
            var json = BuildRequestBody(messages);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_chatKey}");

            var resp = await _httpClient.PostAsync($"{_chatEndpoint}/chat/completions", content);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var reply = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";

            LastCallFallback = false;
            return reply;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AI 调用失败: {ex.Message}");
            LastCallFallback = true;
            var errInfo = $"⚠️ API 调用失败（{ex.Message}），以下为本地生成结果：\n\n";
            return errInfo + FallbackChat(history, selectedFiles, systemPrompt);
        }
    }

    // ================================================================
    // 图像生成（通义万相）
    // ================================================================

    public async Task<(byte[]? data, string? error)> GenerateImageAsync(string prompt, string? imageBase64 = null)
    {
        if (!IsEditConfigured) return (null, "编辑 API 未配置，请在系统设置中填写 API Key。");

        try
        {
            var fmt = ResolveFormat();

            if (fmt == "dashscope")
                return await GenerateDashScopeAsync(prompt, imageBase64);
            else
                return await GenerateOpenAiStyleAsync(prompt, imageBase64);
        }
        catch (Exception ex)
        {
            return (null, $"异常: {ex.Message}");
        }
    }

    private string ResolveFormat()
    {
        if (_editFormat == "dashscope" || _editFormat == "openai") return _editFormat;
        // auto: 根据 URL 推断
        if (_editEndpoint.Contains("dashscope")) return "dashscope";
        return "openai";
    }

    /// <summary>DashScope 通义万相（异步 + 轮询）。</summary>
    private async Task<(byte[]? data, string? error)> GenerateDashScopeAsync(string prompt, string? imageBase64)
    {
        var input = new Dictionary<string, object> { ["prompt"] = prompt };
        if (!string.IsNullOrEmpty(imageBase64)) input["ref_img"] = $"data:image/png;base64,{imageBase64}";
        var model = string.IsNullOrEmpty(imageBase64) ? _editModel : "wanx2.0-i2i-turbo";

        var body = new { model, input, parameters = new { size = "1024*1024", n = 1 } };
        var json = JsonSerializer.Serialize(body);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_editKey}");
        _httpClient.DefaultRequestHeaders.Add("X-DashScope-Async", "enable");

        var resp = await _httpClient.PostAsync(_editEndpoint, content);
        if (!resp.IsSuccessStatusCode)
            return (null, $"API 返回 {resp.StatusCode}: {Truncate(await resp.Content.ReadAsStringAsync(), 300)}");

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var taskId = doc.RootElement.GetProperty("output").GetProperty("task_id").GetString();
        if (taskId == null) return (null, "未获取到 task_id");

        var taskBase = new Uri(new Uri(_editEndpoint), "/api/v1/tasks").ToString();
        for (int i = 0; i < 30; i++)
        {
            await Task.Delay(2000);
            var checkResp = await _httpClient.GetAsync($"{taskBase}/{taskId}");
            if (!checkResp.IsSuccessStatusCode) continue;

            using var checkDoc = JsonDocument.Parse(await checkResp.Content.ReadAsStringAsync());
            var st = checkDoc.RootElement.GetProperty("output").GetProperty("task_status").GetString();
            if (st == "SUCCEEDED")
            {
                var url = checkDoc.RootElement.GetProperty("output").GetProperty("results")[0].GetProperty("url").GetString();
                return url != null ? (await _httpClient.GetByteArrayAsync(url), null) : ((byte[]?)null, "URL 为空");
            }
            if (st == "FAILED")
            {
                var msg = checkDoc.RootElement.GetProperty("output").TryGetProperty("message", out var m) ? m.GetString() : "";
                return (null, $"任务失败: {msg}");
            }
        }
        return (null, "任务超时");
    }

    /// <summary>SiliconFlow / OpenAI 兼容（同步返回）。</summary>
    private async Task<(byte[]? data, string? error)> GenerateOpenAiStyleAsync(string prompt, string? imageBase64 = null)
    {
        object body;
        if (!string.IsNullOrEmpty(imageBase64))
            body = new { model = _editModel, prompt, image = $"data:image/png;base64,{imageBase64}", n = 1, size = "1024x1024" };
        else
            body = new { model = _editModel, prompt, n = 1, size = "1024x1024" };
        var json = JsonSerializer.Serialize(body);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_editKey}");

        var resp = await _httpClient.PostAsync(_editEndpoint, content);
        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await resp.Content.ReadAsStringAsync();
            return (null, $"API {resp.StatusCode}: {Truncate(errBody, 300)}\n发送: {Truncate(json, 200)}");
        }

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var url = doc.RootElement.GetProperty("data")[0].GetProperty("url").GetString();
        return url != null ? (await _httpClient.GetByteArrayAsync(url), null) : ((byte[]?)null, "URL 为空");
    }

    private static string Truncate(string s, int len) =>
        s.Length <= len ? s : s[..len] + "...";

    // ================================================================
    // 语音转文字
    // ================================================================

    public async Task<string> SpeechToTextAsync(string audioPath)
    {
        if (!IsChatConfigured || !File.Exists(audioPath))
            return "⚠ 语音转文字需要 API 连接，且文件必须存在。";

        try
        {
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(await File.ReadAllBytesAsync(audioPath)), "file", Path.GetFileName(audioPath));
            form.Add(new StringContent("whisper-1"), "model");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_chatKey}");

            var resp = await _httpClient.PostAsync($"{_chatEndpoint}/audio/transcriptions", form);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var text = doc.RootElement.GetProperty("text").GetString() ?? "";
            LastCallFallback = false;
            return text;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Whisper 调用失败: {ex.Message}");
            LastCallFallback = true;
            return "⚠ 语音转文字失败（网络错误或 API 不支持），请稍后重试。";
        }
    }

    // ================================================================
    // 构建多模态消息
    // ================================================================

    private string BuildRequestBody(List<object> messages)
    {
        return JsonSerializer.Serialize(new
        {
            model = _chatModel,
            messages,
            max_tokens = 2048
        });
    }

    private async Task<List<object>> BuildMessagesAsync(
        List<ChatMessage> history,
        List<MediaFile>? files,
        string? systemPrompt)
    {
        var messages = new List<object>();

        messages.Add(new { role = "system", content = systemPrompt ?? DefaultSystemPrompt() });

        foreach (var h in history.TakeLast(10))
            messages.Add(new { role = h.Role, content = h.Content });

        if (files != null && files.Count > 0)
        {
            var contentParts = await BuildFileContextAsync(files);
            messages.Add(new { role = "user", content = contentParts });
        }

        return messages;
    }

    /// <summary>
    /// 构建文件上下文（文本元数据 + 图片 base64 / 视频关键帧）。
    /// 返回的是 content array 格式（OpenAI 多模态）。
    /// </summary>
    private async Task<List<object>> BuildFileContextAsync(List<MediaFile> files)
    {
        var parts = new List<object>();
        var metaText = new StringBuilder();
        metaText.AppendLine("以下是我选中的影音文件：\n");

        for (int i = 0; i < files.Count; i++)
        {
            var f = files[i];
            metaText.AppendLine($"[{i + 1}] 文件: {f.Title}");
            metaText.AppendLine($"  类型: {f.Type}, 格式: {f.FileFormat}");
            metaText.AppendLine($"  大小: {f.FileSize / 1024} KB, 时长: {f.Duration}, 分辨率: {f.Width}x{f.Height}");
            metaText.AppendLine();
        }

        parts.Add(new { type = "text", text = metaText.ToString() });

        foreach (var f in files)
        {
            if (f.Type == "图片" && File.Exists(f.FilePath))
            {
                var b64 = Convert.ToBase64String(await File.ReadAllBytesAsync(f.FilePath));
                parts.Add(new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{b64}" } });
            }
            else if (f.Type == "视频" && File.Exists(f.FilePath))
            {
                var frames = ExtractVideoFrames(f.FilePath, 3);
                foreach (var framePath in frames)
                {
                    var b64 = Convert.ToBase64String(await File.ReadAllBytesAsync(framePath));
                    parts.Add(new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{b64}" } });
                    try { File.Delete(framePath); } catch { }
                }
            }
        }

        return parts;
    }

    // ================================================================
    // ffmpeg 视频关键帧提取
    // ================================================================

    public static List<string> ExtractVideoFrames(string videoPath, int maxFrames = 3)
    {
        var list = new List<string>();
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "MediaEnhancerFrames", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            if (!File.Exists(ffmpegPath)) return list;

            // 获取视频时长
            var durationSec = GetVideoDuration(ffmpegPath, videoPath);
            if (durationSec <= 0) durationSec = 10;

            // 均分时间点抽帧
            var interval = durationSec / (maxFrames + 1);
            for (int i = 1; i <= maxFrames; i++)
            {
                var seekSec = interval * i;
                var outPath = Path.Combine(tempDir, $"frame_{i}.jpg");
                var args = $"-y -hide_banner -loglevel error -ss {seekSec:F1} -i \"{videoPath}\" -vframes 1 -q:v 3 \"{outPath}\"";

                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                proc?.WaitForExit(5000);

                if (File.Exists(outPath)) list.Add(outPath);
            }
        }
        catch (Exception ex) { Debug.WriteLine($"抽帧失败: {ex.Message}"); }
        return list;
    }

    private static double GetVideoDuration(string ffmpegPath, string videoPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-i \"{videoPath}\" 2>&1",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return 0;
            var stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit(3000);

            var durIdx = stderr.IndexOf("Duration: ");
            if (durIdx >= 0)
            {
                var durStr = stderr.Substring(durIdx + 10, 11).Trim();
                if (TimeSpan.TryParse(durStr, out var ts))
                    return ts.TotalSeconds;
            }
        }
        catch { }
        return 0;
    }

    // ================================================================
    // 模板化 AI 降级（离线兜底）
    // ================================================================

    private string FallbackChat(List<ChatMessage> history, List<MediaFile>? files, string? systemPrompt)
    {
        LastCallFallback = true;
        var sb = new StringBuilder();

        if (systemPrompt != null && systemPrompt.Contains("简介"))
            sb.Append(TemplateDescription(files));
        else if (systemPrompt != null && systemPrompt.Contains("增强"))
            sb.Append(TemplateEnhancement(files));
        else if (systemPrompt != null && systemPrompt.Contains("美化"))
            sb.Append(TemplateBeautify(files));
        else if (systemPrompt != null && systemPrompt.Contains("数据"))
            sb.Append(TemplateDataSummary(files));
        else
            sb.Append(TemplateGeneral(files));

        return sb.ToString();
    }

    private string TemplateDescription(List<MediaFile>? files)
    {
        if (files == null || files.Count == 0) return "请先选择要分析的文件。";
        var sb = new StringBuilder();
        foreach (var f in files)
        {
            var quality = (f.Width ?? 0) >= 1920 ? "高清" : (f.Width ?? 0) >= 1280 ? "标清" : "低分辨率";
            var durationDesc = !string.IsNullOrEmpty(f.Duration) ? $"时长 {f.Duration}" : "";
            var sizeDesc = f.FileSize > 1024 * 1024 * 100 ? "大文件" : f.FileSize > 1024 * 1024 * 10 ? "中等文件" : "小文件";

            sb.AppendLine($"📁 **{f.Title}**");
            sb.AppendLine($"   类型: {f.Type}, 格式: {f.FileFormat}");
            sb.AppendLine($"   画质: {quality}, {sizeDesc}, {durationDesc}");
            sb.AppendLine($"   简介: 这是一个{quality}{f.Type}文件{f.Type switch { "视频" => "，内容可能包含丰富的动态场景", "图片" => "，画面可能包含人物或风景", "音频" => "，可能包含语音或音乐", _ => "" }}。");
            sb.AppendLine();
        }
        sb.AppendLine("*💡 配置 API 后可获得基于实际画面内容的智能分析。*");
        return sb.ToString();
    }

    private string TemplateEnhancement(List<MediaFile>? files)
    {
        if (files == null || files.Count == 0) return "请先选择要分析的文件。";
        var sb = new StringBuilder();
        sb.AppendLine("基于文件信息的增强建议：\n");

        foreach (var f in files)
        {
            if (f.Type == "图片" || f.Type == "视频")
            {
                var brightness = (f.Width ?? 0) < 1280 ? "建议适当提亮" : "亮度正常";
                var contrast = f.FileFormat is ".jpg" or ".jpeg" ? "JPEG 压缩可能有细节损失，建议适当增强对比度" : "格式良好";
                sb.AppendLine($"📁 {f.Title}: {brightness}，{contrast}。");
            }
            else
            {
                sb.AppendLine($"📁 {f.Title}: 音频文件，建议检查音质后考虑降噪处理。");
            }
        }
        sb.AppendLine("\n*💡 配置 API 后可获得基于实际画面的精准增强建议。*");
        return sb.ToString();
    }

    private string TemplateBeautify(List<MediaFile>? files)
    {
        if (files == null || files.Count == 0) return "请先选择要分析的文件。";
        var sb = new StringBuilder();
        sb.AppendLine("基于文件信息的美化建议：\n");

        foreach (var f in files)
        {
            var ratio = f.Width.HasValue && f.Height.HasValue
                ? $"{(double)f.Width / f.Height:F2}"
                : "未知";
            sb.AppendLine($"📁 {f.Title}: 宽高比 {ratio}。");
            sb.AppendLine(f.Type switch
            {
                "图片" => "   → 建议：自动白平衡 + 轻微锐化 + 16:9 裁剪",
                "视频" => "   → 建议：色彩统一调色 + 稳定防抖 + 去噪",
                _ => "   → 建议：均衡器调节 + 音量归一化"
            });
            sb.AppendLine();
        }
        sb.AppendLine("*💡 配置 API 后可获得基于实际画面的专业美化方案。*");
        return sb.ToString();
    }

    private string TemplateDataSummary(List<MediaFile>? files)
    {
        if (files == null || files.Count == 0) return "请先选择文件或从数据统计页查看全局数据。";
        var totalSize = files.Sum(f => (long)f.FileSize);
        var imgCount = files.Count(f => f.Type == "图片");
        var vidCount = files.Count(f => f.Type == "视频");
        var audCount = files.Count(f => f.Type == "音频");

        var sb = new StringBuilder();
        sb.AppendLine("📊 选中文件统计摘要：\n");
        sb.AppendLine($"   文件总数: {files.Count}");
        sb.AppendLine($"   图片: {imgCount}, 视频: {vidCount}, 音频: {audCount}");
        sb.AppendLine($"   总大小: {totalSize / 1024 / 1024} MB");
        if (files.Count > 0) sb.AppendLine($"   最近导入: {files.Max(f => f.DateAdded):yyyy-MM-dd}");
        sb.AppendLine("\n*💡 配置 API 后可获得更详细的智能分析和个性化推荐。*");
        return sb.ToString();
    }

    private string TemplateGeneral(List<MediaFile>? files)
    {
        if (files == null || files.Count == 0)
            return "我是影音智增强管理系统的 AI 助手。\n\n我可以帮你：\n• 📝 分析影音文件并生成简介\n• ✨ 给出增强参数建议\n• 🎨 推荐美化方案\n• 🎤 语音转文字\n• 📊 生成数据摘要\n\n请从左侧选择文件，然后点击快捷提示或输入问题。\n\n*💡 配置 API 后可开启真实大模型对话。*";
        return TemplateDescription(files);
    }

    // ================================================================
    // 预设 Prompt 模板
    // ================================================================

    public static string DefaultSystemPrompt() =>
        "你是影音智增强管理系统的 AI 助手。你可以帮助用户分析影音文件、给出增强建议、美化方案、生成内容简介和标签。回答简洁、专业、有帮助。";

    public static string DescriptionPrompt() =>
        "你是一个影音内容分析助手。根据用户提供的文件信息和图片/视频帧，为每个文件生成简洁的内容简介（20-50字）和3-5个标签。按文件逐一回复，格式清晰。";

    public static string EnhancementPrompt() =>
        "你是一个图像增强专家。根据用户提供的文件元数据和画面内容，分析画质问题（亮度、对比度、噪点、模糊度），并给出具体的增强参数建议（对比度强度、亮度偏移建议值）。";

    public static string BeautifyPrompt() =>
        "你是一个影音后期美化专家。根据用户提供的文件信息和画面内容，推荐具体的美化方案（调色风格、滤镜类型、裁剪建议、音频均衡器设置等）。";

    public static string DataSummaryPrompt() =>
        "你是一个数据分析助手。根据提供的影音库统计数据，生成简明的使用报告，包括文件组成分析、存储空间评估、使用习惯总结和优化建议。";
}
