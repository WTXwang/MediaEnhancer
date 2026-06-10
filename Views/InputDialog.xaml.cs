using System.Windows;
using System.Windows.Input;

namespace MediaEnhancer.Views
{
    /// <summary>
    /// 简单的文本输入对话框，用于文件重命名等需要用户输入的场景。
    /// </summary>
    public partial class InputDialog : Window
    {
        /// <summary>
        /// 用户输入的文本内容。
        /// </summary>
        public string InputText { get; private set; } = "";

        /// <summary>
        /// 构造函数。
        /// </summary>
        /// <param name="title">对话框标题。</param>
        /// <param name="prompt">提示文字。</param>
        /// <param name="defaultValue">输入框默认值。</param>
        public InputDialog(string title, string prompt, string defaultValue = "")
        {
            InitializeComponent();
            Title = title;
            PromptText.Text = prompt;
            InputBox.Text = defaultValue;
            InputBox.SelectAll();
            InputBox.Focus();
        }

        /// <summary>
        /// 确认按钮点击。
        /// </summary>
        private void OK_Click(object sender, RoutedEventArgs e)
        {
            InputText = InputBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(InputText))
            {
                MessageBox.Show("输入内容不能为空。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                InputBox.Focus();
                return;
            }
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// 取消按钮点击。
        /// </summary>
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 回车键触发确认。
        /// </summary>
        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OK_Click(sender, e);
            }
        }
    }
}
