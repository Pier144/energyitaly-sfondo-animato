using System.Runtime.InteropServices;
using System.Text;
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato;

/// <summary>Raccoglie le finestre visibili e chiede al Core se coprono il desktop su tutti i monitor (RF-30).</summary>
static class CoperturaFinestre
{
    static readonly HashSet<string> ClassiDellaShell = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow"
    };

    public static bool DesktopCoperto()
    {
        var aree = Screen.AllScreens
            .Select(s => new Rettangolo(s.WorkingArea.X, s.WorkingArea.Y, s.WorkingArea.Width, s.WorkingArea.Height))
            .ToList();
        var finestre = new List<Rettangolo>();
        Win32.EnumWindows((h, _) =>
        {
            if (Rilevante(h)) finestre.Add(Area(h));
            return true;
        }, IntPtr.Zero);
        return CoperturaDesktop.TuttiCoperti(aree, finestre);
    }

    static bool Rilevante(IntPtr h)
    {
        if (!Win32.IsWindowVisible(h) || Win32.IsIconic(h)) return false;
        if (Win32.DwmGetWindowAttribute(h, Win32.DWMWA_CLOAKED, out int velata, sizeof(int)) == 0 && velata != 0) return false;
        long esteso = Win32.GetWindowLongPtr(h, Win32.GWL_EXSTYLE).ToInt64();
        if ((esteso & (Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_TRANSPARENT)) != 0) return false;
        var classe = new StringBuilder(64);
        Win32.GetClassName(h, classe, classe.Capacity);
        return !ClassiDellaShell.Contains(classe.ToString());
    }

    static Rettangolo Area(IntPtr h)
    {
        if (Win32.DwmGetWindowAttribute(h, Win32.DWMWA_EXTENDED_FRAME_BOUNDS, out Win32.RECT r, Marshal.SizeOf<Win32.RECT>()) != 0)
            Win32.GetWindowRect(h, out r);
        return new Rettangolo(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }
}
