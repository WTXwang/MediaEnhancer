using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MediaEnhancer.Models;
using Microsoft.Extensions.DependencyInjection;
using MediaEnhancer.Services;
using MediaEnhancer.ViewModels;
using MediaEnhancer.Views;

namespace MediaEnhancer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();

            // 设置视图模型，这是 MVVM 绑定的关键步骤
            this.DataContext = viewModel;
        }

        /// <summary>
        /// 窗口加载后根据屏幕尺寸自适应调整：
        /// - 若窗口大于屏幕工作区则缩小至 85%
        /// - 若窗口小于屏幕的 50% 则放大至 65%（适配 4K 屏）
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var workArea = SystemParameters.WorkArea;

            // 窗口超过工作区时缩小
            if (Width > workArea.Width || Height > workArea.Height)
            {
                Width = Math.Min(Width, workArea.Width * 0.85);
                Height = Math.Min(Height, workArea.Height * 0.85);
            }
            // 在高分辨率屏幕上适当放大
            else if (Width < workArea.Width * 0.5 && Height < workArea.Height * 0.5)
            {
                Width = Math.Min(workArea.Width * 0.65, 1600);
                Height = Math.Min(workArea.Height * 0.65, 900);
            }

            // 回填已保存的 API 密钥到 PasswordBox（非关键操作，异常不影响主流程）
            try
            {
                var authService = App.ServiceProvider.GetRequiredService<IAuthService>();
                var cfg = new AppConfig(authService.CurrentUser?.Id ?? 0);
                var settings = cfg.Load();
                if (ChatApiKeyBox != null) ChatApiKeyBox.Password = settings.ChatKey;
                if (EditApiKeyBox != null) EditApiKeyBox.Password = settings.EditKey;
            }
            catch { }
        }

        /// <summary>
        /// DataGrid 单元格编辑结束事件，用于收藏复选框变更后同步到数据库。
        /// </summary>
        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Column is DataGridCheckBoxColumn && e.EditAction == DataGridEditAction.Commit)
            {
                if (e.Row.Item is MediaFile file && DataContext is MainViewModel vm)
                {
                    vm.ToggleFavoriteCommand.Execute(file);
                }
            }
        }

        /// <summary>
        /// DataGrid 行双击事件，打开浮层详情面板。
        /// </summary>
        private void FileDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGrid dg && dg.SelectedItem is MediaFile && DataContext is MainViewModel vm)
            {
                vm.SelectedFile = dg.SelectedItem as MediaFile;
                vm.ShowDetailPanel = true;
            }
        }

        /// <summary>
        /// DataGrid 选择变化时同步 SelectedFiles 到 ViewModel。
        /// </summary>
        private void FileDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid dg && DataContext is MainViewModel vm)
            {
                vm.SelectedFiles.Clear();
                foreach (MediaFile item in dg.SelectedItems)
                {
                    vm.SelectedFiles.Add(item);
                }
                vm.NotifyHasMultipleSelected();
            }
        }

        /// <summary>
        /// AI 文件下拉选择器。
        /// </summary>
        private void FileDropdown_Click(object sender, RoutedEventArgs e)
        {
            if (FilePopup.IsOpen) { FilePopup.IsOpen = false; return; }
            if (DataContext is MainViewModel vm)
                vm.RefreshSelectedAiFileCount();
            FilePopup.IsOpen = true;
        }

        /// <summary>文件下拉框中勾选/取消勾选时实时刷新计数。</summary>
        private void FileCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.RefreshSelectedAiFileCount();
        }

        /// <summary>
        /// 复制 AI 消息内容到剪贴板。
        /// </summary>
        private void CopyMessage_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Hyperlink link && link.Tag is string text)
                Clipboard.SetText(text);
        }

        /// <summary>
        /// AI 输入框按回车发送消息。
        /// </summary>
        private void AiInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter && DataContext is MainViewModel vm)
            {
                vm.SendAiMessageCommand.Execute(null);
                e.Handled = true;
            }
        }

        private void ChatApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
                vm.ChatApiKey = ChatApiKeyBox.Password;
        }

        private void EditApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel vm)
                vm.EditApiKey = EditApiKeyBox.Password;
        }

        private void ToggleChatApiKeyVisibility_Click(object sender, RoutedEventArgs e)
        {
            bool showing = ChatApiKeyTextBox.Visibility == Visibility.Visible;
            if (showing)
            {
                ChatApiKeyBox.Password = ChatApiKeyTextBox.Text;
                ChatApiKeyBox.Visibility = Visibility.Visible;
                ChatApiKeyTextBox.Visibility = Visibility.Collapsed;
            }
            else
            {
                ChatApiKeyTextBox.Text = ChatApiKeyBox.Password;
                ChatApiKeyBox.Visibility = Visibility.Collapsed;
                ChatApiKeyTextBox.Visibility = Visibility.Visible;
            }
        }

        private void ToggleEditApiKeyVisibility_Click(object sender, RoutedEventArgs e)
        {
            bool showing = EditApiKeyTextBox.Visibility == Visibility.Visible;
            if (showing)
            {
                EditApiKeyBox.Password = EditApiKeyTextBox.Text;
                EditApiKeyBox.Visibility = Visibility.Visible;
                EditApiKeyTextBox.Visibility = Visibility.Collapsed;
            }
            else
            {
                EditApiKeyTextBox.Text = EditApiKeyBox.Password;
                EditApiKeyBox.Visibility = Visibility.Collapsed;
                EditApiKeyTextBox.Visibility = Visibility.Visible;
            }
        }

    }
}