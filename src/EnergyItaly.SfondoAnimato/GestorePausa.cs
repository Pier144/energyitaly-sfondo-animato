using EnergyItaly.SfondoAnimato.Core;
using Microsoft.Win32;

namespace EnergyItaly.SfondoAnimato;

/// <summary>Raccoglie i segnali di sistema per la pausa automatica (spec 6.2) e avvisa quando cambiano.</summary>
sealed class GestorePausa : IDisposable
{
    // Le finestre si seguono con gli eventi di Windows (ripresa immediata quando il desktop torna
    // visibile); il controllo ogni 2 secondi resta per batteria e per gli eventi persi.
    readonly System.Windows.Forms.Timer timer = new() { Interval = 2000 };
    readonly System.Windows.Forms.Timer ritardoFinestre = new() { Interval = 50 };
    readonly Win32.WinEventProc eventoFinestra;
    readonly IntPtr[] agganci;
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
        ritardoFinestre.Tick += (_, _) =>
        {
            ritardoFinestre.Stop();
            Valuta();
        };
        eventoFinestra = FinestraCambiata;
        agganci =
        [
            Aggancia(Win32.EVENT_SYSTEM_FOREGROUND, Win32.EVENT_SYSTEM_FOREGROUND),
            Aggancia(Win32.EVENT_SYSTEM_MOVESIZEEND, Win32.EVENT_SYSTEM_MOVESIZEEND),
            Aggancia(Win32.EVENT_SYSTEM_MINIMIZESTART, Win32.EVENT_SYSTEM_MINIMIZEEND),
            Aggancia(Win32.EVENT_OBJECT_DESTROY, Win32.EVENT_OBJECT_HIDE),
            Aggancia(Win32.EVENT_OBJECT_LOCATIONCHANGE, Win32.EVENT_OBJECT_LOCATIONCHANGE),
            Aggancia(Win32.EVENT_OBJECT_CLOAKED, Win32.EVENT_OBJECT_UNCLOAKED)
        ];
    }

    IntPtr Aggancia(uint primo, uint ultimo) =>
        Win32.SetWinEventHook(primo, ultimo, IntPtr.Zero, eventoFinestra, 0, 0, Win32.WINEVENT_OUTOFCONTEXT);

    // Arriva sul thread dell'interfaccia. Solo finestre principali. Il primo evento di una raffica si
    // valuta subito (con la WebView nascosta, ogni istante di attesa mostra lo sfondo di Windows); il
    // ritardo, che parte e non si riavvia, coglie lo stato finale degli eventi arrivati nel frattempo.
    void FinestraCambiata(IntPtr hook, uint evento, IntPtr h, int idOggetto, int idFiglio, uint thread, uint tempo)
    {
        if (h == IntPtr.Zero || idOggetto != Win32.OBJID_WINDOW || idFiglio != Win32.CHILDID_SELF) return;
        if (evento != Win32.EVENT_OBJECT_DESTROY && Win32.GetAncestor(h, Win32.GA_ROOT) != h) return;
        if (ritardoFinestre.Enabled) return;
        Valuta();
        ritardoFinestre.Start();
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

    // QUNS_RUNNING_D3D_FULL_SCREEN (3) e QUNS_PRESENTATION_MODE (4): RF-31. QUNS_BUSY (2) no: vale
    // per qualunque finestra a schermo intero anche su un solo monitor, e quel caso lo decide la
    // copertura per monitor (RF-30), così l'animazione continua sugli altri (T7).
    static bool SchermoInteroAttivo() =>
        Win32.SHQueryUserNotificationState(out var stato) == 0 && stato is 3 or 4;

    public void Dispose()
    {
        foreach (var aggancio in agganci)
            if (aggancio != IntPtr.Zero) Win32.UnhookWinEvent(aggancio);
        timer.Stop();
        timer.Dispose();
        ritardoFinestre.Stop();
        ritardoFinestre.Dispose();
        messaggi.MonitorAccesi -= Accesi;
        messaggi.RisparmioEnergetico -= Risparmio;
        SystemEvents.SessionSwitch -= SessioneCambiata;
        SystemEvents.PowerModeChanged -= AlimentazioneCambiata;
    }
}
