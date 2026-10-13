using EnergyItaly.SfondoAnimato.Core;
using Microsoft.Win32;

namespace EnergyItaly.SfondoAnimato;

/// <summary>Raccoglie i segnali di sistema per la pausa automatica (spec 6.2) e avvisa quando cambiano.</summary>
sealed class GestorePausa : IDisposable
{
    readonly System.Windows.Forms.Timer timer = new() { Interval = 2000 };
    readonly FinestraMessaggi messaggi;
    bool sessioneNonAttiva;
    bool monitorSpenti;
    bool risparmioEnergetico;
    SegnaliSistema ultimi = new();

    public event Action<SegnaliSistema>? Cambiati;

    public GestorePausa(FinestraMessaggi messaggi)
    {
        this.messaggi = messaggi;
        messaggi.MonitorAccesi += Accesi;
        messaggi.RisparmioEnergetico += Risparmio;
        SystemEvents.SessionSwitch += SessioneCambiata;
        SystemEvents.PowerModeChanged += AlimentazioneCambiata;
        timer.Tick += (_, _) => Valuta();
        timer.Start();
    }

    void Accesi(bool accesi) { monitorSpenti = !accesi; Valuta(); }

    void Risparmio(bool attivo) { risparmioEnergetico = attivo; Valuta(); }

    void SessioneCambiata(object? mittente, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
            case SessionSwitchReason.ConsoleDisconnect:
            case SessionSwitchReason.RemoteDisconnect:
            case SessionSwitchReason.SessionLogoff:
                sessioneNonAttiva = true;
                break;
            case SessionSwitchReason.SessionUnlock:
            case SessionSwitchReason.ConsoleConnect:
            case SessionSwitchReason.RemoteConnect:
            case SessionSwitchReason.SessionLogon:
                sessioneNonAttiva = false;
                break;
        }
        Valuta();
    }

    void AlimentazioneCambiata(object? mittente, PowerModeChangedEventArgs e) => Valuta();

    public void Valuta()
    {
        var segnali = new SegnaliSistema(
            DesktopCoperto: CoperturaFinestre.DesktopCoperto(),
            SchermoIntero: SchermoInteroAttivo(),
            SessioneNonAttiva: sessioneNonAttiva,
            MonitorSpenti: monitorSpenti,
            ABatteria: risparmioEnergetico || SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline);
        if (segnali == ultimi) return;
        ultimi = segnali;
        Cambiati?.Invoke(segnali);
    }

    // QUNS_BUSY (2), QUNS_RUNNING_D3D_FULL_SCREEN (3), QUNS_PRESENTATION_MODE (4): RF-31.
    static bool SchermoInteroAttivo() =>
        Win32.SHQueryUserNotificationState(out var stato) == 0 && stato is 2 or 3 or 4;

    public void Dispose()
    {
        timer.Stop();
        timer.Dispose();
        messaggi.MonitorAccesi -= Accesi;
        messaggi.RisparmioEnergetico -= Risparmio;
        SystemEvents.SessionSwitch -= SessioneCambiata;
        SystemEvents.PowerModeChanged -= AlimentazioneCambiata;
    }
}
