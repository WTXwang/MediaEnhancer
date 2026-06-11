using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MediaEnhancer.ViewModels;

namespace MediaEnhancer.Views;

/// <summary>
/// 登录窗口——支持登录和注册双模式切换。
/// Tab 栏点击、底部链接点击均可切换模式。
/// </summary>
public partial class LoginWindow : Window
{
    private readonly LoginViewModel _vm;

    /// <summary>登录/注册是否成功（由 App.xaml.cs 读取，绕过 DialogResult）。</summary>
    public bool LoginSucceeded { get; private set; }

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = _vm;

        // ViewModel 模式变化时同步 Tab 外观
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LoginViewModel.RegisterMode) ||
                e.PropertyName == nameof(LoginViewModel.SwitchModeText))
            {
                UpdateTabs();
            }
        };

        // 登录/注册成功后关闭窗口
        _vm.CloseRequested += result =>
        {
            LoginSucceeded = true;
            Close();
        };
    }

    private void PwdBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _vm.SetPassword(PwdBox.Password);
    }

    private void PwdBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            _vm.SubmitCommand.Execute(null);
    }

    /// <summary>Tab 栏点击——根据点击位置切换模式。</summary>
    private void TabBar_Click(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition((IInputElement)sender);
        // 判断点击在左半（登录）还是右半（注册）
        bool clickRight = pos.X > ((FrameworkElement)sender).ActualWidth / 2;

        if (clickRight && !_vm.RegisterMode)
            _vm.SwitchModeCommand.Execute(null);
        else if (!clickRight && _vm.RegisterMode)
            _vm.SwitchModeCommand.Execute(null);
    }

    /// <summary>底部链接点击——切换模式。</summary>
    private void SwitchMode_Click(object sender, MouseButtonEventArgs e)
    {
        _vm.SwitchModeCommand.Execute(null);
    }

    /// <summary>关闭按钮。</summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        LoginSucceeded = false;
        Close();
    }

    /// <summary>同步 Tab 高亮、显示名字段、底部链接文本。</summary>
    private void UpdateTabs()
    {
        var isReg = _vm.RegisterMode;

        LoginTabBg.Background = isReg ? Brushes.Transparent : Brushes.White;
        LoginTabText.Foreground = isReg
            ? new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B))
            : new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));

        RegisterTabBg.Background = isReg ? Brushes.White : Brushes.Transparent;
        RegisterTabText.Foreground = isReg
            ? new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A))
            : new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));

        DisplayLabel.Visibility = isReg ? Visibility.Visible : Visibility.Collapsed;
        DisplayBox.Visibility = isReg ? Visibility.Visible : Visibility.Collapsed;

        SwitchLink.Text = isReg ? "已有账号？点击登录" : "还没有账号？点击注册";
    }
}
