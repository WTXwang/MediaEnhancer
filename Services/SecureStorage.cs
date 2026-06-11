using System.Security.Cryptography;
using System.Text;

namespace MediaEnhancer.Services;

/// <summary>
/// 使用 Windows DPAPI (Data Protection API) 对敏感数据进行加密/解密。
/// 加密范围：当前用户 (DataProtectionScope.CurrentUser)，
/// 只有当前 Windows 用户能解密，比明文存储安全得多。
/// </summary>
public static class SecureStorage
{
    /// <summary>加密字符串，返回 Base64 编码的密文。</summary>
    public static string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    /// <summary>解密 Base64 密文，返回原始字符串。解密失败返回空串。</summary>
    public static string Unprotect(string cipherBase64)
    {
        if (string.IsNullOrEmpty(cipherBase64)) return "";
        try
        {
            var bytes = Convert.FromBase64String(cipherBase64);
            var decrypted = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return ""; // 解密失败（可能是不同用户或数据损坏）
        }
    }
}
