using System.Text;
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato;

/// <summary>
/// Mette la finestra dello sfondo dietro le icone del desktop (RF-01..RF-06).
/// Due strutture: classica (WorkerW separato dietro le icone) e Windows 11 24H2 e successivi
/// (icone e WorkerW figli di Progman). Tecnica studiata su Lively Wallpaper, non copiata.
/// </summary>
sealed class DesktopHost
{
    readonly ILog log;
    IntPtr padre;

    public DesktopHost(ILog log) => this.log = log;

    public bool Aggancia(IntPtr finestra, Rettangolo area)
    {
        padre = IntPtr.Zero;
        var progman = Win32.FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
        {
            log.Avviso("Progman non trovato: il desktop non è ancora pronto");
            return false;
        }
        // Messaggio non documentato 0x052C: chiede a Explorer di creare il WorkerW dietro le icone.
        Win32.SendMessageTimeout(progman, 0x052C, new IntPtr(0xD), new IntPtr(0x1), Win32.SMTO_NORMAL, 1000, out _);
        Win32.SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, Win32.SMTO_NORMAL, 1000, out _);

        var icone = Win32.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (icone != IntPtr.Zero)
        {
            // Windows 11 24H2 e successivi: la finestra va dentro Progman, subito sotto le icone.
            RendiFiglia(finestra, progman, stratificata: true);
            Win32.SetWindowPos(finestra, icone, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
            padre = progman;
            log.Info("Aggancio al desktop: struttura 24H2 (figlia di Progman, sotto le icone)");
        }
        else
        {
            IntPtr workerW = IntPtr.Zero;
            Win32.EnumWindows((alto, _) =>
            {
                if (Win32.FindWindowEx(alto, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                    workerW = Win32.FindWindowEx(IntPtr.Zero, alto, "WorkerW", null);
                return true;
            }, IntPtr.Zero);
            if (workerW == IntPtr.Zero)
            {
                log.Avviso("WorkerW non trovato: riprovo più tardi");
                return false;
            }
            RendiFiglia(finestra, workerW, stratificata: false);
            padre = workerW;
            log.Info("Aggancio al desktop: struttura classica (figlia di WorkerW)");
        }
        Posiziona(finestra, area);
        Win32.ShowWindow(finestra, Win32.SW_SHOWNOACTIVATE);
        return true;
    }

    static void RendiFiglia(IntPtr finestra, IntPtr nuovoPadre, bool stratificata)
    {
        long stile = Win32.GetWindowLongPtr(finestra, Win32.GWL_STYLE).ToInt64();
        stile = (stile & ~(long)Win32.WS_POPUP) | Win32.WS_CHILD;
        Win32.SetWindowLongPtr(finestra, Win32.GWL_STYLE, new IntPtr(stile));
        if (stratificata)
        {
            long esteso = Win32.GetWindowLongPtr(finestra, Win32.GWL_EXSTYLE).ToInt64() | Win32.WS_EX_LAYERED;
            Win32.SetWindowLongPtr(finestra, Win32.GWL_EXSTYLE, new IntPtr(esteso));
            Win32.SetLayeredWindowAttributes(finestra, 0, 255, Win32.LWA_ALPHA);
        }
        Win32.SetParent(finestra, nuovoPadre);
    }

    /// <summary>Porta la finestra sull'area indicata (coordinate dello schermo virtuale, px fisici).</summary>
    public void Posiziona(IntPtr finestra, Rettangolo area)
    {
        var p = new Win32.POINT { X = area.X, Y = area.Y };
        if (padre != IntPtr.Zero) Win32.MapWindowPoints(IntPtr.Zero, padre, ref p, 1);
        Win32.SetWindowPos(finestra, IntPtr.Zero, p.X, p.Y, area.L, area.A, Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
    }

    public void Sgancia(IntPtr finestra)
    {
        if (Win32.IsWindow(finestra)) Win32.ShowWindow(finestra, Win32.SW_HIDE);
        padre = IntPtr.Zero;
    }

    /// <summary>Riapplica lo sfondo statico di Windows così il desktop si ridisegna (RF-06).</summary>
    public static void RidisegnaSfondo()
    {
        var attuale = new StringBuilder(520);
        if (Win32.SystemParametersInfo(Win32.SPI_GETDESKWALLPAPER, (uint)attuale.Capacity, attuale, 0))
            Win32.SystemParametersInfo(Win32.SPI_SETDESKWALLPAPER, 0, attuale.ToString(), Win32.SPIF_SENDCHANGE);
    }
}
