namespace MediaEnhancer.Models;

/// <summary>
/// AI 对话消息模型，纯内存使用，不入库。
/// </summary>
public class ChatMessage
{
    public string Role { get; set; } = "user"; // "user" | "assistant" | "thinking"
    public string Content { get; set; } = "";

    /// <summary>
    /// 用户消息附带的影音文件路径（图片直接显示缩略图，视频显示首帧）。
    /// </summary>
    public List<string>? AttachmentPaths { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.Now;
}
