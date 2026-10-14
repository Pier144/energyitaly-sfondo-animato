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
    readonly Aggiornatore aggiornatore;
    readonly System.Windows.Forms.Timer orologio = new() { Interval = 1000 };
    readonly System.Windows.Forms.Timer ritardoMonitor = new() { Interval = 600 };
    readonly System.Windows.Forms.Timer ricreazione = new() { Interval = 2000 };
    readonly Queue<DateTime> crash = new();
    // null solo se all'avvio parte un aggiornamento e il programma esce subito.
    readonly RegisteredWaitHandle? attesaApri;
    FinestraSfondo? sfondo;
    FinestraPrincipale? principale;
    Rettangolo area = new(0, 0, 1920, 1080);
    bool sospeso;
    bool sfondoInterrotto;
    SegnaliSistema segnali = new();
    int tentativiAggancio;
    bool uscita;

    /// <param name="dopoAggiornamento">Rilanciato dall'installer dopo un aggiornamento non riuscito: non si ritenta subito.</param>
    public AppSfondo(bool apriFinestra, EventWaitHandle eventoApri, bool dopoAggiornamento)
    {
        if (SynchronizationContext.Current is not WindowsFormsSynchronizationContext)
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        contesto = SynchronizationContext.Current!;

        log = new LogFile(Percorsi.Log);
        log.Info($"Avvio Energy Italy Sfondo Animato {Application.ProductVersion} su {Environment.OSVersion.VersionString}");
        desktop = new DesktopHost(log);
        iconaApp = CaricaIcona();
        icona = new IconaNotifica(iconaApp);

        // Prima del coordinatore: all'avvio chiama ImpostaAggiornamentiAutomatici.
        aggiornatore = new Aggiornatore(log, () => Aggiornamenti.MomentoBuono(segnali, FinestraVista));
        aggiornatore.Cambiato += InviaAggiornamenti;
        aggiornatore.InstallazioneAvviata += () => contesto.Post(_ =>
        {
            log.Info("Uscita per aggiornamento");
            Esci();
        }, null);
        SystemEvents.SessionEnding += SessioneInChiusura;

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
            aggiornatore.SessioneInChiusura();
            Esci();
        };
        pausa = new GestorePausa(messaggi);
        pausa.Cambiati += nuovi =>
        {
            segnali = nuovi;
            InviaSegnali();
            aggiornatore.Valuta();
        };

        SystemEvents.DisplaySettingsChanged += MonitorCambiati;
        messaggi.MonitorCambiati += () => MonitorCambiati(null, EventArgs.Empty);
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
        // Aggiornamento scaricato in una sessione precedente: all'accesso a Windows si installa prima di
        // mostrare lo sfondo (A5). L'uscita va in coda: parte appena Application.Run avvia il ciclo dei messaggi.
        if (aggiornatore.AllAvvio(avvioAutomatico: !apriFinestra && !dopoAggiornamento))
        {
            contesto.Post(_ => Esci(), null);
            return;
        }
        orologio.Tick += (_, _) =>
        {
            coord.Tick();
            aggiornatore.Tick();
        };
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
        if (uscita || sfondoInterrotto) return;
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
                // Spec 9: pausa profonda, così la finestra mostra il motivo e la rotazione non manda
                // sfondi a un player che non c'è. Resta così fino al prossimo avvio del programma.
                log.Errore("Lo sfondo si è interrotto 3 volte in 5 minuti: pausa profonda fino al prossimo avvio");
                sfondoInterrotto = true;
                ricreazione.Stop();
                DistruggiSfondo(ridisegna: true);
                InviaSegnali();
                return;
            }
        }
        ProgrammaRicreazione(2000);
    }

    void InviaSegnali() => coord.AggiornaSegnali(segnali with { SfondoInterrotto = sfondoInterrotto });

    void ProgrammaRicreazione(int millisecondi)
    {
        if (sfondoInterrotto) return;
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
        {
            principale = new FinestraPrincipale(log, iconaApp, json => coord.DaApp(json), Importa, ComandoAggiornamenti);
            // Chiusa o ridotta a icona: l'aggiornatore può contare su un momento buono (A4).
            principale.VisibleChanged += (_, _) => aggiornatore.Valuta();
            principale.Resize += (_, _) => aggiornatore.Valuta();
        }
        principale.MostraInPrimoPiano();
    }

    bool FinestraVista => principale is { IsDisposed: false } p && p.Vista;

    void InviaAggiornamenti() => principale?.Invia(aggiornatore.MessaggioVista());

    async void ComandoAggiornamenti(string tipo)
    {
        try
        {
            switch (tipo)
            {
                case "controlla": await aggiornatore.ControllaAsync(); break;
                case "installa": aggiornatore.InstallaOra(); break;
                case "pronta": InviaAggiornamenti(); break;
            }
        }
        catch (Exception ex) { log.Errore($"Aggiornamento: comando '{tipo}' non riuscito: {ex.Message}"); }
    }

    void SessioneInChiusura(object? mittente, SessionEndingEventArgs e) => aggiornatore.SessioneInChiusura();

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

    public void ImpostaAggiornamentiAutomatici(bool attivo) => aggiornatore.ImpostaAutomatici(attivo);

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
        attesaApri?.Unregister(null);
        SystemEvents.DisplaySettingsChanged -= MonitorCambiati;
        SystemEvents.SessionEnding -= SessioneInChiusura;
        pausa.Dispose();
        messaggi.Dispose();
        principale?.ChiudiDefinitivamente();
        DistruggiSfondo(ridisegna: true);
        aggiornatore.Dispose();
        icona.Dispose();
        ExitThread();
    }
}
