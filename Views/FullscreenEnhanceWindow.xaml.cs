using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MediaEnhancer.Core;

namespace MediaEnhancer.Views;

/// <summary>
/// 全屏增强覆盖窗口——捕获屏幕内容 → 实时增强 → 透明覆盖显示。
///
/// 关键特性：
///   - 分层窗口 + WS_EX_TRANSPARENT 实现鼠标穿透
///   - SetWindowDisplayAffinity 消除画中画递归
///   - DXGI Desktop Duplication 优先，GDI 自动回退
///   - F11 全局热键安全退出
///   - 只依赖 IRealTimeEnhancer 接口，不绑定具体算法
///
/// DPI 处理说明：
///   WPF 窗口的 Width/Height 是设备无关像素（1/96 英寸），必须用
///   SystemParameters 设置才能正确填充屏幕。捕获端（DXGI/GDI）返回
///   物理像素，BitmapSource 通过设置正确的显示 DPI 实现 1:1 像素映射。
/// </summary>
public partial class FullscreenEnhanceWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 9001;
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint dwAffinity);

    private IRealTimeEnhancer? _method;
    private IReadOnlyDictionary<string, double>? _params;

    // ---- 捕获相关（物理像素，Start() 中根据 DPI 修正） ----
    private int _captureW, _captureH;
    private int _captureStride;
    private int _screenLeft, _screenTop;

    // ---- 显示相关 ----
    private int _windowW, _windowH;             // WPF 窗口逻辑尺寸
    private double _dpiScaleX, _dpiScaleY;      // 显示 DPI 缩放因子
    private double _dpiX, _dpiY;                // 实际显示 DPI

    private CancellationTokenSource? _cts;
    private HwndSource? _hwndSource;
    private DxgiScreenCapture? _dxgiCapture;
    private bool _useDxgi;

    public event Action? Stopped;
    public string? LastError { get; private set; }

    public FullscreenEnhanceWindow()
    {
        try
        {
            InitializeComponent();

            _windowW = (int)SystemParameters.PrimaryScreenWidth;
            _windowH = (int)SystemParameters.PrimaryScreenHeight;

            // 构造函数中尚无 DPI 信息，先用逻辑尺寸作为默认值
            // （Start() 中会根据实际 DPI 修正为物理尺寸）
            _captureW = _windowW;
            _captureH = _windowH;
            _captureStride = _captureW * 4;
            // 获取主显示器在虚拟桌面中的实际坐标（多显示器下不一定为原点）
            var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
            _screenLeft = primaryScreen.Bounds.X;
            _screenTop = primaryScreen.Bounds.Y;

            Left = 0;
            Top = 0;
            Width = _windowW;
            Height = _windowH;

            SourceInitialized += OnSourceInitialized;
        }
        catch (Exception ex)
        {
            LastError = $"窗口初始化失败: {ex.Message}";
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            _hwndSource = (HwndSource?)PresentationSource.FromVisual(this);
            if (_hwndSource == null) return;

            var helper = new WindowInteropHelper(this);
            try { SetWindowDisplayAffinity(helper.Handle, WDA_EXCLUDEFROMCAPTURE); }
            catch { }

            _hwndSource.AddHook(WndProc);
            RegisterHotKey(_hwndSource.Handle, HOTKEY_ID, 0, 0x7A); // VK_F11
        }
        catch (Exception ex)
        {
            LastError = $"窗口初始化失败: {ex.Message}";
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            Dispatcher.Invoke(Stop);
            handled = true;
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// 启动全屏增强。传入任意 IRealTimeEnhancer 实现和参数。
    /// </summary>
    public void Start(IRealTimeEnhancer method, IReadOnlyDictionary<string, double>? parameters)
    {
        _method = method;
        _params = parameters;
        _cts = new CancellationTokenSource();

        try
        {
            // 获取当前显示器的 DPI 缩放因子
            var dpi = VisualTreeHelper.GetDpi(this);
            _dpiScaleX = dpi.DpiScaleX;
            _dpiScaleY = dpi.DpiScaleY;
            _dpiX = 96.0 * _dpiScaleX;
            _dpiY = 96.0 * _dpiScaleY;

            // 根据 WPF 逻辑尺寸 × DPI 缩放 = 物理像素
            // 这比任何 P/Invoke 都可靠，因为直接使用 WPF 自身的 DPI 系统
            _captureW = (int)(_windowW * _dpiScaleX);
            _captureH = (int)(_windowH * _dpiScaleY);
            _captureStride = _captureW * 4;
            // 使用主显示器在虚拟桌面中的实际坐标（多显示器下非主屏可能为负值）
            var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
            _screenLeft = primaryScreen.Bounds.X;
            _screenTop = primaryScreen.Bounds.Y;

            // 优先使用 DXGI Desktop Duplication（更高效）
            _dxgiCapture = new DxgiScreenCapture();
            _useDxgi = _dxgiCapture.Initialize();
            if (_useDxgi)
            {
                // DXGI 返回的物理分辨率覆盖计算值（两者应该一致）
                _captureW = _dxgiCapture.Width;
                _captureH = _dxgiCapture.Height;
                _captureStride = _captureW * 4;
            }

            Show();

            // 鼠标穿透
            var helper = new WindowInteropHelper(this);
            int exStyle = GetWindowLong(helper.Handle, GWL_EXSTYLE);
            SetWindowLong(helper.Handle, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT);
            SetWindowPos(helper.Handle, IntPtr.Zero, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);

            _ = CaptureLoop(_cts.Token);
        }
        catch (Exception ex)
        {
            LastError = $"启动失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 停止全屏增强，Hide 窗口并清理资源。
    /// </summary>
    public void Stop()
    {
        try { _cts?.Cancel(); _cts = null; } catch { }
        try { UnregisterHotKey(_hwndSource?.Handle ?? IntPtr.Zero, HOTKEY_ID); } catch { }
        try { Hide(); } catch { }
        Stopped?.Invoke();
    }

    private async Task CaptureLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await CaptureOneFrame(ct);
            if (ct.IsCancellationRequested) break;

            int targetMs = _useDxgi ? 33 : 100;
            int delay = Math.Max(10, targetMs - (int)sw.ElapsedMilliseconds);
            try { await Task.Delay(delay, ct); }
            catch { break; }
        }
    }

    private async Task CaptureOneFrame(CancellationToken ct)
    {
        try
        {
            var captureTask = Task.Run(() =>
            {
                try
                {
                    int w = _captureW, h = _captureH, stride = _captureStride;
                    byte[]? pixels = null;

                    if (_useDxgi)
                    {
                        pixels = _dxgiCapture?.CaptureFrame();
                        if (pixels != null && pixels.Length > 0)
                            stride = _captureStride; // DXGI 输出恒为 width*4
                    }

                    if (pixels == null || pixels.Length == 0)
                    {
                        // GDI 回退——从主显示器实际坐标捕获物理分辨率画面
                        using var bmp = new System.Drawing.Bitmap(w, h,
                            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                        using (var g = System.Drawing.Graphics.FromImage(bmp))
                            g.CopyFromScreen(_screenLeft, _screenTop, 0, 0,
                                new System.Drawing.Size(w, h));

                        var data = bmp.LockBits(
                            new System.Drawing.Rectangle(0, 0, w, h),
                            System.Drawing.Imaging.ImageLockMode.ReadOnly,
                            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                        stride = data.Stride;
                        int len = stride * h;
                        pixels = new byte[len];
                        Marshal.Copy(data.Scan0, pixels, 0, len);
                        bmp.UnlockBits(data);
                    }

                    if (pixels == null || pixels.Length == 0) return null;

                    // 逐帧增强（操作物理像素）
                    byte[] enhanced = _method!.Enhance(pixels, w, h, stride, _params);

                    // 关键：创建 BitmapSource 时使用显示器物理 DPI
                    // - 物理分辨率 w×h（如 1920×1080）
                    // - DPI = 96 × scale（如 150% → 144 DPI）
                    // WPF 知道此图像应以 144 DPI 渲染 → 1:1 像素映射到屏幕
                    var src = BitmapSource.Create(
                        w, h, _dpiX, _dpiY, PixelFormats.Bgra32, null, enhanced, stride);
                    src.Freeze();
                    return src;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"帧异常: {ex.Message}");
                    return null;
                }
            }, ct);

            // 2 秒超时保护——防止 CopyFromScreen 在某些显卡配置下卡死
            if (await Task.WhenAny(captureTask, Task.Delay(2000, ct)) != captureTask)
                return;

            var bitmap = captureTask.Result;
            if (bitmap == null) return;

            Dispatcher.Invoke(() => Display.Source = bitmap, DispatcherPriority.Render);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"帧处理异常: {ex.Message}");
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _cts?.Cancel();
        _dxgiCapture?.Dispose();
        UnregisterHotKey(_hwndSource?.Handle ?? IntPtr.Zero, HOTKEY_ID);
        base.OnClosed(e);
    }
}
