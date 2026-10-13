using System.Text.Json;

namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Uno sfondo della libreria: animazione aziendale o file importato dall'utente.</summary>
public sealed record Voce(
    string Id,
    TipoVoce Tipo,
    string Titolo,
    bool Aziendale,
    string File,
    IReadOnlyDictionary<string, string> Parametri,
    int? DurataSecondi,
    bool Disponibile);

/// <summary>Una voce di libreria.json (file importato dall'utente).</summary>
public sealed class VoceLibreria
{
    public string Id { get; set; } = "";
    public string Tipo { get; set; } = "";
    public string Titolo { get; set; } = "";
    public string File { get; set; } = "";
    public int? DurataSecondi { get; set; }
    public DateTimeOffset ImportataIl { get; set; }
}

/// <summary>Contenuto di libreria.json.</summary>
public sealed class DatiLibreria
{
    public int Versione { get; set; } = 1;
    public List<VoceLibreria> Voci { get; set; } = [];

    /// <summary>File eliminati dalla libreria che Windows non ha lasciato cancellare (erano in uso): si riprova all'avvio.</summary>
    public List<string> DaCancellare { get; set; } = [];
}

/// <summary>Unisce le animazioni aziendali (manifest.json) e gli sfondi importati (libreria.json).</summary>
public static class Catalogo
{
    public const string HostApp = "sfondo.energyitaly";
    public const string HostLibreria = "libreria.energyitaly";

    /// <summary>
    /// Host delle pagine HTML importate. Non è mappato su una cartella: ogni richiesta passa dal
    /// programma, che la consegna con una Content-Security-Policy senza rete.
    /// </summary>
    public const string HostPagine = "pagine.energyitaly";

    static readonly JsonSerializerOptions Opzioni = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    static readonly IReadOnlyDictionary<string, string> NessunParametro = new Dictionary<string, string>();

    public static (IReadOnlyList<Voce> Voci, IReadOnlyList<string> Avvisi) Carica(
        string manifestJson, DatiLibreria libreria, Func<string, bool> fileEsiste)
    {
        var voci = new List<Voce>();
        var avvisi = new List<string>();
        var usati = new HashSet<string>(StringComparer.Ordinal);
        LeggiManifest(manifestJson, voci, avvisi, usati);

        foreach (var v in libreria.Voci)
        {
            var tipo = NomiJson.LeggiTipo(v.Tipo);
            if (tipo is null || tipo == TipoVoce.Animazione)
            {
                avvisi.Add($"libreria.json: tipo '{v.Tipo}' non valido per '{v.Id}', voce ignorata");
                continue;
            }
            if (v.Id is null || !v.Id.StartsWith("u-", StringComparison.Ordinal) || usati.Contains(v.Id))
            {
                avvisi.Add($"libreria.json: id '{v.Id}' non valido o ripetuto, voce ignorata");
                continue;
            }
            if (!NomeFileSicuro(v.File))
            {
                avvisi.Add($"libreria.json: nome file '{v.File}' non valido per '{v.Id}', voce ignorata");
                continue;
            }
            usati.Add(v.Id);
            bool disponibile = fileEsiste(v.File);
            if (!disponibile) avvisi.Add($"Il file '{v.File}' dello sfondo '{v.Titolo}' non c'è più");
            var titolo = string.IsNullOrWhiteSpace(v.Titolo) ? v.File : v.Titolo;
            var durata = v.DurataSecondi is > 0 ? v.DurataSecondi : null;
            voci.Add(new Voce(v.Id, tipo.Value, titolo, false, v.File, NessunParametro, durata, disponibile));
        }
        return (voci, avvisi);
    }

    static void LeggiManifest(string json, List<Voce> voci, List<string> avvisi, HashSet<string> usati)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("animazioni", out var elenco) ||
                elenco.ValueKind != JsonValueKind.Array)
            {
                avvisi.Add("manifest.json: manca l'elenco 'animazioni'");
                return;
            }
            foreach (var a in elenco.EnumerateArray())
            {
                var id = Testo(a, "id");
                var file = Testo(a, "file");
                if (string.IsNullOrWhiteSpace(id) || !NomeFileSicuro(file) || usati.Contains(id))
                {
                    avvisi.Add($"manifest.json: animazione '{id}' non valida, ignorata");
                    continue;
                }
                usati.Add(id);
                var parametri = new Dictionary<string, string>();
                if (a.TryGetProperty("parametri", out var p) && p.ValueKind == JsonValueKind.Object)
                {
                    foreach (var kv in p.EnumerateObject())
                    {
                        parametri[kv.Name] = kv.Value.ValueKind switch
                        {
                            JsonValueKind.String => kv.Value.GetString()!,
                            JsonValueKind.True => "true",
                            JsonValueKind.False => "false",
                            _ => kv.Value.GetRawText()
                        };
                    }
                }
                voci.Add(new Voce(id, TipoVoce.Animazione, Testo(a, "titolo") ?? id, true, file!, parametri, null, true));
            }
        }
        catch (JsonException ex)
        {
            avvisi.Add($"manifest.json non leggibile: {ex.Message}");
        }
    }

    static string? Testo(JsonElement e, string nome) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    /// <summary>Solo un nome di file semplice: niente cartelle, unità o "..".</summary>
    public static bool NomeFileSicuro(string? nome) =>
        !string.IsNullOrWhiteSpace(nome) &&
        nome.IndexOfAny(['/', '\\', ':']) < 0 &&
        !nome.Contains("..", StringComparison.Ordinal);

    public static DatiLibreria LeggiLibreria(string? json, List<string> avvisi) => LeggiLibreria(json, avvisi, out _);

    /// <summary>
    /// Lettura tollerante voce per voce: una voce difettosa viene scartata con un avviso, le altre restano.
    /// <paramref name="illeggibile"/> è vero se il file intero non si può usare (JSON rotto o versione diversa).
    /// </summary>
    public static DatiLibreria LeggiLibreria(string? json, List<string> avvisi, out bool illeggibile)
    {
        illeggibile = false;
        var dati = new DatiLibreria();
        if (string.IsNullOrWhiteSpace(json)) return dati;
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            avvisi.Add($"libreria.json non leggibile ({ex.Message}): libreria vuota");
            illeggibile = true;
            return dati;
        }
        using (doc)
        {
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object || !r.TryGetProperty("versione", out var v) ||
                v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out var versione) || versione != 1)
            {
                avvisi.Add("libreria.json: versione non supportata, libreria vuota");
                illeggibile = true;
                return dati;
            }
            if (r.TryGetProperty("voci", out var voci) && voci.ValueKind == JsonValueKind.Array)
            {
                int n = 0;
                foreach (var e in voci.EnumerateArray())
                {
                    n++;
                    VoceLibreria? voce = null;
                    try { voce = e.ValueKind == JsonValueKind.Object ? e.Deserialize<VoceLibreria>(Opzioni) : null; }
                    catch (JsonException) { }
                    if (voce is null || string.IsNullOrWhiteSpace(voce.Id))
                    {
                        avvisi.Add($"libreria.json: voce {n} non valida, ignorata");
                        continue;
                    }
                    dati.Voci.Add(voce);
                }
            }
            if (r.TryGetProperty("daCancellare", out var daCancellare) && daCancellare.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in daCancellare.EnumerateArray())
                {
                    if (e.ValueKind == JsonValueKind.String && NomeFileSicuro(e.GetString())) dati.DaCancellare.Add(e.GetString()!);
                    else avvisi.Add("libreria.json: voce di 'daCancellare' non valida, ignorata");
                }
            }
        }
        return dati;
    }

    public static string ScriviLibreria(DatiLibreria dati) => JsonSerializer.Serialize(dati, Opzioni);

    /// <summary>URL della voce sugli host virtuali, senza il parametro monitor.</summary>
    public static string UrlBase(Voce v)
    {
        if (v.Tipo == TipoVoce.Html) return $"https://{HostPagine}/{Uri.EscapeDataString(v.File)}";
        if (v.Tipo != TipoVoce.Animazione) return $"https://{HostLibreria}/{Uri.EscapeDataString(v.File)}";
        var url = $"https://{HostApp}/animazioni/{Uri.EscapeDataString(v.File)}";
        if (v.Parametri.Count == 0) return url;
        return url + "?" + string.Join('&', v.Parametri.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
    }
}
