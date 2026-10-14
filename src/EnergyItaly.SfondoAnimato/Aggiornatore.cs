using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using EnergyItaly.SfondoAnimato.Core;
using Microsoft.Win32;

namespace EnergyItaly.SfondoAnimato;

/// <summary>
/// Controllo, download e installazione degli aggiornamenti (spec 2026-10-14-aggiornamento-automatico,
/// 4.2-4.4). Le regole stanno in <see cref="Aggiornamenti"/>; qui rete, file e processi.
/// Vive sul thread dell'interfaccia: solo l'attesa di rete e disco è asincrona.
/// </summary>
sealed class Aggiornatore : IDisposable
{
    const string ChiaveDisinstallazione = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{6B2E7C1A-4F0D-4C55-9E7B-3A1D2F8C9E10}_is1";
    const long DimensioneMassima = 500L * 1024 * 1024;
    static readonly TimeSpan AttesaInfo = TimeSpan.FromSeconds(30);
    static readonly TimeSpan AttesaInstaller = TimeSpan.FromMinutes(10);
    static readonly TimeSpan DurataMomentoBuono = TimeSpan.FromSeconds(60);

    readonly ILog log;
    readonly Func<bool> momentoBuono;
    readonly HttpClient http;
    readonly DateTimeOffset primoControlloPossibile = DateTimeOffset.Now + Aggiornamenti.RitardoPrimoControllo(Random.Shared);
    readonly string fileStato = Path.Combine(Percorsi.Aggiornamenti, "stato.json");
    StatoAggiornamenti stato;
    bool automatici = true;
    bool inCorso;
    bool installazioneAvviata;
    bool chiusuraSessione;
    DateTimeOffset? inizioMomentoBuono;
    string? errore;
    Version? disponibileInSviluppo;

    /// <summary>Lo stato da mostrare nella finestra è cambiato.</summary>
    public event Action? Cambiato;

    /// <summary>L'installer è partito durante la sessione: il programma deve uscire.</summary>
    public event Action? InstallazioneAvviata;

    public Version InUso { get; } = Aggiornamenti.VersioneDa(Application.ProductVersion) ?? new Version(0, 0, 0);

    /// <summary>Solo la copia installata, nella build Release, scarica e installa (A7).</summary>
    public bool CopiaInstallata { get; } = EInstallata();

    /// <param name="momentoBuono">true se ora l'utente non vede il desktop e la finestra è chiusa (A4).</param>
    public Aggiornatore(ILog log, Func<bool> momentoBuono)
    {
        this.log = log;
        this.momentoBuono = momentoBuono;
        // Proxy di sistema con le credenziali dell'utente: nelle reti aziendali il proxy spesso le chiede.
        http = new HttpClient(new SocketsHttpHandler { DefaultProxyCredentials = CredentialCache.DefaultCredentials })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"EnergyItaly-SfondoAnimato/{InUso.ToString(3)}");
        stato = CopiaInstallata ? Aggiornamenti.LeggiStato(LeggiFile(fileStato)) : new StatoAggiornamenti(null, null, 0);
    }

    static bool EInstallata()
    {
#if DEBUG
        return false;
#else
        try
        {
            using var chiave = Registry.CurrentUser.OpenSubKey(ChiaveDisinstallazione);
            if (chiave?.GetValue("InstallLocation") is not string cartella || cartella.Length == 0) return false;
            return string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(cartella)), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
#endif
    }

    InfoVersione? Pronta => Aggiornamenti.PiuNuova(stato.Pronta, InUso) ? stato.Pronta : null;

    bool TentativiFiniti => stato.Tentativi >= Aggiornamenti.TentativiMassimi;

    bool PuoInstallareDaSolo => automatici && CopiaInstallata && !inCorso && !installazioneAvviata && !chiusuraSessione
        && Pronta is not null && !TentativiFiniti;

    /// <summary>Impostazione "Aggiornamenti automatici" (A9).</summary>
    public void ImpostaAutomatici(bool attivo)
    {
        if (automatici != attivo) log.Info($"Aggiornamento: automatici {(attivo ? "attivi" : "spenti")}");
        automatici = attivo;
        Valuta();
        Cambiato?.Invoke();
    }

    /// <summary>Windows sta chiudendo la sessione o spegnendo: nessuna installazione da qui in poi.</summary>
    public void SessioneInChiusura()
    {
        chiusuraSessione = true;
        inizioMomentoBuono = null;
    }

    /// <summary>
    /// All'avvio, prima di mostrare lo sfondo (4.2 "Avvio"). Pulisce sempre (copia installata) e, se l'avvio
    /// è automatico, installa l'aggiornamento pronto. true = l'installer è partito, il programma deve uscire.
    /// </summary>
    public bool AllAvvio(bool avvioAutomatico)
    {
        if (!CopiaInstallata) return false;
        foreach (var parziale in ElencaParziali()) CancellaFile(parziale);
        if (stato.Pronta is { } p)
        {
            if (!Aggiornamenti.PiuNuova(p, InUso))
            {
                if (p.Versione == InUso) log.Info($"Aggiornamento: installata la versione {InUso.ToString(3)}");
                Dimentica(p);
            }
            else if (stato.Tentativi > 0)
            {
                log.Avviso($"Aggiornamento: installazione della {p.Versione.ToString(3)} non riuscita (tentativo {stato.Tentativi}), dettagli in installazione.log");
            }
        }
        if (!avvioAutomatico || !PuoInstallareDaSolo) return false;
        return Installa(riapri: false);
    }

    /// <summary>Da chiamare ogni secondo: controllo programmato e installazione dopo 60 secondi di momento buono.</summary>
    public void Tick()
    {
        if (installazioneAvviata) return;
        if (automatici && !inCorso && DateTimeOffset.Now >= Aggiornamenti.ProssimoControllo(primoControlloPossibile, stato.UltimoControllo))
        {
            _ = ControllaAsync();
            return;
        }
        if (inizioMomentoBuono is { } inizio && DateTimeOffset.Now - inizio >= DurataMomentoBuono)
        {
            inizioMomentoBuono = null;
            if (PuoInstallareDaSolo && momentoBuono() && Installa(riapri: false)) InstallazioneAvviata?.Invoke();
        }
    }

    /// <summary>Segnali di pausa o finestra cambiati: comincia o annulla l'attesa di 60 secondi (A4).</summary>
    public void Valuta()
    {
        if (PuoInstallareDaSolo && momentoBuono()) inizioMomentoBuono ??= DateTimeOffset.Now;
        else inizioMomentoBuono = null;
    }

    /// <summary>"Installa ora" dalla finestra: subito, e il programma riparte con la finestra aperta.</summary>
    public void InstallaOra()
    {
        if (!CopiaInstallata || inCorso || installazioneAvviata || Pronta is null) return;
        if (Installa(riapri: true)) InstallazioneAvviata?.Invoke();
    }

    /// <summary>Controllo (programmato o da "Controlla ora"). Un solo controllo alla volta.</summary>
    public async Task ControllaAsync()
    {
        if (inCorso || installazioneAvviata) return;
        inCorso = true;
        inizioMomentoBuono = null;
        errore = null;
        Cambiato?.Invoke();
        try
        {
            var info = await LeggiInfoAsync();
            if (!Aggiornamenti.PiuNuova(info, InUso))
            {
                disponibileInSviluppo = null;
                log.Info($"Aggiornamento: nessuna versione più nuova della {InUso.ToString(3)}");
                if (CopiaInstallata && stato.Pronta is { } superata) Dimentica(superata);
            }
            else if (!CopiaInstallata)
            {
                // Copia di sviluppo: si dice soltanto che c'è (A7), senza scaricare né scrivere nulla.
                disponibileInSviluppo = info!.Versione;
                log.Info($"Aggiornamento: disponibile la {info.Versione.ToString(3)} (copia di sviluppo, non si scarica)");
            }
            else if (stato.Pronta == info && File.Exists(PercorsoInstaller(info!)))
            {
                log.Info($"Aggiornamento: la {info!.Versione.ToString(3)} è già scaricata");
            }
            else
            {
                await ScaricaAsync(info!);
            }
        }
        catch (Exception ex)
        {
            errore = Breve(ex);
            log.Avviso($"Aggiornamento: controllo non riuscito ({ex.Message})");
        }
        finally
        {
            inCorso = false;
            // Anche se non è riuscito: si riprova al turno successivo, non a raffica.
            stato = stato with { UltimoControllo = DateTimeOffset.Now };
            SalvaStato();
            Valuta();
            Cambiato?.Invoke();
        }
    }

    async Task<InfoVersione?> LeggiInfoAsync()
    {
        using var limite = new CancellationTokenSource(AttesaInfo);
        using var risposta = await http.GetAsync(Aggiornamenti.IndirizzoInfo, limite.Token);
        ControllaIndirizzo(risposta);
        risposta.EnsureSuccessStatusCode();
        var json = await risposta.Content.ReadAsStringAsync(limite.Token);
        return Aggiornamenti.Leggi(json) ?? throw new InvalidDataException("versione.json non valido");
    }

    async Task ScaricaAsync(InfoVersione info)
    {
        Directory.CreateDirectory(Percorsi.Aggiornamenti);
        var finale = PercorsoInstaller(info);
        var parziale = finale + ".part";
        log.Info($"Aggiornamento: scarico la versione {info.Versione.ToString(3)}");
        try
        {
            using var limite = new CancellationTokenSource(AttesaInstaller);
            using (var risposta = await http.GetAsync(Aggiornamenti.IndirizzoInstaller(info), HttpCompletionOption.ResponseHeadersRead, limite.Token))
            {
                ControllaIndirizzo(risposta);
                risposta.EnsureSuccessStatusCode();
                if (risposta.Content.Headers.ContentLength > DimensioneMassima) throw new InvalidDataException("installer troppo grande");
                await using var origine = await risposta.Content.ReadAsStreamAsync(limite.Token);
                await using var destinazione = File.Create(parziale);
                await origine.CopyToAsync(destinazione, limite.Token);
            }
            if (!await Task.Run(() => ImprontaGiusta(parziale, info)))
                throw new InvalidDataException("impronta SHA-256 diversa da versione.json");
            File.Move(parziale, finale, overwrite: true);
        }
        finally
        {
            CancellaFile(parziale, silenzioso: true);
        }
        if (stato.Pronta is { } vecchia && vecchia.File != info.File) CancellaFile(PercorsoInstaller(vecchia));
        stato = stato with { Pronta = info, Tentativi = 0 };
        SalvaStato();
        log.Info($"Aggiornamento: versione {info.Versione.ToString(3)} scaricata e verificata");
    }

    bool Installa(bool riapri)
    {
        var info = Pronta!;
        var percorso = PercorsoInstaller(info);
        try
        {
            if (!File.Exists(percorso) || !ImprontaGiusta(percorso, info))
            {
                log.Avviso($"Aggiornamento: installer della {info.Versione.ToString(3)} mancante o alterato, lo riscarico al prossimo controllo");
                Dimentica(info);
                Cambiato?.Invoke();
                return false;
            }
            stato = stato with { Tentativi = stato.Tentativi + 1 };
            SalvaStato();
            var registro = Path.Combine(Percorsi.Aggiornamenti, "installazione.log");
            var argomenti = $"/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /LOG=\"{registro}\"" + (riapri ? " /APRI=1" : "");
            log.Info($"Aggiornamento: avvio l'installazione della versione {info.Versione.ToString(3)} (tentativo {stato.Tentativi})");
            Process.Start(new ProcessStartInfo(percorso, argomenti) { UseShellExecute = false, WorkingDirectory = Percorsi.Aggiornamenti });
            installazioneAvviata = true;
            inizioMomentoBuono = null;
            return true;
        }
        catch (Exception ex)
        {
            errore = Breve(ex);
            log.Errore($"Aggiornamento: installazione non avviata ({ex.Message})");
            Cambiato?.Invoke();
            return false;
        }
    }

    /// <summary>Il messaggio "aggiornamenti" per la finestra (spec 4.6).</summary>
    public string MessaggioVista()
    {
        string s = inCorso ? "controllo"
            : !CopiaInstallata ? "sviluppo"
            : errore is not null ? "errore"
            : Pronta is not null && TentativiFiniti ? "fallito"
            : Pronta is not null ? "pronto"
            : "aggiornato";
        var nuova = CopiaInstallata ? Pronta?.Versione : disponibileInSviluppo;
        return Aggiornamenti.MessaggioVista(InUso, s, stato.UltimoControllo, nuova, errore);
    }

    static void ControllaIndirizzo(HttpResponseMessage risposta)
    {
        var finale = risposta.RequestMessage?.RequestUri;
        if (finale is null || !Aggiornamenti.IndirizzoAffidabile(finale))
            throw new InvalidDataException($"indirizzo non previsto ({finale?.Host})");
    }

    static bool ImprontaGiusta(string percorso, InfoVersione info)
    {
        using var flusso = File.OpenRead(percorso);
        return Convert.ToHexStringLower(SHA256.HashData(flusso)) == info.Sha256;
    }

    static string PercorsoInstaller(InfoVersione info) => Path.Combine(Percorsi.Aggiornamenti, info.File);

    static IEnumerable<string> ElencaParziali()
    {
        try { return Directory.Exists(Percorsi.Aggiornamenti) ? Directory.GetFiles(Percorsi.Aggiornamenti, "*.part") : []; }
        catch { return []; }
    }

    void Dimentica(InfoVersione info)
    {
        CancellaFile(PercorsoInstaller(info));
        stato = stato with { Pronta = null, Tentativi = 0 };
        SalvaStato();
    }

    void CancellaFile(string percorso, bool silenzioso = false)
    {
        try { File.Delete(percorso); }
        catch (Exception ex) { if (!silenzioso) log.Avviso($"Aggiornamento: {Path.GetFileName(percorso)} non cancellato ({ex.Message})"); }
    }

    // Scrittura atomica: un file temporaneo poi spostato sopra quello vecchio (un'interruzione non lo lascia a metà).
    void SalvaStato()
    {
        if (!CopiaInstallata) return;
        try
        {
            Directory.CreateDirectory(Percorsi.Aggiornamenti);
            var temporaneo = fileStato + ".tmp";
            File.WriteAllText(temporaneo, Aggiornamenti.ScriviStato(stato));
            File.Move(temporaneo, fileStato, overwrite: true);
        }
        catch (Exception ex) { log.Avviso($"Aggiornamento: stato.json non salvato ({ex.Message})"); }
    }

    static string? LeggiFile(string percorso)
    {
        try { return File.Exists(percorso) ? File.ReadAllText(percorso) : null; }
        catch { return null; }
    }

    // Testo breve per la finestra; il dettaglio va nel registro.
    static string Breve(Exception ex) => ex switch
    {
        HttpRequestException { StatusCode: HttpStatusCode.NotFound } => "nessuna versione pubblicata",
        HttpRequestException => "rete non raggiungibile",
        OperationCanceledException => "tempo scaduto",
        InvalidDataException d => d.Message,
        IOException => "errore sul disco",
        _ => "errore imprevisto"
    };

    public void Dispose() => http.Dispose();
}
