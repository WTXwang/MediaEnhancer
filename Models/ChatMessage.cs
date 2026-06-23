namespace MediaEnhancer.Models;

/// <summary>
/// AI 对话消息模型，纯内存使用，不入库。
/// 描述消息的 角色、内容、附件、时间戳
/// </summary>
public class ChatMessage
{
    // 角色字段
    public string Role { get; set; } = "user"; // "user" | "assistant" | "thinking"
    // 内容字段
    public string Content { get; set; } = "";

    /// <summary>
    /// 用户消息附带的影音文件路径（图片直接显示缩略图，视频显示首帧），允许为空。
    /// </summary>
    public List<string>? AttachmentPaths { get; set; }

    // 时间戳
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
