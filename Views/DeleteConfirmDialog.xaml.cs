using System.Windows;
using MediaEnhancer.Models;

namespace MediaEnhancer.Views
{
    /// <summary>
    /// 删除确认对话框，提供三种操作选项。
    /// </summary>
    public partial class DeleteConfirmDialog : Window
    {
        /// <summary>
        /// 用户选择的操作。
        /// 0 = 取消, 1 = 仅删除记录, 2 = 删除源文件+记录。
        /// </summary>
        public int SelectedAction { get; private set; } = 0;

        /// <summary>
        /// 构造函数。
        /// </summary>
        /// <param name="file">要删除的媒体文件。</param>
        public DeleteConfirmDialog(MediaFile file)
        {
            InitializeComponent();
            NameText.Text = file.Title;
            var path = file.FilePath;
            if (path.Length > 80)
                path = path[..35] + "..." + path[^42..];
            FileText.Text = path;
        }

        /// <summary>
        /// 删除源文件（文件+数据库记录）。
        /// </summary>
        private void DeleteSource_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "此操作将永久删除源文件和数据库记录，不可恢复。确定要继续吗？",
                "确认删除源文件",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                SelectedAction = 2;
                DialogResult = true;
                Close();
            }
        }

        /// <summary>
        /// 仅删除数据库记录，保留源文件。
        /// </summary>
        private void DeleteRecord_Click(object sender, RoutedEventArgs e)
        {
            SelectedAction = 1;
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 取消操作。
        /// </summary>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            SelectedAction = 0;
            DialogResult = false;
            Close();
        }
    }
}
