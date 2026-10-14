using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Comando mandato dalla finestra principale.</summary>
public sealed record ComandoApp(string Tipo, string? Id, IReadOnlyList<string>? Ids, ModificheImpostazioni? Modifiche);

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
    Disposizione Disposizione);

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
            if (r.TryGetProperty("modifiche", out var m) && m.ValueKind == JsonValueKind.Object)
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

    public static string Mostra(Voce v, Disposizione d, int dissolvenzaMs, bool fineVideo) => Scrivi(w =>
    {
        w.WriteString("tipo", "mostra");
        w.WriteString("id", v.Id);
        w.WriteString("tipoVoce", NomiJson.Di(v.Tipo));
        w.WriteString("url", Catalogo.UrlBase(v));
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
        w.WriteStartArray("voci");
        foreach (var v in s.Voci)
        {
            w.WriteStartObject();
            w.WriteString("id", v.Id);
            w.WriteString("tipoVoce", NomiJson.Di(v.Tipo));
            w.WriteString("titolo", v.Titolo);
            w.WriteBoolean("aziendale", v.Aziendale);
            w.WriteString("url", Catalogo.UrlBase(v));
            if (v.DurataSecondi is { } durata) w.WriteNumber("durataSecondi", durata); else w.WriteNull("durataSecondi");
            w.WriteBoolean("disponibile", v.Disponibile);
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
