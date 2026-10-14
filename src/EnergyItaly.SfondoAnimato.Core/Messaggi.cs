using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Comando mandato dalla finestra principale.</summary>
public sealed record ComandoApp(string Tipo, string? Id, IReadOnlyList<string>? Ids, ModificheImpostazioni? Modifiche,
    ModifichePersonalizzazione? Personalizzazione = null);

/// <summary>Evento mandato dal player dello sfondo.</summary>
public sealed record EventoPlayer(string Tipo, string? Id, string? Messaggio, double? Secondi);

/// <summary>Tutto ciò che la finestra principale deve sapere per disegnarsi.</summary>
public sealed record StatoVista(
    IReadOnlyList<Voce> Voci,
    Impostazioni Impostazioni,
    string? Corrente,
    bool StoricoVuoto,
    bool InPausa,
    bool PausaManuale,
    string? MotivoPausa,
    long? RestanteMs,
    long? TotaleMs,
    bool ContoAttivo,
    bool AttendeFineVideo,
    Disposizione Disposizione,
    IReadOnlyList<Tavolozza>? Tavolozze = null);

/// <summary>Lettura e scrittura dei messaggi JSON (spec sezione 7).</summary>
public static class Messaggi
{
    static readonly JsonWriterOptions OpzioniScrittura = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static ComandoApp? LeggiComandoApp(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object || Testo(r, "tipo") is not { } tipo) return null;

            IReadOnlyList<string>? ids = null;
            if (r.TryGetProperty("ids", out var a) && a.ValueKind == JsonValueKind.Array)
                ids = a.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList();

            ModificheImpostazioni? modifiche = null;
            if (r.TryGetProperty("modifiche", out var m) && m.ValueKind == JsonValueKind.Object && tipo == "personalizza")
                return new ComandoApp(tipo, Testo(r, "id"), ids, null, LeggiModifichePersonalizzazione(m));
            if (r.TryGetProperty("modifiche", out m) && m.ValueKind == JsonValueKind.Object)
            {
                modifiche = new ModificheImpostazioni(
                    IntervalloValore: Intero(m, "intervalloValore"),
                    IntervalloUnita: Testo(m, "intervalloUnita"),
                    RotazioneAttiva: Booleano(m, "rotazioneAttiva"),
                    FineVideo: Booleano(m, "fineVideo"),
                    Ordine: Testo(m, "ordine"),
                    DissolvenzaSecondi: Intero(m, "dissolvenzaSecondi"),
                    ModoMonitor: Testo(m, "modoMonitor"),
                    PausaABatteria: Booleano(m, "pausaABatteria"),
                    AvvioConWindows: Booleano(m, "avvioConWindows"),
                    AggiornamentiAutomatici: Booleano(m, "aggiornamentiAutomatici"));
            }
            return new ComandoApp(tipo, Testo(r, "id"), ids, modifiche);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Oggetto "modifiche" del comando "personalizza" (spec personalizzazione 4.4).</summary>
    static ModifichePersonalizzazione LeggiModifichePersonalizzazione(JsonElement m)
    {
        Dictionary<string, ValoreOpzione>? opzioni = null;
        if (m.TryGetProperty("opzioni", out var o) && o.ValueKind == JsonValueKind.Object)
        {
            opzioni = [];
            foreach (var kv in o.EnumerateObject())
            {
                ValoreOpzione? valore = kv.Value.ValueKind switch
                {
                    JsonValueKind.Number => ValoreOpzione.DiNumero(kv.Value.GetDouble()),
                    JsonValueKind.String => ValoreOpzione.DiTesto(kv.Value.GetString()!),
                    JsonValueKind.True => ValoreOpzione.DiBooleano(true),
                    JsonValueKind.False => ValoreOpzione.DiBooleano(false),
                    _ => null
                };
                // Un valore di tipo impossibile resta come testo vuoto: Applica lo rifiuta con un errore chiaro.
                opzioni[kv.Name] = valore ?? ValoreOpzione.DiTesto("");
            }
        }
        return new ModifichePersonalizzazione(Testo(m, "tavolozza"), Testo(m, "fondo"), Testo(m, "accento"), opzioni,
            Testo(m, "modoMonitor"), Testo(m, "riempimento"));
    }

    public static EventoPlayer? LeggiEventoPlayer(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object || Testo(r, "tipo") is not { } tipo) return null;
            double? secondi = r.TryGetProperty("secondi", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetDouble() : null;
            return new EventoPlayer(tipo, Testo(r, "id"), Testo(r, "messaggio"), secondi);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static string? Testo(JsonElement o, string nome) =>
        o.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static bool? Booleano(JsonElement o, string nome) =>
        o.TryGetProperty(nome, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;

    /// <summary>Intero; un numero non intero diventa 0 così la validazione lo rifiuta.</summary>
    static int? Intero(JsonElement o, string nome)
    {
        if (!o.TryGetProperty(nome, out var v) || v.ValueKind != JsonValueKind.Number) return null;
        return v.TryGetInt32(out var n) ? n : 0;
    }

    public static string Mostra(Voce v, string url, Riempimento riempimento, Disposizione d, int dissolvenzaMs, bool fineVideo) => Scrivi(w =>
    {
        w.WriteString("tipo", "mostra");
        w.WriteString("id", v.Id);
        w.WriteString("tipoVoce", NomiJson.Di(v.Tipo));
        w.WriteString("url", url);
        w.WriteString("riempimento", NomiJson.Di(riempimento));
        ScriviScene(w, d);
        w.WriteNumber("dissolvenzaMs", dissolvenzaMs);
        w.WriteBoolean("fineVideo", fineVideo);
    });

    public static string Layout(Disposizione d) => Scrivi(w =>
    {
        w.WriteString("tipo", "layout");
        ScriviScene(w, d);
    });

    public static string Semplice(string tipo) => Scrivi(w => w.WriteString("tipo", tipo));

    public static string Avviso(string testo) => Scrivi(w =>
    {
        w.WriteString("tipo", "avviso");
        w.WriteString("testo", testo);
    });

    public static string Stato(StatoVista s) => Scrivi(w =>
    {
        w.WriteString("tipo", "stato");
        var tavolozze = s.Tavolozze ?? [Personalizzazioni.EnergyItaly];
        w.WriteStartArray("voci");
        foreach (var v in s.Voci)
        {
            var p = s.Impostazioni.Personalizzazioni.GetValueOrDefault(v.Id);
            w.WriteStartObject();
            w.WriteString("id", v.Id);
            w.WriteString("tipoVoce", NomiJson.Di(v.Tipo));
            w.WriteString("titolo", v.Titolo);
            w.WriteBoolean("aziendale", v.Aziendale);
            w.WriteString("url", Personalizzazioni.Url(v, p, tavolozze));
            if (v.DurataSecondi is { } durata) w.WriteNumber("durataSecondi", durata); else w.WriteNull("durataSecondi");
            w.WriteBoolean("disponibile", v.Disponibile);
            w.WriteStartArray("opzioni");
            foreach (var o in v.Opzioni) ScriviOpzione(w, o);
            w.WriteEndArray();
            if (p is null) w.WriteNull("personalizzazione");
            else
            {
                w.WritePropertyName("personalizzazione");
                Personalizzazioni.Scrivi(w, p);
            }
            w.WriteString("modoMonitor", NomiJson.Di(Personalizzazioni.ModoEffettivo(p, s.Impostazioni.ModoMonitor)));
            w.WriteString("riempimento", NomiJson.Di(Personalizzazioni.RiempimentoEffettivo(p)));
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteStartArray("tavolozze");
        foreach (var t in tavolozze)
        {
            w.WriteStartObject();
            w.WriteString("id", t.Id);
            w.WriteString("titolo", t.Titolo);
            w.WriteString("fondo", t.Fondo);
            w.WriteString("accento", t.Accento);
            w.WriteString("secondario", t.Secondario);
            w.WriteEndObject();
        }
        w.WriteEndArray();

        var i = s.Impostazioni;
        w.WriteStartArray("playlist");
        foreach (var id in i.Playlist) w.WriteStringValue(id);
        w.WriteEndArray();
        w.WriteStartObject("impostazioni");
        w.WriteNumber("intervalloValore", i.IntervalloValore);
        w.WriteString("intervalloUnita", NomiJson.Di(i.IntervalloUnita));
        w.WriteBoolean("rotazioneAttiva", i.RotazioneAttiva);
        w.WriteBoolean("fineVideo", i.FineVideo);
        w.WriteString("ordine", NomiJson.Di(i.Ordine));
        w.WriteNumber("dissolvenzaSecondi", i.DissolvenzaSecondi);
        w.WriteString("modoMonitor", NomiJson.Di(i.ModoMonitor));
        w.WriteBoolean("pausaABatteria", i.PausaABatteria);
        w.WriteBoolean("avvioConWindows", i.AvvioConWindows);
        w.WriteBoolean("aggiornamentiAutomatici", i.AggiornamentiAutomatici);
        w.WriteEndObject();

        StringaONull(w, "corrente", s.Corrente);
        w.WriteBoolean("storicoVuoto", s.StoricoVuoto);
        w.WriteBoolean("inPausa", s.InPausa);
        w.WriteBoolean("pausaManuale", s.PausaManuale);
        StringaONull(w, "motivoPausa", s.MotivoPausa);
        NumeroONull(w, "restanteMs", s.RestanteMs);
        NumeroONull(w, "totaleMs", s.TotaleMs);
        w.WriteBoolean("contoAttivo", s.ContoAttivo);
        w.WriteBoolean("attendeFineVideo", s.AttendeFineVideo);

        var d = s.Disposizione;
        w.WriteStartObject("finestra");
        w.WriteNumber("w", d.Finestra.L);
        w.WriteNumber("h", d.Finestra.A);
        w.WriteEndObject();
        w.WriteStartArray("monitor");
        foreach (var m in d.Monitor)
        {
            w.WriteStartObject();
            w.WriteString("nome", m.Nome);
            w.WriteNumber("x", m.Area.X);
            w.WriteNumber("y", m.Area.Y);
            w.WriteNumber("w", m.Area.L);
            w.WriteNumber("h", m.Area.A);
            w.WriteBoolean("principale", m.Principale);
            w.WriteEndObject();
        }
        w.WriteEndArray();
        ScriviScene(w, d);
    });

    static void ScriviOpzione(Utf8JsonWriter w, Opzione o)
    {
        w.WriteStartObject();
        w.WriteString("id", o.Id);
        w.WriteString("titolo", o.Titolo);
        w.WriteString("tipo", NomiJson.Di(o.Tipo));
        if (o.Min is { } min) w.WriteNumber("min", min);
        if (o.Max is { } max) w.WriteNumber("max", max);
        if (o.Passo is { } passo) w.WriteNumber("passo", passo);
        if (o.Unita is not null) w.WriteString("unita", o.Unita);
        if (o.Valori is not null)
        {
            w.WriteStartArray("valori");
            foreach (var x in o.Valori)
            {
                w.WriteStartObject();
                w.WriteString("valore", x.Valore);
                w.WriteString("titolo", x.Titolo);
                w.WriteEndObject();
            }
            w.WriteEndArray();
        }
        Personalizzazioni.ScriviValore(w, "predefinito", o.Predefinito);
        w.WriteEndObject();
    }

    static void ScriviScene(Utf8JsonWriter w, Disposizione d)
    {
        w.WriteString("modo", NomiJson.Di(d.ModoEffettivo));
        w.WriteStartArray("scene");
        foreach (var s in d.Scene)
        {
            w.WriteStartObject();
            w.WritePropertyName("area");
            Rettangolo(w, s.Area);
            w.WriteStartArray("monitor");
            foreach (var m in s.Monitor) Rettangolo(w, m);
            w.WriteEndArray();
            w.WriteEndObject();
        }
        w.WriteEndArray();
    }

    static void Rettangolo(Utf8JsonWriter w, Rettangolo r)
    {
        w.WriteStartObject();
        w.WriteNumber("x", r.X);
        w.WriteNumber("y", r.Y);
        w.WriteNumber("w", r.L);
        w.WriteNumber("h", r.A);
        w.WriteEndObject();
    }

    static void StringaONull(Utf8JsonWriter w, string nome, string? valore)
    {
        if (valore is null) w.WriteNull(nome); else w.WriteString(nome, valore);
    }

    static void NumeroONull(Utf8JsonWriter w, string nome, long? valore)
    {
        if (valore is null) w.WriteNull(nome); else w.WriteNumber(nome, valore.Value);
    }

    static string Scrivi(Action<Utf8JsonWriter> corpo)
    {
        using var flusso = new MemoryStream();
        using (var w = new Utf8JsonWriter(flusso, OpzioniScrittura))
        {
            w.WriteStartObject();
            corpo(w);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(flusso.ToArray());
    }
}
