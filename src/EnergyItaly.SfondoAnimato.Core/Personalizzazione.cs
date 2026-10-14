using System.Globalization;

namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Come si sceglie un'opzione di un'animazione (manifest.json versione 2).</summary>
public enum TipoOpzione { Numero, Scelta, Interruttore }

/// <summary>Immagini e video: riempire lo schermo tagliando i bordi o mostrarli interi.</summary>
public enum Riempimento { Riempi, Adatta }

/// <summary>Valore di un'opzione: è valorizzato solo il campo del suo tipo.</summary>
public sealed record ValoreOpzione(double? Numero = null, string? Testo = null, bool? Booleano = null)
{
    public static ValoreOpzione DiNumero(double n) => new(Numero: n);
    public static ValoreOpzione DiTesto(string t) => new(Testo: t);
    public static ValoreOpzione DiBooleano(bool b) => new(Booleano: b);

    /// <summary>Valore per la query string: numeri con il punto decimale, interruttori 1/0 (spec personalizzazione P12).</summary>
    public string PerUrl() =>
        Numero is { } n ? n.ToString("0.######", CultureInfo.InvariantCulture)
        : Booleano is { } b ? (b ? "1" : "0")
        : Testo ?? "";

    public bool DelTipo(TipoOpzione tipo) => tipo switch
    {
        TipoOpzione.Numero => Numero is not null,
        TipoOpzione.Scelta => Testo is not null,
        _ => Booleano is not null
    };
}

public sealed record ValoreScelta(string Valore, string Titolo);

/// <summary>Un'opzione dichiarata nel manifest.</summary>
public sealed record Opzione(string Id, string Titolo, TipoOpzione Tipo, ValoreOpzione Predefinito,
    double? Min = null, double? Max = null, double? Passo = null, string? Unita = null,
    IReadOnlyList<ValoreScelta>? Valori = null);

/// <summary>Una tavolozza del manifest: colori "#rrggbb" minuscoli.</summary>
public sealed record Tavolozza(string Id, string Titolo, string Fondo, string Accento, string Secondario);

/// <summary>
/// Le scelte dell'utente per uno sfondo, solo come differenze dai valori predefiniti (spec personalizzazione P7).
/// Il confronto tra due personalizzazioni non va fatto con ==: Opzioni è un dizionario.
/// </summary>
public sealed record Personalizzazione
{
    /// <summary>Id di tavolozza o "personalizzata"; null = la tavolozza predefinita.</summary>
    public string? Tavolozza { get; init; }
    /// <summary>"#rrggbb", solo con la tavolozza "personalizzata".</summary>
    public string? Fondo { get; init; }
    public string? Accento { get; init; }
    /// <summary>Solo le opzioni diverse dal predefinito.</summary>
    public IReadOnlyDictionary<string, ValoreOpzione> Opzioni { get; init; } = new Dictionary<string, ValoreOpzione>();
    /// <summary>null = come in Impostazioni.</summary>
    public ModoMonitor? ModoMonitor { get; init; }
    /// <summary>Solo immagini e video; null = Riempi.</summary>
    public Riempimento? Riempimento { get; init; }

    public bool Vuota => Tavolozza is null && Fondo is null && Accento is null && Opzioni.Count == 0 &&
        ModoMonitor is null && Riempimento is null;
}

/// <summary>Comando "personalizza" dalla finestra: null = campo non toccato.</summary>
public sealed record ModifichePersonalizzazione(
    string? Tavolozza = null,
    string? Fondo = null,
    string? Accento = null,
    IReadOnlyDictionary<string, ValoreOpzione>? Opzioni = null,
    string? ModoMonitor = null,
    string? Riempimento = null);
