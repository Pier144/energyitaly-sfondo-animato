using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>
/// Lettura tollerante e scrittura di impostazioni.json. Un valore sbagliato non blocca mai:
/// si usa quello predefinito e si aggiunge un avviso per il log (RF-51).
/// </summary>
public static class ArchivioImpostazioni
{
    public const int Versione = 2;

    /// <summary>Sfondi aziendali fino alla versione 1.1.0, che non salvava sfondiAziendaliVisti.</summary>
    public static readonly IReadOnlyList<string> SfondiAziendaliFinoA110 =
        ["onda-di-luce", "onde-di-energia", "paesaggio-rinnovabile", "circuito", "aurora"];

    /// <summary>Valori del codice, eventualmente sovrascritti da impostazioni-predefinite.json (RF-52).</summary>
    public static Impostazioni Predefinite(string? jsonAziendale, IReadOnlyCollection<string> idValidi,
        IReadOnlyList<string> playlistDiBase, List<string> avvisi)
    {
        var codice = new Impostazioni { Playlist = playlistDiBase.ToList() };
        return string.IsNullOrWhiteSpace(jsonAziendale)
            ? codice
            : Leggi(jsonAziendale, codice, idValidi, avvisi, "impostazioni-predefinite.json", codice.SfondiAziendaliVisti);
    }

    /// <summary>Impostazioni dell'utente sopra le predefinite.</summary>
    public static Impostazioni Carica(string? jsonUtente, Impostazioni predefinite,
        IReadOnlyCollection<string> idValidi, List<string> avvisi) =>
        string.IsNullOrWhiteSpace(jsonUtente)
            ? predefinite
            : Leggi(jsonUtente, predefinite, idValidi, avvisi, "impostazioni.json", SfondiAziendaliFinoA110);

    static Impostazioni Leggi(string json, Impostazioni b, IReadOnlyCollection<string> idValidi, List<string> avvisi, string nome,
        IReadOnlyList<string>? vistiSeManca)
    {
        JsonElement r;
        try
        {
            using var doc = JsonDocument.Parse(json);
            r = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            avvisi.Add($"{nome} non è un JSON valido: uso i valori predefiniti");
            return b;
        }
        if (r.ValueKind != JsonValueKind.Object || !r.TryGetProperty("versione", out var ver) ||
            ver.ValueKind != JsonValueKind.Number || !ver.TryGetInt32(out var v) || v != Versione)
        {
            avvisi.Add($"{nome}: versione non supportata, uso i valori predefiniti");
            return b;
        }

        var lettore = new Lettore(r, nome, avvisi);
        var (unita, valore) = LeggiIntervallo(r, b, nome, avvisi);
        string? ultima = r.TryGetProperty("ultimaVoce", out var u) && u.ValueKind == JsonValueKind.String && idValidi.Contains(u.GetString()!)
            ? u.GetString()
            : b.UltimaVoce;

        return b with
        {
            IntervalloUnita = unita,
            IntervalloValore = valore,
            RotazioneAttiva = lettore.Booleano("rotazioneAttiva", b.RotazioneAttiva),
            FineVideo = lettore.Booleano("fineVideo", b.FineVideo),
            Ordine = lettore.Enumerato("ordine", b.Ordine, NomiJson.LeggiOrdine),
            Playlist = LeggiPlaylist(r, b.Playlist, idValidi, nome, avvisi),
            DissolvenzaSecondi = lettore.Intero("dissolvenzaSecondi", b.DissolvenzaSecondi, x => Impostazioni.DissolvenzeAmmesse.Contains(x)),
            ModoMonitor = lettore.Enumerato("modoMonitor", b.ModoMonitor, NomiJson.LeggiModo),
            PausaABatteria = lettore.Booleano("pausaABatteria", b.PausaABatteria),
            AvvioConWindows = lettore.Booleano("avvioConWindows", b.AvvioConWindows),
            AggiornamentiAutomatici = lettore.Booleano("aggiornamentiAutomatici", b.AggiornamentiAutomatici),
            UltimaVoce = ultima,
            SfondiAziendaliVisti = LeggiVisti(r, vistiSeManca, nome, avvisi)
        };
    }

    static (UnitaTempo Unita, int Valore) LeggiIntervallo(JsonElement r, Impostazioni b, string nome, List<string> avvisi)
    {
        if (!r.TryGetProperty("intervallo", out var iv)) return (b.IntervalloUnita, b.IntervalloValore);
        if (iv.ValueKind != JsonValueKind.Object)
        {
            avvisi.Add($"{nome}: 'intervallo' non valido, uso il predefinito");
            return (b.IntervalloUnita, b.IntervalloValore);
        }
        var unita = new Lettore(iv, nome, avvisi).Enumerato("unita", b.IntervalloUnita, NomiJson.LeggiUnita);
        if (!iv.TryGetProperty("valore", out var val)) return (unita, Math.Min(b.IntervalloValore, Impostazioni.Massimo(unita)));
        if (val.ValueKind == JsonValueKind.Number && val.TryGetInt32(out var n) && n >= 1 && n <= Impostazioni.Massimo(unita))
            return (unita, n);
        avvisi.Add($"{nome}: valore non valido per 'intervallo', uso {b.IntervalloValore} {NomiJson.Di(b.IntervalloUnita)}");
        return (b.IntervalloUnita, b.IntervalloValore);
    }

    static IReadOnlyList<string>? LeggiVisti(JsonElement r, IReadOnlyList<string>? seManca, string nome, List<string> avvisi)
    {
        if (!r.TryGetProperty("sfondiAziendaliVisti", out var v)) return seManca;
        if (v.ValueKind == JsonValueKind.Array && v.EnumerateArray().All(e => e.ValueKind == JsonValueKind.String))
            return v.EnumerateArray().Select(e => e.GetString()!).Distinct().ToList();
        avvisi.Add($"{nome}: 'sfondiAziendaliVisti' non valido, lo ignoro");
        return seManca;
    }

    static IReadOnlyList<string> LeggiPlaylist(JsonElement r, IReadOnlyList<string> base_, IReadOnlyCollection<string> idValidi, string nome, List<string> avvisi)
    {
        if (!r.TryGetProperty("playlist", out var p)) return base_;
        if (p.ValueKind != JsonValueKind.Array)
        {
            avvisi.Add($"{nome}: 'playlist' non valida, uso la predefinita");
            return base_;
        }
        var risultato = new List<string>();
        foreach (var e in p.EnumerateArray())
        {
            var id = e.ValueKind == JsonValueKind.String ? e.GetString() : null;
            if (id is null || !idValidi.Contains(id))
            {
                avvisi.Add($"{nome}: sfondo '{id ?? e.GetRawText()}' sconosciuto, tolto dalla playlist");
                continue;
            }
            if (!risultato.Contains(id)) risultato.Add(id);
        }
        if (risultato.Count == 0)
        {
            avvisi.Add($"{nome}: playlist vuota, uso la predefinita");
            return base_;
        }
        return risultato;
    }

    sealed class Lettore(JsonElement oggetto, string nome, List<string> avvisi)
    {
        public bool Booleano(string campo, bool predefinito)
        {
            if (!oggetto.TryGetProperty(campo, out var v)) return predefinito;
            if (v.ValueKind is JsonValueKind.True or JsonValueKind.False) return v.GetBoolean();
            avvisi.Add($"{nome}: valore non valido per '{campo}', uso {(predefinito ? "true" : "false")}");
            return predefinito;
        }

        public int Intero(string campo, int predefinito, Func<int, bool> valido)
        {
            if (!oggetto.TryGetProperty(campo, out var v)) return predefinito;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) && valido(n)) return n;
            avvisi.Add($"{nome}: valore non valido per '{campo}', uso {predefinito}");
            return predefinito;
        }

        public T Enumerato<T>(string campo, T predefinito, Func<string?, T?> leggi) where T : struct
        {
            if (!oggetto.TryGetProperty(campo, out var v)) return predefinito;
            if (v.ValueKind == JsonValueKind.String && leggi(v.GetString()) is { } t) return t;
            avvisi.Add($"{nome}: valore non valido per '{campo}', uso il predefinito");
            return predefinito;
        }
    }

    public static string Scrivi(Impostazioni i)
    {
        using var flusso = new MemoryStream();
        using (var w = new Utf8JsonWriter(flusso, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartObject();
            w.WriteNumber("versione", Versione);
            w.WriteStartObject("intervallo");
            w.WriteNumber("valore", i.IntervalloValore);
            w.WriteString("unita", NomiJson.Di(i.IntervalloUnita));
            w.WriteEndObject();
            w.WriteBoolean("rotazioneAttiva", i.RotazioneAttiva);
            w.WriteBoolean("fineVideo", i.FineVideo);
            w.WriteString("ordine", NomiJson.Di(i.Ordine));
            w.WriteStartArray("playlist");
            foreach (var id in i.Playlist) w.WriteStringValue(id);
            w.WriteEndArray();
            w.WriteNumber("dissolvenzaSecondi", i.DissolvenzaSecondi);
            w.WriteString("modoMonitor", NomiJson.Di(i.ModoMonitor));
            w.WriteBoolean("pausaABatteria", i.PausaABatteria);
            w.WriteBoolean("avvioConWindows", i.AvvioConWindows);
            w.WriteBoolean("aggiornamentiAutomatici", i.AggiornamentiAutomatici);
            if (i.UltimaVoce is null) w.WriteNull("ultimaVoce"); else w.WriteString("ultimaVoce", i.UltimaVoce);
            if (i.SfondiAziendaliVisti is not null)
            {
                w.WriteStartArray("sfondiAziendaliVisti");
                foreach (var id in i.SfondiAziendaliVisti) w.WriteStringValue(id);
                w.WriteEndArray();
            }
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(flusso.ToArray());
    }
}
