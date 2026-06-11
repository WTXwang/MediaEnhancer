using System;

namespace MediaEnhancer.Models;

/// <summary>
/// 实时全屏增强会话记录——持久化到数据库。
/// 每次启动/停止全屏增强对应一条记录。
/// </summary>
public class RealtimeSession
{
    /// <summary>主键。</summary>
    public int Id { get; set; }

    /// <summary>归属用户 ID（外键）。</summary>
    public int UserId { get; set; }

    /// <summary>导航属性：归属用户。</summary>
    public User? User { get; set; }

    /// <summary>使用的增强方法名称。</summary>
    public string MethodName { get; set; } = "";

    /// <summary>启动时间。</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>停止时间（null 表示异常退出未正常记录）。</summary>
    public DateTime? StoppedAt { get; set; }

    /// <summary>持续秒数（StoppedAt - StartedAt，便于排序统计）。</summary>
    public double DurationSeconds { get; set; }
}
