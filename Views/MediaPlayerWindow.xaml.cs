using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MediaEnhancer.Models;

namespace MediaEnhancer.Views
{
    /// <summary>
    /// 媒体播放窗口，支持视频和音频文件的播放控制。
    /// </summary>
    public partial class MediaPlayerWindow : Window
    {
        private readonly List<MediaFile> _playlist;
        private int _currentIndex;
        private bool _isPlaying = false;
        private bool _isDraggingProgress = false;
        private bool _isFullscreen = false;
        private WindowState _previousWindowState;
        private WindowStyle _previousWindowStyle;

        /// <summary>
        /// 构造函数（单文件播放）。
        /// </summary>
        /// <param name="file">要播放的媒体文件。</param>
        public MediaPlayerWindow(MediaFile file)
            : this(file, new List<MediaFile> { file }, 0)
        {
        }

        /// <summary>
        /// 构造函数（带播放列表）。
        /// </summary>
        /// <param name="file">当前播放的文件。</param>
        /// <param name="playlist">播放列表。</param>
        /// <param name="startIndex">起始索引。</param>
        public MediaPlayerWindow(MediaFile file, List<MediaFile> playlist, int startIndex)
        {
            InitializeComponent();
            _playlist = playlist;
            _currentIndex = startIndex;

            // 音频模式显示占位封面
            if (file.Type == "音频")
            {
                MediaPlayer.Visibility = Visibility.Collapsed;
                AudioPlaceholder.Visibility = Visibility.Visible;
                AudioTitleText.Text = file.Title;
                PrevButton.Visibility = playlist.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
                NextButton.Visibility = playlist.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            }

            // 单文件时隐藏切换按钮
            if (playlist.Count <= 1)
            {
                PrevButton.Visibility = Visibility.Collapsed;
                NextButton.Visibility = Visibility.Collapsed;
            }

            Title = $"播放 - {file.Title}";
            LoadFile(file);

            // 键盘快捷键
            this.KeyDown += Window_KeyDown;

            // 启动进度更新定时器
            StartTimer();
        }

        /// <summary>
        /// 加载媒体文件到播放器。
        /// </summary>
        private void LoadFile(MediaFile file)
        {
            if (!File.Exists(file.FilePath))
            {
                MessageBox.Show($"文件不存在：{file.FilePath}", "播放失败",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MediaPlayer.Source = new Uri(file.FilePath);
            MediaPlayer.Play();
            _isPlaying = true;
            PlayPauseButton.Content = "⏸";
        }

        /// <summary>
        /// 媒体打开完成，获取总时长。
        /// </summary>
        private void MediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (MediaPlayer.NaturalDuration.HasTimeSpan)
            {
                var total = MediaPlayer.NaturalDuration.TimeSpan;
                TotalTimeText.Text = FormatTime(total);
                ProgressSlider.Maximum = total.TotalSeconds;
            }
        }

        /// <summary>
        /// 播放结束，自动切到下一首。
        /// </summary>
        private void MediaPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (_currentIndex < _playlist.Count - 1)
            {
                PlayNext();
            }
            else
            {
                _isPlaying = false;
                PlayPauseButton.Content = "▶";
                // 播放完毕，停止并回到开头
                MediaPlayer.Stop();
                MediaPlayer.Position = TimeSpan.Zero;
                // 强制刷新播放状态（Stop 后某些格式仍会显示最后一帧）
                MediaPlayer.Play();
                MediaPlayer.Pause();
                ProgressSlider.Value = 0;
                CurrentTimeText.Text = "00:00";
            }
        }

        /// <summary>
        /// 播放/暂停切换。
        /// </summary>
        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            TogglePlayPause();
        }

        /// <summary>
        /// 切换播放/暂停状态。
        /// </summary>
        private void TogglePlayPause()
        {
            if (_isPlaying)
            {
                MediaPlayer.Pause();
                PlayPauseButton.Content = "▶";
            }
            else
            {
                MediaPlayer.Play();
                PlayPauseButton.Content = "⏸";
            }
            _isPlaying = !_isPlaying;
        }

        /// <summary>
        /// 上一首。
        /// </summary>
        private void Prev_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex > 0)
                SwitchTo(_currentIndex - 1);
        }

        /// <summary>
        /// 下一首。
        /// </summary>
        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex < _playlist.Count - 1)
                SwitchTo(_currentIndex + 1);
        }

        /// <summary>
        /// 切换到指定索引的文件。
        /// </summary>
        private void PlayNext()
        {
            if (_currentIndex < _playlist.Count - 1)
                SwitchTo(_currentIndex + 1);
        }

        /// <summary>
        /// 切换到指定索引的文件。
        /// </summary>
        private void SwitchTo(int index)
        {
            MediaPlayer.Stop();
            _currentIndex = index;
            var file = _playlist[index];
            Title = $"播放 - {file.Title}";
            if (file.Type == "音频")
                AudioTitleText.Text = file.Title;
            LoadFile(file);
        }

        /// <summary>
        /// 进度条拖动开始。
        /// </summary>
        private void ProgressSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingProgress = true;
        }

        /// <summary>
        /// 进度条拖动结束。
        /// </summary>
        private void ProgressSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingProgress = false;
            if (MediaPlayer.Source != null)
                MediaPlayer.Position = TimeSpan.FromSeconds(ProgressSlider.Value);
        }

        /// <summary>
        /// 进度条值变化（播放中自动更新）。
        /// </summary>
        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isDraggingProgress)
            {
                CurrentTimeText.Text = FormatTime(TimeSpan.FromSeconds(e.NewValue));
            }
        }

        /// <summary>
        /// 音量调节。
        /// </summary>
        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            MediaPlayer.Volume = VolumeSlider.Value;
        }

        /// <summary>
        /// 全屏切换。
        /// </summary>
        private void Fullscreen_Click(object sender, RoutedEventArgs e)
        {
            ToggleFullscreen();
        }

        /// <summary>
        /// 切换全屏/窗口模式。
        /// </summary>
        private void ToggleFullscreen()
        {
            _isFullscreen = !_isFullscreen;
            if (_isFullscreen)
            {
                _previousWindowState = this.WindowState;
                _previousWindowStyle = this.WindowStyle;
                this.WindowStyle = WindowStyle.None;
                this.WindowState = WindowState.Maximized;
                this.ResizeMode = ResizeMode.NoResize;
            }
            else
            {
                this.WindowStyle = _previousWindowStyle;
                this.WindowState = _previousWindowState;
                this.ResizeMode = ResizeMode.CanResize;
            }
        }

        /// <summary>
        /// 键盘快捷键处理。
        /// </summary>
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Space:
                    TogglePlayPause();
                    e.Handled = true;
                    break;
                case Key.Left:
                    if (MediaPlayer.Position.TotalSeconds > 5)
                        MediaPlayer.Position = MediaPlayer.Position.Subtract(TimeSpan.FromSeconds(5));
                    e.Handled = true;
                    break;
                case Key.Right:
                    MediaPlayer.Position = MediaPlayer.Position.Add(TimeSpan.FromSeconds(5));
                    e.Handled = true;
                    break;
                case Key.F11:
                    ToggleFullscreen();
                    e.Handled = true;
                    break;
                case Key.Escape:
                    if (_isFullscreen) ToggleFullscreen();
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>
        /// 窗口关闭时停止播放。
        /// </summary>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MediaPlayer.Stop();
            MediaPlayer.Close();
        }

        /// <summary>
        /// 定时更新进度条（通过 DispatcherTimer 驱动）。
        /// </summary>
        private void UpdateProgress()
        {
            if (!_isDraggingProgress && MediaPlayer.Source != null && MediaPlayer.Position.TotalSeconds > 0)
            {
                ProgressSlider.Value = MediaPlayer.Position.TotalSeconds;
                CurrentTimeText.Text = FormatTime(MediaPlayer.Position);
            }
        }

        /// <summary>
        /// 格式化时间显示。
        /// </summary>
        private static string FormatTime(TimeSpan time)
        {
            return time.TotalHours >= 1
                ? time.ToString(@"hh\:mm\:ss")
                : time.ToString(@"mm\:ss");
        }

        /// <summary>
        /// 启动时开启定时器更新进度。
        /// </summary>
        private void StartTimer()
        {
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += (s, e) => UpdateProgress();
            timer.Start();
        }
    }
}
