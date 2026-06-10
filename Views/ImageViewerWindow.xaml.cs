using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MediaEnhancer.Models;

namespace MediaEnhancer.Views
{
    /// <summary>
    /// 图片查看窗口，支持缩放、拖动、上一张/下一张。
    /// 滚轮缩放，Ctrl+滚轮缩放，拖拽平移。
    /// </summary>
    public partial class ImageViewerWindow : Window
    {
        private readonly List<MediaFile> _imageList;
        private int _currentIndex;
        private double _currentZoom = 1.0;
        private const double ZoomMin = 0.1;
        private const double ZoomMax = 10.0;
        private const double ZoomStep = 0.1;
        private Point _dragStart;
        private bool _isDragging = false;

        /// <summary>
        /// 构造函数（单张图片）。
        /// </summary>
        public ImageViewerWindow(MediaFile file)
            : this(file, new List<MediaFile> { file }, 0)
        {
        }

        /// <summary>
        /// 构造函数（带图片列表，支持切换）。
        /// </summary>
        public ImageViewerWindow(MediaFile file, List<MediaFile> imageList, int startIndex)
        {
            InitializeComponent();
            _imageList = imageList;
            _currentIndex = startIndex;

            if (imageList.Count <= 1)
            {
                PrevImageButton.Visibility = Visibility.Collapsed;
                NextImageButton.Visibility = Visibility.Collapsed;
                ImageCounter.Visibility = Visibility.Collapsed;
            }

            Title = $"查看 - {file.Title}";
            LoadImage(file);
            UpdateCounter();

            // 鼠标拖拽平移
            ImageContainer.MouseDown += ImageContainer_MouseDown;
            ImageContainer.MouseMove += ImageContainer_MouseMove;
            ImageContainer.MouseUp += ImageContainer_MouseUp;
            ImageContainer.MouseLeave += ImageContainer_MouseLeave;
        }

        /// <summary>
        /// 加载图片到显示控件。
        /// </summary>
        private void LoadImage(MediaFile file)
        {
            if (!File.Exists(file.FilePath))
            {
                MessageBox.Show($"文件不存在：{file.FilePath}", "查看失败",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(file.FilePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.EndInit();
            bitmap.Freeze();

            DisplayImage.Source = bitmap;
            FitToWindow();
            Title = $"查看 - {file.Title} ({file.Width}×{file.Height})";
        }

        /// <summary>
        /// 适应窗口显示（重置缩放）。
        /// </summary>
        private void FitToWindow()
        {
            _currentZoom = 1.0;
            DisplayImage.Stretch = Stretch.Uniform;
            ImageScale.ScaleX = 1.0;
            ImageScale.ScaleY = 1.0;
            UpdateZoomText();
        }

        /// <summary>
        /// 1:1 实际大小显示。
        /// </summary>
        private void SetActualSize()
        {
            _currentZoom = 1.0;
            DisplayImage.Stretch = Stretch.None;
            ImageScale.ScaleX = 1.0;
            ImageScale.ScaleY = 1.0;
            UpdateZoomText();
        }

        /// <summary>
        /// 应用缩放级别。
        /// </summary>
        private void ApplyZoom(double newZoom)
        {
            _currentZoom = Math.Max(ZoomMin, Math.Min(ZoomMax, newZoom));
            DisplayImage.Stretch = Stretch.None;
            ImageScale.ScaleX = _currentZoom;
            ImageScale.ScaleY = _currentZoom;
            UpdateZoomText();
        }

        /// <summary>
        /// 更新缩放百分比显示。
        /// </summary>
        private void UpdateZoomText()
        {
            ZoomText.Text = $"{_currentZoom * 100:F0}%";
        }

        // ============================================================
        // 鼠标拖拽平移
        // ============================================================

        /// <summary>
        /// 鼠标按下，开始拖拽。
        /// </summary>
        private void ImageContainer_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _isDragging = true;
                _dragStart = e.GetPosition(ImageScrollViewer);
                ImageContainer.Cursor = Cursors.Hand;
                ImageContainer.CaptureMouse();
                e.Handled = true;
            }
        }

        private void ImageContainer_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPos = e.GetPosition(ImageScrollViewer);
                var offset = currentPos - _dragStart;

                ImageScrollViewer.ScrollToHorizontalOffset(
                    ImageScrollViewer.HorizontalOffset - offset.X);
                ImageScrollViewer.ScrollToVerticalOffset(
                    ImageScrollViewer.VerticalOffset - offset.Y);

                _dragStart = currentPos;
                e.Handled = true;
            }
        }

        private void ImageContainer_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && _isDragging)
            {
                _isDragging = false;
                ImageContainer.Cursor = Cursors.Arrow;
                ImageContainer.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void ImageContainer_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ImageContainer.Cursor = Cursors.Arrow;
                ImageContainer.ReleaseMouseCapture();
            }
        }

        // ============================================================
        // 事件处理
        // ============================================================

        private void FitToWindow_Click(object sender, RoutedEventArgs e) => FitToWindow();

        private void ActualSize_Click(object sender, RoutedEventArgs e) => SetActualSize();

        private void ZoomIn_Click(object sender, RoutedEventArgs e) => ApplyZoom(_currentZoom + ZoomStep);

        private void ZoomOut_Click(object sender, RoutedEventArgs e) => ApplyZoom(_currentZoom - ZoomStep);

        /// <summary>
        /// 滚轮缩放：Ctrl+滚轮缩放，普通滚轮滚动。
        /// </summary>
        private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                var delta = e.Delta > 0 ? ZoomStep : -ZoomStep;
                ApplyZoom(_currentZoom + delta);
                e.Handled = true;
            }
            // 非 Ctrl 时正常滚动
        }

        private void PrevImage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex > 0)
            {
                _currentIndex--;
                LoadImage(_imageList[_currentIndex]);
                UpdateCounter();
            }
        }

        private void NextImage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex < _imageList.Count - 1)
            {
                _currentIndex++;
                LoadImage(_imageList[_currentIndex]);
                UpdateCounter();
            }
        }

        private void UpdateCounter()
        {
            ImageCounter.Text = $"{_currentIndex + 1} / {_imageList.Count}";
        }
    }
}
