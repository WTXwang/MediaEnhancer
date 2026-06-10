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
    private int _screenW, _screenH;
    private CancellationTokenSource? _cts;
    private HwndSource? _hwndSource;
    private DxgiScreenCapture? _dxgiCapture;
    private bool _useDxgi;

    /// <summary>停止时触发（F11 或外部调用 Stop）。</summary>
    public event Action? Stopped;

    /// <summary>最后发生的错误信息。</summary>
    public string? LastError { get; private set; }

    public FullscreenEnhanceWindow()
    {
        try
        {
            InitializeComponent();
            _screenW = (int)SystemParameters.PrimaryScreenWidth;
            _screenH = (int)SystemParameters.PrimaryScreenHeight;

            Left = 0;
            Top = 0;
            Width = _screenW;
            Height = _screenH;

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
    /// <param name="method">增强算法（如线性拉伸、直方图均衡化等）。</param>
    /// <param name="parameters">参数字典，键为参数 Key，值为当前设置。</param>
    public void Start(IRealTimeEnhancer method, IReadOnlyDictionary<string, double>? parameters)
    {
        _method = method;
        _params = parameters;
        _cts = new CancellationTokenSource();

        try
        {
            // 优先使用 DXGI Desktop Duplication（更快、更省 CPU）
            _dxgiCapture = new DxgiScreenCapture();
            _useDxgi = _dxgiCapture.Initialize();
            if (_useDxgi)
            {
                _screenW = _dxgiCapture.Width;
                _screenH = _dxgiCapture.Height;
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

            // 动态间隔：DXGI 目标 ~30 FPS，GDI 目标 ~10 FPS
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
                    byte[]? pixels = null;
                    int w = _screenW, h = _screenH, stride = _screenW * 4;

                    if (_useDxgi)
                    {
                        // DXGI 捕获（GPU 零拷贝）
                        pixels = _dxgiCapture?.CaptureFrame();
                    }

                    if (pixels == null)
                    {
                        // GDI 回退
                        using var bmp = new System.Drawing.Bitmap(
                            _screenW, _screenH,
                            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                        using (var g = System.Drawing.Graphics.FromImage(bmp))
                            g.CopyFromScreen(0, 0, 0, 0,
                                new System.Drawing.Size(_screenW, _screenH));

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

                    // 调用可插拔增强方法
                    byte[] enhanced = _method!.Enhance(pixels, w, h, stride, _params);

                    var src = BitmapSource.Create(
                        w, h, 96, 96, PixelFormats.Bgra32, null, enhanced, stride);
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
