namespace MediaEnhancer.Models;

/// <summary>
/// AI 对话消息模型，纯内存使用，不入库。
/// </summary>
public class ChatMessage
{
    public string Role { get; set; } = "user"; // "user" | "assistant"
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
