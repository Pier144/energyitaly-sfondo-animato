namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Regole di cambio: intervallo (null = "Mai"), fine video, ordine.</summary>
public sealed record RegoleRotazione(TimeSpan? Intervallo, bool FineVideo, Ordine Ordine);

/// <summary>
/// Decide quale sfondo mostrare e quando cambiarlo. La scadenza è un istante assoluto:
/// le pause automatiche non la spostano (RF-23), la pausa manuale sì (decisione D8).
/// </summary>
public sealed class MotoreRotazione
{
    /// <summary>Margine oltre la durata di un video prima di cambiare comunque (rete di sicurezza).</summary>
    public static readonly TimeSpan MargineVideo = TimeSpan.FromSeconds(10);
    public const int MassimoStorico = 20;

    readonly TimeProvider tempo;
    readonly Random casuale;
    readonly Dictionary<string, Voce> note = new(StringComparer.Ordinal);
    readonly List<string> storico = [];
    List<Voce> playlist = [];
    RegoleRotazione regole = new(TimeSpan.FromMinutes(30), true, Ordine.Sequenziale);
    DateTimeOffset inizio;
    DateTimeOffset? pausaDa;

    public MotoreRotazione(TimeProvider tempo, Random? casuale = null)
    {
        this.tempo = tempo;
        this.casuale = casuale ?? Random.Shared;
        inizio = tempo.GetUtcNow();
    }

    public Voce? Corrente { get; private set; }

    /// <summary>Permanenza della voce corrente; null = nessun cambio automatico.</summary>
    public TimeSpan? Totale { get; private set; }

    public bool InPausaManuale => pausaDa is not null;
    public bool StoricoVuoto => storico.Count == 0;
    public IReadOnlyList<Voce> Playlist => playlist;

    DateTimeOffset Adesso => tempo.GetUtcNow();

    /// <summary>Vero se la voce corrente è un video che resta fino alla fine (durata nota).</summary>
    public bool AttendeFineVideo =>
        regole.Intervallo is not null && regole.FineVideo && Corrente is { Tipo: TipoVoce.Video, DurataSecondi: > 0 };

    public TimeSpan? Restante
    {
        get
        {
            if (Totale is null) return null;
            var r = Totale.Value - ((pausaDa ?? Adesso) - inizio);
            return r < TimeSpan.Zero ? TimeSpan.Zero : r;
        }
    }

    public bool Scaduta =>
        pausaDa is null && Totale is not null &&
        Adesso >= inizio + Totale.Value + (AttendeFineVideo ? MargineVideo : TimeSpan.Zero);

    /// <summary>Aggiorna l'elenco di tutte le voci e la playlist (solo voci note e disponibili).</summary>
    public void AggiornaVoci(IReadOnlyList<Voce> tutte, IReadOnlyList<string> idPlaylist)
    {
        note.Clear();
        foreach (var v in tutte) note[v.Id] = v;
        playlist = idPlaylist
            .Where(id => note.TryGetValue(id, out var v) && v.Disponibile)
            .Select(id => note[id])
            .ToList();
        storico.RemoveAll(id => !note.ContainsKey(id));
        if (Corrente is not null && note.TryGetValue(Corrente.Id, out var aggiornata))
        {
            Corrente = aggiornata;
            Totale = Permanenza(aggiornata);
        }
    }

    public void AggiornaRegole(RegoleRotazione nuove, bool riavvia)
    {
        regole = nuove;
        if (riavvia) RiavviaConteggio();
        else Totale = Permanenza(Corrente);
    }

    /// <summary>All'avvio mostra la voce dopo l'ultima mostrata (RF-25).</summary>
    public void Avvia(string? ultima)
    {
        storico.Clear();
        Corrente = ultima is not null && note.TryGetValue(ultima, out var u) && u.Disponibile ? u : null;
        var id = Prossima();
        if (id is not null) Corrente = note[id];
        RiavviaConteggio();
    }

    public string? Prossima()
    {
        if (playlist.Count == 0) return null;
        if (regole.Ordine == Ordine.Casuale)
        {
            var altre = playlist.Where(v => v.Id != Corrente?.Id).ToList();
            return altre.Count == 0 ? playlist[0].Id : altre[casuale.Next(altre.Count)].Id;
        }
        int i = IndiceInPlaylist(Corrente?.Id);
        return playlist[(i + 1) % playlist.Count].Id;
    }

    public bool Mostra(string id)
    {
        if (!note.TryGetValue(id, out var v) || !v.Disponibile) return false;
        if (Corrente is not null && Corrente.Id != id)
        {
            storico.Add(Corrente.Id);
            if (storico.Count > MassimoStorico) storico.RemoveAt(0);
        }
        Corrente = v;
        RiavviaConteggio();
        return true;
    }

    /// <summary>Torna allo sfondo mostrato prima (D9); storico vuoto = voce precedente in playlist.</summary>
    public bool TornaIndietro()
    {
        while (storico.Count > 0)
        {
            var id = storico[^1];
            storico.RemoveAt(storico.Count - 1);
            if (note.TryGetValue(id, out var v) && v.Disponibile)
            {
                Corrente = v;
                RiavviaConteggio();
                return true;
            }
        }
        if (playlist.Count == 0) return false;
        int i = IndiceInPlaylist(Corrente?.Id);
        Corrente = i < 0 ? playlist[^1] : playlist[(i - 1 + playlist.Count) % playlist.Count];
        RiavviaConteggio();
        return true;
    }

    public bool DeveAvanzareDopoVideo(string id) =>
        pausaDa is null && regole.Intervallo is not null && regole.FineVideo &&
        Corrente is { Tipo: TipoVoce.Video } c && c.Id == id;

    public void PausaManuale() => pausaDa ??= Adesso;

    public void RiprendiManuale()
    {
        if (pausaDa is null) return;
        inizio += Adesso - pausaDa.Value;
        pausaDa = null;
    }

    public void RiavviaConteggio()
    {
        inizio = Adesso;
        if (pausaDa is not null) pausaDa = inizio;
        Totale = Permanenza(Corrente);
    }

    TimeSpan? Permanenza(Voce? v)
    {
        if (regole.Intervallo is null || v is null) return null;
        if (regole.FineVideo && v.Tipo == TipoVoce.Video && v.DurataSecondi is > 0)
            return TimeSpan.FromSeconds(v.DurataSecondi.Value);
        return regole.Intervallo;
    }

    int IndiceInPlaylist(string? id) => id is null ? -1 : playlist.FindIndex(v => v.Id == id);
}
