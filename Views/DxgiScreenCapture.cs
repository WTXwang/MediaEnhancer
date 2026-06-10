using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MediaEnhancer.Views;

/// <summary>
/// 基于 DXGI Desktop Duplication 的屏幕捕获（纯 P/Invoke，无第三方依赖）
/// 支持 Windows 8.1+，比 GDI CopyFromScreen 更高效
/// </summary>
public class DxgiScreenCapture : IDisposable
{
    // ---- D3D11 ----
    private const uint D3D11_SDK_VERSION = 7;

    [DllImport("d3d11.dll", SetLastError = true)]
    private static extern int D3D11CreateDevice(
        IntPtr pAdapter, DriverType driverType, IntPtr software,
        uint flags, IntPtr pFeatureLevels, uint featureLevels,
        uint sdkVersion, out IntPtr ppDevice, out uint pFeatureLevel,
        out IntPtr ppImmediateContext);

    private enum DriverType : uint
    {
        Unknown = 0,
        Hardware = 1,
        Reference = 2,
        Null = 3,
        Software = 4,
        Warp = 5
    }

    // ---- COM GUIDs ----
    private static readonly Guid IID_IDXGIFactory1 = new("7706BB76-F83F-4D74-A553-89F2C82969AB");
    private static readonly Guid IID_IDXGIAdapter1 = new("29038F03-3839-4F0B-9224-DB93E9665B3B");
    private static readonly Guid IID_IDXGIOutput1 = new("00CDECA8-392B-4665-A556-DE23F3C0C2E3");
    private static readonly Guid IID_ID3D11Texture2D = new("A7C3B0E7-8E7C-4D5F-A5D4-3E5E8D5A5F7C");

    // ---- COM API ----
    [DllImport("dxgi.dll", SetLastError = true)]
    private static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr ppFactory);

    // ---- DXGI Types ----
    [StructLayout(LayoutKind.Sequential)]
    private struct DXGI_ADAPTER_DESC1
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
        public char[] Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public IntPtr DedicatedVideoMemory;
        public IntPtr DedicatedSystemMemory;
        public IntPtr SharedSystemMemory;
        public ulong AdapterLuid;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DXGI_OUTPUT_DESC
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public char[] DeviceName;
        public RECT DesktopCoordinates;
        public bool AttachedToDesktop;
        public uint Rotation;
        public IntPtr Monitor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DXGI_FORMAT { public uint Value; public static readonly DXGI_FORMAT B8G8R8A8_UNORM = new() { Value = 87 }; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DXGI_SAMPLE_DESC { public uint Count; public uint Quality; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DXGI_MODE_DESC
    {
        public uint Width, Height;
        public DXGI_RATIONAL RefreshRate;
        public DXGI_FORMAT Format;
        public uint ScanlineOrdering;
        public uint Scaling;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DXGI_RATIONAL { public uint Numerator; public uint Denominator; }

    // ---- D3D11 Types ----
    [StructLayout(LayoutKind.Sequential)]
    private struct D3D11_TEXTURE2D_DESC
    {
        public uint Width, Height, MipLevels, ArraySize;
        public DXGI_FORMAT Format;
        public DXGI_SAMPLE_DESC SampleDesc;
        public uint Usage;
        public uint BindFlags;
        public uint CPUAccessFlags;
        public uint MiscFlags;
    }

    // ---- COM VTable ----
    [StructLayout(LayoutKind.Sequential)]
    private struct IUnknownVtbl
    {
        public IntPtr QueryInterface;
        public IntPtr AddRef;
        public IntPtr Release;
    }

    // ---- Instance fields ----
    private IntPtr _device = IntPtr.Zero;
    private IntPtr _context = IntPtr.Zero;
    private IntPtr _duplication = IntPtr.Zero;
    private IntPtr _stagingTex = IntPtr.Zero;
    private int _width, _height;
    private bool _initialized;

    public bool IsReady => _initialized;
    public int Width => _width;
    public int Height => _height;

    // ---- Native COM helpers ----
    private delegate int QueryInterfaceDelegate(IntPtr pThis, ref Guid riid, out IntPtr ppvObject);
    private delegate uint AddRefDelegate(IntPtr pThis);
    private delegate uint ReleaseDelegate(IntPtr pThis);

    private static T GetVtblFunc<T>(IntPtr pObj, int index) where T : Delegate
    {
        var vtbl = Marshal.ReadIntPtr(pObj);
        var funcPtr = Marshal.ReadIntPtr(vtbl + index * IntPtr.Size);
        return Marshal.GetDelegateForFunctionPointer<T>(funcPtr);
    }

    // ---- DXGI Adapter/Output enumeration ----
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumAdapters1Delegate(IntPtr pFactory, uint adapterIndex, out IntPtr ppAdapter);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumOutputsDelegate(IntPtr pAdapter, uint outputIndex, out IntPtr ppOutput);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void GetDesc1Delegate(IntPtr pAdapter, out DXGI_ADAPTER_DESC1 pDesc);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void GetOutputDescDelegate(IntPtr pOutput, out DXGI_OUTPUT_DESC pDesc);

    // ---- DXGI Output Duplication ----
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DuplicateOutputDelegate(IntPtr pOutput1, IntPtr pDevice, out IntPtr ppDuplication);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int AcquireNextFrameDelegate(IntPtr pDup, uint timeoutMs, out DXGI_OUTDUPL_FRAME_INFO frameInfo, out IntPtr ppResource);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ReleaseFrameDelegate(IntPtr pDup);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void GetDescDelegate(IntPtr pResource, out D3D11_TEXTURE2D_DESC pDesc);

    [StructLayout(LayoutKind.Sequential)]
    private struct DXGI_OUTDUPL_FRAME_INFO
    {
        public long LastPresentTime;
        public long LastMouseUpdateTime;
        public uint AccumulatedFrames;
        public bool RectsCoalesced;
        public bool ProtectedContentMaskedOut;
        public IntPtr PointerPosition;
        public uint TotalMetadataBufferSize;
        public uint PointerShapeBufferSize;
    }

    // ---- D3D11 Device ----
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateTexture2DDelegate(IntPtr pDevice, ref D3D11_TEXTURE2D_DESC pDesc, IntPtr pInitialData, out IntPtr ppTexture);

    // ---- D3D11 Device Context ----
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void CopyResourceDelegate(IntPtr pContext, IntPtr pDst, IntPtr pSrc);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void FlushDelegate(IntPtr pContext);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int MapDelegate(IntPtr pContext, IntPtr pResource, uint subresource, uint mapType, uint mapFlags, out D3D11_MAPPED_SUBRESOURCE pMapped);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void UnmapDelegate(IntPtr pContext, IntPtr pResource, uint subresource);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int QueryInterfaceDelegate2(IntPtr pThis, ref Guid riid, out IntPtr ppv);

    // DXGI_ERROR codes
    private const int DXGI_ERROR_WAIT_TIMEOUT = unchecked((int)0x887A0027);
    private const int DXGI_ERROR_ACCESS_LOST = unchecked((int)0x887A0026);

    [StructLayout(LayoutKind.Sequential)]
    private struct D3D11_MAPPED_SUBRESOURCE
    {
        public IntPtr pData;
        public int RowPitch;
        public int DepthPitch;
    }

    public bool Initialize()
    {
        try
        {
            // 1. Create DXGI factory
            var facGuid = IID_IDXGIFactory1;
            var hr = CreateDXGIFactory1(ref facGuid, out var pFactory);
            if (hr < 0) return false;

            // 2. Enumerate adapter (use the first one)
            var enumAdapters = GetVtblFunc<EnumAdapters1Delegate>(pFactory, 7);
            hr = enumAdapters(pFactory, 0, out var pAdapter);
            ReleaseCom(pFactory);
            if (hr < 0) return false;

            // 3. Get output 0 (primary monitor)
            var enumOutputs = GetVtblFunc<EnumOutputsDelegate>(pAdapter, 7);
            hr = enumOutputs(pAdapter, 0, out var pOutput);
            ReleaseCom(pAdapter);
            if (hr < 0) return false;

            // 4. Get output description (resolution)
            var getDesc = GetVtblFunc<GetOutputDescDelegate>(pOutput, 8);
            getDesc(pOutput, out var outputDesc);
            _width = outputDesc.DesktopCoordinates.Right - outputDesc.DesktopCoordinates.Left;
            _height = outputDesc.DesktopCoordinates.Bottom - outputDesc.DesktopCoordinates.Top;

            // 5. Query for IDXGIOutput1
            var qiOutput = GetVtblFunc<QueryInterfaceDelegate>(pOutput, 0);
            var out1Guid = IID_IDXGIOutput1;
            hr = qiOutput(pOutput, ref out1Guid, out var pOutput1);
            if (hr < 0) { ReleaseCom(pOutput); return false; }

            // 6. Create D3D11 device
            hr = D3D11CreateDevice(IntPtr.Zero, DriverType.Hardware, IntPtr.Zero, 0,
                IntPtr.Zero, 0, D3D11_SDK_VERSION, out _device, out _, out _context);
            if (hr < 0) { ReleaseCom(pOutput); ReleaseCom(pOutput1); return false; }

            // 7. Create Output Duplication
            var dupOut = GetVtblFunc<DuplicateOutputDelegate>(pOutput1, 5);
            hr = dupOut(pOutput1, _device, out _duplication);
            ReleaseCom(pOutput1);
            ReleaseCom(pOutput);
            if (hr < 0) return false;

            // 8. Create staging texture
            var texDesc = new D3D11_TEXTURE2D_DESC
            {
                Width = (uint)_width,
                Height = (uint)_height,
                MipLevels = 1,
                ArraySize = 1,
                Format = DXGI_FORMAT.B8G8R8A8_UNORM,
                SampleDesc = new DXGI_SAMPLE_DESC { Count = 1, Quality = 0 },
                Usage = 2, // Staging
                BindFlags = 0,
                CPUAccessFlags = 0x20000, // Read
                MiscFlags = 0
            };

            var createTex = GetVtblFunc<CreateTexture2DDelegate>(_device, 5);
            hr = createTex(_device, ref texDesc, IntPtr.Zero, out _stagingTex);
            if (hr < 0) return false;

            _initialized = true;
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DXGI init error: {ex.Message}");
            Cleanup();
            return false;
        }
    }

    /// <summary>
    /// 捕获一帧。超时（桌面未变化）自动重试，最多等待 100ms。
    /// </summary>
    /// <param name="maxWaitMs">最长等待时间（毫秒），默认 100。</param>
    public byte[]? CaptureFrame(int maxWaitMs = 100)
    {
        if (!_initialized) return null;

        try
        {
            // ---- Acquire with retry on timeout ----
            var acquire = GetVtblFunc<AcquireNextFrameDelegate>(_duplication, 3);
            var start = Environment.TickCount64;
            IntPtr pResource;
            int hr;

            while (true)
            {
                int remaining = (int)Math.Min(16, maxWaitMs - (Environment.TickCount64 - start));
                if (remaining < 0) remaining = 0;

                hr = acquire(_duplication, (uint)remaining, out _, out pResource);
                if (hr >= 0) break;                               // success
                if (hr == DXGI_ERROR_WAIT_TIMEOUT)                // no new frame yet
                {
                    if (Environment.TickCount64 - start >= maxWaitMs)
                        return null;                               // waited long enough, give up
                    continue;                                      // retry
                }
                // ACCESS_LOST or other fatal error
                if (hr == DXGI_ERROR_ACCESS_LOST)
                    _initialized = false;
                return null;
            }

            using var resourceOwner = new ComOwner(pResource);

            // ---- Copy to staging ----
            var copyRes = GetVtblFunc<CopyResourceDelegate>(_context, 47);
            copyRes(_context, _stagingTex, pResource);

            // Flush GPU command queue so Map() reads complete data
            var flush = GetVtblFunc<FlushDelegate>(_context, 54);
            flush(_context);

            // Release frame back to DXGI
            var releaseFrame = GetVtblFunc<ReleaseFrameDelegate>(_duplication, 4);
            releaseFrame(_duplication);

            // ---- Map staging texture ----
            var map = GetVtblFunc<MapDelegate>(_context, 14);
            hr = map(_context, _stagingTex, 0, 1, 0, out var mapped); // D3D11_MAP_READ = 1
            if (hr < 0) return null;

            try
            {
                int srcStride = mapped.RowPitch;
                int dstStride = _width * 4;
                byte[] pixels = new byte[_height * dstStride];

                for (int y = 0; y < _height; y++)
                    Marshal.Copy(mapped.pData + y * srcStride, pixels, y * dstStride, dstStride);

                return pixels;
            }
            finally
            {
                var unmap = GetVtblFunc<UnmapDelegate>(_context, 15);
                unmap(_context, _stagingTex, 0);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"DXGI capture frame error: {ex.Message}");
            if (ex.Message.Contains("AccessLost") || ex.Message.Contains("0x887A002"))
                _initialized = false;
            return null;
        }
    }

    private static void ReleaseCom(IntPtr pCom)
    {
        if (pCom == IntPtr.Zero) return;
        var release = GetVtblFunc<ReleaseDelegate>(pCom, 2);
        release(pCom);
    }

    private struct ComOwner : IDisposable
    {
        private readonly IntPtr _ptr;
        public ComOwner(IntPtr ptr) => _ptr = ptr;
        public void Dispose() => ReleaseCom(_ptr);
    }

    private void Cleanup()
    {
        if (_stagingTex != IntPtr.Zero) ReleaseCom(_stagingTex);
        if (_duplication != IntPtr.Zero) ReleaseCom(_duplication);
        if (_context != IntPtr.Zero) ReleaseCom(_context);
        if (_device != IntPtr.Zero) ReleaseCom(_device);
        _stagingTex = _duplication = _context = _device = IntPtr.Zero;
        _initialized = false;
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }
}
