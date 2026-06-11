using System.Security.Cryptography;
using System.Text;
using MediaEnhancer.Data;
using MediaEnhancer.Models;
using Microsoft.EntityFrameworkCore;

namespace MediaEnhancer.Services;

/// <summary>
/// 用户认证服务——负责注册、登录、密码哈希和当前会话管理。
/// 密码以 SHA256(salt + password) 形式存储，不可逆。
/// </summary>
public interface IAuthService
{
    /// <summary>当前登录用户（未登录时为 null）。</summary>
    User? CurrentUser { get; }

    /// <summary>注册新用户。</summary>
    /// <returns>true 成功；false 用户名已存在。</returns>
    Task<(bool success, string error)> RegisterAsync(string username, string password, string displayName);

    /// <summary>登录验证。</summary>
    /// <returns>成功返回 User，失败返回 null。</returns>
    Task<(User? user, string error)> LoginAsync(string username, string password);

    /// <summary>退出登录，清除会话。</summary>
    void Logout();
}

public class AuthService : IAuthService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public User? CurrentUser { get; private set; }

    public AuthService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<(bool success, string error)> RegisterAsync(
        string username, string password, string displayName)
    {
        username = username.Trim();
        if (string.IsNullOrWhiteSpace(username) || username.Length < 2)
            return (false, "用户名至少需要 2 个字符。");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
            return (false, "密码至少需要 4 个字符。");

        await using var ctx = await _contextFactory.CreateDbContextAsync();

        if (await ctx.Users.AnyAsync(u => u.Username == username))
            return (false, "该用户名已被注册。");

        var salt = GenerateSalt();
        var user = new User
        {
            Username = username,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName.Trim(),
            PasswordHash = HashPassword(password, salt),
            Salt = salt,
            CreatedAt = DateTime.Now
        };

        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        // 注册成功自动登录
        CurrentUser = user;
        return (true, "");
    }

    public async Task<(User? user, string error)> LoginAsync(string username, string password)
    {
        username = username.Trim();
        if (string.IsNullOrWhiteSpace(username))
            return (null, "请输入用户名。");
        if (string.IsNullOrWhiteSpace(password))
            return (null, "请输入密码。");

        await using var ctx = await _contextFactory.CreateDbContextAsync();

        var user = await ctx.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
            return (null, "用户名或密码错误。");

        var hash = HashPassword(password, user.Salt);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(hash),
                Encoding.UTF8.GetBytes(user.PasswordHash)))
            return (null, "用户名或密码错误。");

        // 更新末次登录时间
        user.LastLoginAt = DateTime.Now;
        await ctx.SaveChangesAsync();

        CurrentUser = user;
        return (user, "");
    }

    public void Logout()
    {
        CurrentUser = null;
    }

    // ─── 密码哈希 ───

    private static string GenerateSalt()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    private static string HashPassword(string password, string salt)
    {
        var combined = salt + password;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return Convert.ToBase64String(hash);
    }
}
