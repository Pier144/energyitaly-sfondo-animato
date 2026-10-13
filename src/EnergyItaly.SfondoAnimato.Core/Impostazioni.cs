namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Impostazioni dell'utente (impostazioni.json versione 2).</summary>
public sealed record Impostazioni
{
    public static readonly int[] DissolvenzeAmmesse = [1, 2, 4];

    public int IntervalloValore { get; init; } = 30;
    public UnitaTempo IntervalloUnita { get; init; } = UnitaTempo.Minuti;
    public bool RotazioneAttiva { get; init; } = true;
    public bool FineVideo { get; init; } = true;
    public Ordine Ordine { get; init; } = Ordine.Sequenziale;
    public IReadOnlyList<string> Playlist { get; init; } = [];
    public int DissolvenzaSecondi { get; init; } = 2;
    public ModoMonitor ModoMonitor { get; init; } = ModoMonitor.Estendi;
    public bool PausaABatteria { get; init; } = true;
    public bool AvvioConWindows { get; init; } = true;
    public string? UltimaVoce { get; init; }

    /// <summary>Durata dell'intervallo di cambio; null se la rotazione è spenta ("Mai").</summary>
    public TimeSpan? Intervallo => !RotazioneAttiva
        ? null
        : IntervalloUnita == UnitaTempo.Ore ? TimeSpan.FromHours(IntervalloValore) : TimeSpan.FromMinutes(IntervalloValore);

    public static int Massimo(UnitaTempo unita) => unita == UnitaTempo.Ore ? 24 : 1440;

    /// <summary>Applica le modifiche chieste dalla finestra; se un valore non è valido restituisce l'errore da mostrare.</summary>
    public (Impostazioni Risultato, string? Errore) Applica(ModificheImpostazioni m)
    {
        var unita = IntervalloUnita;
        if (m.IntervalloUnita is not null)
        {
            if (NomiJson.LeggiUnita(m.IntervalloUnita) is not { } u) return (this, "Unità di tempo non valida");
            unita = u;
        }
        int valore = m.IntervalloValore ?? (unita != IntervalloUnita ? Math.Min(IntervalloValore, Massimo(unita)) : IntervalloValore);
        if (valore < 1 || valore > Massimo(unita))
            return (this, $"Scrivi un numero intero da 1 a {Massimo(unita)} {NomiJson.Di(unita)}");

        var ordine = Ordine;
        if (m.Ordine is not null)
        {
            if (NomiJson.LeggiOrdine(m.Ordine) is not { } o) return (this, "Ordine non valido");
            ordine = o;
        }
        var modo = ModoMonitor;
        if (m.ModoMonitor is not null)
        {
            if (NomiJson.LeggiModo(m.ModoMonitor) is not { } mm) return (this, "Modo monitor non valido");
            modo = mm;
        }
        var dissolvenza = m.DissolvenzaSecondi ?? DissolvenzaSecondi;
        if (!DissolvenzeAmmesse.Contains(dissolvenza)) return (this, "Dissolvenza non valida: scegli 1, 2 o 4 secondi");

        return (this with
        {
            IntervalloUnita = unita,
            IntervalloValore = valore,
            Ordine = ordine,
            ModoMonitor = modo,
            DissolvenzaSecondi = dissolvenza,
            RotazioneAttiva = m.RotazioneAttiva ?? RotazioneAttiva,
            FineVideo = m.FineVideo ?? FineVideo,
            PausaABatteria = m.PausaABatteria ?? PausaABatteria,
            AvvioConWindows = m.AvvioConWindows ?? AvvioConWindows
        }, null);
    }
}

/// <summary>Campi cambiati dalla finestra principale; null = invariato.</summary>
public sealed record ModificheImpostazioni(
    int? IntervalloValore = null,
    string? IntervalloUnita = null,
    bool? RotazioneAttiva = null,
    bool? FineVideo = null,
    string? Ordine = null,
    int? DissolvenzaSecondi = null,
    string? ModoMonitor = null,
    bool? PausaABatteria = null,
    bool? AvvioConWindows = null);
