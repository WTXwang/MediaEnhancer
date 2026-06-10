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
            FilePopup.IsOpen = true;
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

    }
}