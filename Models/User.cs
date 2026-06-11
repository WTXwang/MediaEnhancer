using System;
using System.ComponentModel.DataAnnotations;

namespace MediaEnhancer.Models;

/// <summary>
/// 用户实体——对应 SQLite 中 Users 表。
/// 密码以 SHA256(salt + password) 哈希存储，不保存明文。
/// </summary>
public class User
{
    /// <summary>用户唯一标识（主键，自增）。</summary>
    public int Id { get; set; }

    /// <summary>登录用户名（唯一）。</summary>
    public string Username { get; set; } = "";

    /// <summary>密码哈希（SHA256）。</summary>
    public string PasswordHash { get; set; } = "";

    /// <summary>随机盐值（Base64）。</summary>
    public string Salt { get; set; } = "";

    /// <summary>界面显示名称。</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>账号创建时间。</summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>末次登录时间。</summary>
    public DateTime? LastLoginAt { get; set; }
}
