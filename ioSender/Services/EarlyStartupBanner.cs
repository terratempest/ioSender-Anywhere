using System.Runtime.InteropServices;

namespace ioSender.Services;

internal static class EarlyStartupBanner
{
#if IOSENDER_WINDOWS
    private const int Width = 420;
    private const int Height = 180;
    private const string WindowClassName = "ioSenderEarlyStartupBanner";
    private static readonly ManualResetEventSlim Created = new(false);
    private static readonly WndProc WindowProcedure = HandleWindowMessage;
    private static readonly object ProgressSync = new();
    private static Thread? _thread;
    private static nint _windowHandle;
    private static string _statusText = "Starting ioSender...";
    private static int _progressPercent = 5;

    public static bool IsActive => _windowHandle != 0;

    public static void Show()
    {
        if (_thread is not null)
            return;

        _thread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "ioSender startup banner"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        Created.Wait(TimeSpan.FromMilliseconds(300));
    }

    public static void Close()
    {
        var handle = _windowHandle;
        if (handle != 0)
            PostMessage(handle, WM_CLOSE, 0, 0);
    }

    public static void ReportProgress(string statusText, int percent)
    {
        lock (ProgressSync)
        {
            _statusText = statusText;
            _progressPercent = Math.Clamp(percent, 0, 99);
        }

        var handle = _windowHandle;
        if (handle != 0)
            InvalidateRect(handle, 0, true);
    }

    private static void RunMessageLoop()
    {
        var instance = GetModuleHandle(null);
        var windowClass = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(WindowProcedure),
            hInstance = instance,
            lpszClassName = WindowClassName,
            hbrBackground = GetStockObject(NULL_BRUSH)
        };

        RegisterClassEx(ref windowClass);

        var bounds = GetLaunchMonitorWorkArea();
        var left = bounds.Left + Math.Max(0, (bounds.Right - bounds.Left - Width) / 2);
        var top = bounds.Top + Math.Max(0, (bounds.Bottom - bounds.Top - Height) / 2);

        _windowHandle = CreateWindowEx(
            WS_EX_TOOLWINDOW | WS_EX_TOPMOST,
            WindowClassName,
            "ioSender",
            WS_POPUP,
            left,
            top,
            Width,
            Height,
            0,
            0,
            instance,
            0);

        if (_windowHandle == 0)
        {
            Created.Set();
            return;
        }

        ShowWindow(_windowHandle, SW_SHOW);
        UpdateWindow(_windowHandle);
        Created.Set();

        while (GetMessage(out var message, 0, 0, 0) > 0)
        {
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }

        _windowHandle = 0;
    }

    private static nint HandleWindowMessage(nint windowHandle, uint message, nuint wParam, nint lParam)
    {
        switch (message)
        {
            case WM_PAINT:
                Paint(windowHandle);
                return 0;
            case WM_LBUTTONDOWN:
                ReleaseCapture();
                SendMessage(windowHandle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                return 0;
            case WM_CLOSE:
                DestroyWindow(windowHandle);
                return 0;
            case WM_DESTROY:
                PostQuitMessage(0);
                return 0;
            default:
                return DefWindowProc(windowHandle, message, wParam, lParam);
        }
    }

    private static RECT GetLaunchMonitorWorkArea()
    {
        var monitor = MonitorFromWindow(GetForegroundWindow(), MONITOR_DEFAULTTONEAREST);
        if (monitor == 0 && GetCursorPos(out var cursor))
            monitor = MonitorFromPoint(cursor, MONITOR_DEFAULTTONEAREST);

        var monitorInfo = new MONITORINFO
        {
            cbSize = Marshal.SizeOf<MONITORINFO>()
        };

        if (monitor != 0 && GetMonitorInfo(monitor, ref monitorInfo))
            return monitorInfo.rcWork;

        return new RECT
        {
            Left = GetSystemMetrics(SM_XVIRTUALSCREEN),
            Top = GetSystemMetrics(SM_YVIRTUALSCREEN),
            Right = GetSystemMetrics(SM_XVIRTUALSCREEN) + GetSystemMetrics(SM_CXVIRTUALSCREEN),
            Bottom = GetSystemMetrics(SM_YVIRTUALSCREEN) + GetSystemMetrics(SM_CYVIRTUALSCREEN)
        };
    }

    private static void Paint(nint windowHandle)
    {
        var hdc = BeginPaint(windowHandle, out var paint);
        var background = CreateSolidBrush(0x00262525);
        var accent = CreateSolidBrush(0x00FF9437);
        var titleFont = CreateFont(
            -32,
            0,
            0,
            0,
            FW_SEMIBOLD,
            0,
            0,
            0,
            DEFAULT_CHARSET,
            OUT_DEFAULT_PRECIS,
            CLIP_DEFAULT_PRECIS,
            CLEARTYPE_QUALITY,
            DEFAULT_PITCH | FF_SWISS,
            "Segoe UI");
        var statusFont = CreateFont(
            -16,
            0,
            0,
            0,
            FW_NORMAL,
            0,
            0,
            0,
            DEFAULT_CHARSET,
            OUT_DEFAULT_PRECIS,
            CLIP_DEFAULT_PRECIS,
            CLEARTYPE_QUALITY,
            DEFAULT_PITCH | FF_SWISS,
            "Segoe UI");
        string statusText;
        int progressPercent;

        lock (ProgressSync)
        {
            statusText = _statusText;
            progressPercent = _progressPercent;
        }

        try
        {
            var rect = new RECT { Left = 0, Top = 0, Right = Width, Bottom = Height };
            FillRect(hdc, ref rect, background);

            SetBkMode(hdc, TRANSPARENT);
            SetTextColor(hdc, 0x00D4D4D4);

            var titleRect = new RECT { Left = 24, Top = 42, Right = Width - 24, Bottom = 84 };
            var previousFont = SelectObject(hdc, titleFont);
            DrawText(hdc, "ioSender", -1, ref titleRect, DT_CENTER | DT_SINGLELINE | DT_VCENTER);

            var statusRect = new RECT { Left = 24, Top = 92, Right = Width - 24, Bottom = 122 };
            SelectObject(hdc, statusFont);
            DrawText(hdc, statusText, -1, ref statusRect, DT_CENTER | DT_SINGLELINE | DT_VCENTER);

            var barRect = new RECT { Left = 96, Top = 140, Right = Width - 96, Bottom = 144 };
            using var barBackground = new NativeBrush(0x00403D3D);
            FillRect(hdc, ref barRect, barBackground.Handle);

            var fillWidth = Math.Max(8, (barRect.Right - barRect.Left) * progressPercent / 100);
            var fillRect = new RECT
            {
                Left = barRect.Left,
                Top = barRect.Top,
                Right = barRect.Left + fillWidth,
                Bottom = barRect.Bottom
            };
            FillRect(hdc, ref fillRect, accent);
            SelectObject(hdc, previousFont);
        }
        finally
        {
            DeleteObject(statusFont);
            DeleteObject(titleFont);
            DeleteObject(accent);
            DeleteObject(background);
            EndPaint(windowHandle, ref paint);
        }
    }

    private sealed class NativeBrush : IDisposable
    {
        public NativeBrush(uint color) => Handle = CreateSolidBrush(color);

        public nint Handle { get; }

        public void Dispose()
        {
            if (Handle != 0)
                DeleteObject(Handle);
        }
    }

    private delegate nint WndProc(nint windowHandle, uint message, nuint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpszClassName;
        public nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint hwnd;
        public uint message;
        public nuint wParam;
        public nint lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PAINTSTRUCT
    {
        public nint hdc;
        public int fErase;
        public RECT rcPaint;
        public int fRestore;
        public int fIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] rgbReserved;
    }

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;
    private const int SW_SHOW = 5;
    private const int NULL_BRUSH = 5;
    private const uint WS_POPUP = 0x80000000;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_EX_TOPMOST = 0x00000008;
    private const uint WM_CLOSE = 0x0010;
    private const uint WM_DESTROY = 0x0002;
    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_NCLBUTTONDOWN = 0x00A1;
    private const uint WM_PAINT = 0x000F;
    private const nuint HTCAPTION = 2;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int TRANSPARENT = 1;
    private const int FW_NORMAL = 400;
    private const int FW_SEMIBOLD = 600;
    private const uint DEFAULT_CHARSET = 1;
    private const uint OUT_DEFAULT_PRECIS = 0;
    private const uint CLIP_DEFAULT_PRECIS = 0;
    private const uint CLEARTYPE_QUALITY = 5;
    private const uint DEFAULT_PITCH = 0;
    private const uint FF_SWISS = 32;
    private const uint DT_CENTER = 0x00000001;
    private const uint DT_VCENTER = 0x00000004;
    private const uint DT_SINGLELINE = 0x00000020;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        nint hWndParent,
        nint hMenu,
        nint hInstance,
        nint lpParam);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool UpdateWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(nint hWnd, uint msg, nuint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProc(nint hWnd, uint uMsg, nuint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, uint msg, nuint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool InvalidateRect(nint hWnd, nint lpRect, bool bErase);

    [DllImport("user32.dll")]
    private static extern nint BeginPaint(nint hwnd, out PAINTSTRUCT lpPaint);

    [DllImport("user32.dll")]
    private static extern bool EndPaint(nint hWnd, ref PAINTSTRUCT lpPaint);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int DrawText(nint hdc, string lpchText, int cchText, ref RECT lprc, uint format);

    [DllImport("gdi32.dll")]
    private static extern nint CreateSolidBrush(uint color);

    [DllImport("user32.dll")]
    private static extern int FillRect(nint hdc, ref RECT lprc, nint hbr);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern nint CreateFont(
        int cHeight,
        int cWidth,
        int cEscapement,
        int cOrientation,
        int cWeight,
        uint bItalic,
        uint bUnderline,
        uint bStrikeOut,
        uint iCharSet,
        uint iOutPrecision,
        uint iClipPrecision,
        uint iQuality,
        uint iPitchAndFamily,
        string pszFaceName);

    [DllImport("gdi32.dll")]
    private static extern nint SelectObject(nint hdc, nint hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint ho);

    [DllImport("gdi32.dll")]
    private static extern nint GetStockObject(int i);

    [DllImport("gdi32.dll")]
    private static extern int SetBkMode(nint hdc, int mode);

    [DllImport("gdi32.dll")]
    private static extern uint SetTextColor(nint hdc, uint color);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? lpModuleName);
#else
    public static bool IsActive => false;
    public static void Show()
    {
    }

    public static void ReportProgress(string statusText, int percent)
    {
    }

    public static void Close()
    {
    }
#endif
}
