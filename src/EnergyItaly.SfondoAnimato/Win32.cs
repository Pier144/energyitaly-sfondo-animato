using System.Runtime.InteropServices;
using System.Text;

namespace EnergyItaly.SfondoAnimato;

/// <summary>Funzioni e costanti di Windows usate dal programma.</summary>
static class Win32
{
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;
    public const uint WS_POPUP = 0x80000000;
    public const uint WS_CHILD = 0x40000000;
    public const uint WS_CLIPSIBLINGS = 0x04000000;
    public const uint WS_CLIPCHILDREN = 0x02000000;
    public const int WS_EX_TRANSPARENT = 0x20;
    public const int WS_EX_TOOLWINDOW = 0x80;
    public const int WS_EX_LAYERED = 0x80000;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const uint SWP_NOSIZE = 0x1;
    public const uint SWP_NOMOVE = 0x2;
    public const uint SWP_NOZORDER = 0x4;
    public const uint SWP_NOACTIVATE = 0x10;
    public const int SW_HIDE = 0;
    public const int SW_SHOWNOACTIVATE = 4;
    public const uint LWA_ALPHA = 0x2;
    public const uint SMTO_NORMAL = 0x0;
    public const int WM_MOUSEACTIVATE = 0x21;
    public const int WM_NCDESTROY = 0x82;
    public const int MA_NOACTIVATE = 3;
    public const int WM_POWERBROADCAST = 0x218;
    public const int WM_QUERYENDSESSION = 0x11;
    public const int WM_ENDSESSION = 0x16;
    public const int WM_SETTINGCHANGE = 0x1A;
    public const int WM_DPICHANGED = 0x2E0;
    public const int ASFW_ANY = -1;
    public const int PBT_POWERSETTINGCHANGE = 0x8013;
    public const uint SPI_SETDESKWALLPAPER = 0x14;
    public const uint SPI_GETDESKWALLPAPER = 0x73;
    public const uint SPIF_SENDCHANGE = 0x2;
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    public const int DWMWA_CLOAKED = 14;
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE_PRIMA_20H1 = 19;
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint EVENT_SYSTEM_MOVESIZEEND = 0x000B;
    public const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
    public const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;
    public const uint EVENT_OBJECT_DESTROY = 0x8001;
    public const uint EVENT_OBJECT_HIDE = 0x8003;
    public const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
    public const uint EVENT_OBJECT_CLOAKED = 0x8017;
    public const uint EVENT_OBJECT_UNCLOAKED = 0x8018;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0;
    public const int OBJID_WINDOW = 0;
    public const int CHILDID_SELF = 0;
    public const uint GA_ROOT = 2;
    public static readonly Guid GUID_CONSOLE_DISPLAY_STATE = new("6FE69556-704A-47A0-8F24-C28D936FDA47");
    public static readonly Guid GUID_POWER_SAVING_STATUS = new("E00958C0-C213-4ACE-AC77-FECCED2EEEA5");

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct POWERBROADCAST_SETTING { public Guid PowerSetting; public uint DataLength; public uint Data; }

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    public delegate void WinEventProc(IntPtr hook, uint evento, IntPtr hWnd, int idOggetto, int idFiglio, uint thread, uint tempo);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string? classe, string? titolo);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr padre, IntPtr dopo, string? classe, string? titolo);
    [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr risultato);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] public static extern IntPtr SetParent(IntPtr figlia, IntPtr padre);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int indice);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int indice, IntPtr valore);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr dopo, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool ShowWindow(IntPtr hWnd, int comando);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int MapWindowPoints(IntPtr da, IntPtr a, ref POINT punti, int quanti);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint chiave, byte alfa, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder nome, int massimo);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rettangolo);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string nome);
    [DllImport("user32.dll")] public static extern IntPtr RegisterPowerSettingNotification(IntPtr destinatario, ref Guid impostazione, int flags);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool UnregisterPowerSettingNotification(IntPtr handle);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = CharSet.Unicode)][return: MarshalAs(UnmanagedType.Bool)] public static extern bool SystemParametersInfo(uint azione, uint parametro, StringBuilder? valore, uint winIni);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = CharSet.Unicode)][return: MarshalAs(UnmanagedType.Bool)] public static extern bool SystemParametersInfo(uint azione, uint parametro, string? valore, uint winIni);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attributo, out RECT valore, int dimensione);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attributo, out int valore, int dimensione);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hWnd, int attributo, ref int valore, int dimensione);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool AllowSetForegroundWindow(int processo);
    [DllImport("shell32.dll")] public static extern int SHQueryUserNotificationState(out int stato);
    [DllImport("user32.dll")] public static extern IntPtr SetWinEventHook(uint eventoMin, uint eventoMax, IntPtr modulo, WinEventProc callback, uint processo, uint thread, uint flags);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hWnd, uint tipo);
}
