using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaEnhancer.Services;

namespace MediaEnhancer.ViewModels;

/// <summary>
/// 登录窗口视图模型——支持登录/注册双模式切换。
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>用户名。</summary>
    [ObservableProperty]
    private string _username = "";

    /// <summary>显示名称（仅注册时使用）。</summary>
    [ObservableProperty]
    private string _displayName = "";

    /// <summary>错误提示消息。</summary>
    [ObservableProperty]
    private string _errorMessage = "";

    /// <summary>是否正在处理中（防重复点击）。</summary>
    [ObservableProperty]
    private bool _isProcessing = false;

    /// <summary>是否处于注册模式（false=登录）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModeButtonText))]
    [NotifyPropertyChangedFor(nameof(SwitchModeText))]
    [NotifyPropertyChangedFor(nameof(IsRegisterMode))]
    private bool _registerMode = false;

    public bool IsRegisterMode => RegisterMode;
    public string ModeButtonText => RegisterMode ? "注 册" : "登 录";
    public string SwitchModeText => RegisterMode ? "已有账号？点击登录" : "还没有账号？点击注册";

    /// <summary>登录/注册。</summary>
    [RelayCommand]
    private async Task SubmitAsync()
    {
        // PasswordBox 绑定需要从 LoginWindow.xaml.cs 设置密码
        // 此处通过属性获取（由 LoginWindow 的 PasswordChanged 事件同步）
        var pwd = _password;
        if (string.IsNullOrEmpty(pwd))
        {
            ErrorMessage = "请输入密码。";
            return;
        }

        IsProcessing = true;
        ErrorMessage = "";

        try
        {
            if (RegisterMode)
            {
                var (success, error) = await _authService.RegisterAsync(Username, pwd, DisplayName);
                if (success)
                    RequestClose(true);
                else
                    ErrorMessage = error;
            }
            else
            {
                var (user, error) = await _authService.LoginAsync(Username, pwd);
                if (user != null)
                    RequestClose(true);
                else
                    ErrorMessage = error;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"操作失败：{ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>切换登录/注册模式。</summary>
    [RelayCommand]
    private void SwitchMode()
    {
        RegisterMode = !RegisterMode;
        ErrorMessage = "";
    }

    // ─── 窗口关闭信号 ───

    /// <summary>登录/注册成功后触发，通知窗口关闭。</summary>
    public event Action<bool>? CloseRequested;

    private void RequestClose(bool dialogResult)
    {
        CloseRequested?.Invoke(dialogResult);
    }

    // ─── 密码同步（PasswordBox 不支持绑定，由 LoginWindow.cs 同步） ───

    private string _password = "";

    /// <summary>由 LoginWindow 在 PasswordChanged 事件中调用。</summary>
    public void SetPassword(string password)
    {
        _password = password;
        if (!string.IsNullOrEmpty(ErrorMessage) && ErrorMessage.Contains("密码"))
            ErrorMessage = "";
    }
}
