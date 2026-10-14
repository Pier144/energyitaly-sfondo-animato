namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>
/// Tiene lo stato unico del programma e decide cosa mandare a player e finestra.
/// Tutti i metodi vanno chiamati dallo stesso thread (il thread dell'interfaccia).
/// </summary>
public sealed class Coordinatore
{
    readonly string manifestJson;
    readonly string? predefiniteJson;
    readonly IVistaPlayer player;
    readonly IVistaApp app;
    readonly IArchivio archivio;
    readonly ISistema sistema;
    readonly ILog log;
    readonly TimeProvider tempo;
    readonly MotoreRotazione motore;

    DatiLibreria libreria = new();
    IReadOnlyList<Voce> voci = [];
    Impostazioni predefinite = new();
    Impostazioni imp = new();
    IReadOnlyList<Monitor> monitor = [];
    Disposizione disposizione = LayoutMonitor.Calcola([], ModoMonitor.Estendi);
    SegnaliSistema segnali = new();
    DecisionePausa decisione = DecisionePausa.Nessuna;
    bool playerPronto;
    int erroriDiFila;

    public Coordinatore(string manifestJson, string? predefiniteJson, IVistaPlayer player, IVistaApp app,
        IArchivio archivio, ISistema sistema, ILog log, TimeProvider tempo, Random? casuale = null)
    {
        this.manifestJson = manifestJson;
        this.predefiniteJson = predefiniteJson;
        this.player = player;
        this.app = app;
        this.archivio = archivio;
        this.sistema = sistema;
        this.log = log;
        this.tempo = tempo;
        motore = new MotoreRotazione(tempo, casuale);
    }

    public Impostazioni Impostazioni => imp;
    public Voce? Corrente => motore.Corrente;
    public bool InPausaManuale => motore.InPausaManuale;
    public DecisionePausa Pausa => decisione;
    public Disposizione Disposizione => disposizione;

    // ---------- Avvio ----------

    public void Avvia(IReadOnlyList<Monitor> monitorIniziali)
    {
        var avvisi = new List<string>();
        libreria = Catalogo.LeggiLibreria(Leggi(archivio.LeggiLibreria, "libreria.json"), avvisi, out var illeggibile);
        if (illeggibile)
        {
            log.Errore("libreria.json illeggibile: ne conservo una copia e riparto con una libreria vuota");
            try { archivio.ConservaLibreriaIlleggibile(); }
            catch (Exception ex) { log.Errore($"Copia di libreria.json non riuscita: {ex.Message}"); }
        }
        RiprovaCancellazioni();
        RicaricaVoci(avvisi);
        var idValidi = voci.Select(v => v.Id).ToList();
        var playlistDiBase = voci.Where(v => v.Aziendale).Select(v => v.Id).ToList();
        predefinite = ArchivioImpostazioni.Predefinite(predefiniteJson, idValidi, playlistDiBase, avvisi);
        imp = ArchivioImpostazioni.Carica(Leggi(archivio.LeggiImpostazioni, "impostazioni.json"), predefinite, idValidi, avvisi);
        foreach (var a in avvisi) log.Avviso(a);

        monitor = monitorIniziali;
        disposizione = LayoutMonitor.Calcola(monitor, imp.ModoMonitor);
        log.Info(DescriviMonitor());
        player.ImpostaArea(disposizione.Finestra);

        motore.AggiornaVoci(voci, imp.Playlist);
        motore.AggiornaRegole(Regole(), riavvia: true);
        motore.Avvia(imp.UltimaVoce);
        if (motore.Corrente is { } c) log.Info($"Primo sfondo: {c.Titolo}");
        else log.Errore("Nessuno sfondo disponibile da mostrare");

        try { sistema.ImpostaAvvioConWindows(imp.AvvioConWindows); }
        catch (Exception ex) { log.Errore($"Avvio con Windows non impostato: {ex.Message}"); }
        ImpostaAggiornamenti();
        SalvaUltima();
        AggiornaIcona();
    }

    string? Leggi(Func<string?> lettura, string nome)
    {
        try { return lettura(); }
        catch (Exception ex)
        {
            log.Errore($"{nome} non leggibile: {ex.Message}");
            return null;
        }
    }

    // ---------- Tempo ----------

    /// <summary>Da chiamare ogni secondo: cambia sfondo se il tempo è scaduto e non c'è pausa.</summary>
    public void Tick()
    {
        if (decisione.Livello == LivelloPausa.Nessuna && motore.Scaduta) Avanti();
    }

    public void Successivo() => Avanti();

    public void AlternaPausaManuale() => ImpostaPausaManuale(!motore.InPausaManuale);

    void Avanti()
    {
        var id = motore.Prossima();
        if (id is not null) MostraVoce(id);
    }

    void MostraVoce(string id)
    {
        if (!motore.Mostra(id))
        {
            InviaAvviso("Questo sfondo non è disponibile");
            return;
        }
        DopoCambio();
    }

    void DopoCambio()
    {
        SalvaUltima();
        MandaMostra(imp.DissolvenzaSecondi * 1000);
        if (motore.Corrente is { } c) log.Info($"Sfondo: {c.Titolo}");
        AggiornaIcona();
        InviaStato();
    }

    // ---------- Player ----------

    public void PlayerRicreato() => playerPronto = false;

    public void DaPlayer(string json)
    {
        var e = Messaggi.LeggiEventoPlayer(json);
        if (e is null)
        {
            log.Avviso("Messaggio dal player non riconosciuto");
            return;
        }
        switch (e.Tipo)
        {
            case "pronto":
                playerPronto = true;
                MandaMostra(0);
                if (decisione.Livello != LivelloPausa.Nessuna) player.Invia(Messaggi.Semplice("pausa"));
                break;
            case "mostrato":
                erroriDiFila = 0;
                break;
            case "errore":
                GestisciErrore(e);
                break;
            case "videoFinito":
                if (e.Id is not null && decisione.Livello == LivelloPausa.Nessuna && motore.DeveAvanzareDopoVideo(e.Id)) Avanti();
                break;
            case "durataVideo":
                AggiornaDurata(e);
                break;
            default:
                log.Avviso($"Evento del player non riconosciuto: {e.Tipo}");
                break;
        }
    }

    void GestisciErrore(EventoPlayer e)
    {
        log.Errore($"Lo sfondo '{e.Id}' non si carica: {e.Messaggio}");
        if (e.Id is null || e.Id != motore.Corrente?.Id) return;
        erroriDiFila++;
        if (erroriDiFila >= Math.Max(1, motore.Playlist.Count))
        {
            log.Errore("Nessuno sfondo della playlist si carica: resto sull'ultimo");
            return;
        }
        Avanti();
    }

    void AggiornaDurata(EventoPlayer e)
    {
        if (e.Id is null || e.Secondi is not > 0) return;
        var voce = libreria.Voci.Find(v => v.Id == e.Id);
        int secondi = (int)Math.Round(e.Secondi.Value);
        if (voce is null || voce.DurataSecondi == secondi) return;
        voce.DurataSecondi = secondi;
        SalvaLibreria();
        RicaricaVoci(null);
        motore.AggiornaVoci(voci, imp.Playlist);
        InviaStato();
    }

    void MandaMostra(int dissolvenzaMs)
    {
        if (!playerPronto || motore.Corrente is not { } c) return;
        bool fineVideo = imp.FineVideo && imp.RotazioneAttiva;
        player.Invia(Messaggi.Mostra(c, disposizione, dissolvenzaMs, fineVideo));
    }

    // ---------- Finestra principale ----------

    public void DaApp(string json)
    {
        var c = Messaggi.LeggiComandoApp(json);
        if (c is null)
        {
            log.Avviso("Comando della finestra non riconosciuto");
            return;
        }
        switch (c.Tipo)
        {
            case "pronta": InviaStato(); break;
            case "mostra": if (c.Id is not null) MostraVoce(c.Id); break;
            case "successivo": Avanti(); break;
            case "precedente": if (motore.TornaIndietro()) DopoCambio(); break;
            case "pausa": ImpostaPausaManuale(true); break;
            case "riprendi": ImpostaPausaManuale(false); break;
            case "impostazioni": if (c.Modifiche is not null) CambiaImpostazioni(c.Modifiche); break;
            case "playlist": CambiaPlaylist(c.Ids ?? []); break;
            case "elimina": if (c.Id is not null) Elimina(c.Id); break;
            case "apriLog": sistema.ApriCartellaLog(); break;
            case "ripristina": Ripristina(); break;
            default: log.Avviso($"Comando della finestra non riconosciuto: {c.Tipo}"); break;
        }
    }

    void ImpostaPausaManuale(bool attiva)
    {
        if (attiva == motore.InPausaManuale) return;
        if (attiva) motore.PausaManuale(); else motore.RiprendiManuale();
        log.Info(attiva ? "Pausa manuale" : "Ripresa manuale");
        RivalutaPausa();
        AggiornaIcona();
        InviaStato();
    }

    void CambiaImpostazioni(ModificheImpostazioni m)
    {
        var (nuova, errore) = imp.Applica(m);
        if (errore is not null)
        {
            InviaAvviso(errore);
            InviaStato();
            return;
        }
        var vecchia = imp;
        imp = nuova;
        SalvaImpostazioni();

        bool tempoCambiato = vecchia.Intervallo != nuova.Intervallo || vecchia.FineVideo != nuova.FineVideo;
        if (tempoCambiato || vecchia.Ordine != nuova.Ordine) motore.AggiornaRegole(Regole(), riavvia: tempoCambiato);
        if (vecchia.ModoMonitor != nuova.ModoMonitor) RicalcolaDisposizione(conDissolvenza: true);
        else if (vecchia.FineVideo != nuova.FineVideo || vecchia.RotazioneAttiva != nuova.RotazioneAttiva)
        {
            if (motore.Corrente?.Tipo == TipoVoce.Video) MandaMostra(0);
        }
        if (vecchia.PausaABatteria != nuova.PausaABatteria) RivalutaPausa();
        if (vecchia.AvvioConWindows != nuova.AvvioConWindows)
        {
            try { sistema.ImpostaAvvioConWindows(nuova.AvvioConWindows); }
            catch (Exception ex) { log.Errore($"Avvio con Windows non impostato: {ex.Message}"); }
        }
        if (vecchia.AggiornamentiAutomatici != nuova.AggiornamentiAutomatici) ImpostaAggiornamenti();
        InviaStato();
    }

    void ImpostaAggiornamenti()
    {
        try { sistema.ImpostaAggiornamentiAutomatici(imp.AggiornamentiAutomatici); }
        catch (Exception ex) { log.Errore($"Aggiornamenti automatici non impostati: {ex.Message}"); }
    }

    void CambiaPlaylist(IReadOnlyList<string> ids)
    {
        var valide = ids.Distinct().Where(id => voci.Any(v => v.Id == id)).ToList();
        if (valide.Count == 0)
        {
            InviaAvviso("Lascia almeno uno sfondo nella playlist");
            InviaStato();
            return;
        }
        imp = imp with { Playlist = valide };
        SalvaImpostazioni();
        motore.AggiornaVoci(voci, valide);
        InviaStato();
    }

    void Elimina(string id)
    {
        var voce = libreria.Voci.Find(v => v.Id == id);
        if (voce is null)
        {
            InviaAvviso("Si possono eliminare solo gli sfondi importati");
            return;
        }
        try { archivio.CancellaDaLibreria(voce.File); }
        catch (Exception ex)
        {
            // Spesso è un video ancora aperto dal player: si riprova al prossimo avvio.
            log.Avviso($"File '{voce.File}' non cancellato ({ex.Message}): riprovo al prossimo avvio");
            libreria.DaCancellare.Add(voce.File);
        }
        libreria.Voci.Remove(voce);
        SalvaLibreria();
        RicaricaVoci(null);

        var playlist = imp.Playlist.Where(x => x != id).ToList();
        if (playlist.Count == 0) playlist = predefinite.Playlist.ToList();
        imp = imp with { Playlist = playlist };
        SalvaImpostazioni();
        motore.AggiornaVoci(voci, playlist);
        log.Info($"Eliminato dalla libreria: {voce.Titolo}");
        InviaAvviso($"Eliminato dalla libreria: {voce.Titolo}");
        if (motore.Corrente?.Id == id) Avanti();
        else InviaStato();
    }

    void Ripristina()
    {
        imp = predefinite with { UltimaVoce = imp.UltimaVoce };
        SalvaImpostazioni();
        motore.AggiornaVoci(voci, imp.Playlist);
        motore.AggiornaRegole(Regole(), riavvia: true);
        try { sistema.ImpostaAvvioConWindows(imp.AvvioConWindows); }
        catch (Exception ex) { log.Errore($"Avvio con Windows non impostato: {ex.Message}"); }
        ImpostaAggiornamenti();
        RicalcolaDisposizione(conDissolvenza: true);
        RivalutaPausa();
        InviaAvviso("Impostazioni aziendali ripristinate");
        InviaStato();
    }

    // ---------- Importazione ----------

    /// <summary>Soglia oltre la quale avvisa che la copia è in corso (i video possono essere grandi).</summary>
    const long SogliaAvvisoImport = 20L * 1024 * 1024;

    /// <summary>
    /// Importa i file nella libreria. La copia è asincrona: l'interfaccia, la rotazione e il player
    /// restano reattivi; il proseguimento torna sul thread chiamante.
    /// </summary>
    public async Task ImportaFileAsync(IReadOnlyList<string> percorsi)
    {
        var aggiunti = new List<string>();
        var scartati = new List<string>();
        var validi = new List<(string Percorso, string Nome, EsitoImport Esito, long Dimensione)>();
        foreach (var percorso in percorsi)
        {
            var nome = Importazione.NomeFile(percorso);
            long dimensione;
            try { dimensione = archivio.Dimensione(percorso); }
            catch (Exception ex)
            {
                log.Avviso($"Importazione: '{percorso}' non leggibile ({ex.Message})");
                scartati.Add($"{nome}: file non leggibile");
                continue;
            }
            var esito = Importazione.Valuta(nome, dimensione);
            if (esito.Valido) validi.Add((percorso, nome, esito, dimensione));
            else scartati.Add($"{nome}: {esito.Motivo}");
        }

        if (validi.Sum(x => x.Dimensione) > SogliaAvvisoImport) InviaAvviso($"Importazione in corso: {validi.Count} file");
        foreach (var (percorso, nome, esito, _) in validi)
        {
            try { await archivio.CopiaInLibreriaAsync(percorso, esito.NomeFile); }
            catch (Exception ex)
            {
                log.Errore($"Importazione: copia di '{percorso}' non riuscita ({ex.Message})");
                scartati.Add($"{nome}: copia non riuscita");
                continue;
            }
            libreria.Voci.Add(new VoceLibreria
            {
                Id = esito.Id,
                Tipo = NomiJson.Di(esito.Tipo),
                Titolo = esito.Titolo,
                File = esito.NomeFile,
                ImportataIl = tempo.GetUtcNow()
            });
            aggiunti.Add(esito.Titolo);
            log.Info($"Importato '{nome}' come {esito.NomeFile}");
        }

        if (aggiunti.Count > 0)
        {
            SalvaLibreria();
            RicaricaVoci(null);
            motore.AggiornaVoci(voci, imp.Playlist);
        }
        var testo = aggiunti.Count switch
        {
            0 => "",
            1 => $"Aggiunto alla libreria: {aggiunti[0]}",
            _ => $"{aggiunti.Count} sfondi aggiunti alla libreria"
        };
        if (scartati.Count > 0)
        {
            var scarto = scartati.Count == 1 ? scartati[0] : $"{scartati.Count} file non importati. {scartati[0]}";
            testo = testo.Length > 0 ? $"{testo}. {scarto}" : scarto;
        }
        if (testo.Length > 0) InviaAvviso(testo);
        InviaStato();
    }

    // ---------- Monitor ----------

    public void AggiornaMonitor(IReadOnlyList<Monitor> nuovi)
    {
        monitor = nuovi;
        RicalcolaDisposizione(conDissolvenza: false);
    }

    void RicalcolaDisposizione(bool conDissolvenza)
    {
        var nuova = LayoutMonitor.Calcola(monitor, imp.ModoMonitor);
        if (Uguale(nuova, disposizione)) return;
        bool modoCambiato = nuova.ModoEffettivo != disposizione.ModoEffettivo;
        disposizione = nuova;
        log.Info(DescriviMonitor());
        player.ImpostaArea(nuova.Finestra);
        if (modoCambiato) MandaMostra(conDissolvenza ? imp.DissolvenzaSecondi * 1000 : 0);
        else if (playerPronto) player.Invia(Messaggi.Layout(nuova));
        InviaStato();
    }

    static bool Uguale(Disposizione a, Disposizione b) =>
        a.Finestra == b.Finestra && a.ModoEffettivo == b.ModoEffettivo && a.Monitor.SequenceEqual(b.Monitor);

    string DescriviMonitor()
    {
        var f = disposizione.Finestra;
        var elenco = string.Join(", ", disposizione.Monitor.Select(m =>
            $"{m.Nome} {m.Area.L}x{m.Area.A} in ({f.X + m.Area.X},{f.Y + m.Area.Y}){(m.Principale ? " principale" : "")}"));
        return $"Monitor: {elenco}; finestra {f.L}x{f.A} da ({f.X},{f.Y}); modo {NomiJson.Di(disposizione.ModoEffettivo)}";
    }

    // ---------- Pausa ----------

    public void AggiornaSegnali(SegnaliSistema nuovi)
    {
        if (nuovi == segnali) return;
        segnali = nuovi;
        RivalutaPausa();
        InviaStato();
    }

    void RivalutaPausa()
    {
        var nuova = RegolePausa.Valuta(segnali, imp.PausaABatteria, motore.InPausaManuale);
        if (nuova == decisione) return;
        var vecchia = decisione;
        decisione = nuova;
        log.Info(nuova.Livello == LivelloPausa.Nessuna ? "Sfondo ripreso" : $"Pausa {nuova.Livello.ToString().ToLowerInvariant()}: {nuova.Motivo}");

        switch (nuova.Livello)
        {
            case LivelloPausa.Profonda:
                player.Invia(Messaggi.Semplice("pausa"));
                player.ImpostaSospensione(true);
                break;
            case LivelloPausa.Leggera:
                if (vecchia.Livello == LivelloPausa.Profonda) player.ImpostaSospensione(false);
                player.Invia(Messaggi.Semplice("pausa"));
                break;
            default:
                if (vecchia.Livello == LivelloPausa.Profonda) player.ImpostaSospensione(false);
                player.Invia(Messaggi.Semplice("riprendi"));
                Tick();
                break;
        }
    }

    // ---------- Supporto ----------

    RegoleRotazione Regole() => new(imp.Intervallo, imp.FineVideo, imp.Ordine);

    void RiprovaCancellazioni()
    {
        if (libreria.DaCancellare.Count == 0) return;
        var rimasti = new List<string>();
        foreach (var nome in libreria.DaCancellare)
        {
            try
            {
                archivio.CancellaDaLibreria(nome);
                log.Info($"Cancellato il file rimasto dalla libreria: {nome}");
            }
            catch (Exception ex)
            {
                log.Avviso($"File '{nome}' ancora non cancellabile: {ex.Message}");
                rimasti.Add(nome);
            }
        }
        libreria.DaCancellare = rimasti;
        SalvaLibreria();
    }

    void RicaricaVoci(List<string>? avvisi)
    {
        var (nuove, a) = Catalogo.Carica(manifestJson, libreria, nome =>
        {
            try { return archivio.EsisteInLibreria(nome); }
            catch { return false; }
        });
        voci = nuove;
        if (avvisi is not null) avvisi.AddRange(a);
    }

    void SalvaUltima()
    {
        if (motore.Corrente is null) return;
        imp = imp with { UltimaVoce = motore.Corrente.Id };
        SalvaImpostazioni();
    }

    void SalvaImpostazioni()
    {
        try { archivio.ScriviImpostazioni(ArchivioImpostazioni.Scrivi(imp)); }
        catch (Exception ex) { log.Errore($"impostazioni.json non salvato: {ex.Message}"); }
    }

    void SalvaLibreria()
    {
        try { archivio.ScriviLibreria(Catalogo.ScriviLibreria(libreria)); }
        catch (Exception ex) { log.Errore($"libreria.json non salvato: {ex.Message}"); }
    }

    void AggiornaIcona() =>
        sistema.AggiornaIcona(motore.Corrente?.Titolo ?? "Energy Italy Sfondo Animato", motore.InPausaManuale);

    void InviaAvviso(string testo) => app.Invia(Messaggi.Avviso(testo));

    void InviaStato() => app.Invia(Messaggi.Stato(CreaStato()));

    StatoVista CreaStato() => new(
        voci,
        imp,
        motore.Corrente?.Id,
        motore.StoricoVuoto,
        decisione.Livello != LivelloPausa.Nessuna,
        motore.InPausaManuale,
        decisione.Motivo is MotivoPausa.Nessuno or MotivoPausa.Manuale ? null : RegolePausa.Testo(decisione.Motivo),
        Millisecondi(motore.Restante),
        Millisecondi(motore.Totale),
        motore.Totale is not null && !motore.InPausaManuale,
        motore.AttendeFineVideo,
        disposizione);

    static long? Millisecondi(TimeSpan? t) => t is null ? null : (long)t.Value.TotalMilliseconds;
}
