using System.Diagnostics;
using System.Drawing;
using EnergyItaly.SfondoAnimato.Core;
using Microsoft.Win32;

namespace EnergyItaly.SfondoAnimato;

/// <summary>
/// Compone il programma: coordinatore, finestra di sfondo (ricreata se Explorer si riavvia o la WebView
/// cade), finestra principale, icona nell'area di notifica e segnali di sistema.
/// </summary>
sealed class AppSfondo : ApplicationContext, IVistaPlayer, IVistaApp, ISistema
{
    const string NomeAvvio = "EnergyItaly.SfondoAnimato";
    static readonly TimeSpan IntervalloCrash = TimeSpan.FromMinutes(5);

    readonly SynchronizationContext contesto;
    readonly LogFile log;
    readonly DesktopHost desktop;
    readonly Icon iconaApp;
    readonly IconaNotifica icona;
    readonly FinestraMessaggi messaggi;
    readonly GestorePausa pausa;
    readonly Coordinatore coord;
    readonly System.Windows.Forms.Timer orologio = new() { Interval = 1000 };
    readonly System.Windows.Forms.Timer ritardoMonitor = new() { Interval = 600 };
    readonly System.Windows.Forms.Timer ricreazione = new() { Interval = 2000 };
    readonly Queue<DateTime> crash = new();
    readonly RegisteredWaitHandle attesaApri;
    FinestraSfondo? sfondo;
    FinestraPrincipale? principale;
    Rettangolo area = new(0, 0, 1920, 1080);
    bool sospeso;
    int tentativiAggancio;
    bool uscita;

    public AppSfondo(bool apriFinestra, EventWaitHandle eventoApri)
    {
        if (SynchronizationContext.Current is not WindowsFormsSynchronizationContext)
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        contesto = SynchronizationContext.Current!;

        log = new LogFile(Percorsi.Log);
        log.Info($"Avvio Energy Italy Sfondo Animato {Application.ProductVersion} su {Environment.OSVersion.VersionString}");
        desktop = new DesktopHost(log);
        iconaApp = CaricaIcona();
        icona = new IconaNotifica(iconaApp);

        var manifest = File.ReadAllText(Percorsi.Manifest);
        var predefinite = File.Exists(Percorsi.PredefiniteAziendali) ? File.ReadAllText(Percorsi.PredefiniteAziendali) : null;
        coord = new Coordinatore(manifest, predefinite, this, this, new ArchivioFile(), this, log, TimeProvider.System);

        icona.Apri += ApriFinestra;
        icona.Successivo += () => coord.Successivo();
        icona.AlternaPausa += () => coord.AlternaPausaManuale();
        icona.ApriLog += ApriCartellaLog;
        icona.Esci += () =>
        {
            log.Info("Uscita richiesta dall'utente");
            Esci();
        };

        messaggi = new FinestraMessaggi();
        messaggi.ExplorerRiavviato += () =>
        {
            log.Info("Explorer riavviato: riaggancio lo sfondo");
            ProgrammaRicreazione(1500);
        };
        messaggi.ChiusuraRichiesta += () =>
        {
            log.Info("Chiusura richiesta da Windows o dall'installer");
            Esci();
        };
        pausa = new GestorePausa(messaggi);
        pausa.Cambiati += segnali => coord.AggiornaSegnali(segnali);

        SystemEvents.DisplaySettingsChanged += MonitorCambiati;
        ritardoMonitor.Tick += (_, _) =>
        {
            ritardoMonitor.Stop();
            coord.AggiornaMonitor(LettoreMonitor.Leggi());
        };
        ricreazione.Tick += (_, _) =>
        {
            ricreazione.Stop();
            _ = CreaSfondoAsync();
        };

        coord.Avvia(LettoreMonitor.Leggi());
        orologio.Tick += (_, _) => coord.Tick();
        orologio.Start();
        pausa.Valuta();
        _ = CreaSfondoAsync();
        if (apriFinestra) ApriFinestra();
        attesaApri = ThreadPool.RegisterWaitForSingleObject(eventoApri,
            (_, _) => contesto.Post(_ => ApriFinestra(), null), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    // ---------- Finestra di sfondo ----------

    async Task CreaSfondoAsync()
    {
        if (uscita) return;
        DistruggiSfondo(ridisegna: false);
        coord.PlayerRicreato();
        var finestra = new FinestraSfondo(log, desktop);
        finestra.MessaggioDalPlayer += json => coord.DaPlayer(json);
        finestra.Persa += crashWebView => contesto.Post(_ => SfondoPerso(finestra, crashWebView), null);
        sfondo = finestra;
        if (!finestra.Crea(area))
        {
            DistruggiSfondo(ridisegna: false);
            if (++tentativiAggancio <= 15) ProgrammaRicreazione(2000);
            else log.Errore("Impossibile agganciare lo sfondo al desktop dopo 15 tentativi");
            return;
        }
        tentativiAggancio = 0;
        try
        {
            await finestra.InizializzaWebAsync();
            if (sospeso && sfondo == finestra) finestra.ImpostaSospensione(true);
        }
        catch (Exception ex)
        {
            log.Errore($"WebView2 dello sfondo non avviata: {ex.Message}");
            SfondoPerso(finestra, crashWebView: true);
        }
    }

    void SfondoPerso(FinestraSfondo finestra, bool crashWebView)
    {
        if (uscita || finestra != sfondo) return;
        if (crashWebView)
        {
            var adesso = DateTime.UtcNow;
            crash.Enqueue(adesso);
            while (crash.Count > 0 && adesso - crash.Peek() > IntervalloCrash) crash.Dequeue();
            if (crash.Count >= 3)
            {
                log.Errore("Lo sfondo si è interrotto 3 volte in 5 minuti: resta spento fino al prossimo avvio");
                DistruggiSfondo(ridisegna: true);
                return;
            }
        }
        ProgrammaRicreazione(2000);
    }

    void ProgrammaRicreazione(int millisecondi)
    {
        ricreazione.Stop();
        ricreazione.Interval = millisecondi;
        ricreazione.Start();
    }

    void DistruggiSfondo(bool ridisegna)
    {
        var finestra = sfondo;
        sfondo = null;
        finestra?.Chiudi(ridisegna);
    }

    void MonitorCambiati(object? mittente, EventArgs e)
    {
        ritardoMonitor.Stop();
        ritardoMonitor.Start();
    }

    void IVistaPlayer.Invia(string json) => sfondo?.Invia(json);

    void IVistaPlayer.ImpostaArea(Rettangolo nuova)
    {
        area = nuova;
        sfondo?.ImpostaArea(nuova);
    }

    void IVistaPlayer.ImpostaSospensione(bool sospesa)
    {
        sospeso = sospesa;
        sfondo?.ImpostaSospensione(sospesa);
    }

    // ---------- Finestra principale ----------

    void IVistaApp.Invia(string json) => principale?.Invia(json);

    void ApriFinestra()
    {
        if (uscita) return;
        if (principale is null || principale.IsDisposed)
            principale = new FinestraPrincipale(log, iconaApp, json => coord.DaApp(json), Importa);
        principale.MostraInPrimoPiano();
    }

    async void Importa(IReadOnlyList<string> percorsi)
    {
        try { await coord.ImportaFileAsync(percorsi); }
        catch (Exception ex) { log.Errore($"Importazione non riuscita: {ex}"); }
    }

    // ---------- Servizi di sistema ----------

    public void ImpostaAvvioConWindows(bool attivo)
    {
#if DEBUG
        log.Info($"Avvio con Windows {(attivo ? "attivo" : "spento")}: ignorato nella build di sviluppo");
#else
        using var chiave = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (attivo) chiave.SetValue(NomeAvvio, $"\"{Environment.ProcessPath}\" --avvio");
        else chiave.DeleteValue(NomeAvvio, throwOnMissingValue: false);
#endif
    }

    public void ApriCartellaLog()
    {
        Directory.CreateDirectory(Percorsi.Log);
        Process.Start(new ProcessStartInfo { FileName = Percorsi.Log, UseShellExecute = true });
    }

    public void AggiornaIcona(string titolo, bool inPausa) => icona.Aggiorna(titolo, inPausa);

    static Icon CaricaIcona()
    {
        using var flusso = typeof(AppSfondo).Assembly.GetManifestResourceStream("icona.ico");
        return flusso is null ? (Icon)SystemIcons.Application.Clone() : new Icon(flusso);
    }

    // ---------- Uscita ----------

    void Esci()
    {
        if (uscita) return;
        uscita = true;
        orologio.Stop();
        ricreazione.Stop();
        ritardoMonitor.Stop();
        attesaApri.Unregister(null);
        SystemEvents.DisplaySettingsChanged -= MonitorCambiati;
        pausa.Dispose();
        messaggi.Dispose();
        principale?.ChiudiDefinitivamente();
        DistruggiSfondo(ridisegna: true);
        icona.Dispose();
        ExitThread();
    }
}
