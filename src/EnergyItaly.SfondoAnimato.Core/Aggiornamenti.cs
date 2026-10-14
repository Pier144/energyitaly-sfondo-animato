using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Una versione pubblicata: il contenuto di versione.json.</summary>
public sealed record InfoVersione(Version Versione, string File, string Sha256);

/// <summary>
/// Ciò che l'aggiornatore ricorda tra un avvio e l'altro (aggiornamenti\stato.json): ultimo controllo,
/// aggiornamento scaricato e verificato, installazioni avviate per quell'aggiornamento (A11).
/// </summary>
public sealed record StatoAggiornamenti(DateTimeOffset? UltimoControllo, InfoVersione? Pronta, int Tentativi);

/// <summary>
/// Regole dell'aggiornamento automatico (spec 2026-10-14-aggiornamento-automatico, 4.1): da dove si
/// scarica, cosa è valido, quando controllare e quando installare. Rete, file e processi stanno nell'app.
/// </summary>
public static partial class Aggiornamenti
{
    public const string Repository = "Pier144/energyitaly-sfondo-animato";
    public const int TentativiMassimi = 2;
    public static readonly TimeSpan IntervalloControlli = TimeSpan.FromHours(6);
    static readonly TimeSpan RitardoMinimo = TimeSpan.FromMinutes(2);
    static readonly TimeSpan RitardoMassimo = TimeSpan.FromMinutes(30);

    // Non l'API: 60 richieste l'ora per indirizzo IP, condivise da tutti i PC dell'azienda (A2).
    public static readonly Uri IndirizzoInfo = new($"https://github.com/{Repository}/releases/latest/download/versione.json");

    public static Uri IndirizzoInstaller(InfoVersione info) =>
        new($"https://github.com/{Repository}/releases/download/v{info.Versione.ToString(3)}/{info.File}");

    [GeneratedRegex(@"^\d{1,5}\.\d{1,5}\.\d{1,5}$")]
    private static partial Regex TreNumeri();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]*\.exe$")]
    private static partial Regex NomeFile();

    [GeneratedRegex(@"^[0-9a-fA-F]{64}$")]
    private static partial Regex Impronta();

    /// <summary>"1.2.3", "v1.2.3" o "1.2.3+commit" (ProductVersion) → 1.2.3; null se non è X.Y.Z.</summary>
    public static Version? VersioneDa(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return null;
        var t = testo.Trim();
        if (t[0] is 'v' or 'V') t = t[1..];
        int piu = t.IndexOf('+');
        if (piu >= 0) t = t[..piu];
        return TreNumeri().IsMatch(t) && Version.TryParse(t, out var v) ? v : null;
    }

    /// <summary>Legge versione.json; null se manca qualcosa o un campo non è valido (spec 3.3).</summary>
    public static InfoVersione? Leggi(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return LeggiInfo(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static InfoVersione? LeggiInfo(JsonElement r)
    {
        if (r.ValueKind != JsonValueKind.Object) return null;
        if (Testo(r, "versione") is not { } testoVersione || !TreNumeri().IsMatch(testoVersione)) return null;
        if (VersioneDa(testoVersione) is not { } versione) return null;
        if (Testo(r, "file") is not { } file || !NomeFile().IsMatch(file)) return null;
        if (Testo(r, "sha256") is not { } sha || !Impronta().IsMatch(sha)) return null;
        return new InfoVersione(versione, file, sha.ToLowerInvariant());
    }

    /// <summary>true se la versione pubblicata è più nuova di quella in uso.</summary>
    public static bool PiuNuova(InfoVersione? info, Version inUso) => info is not null && info.Versione > inUso;

    /// <summary>
    /// Indirizzo finale di un download (dopo i reindirizzamenti) accettabile: HTTPS e le release di questo
    /// repository su github.com, oppure gli host dove GitHub tiene i file delle release.
    /// </summary>
    public static bool IndirizzoAffidabile(Uri indirizzo)
    {
        if (!indirizzo.IsAbsoluteUri || indirizzo.Scheme != Uri.UriSchemeHttps) return false;
        var host = indirizzo.Host.ToLowerInvariant();
        if (host == "github.com")
            return indirizzo.AbsolutePath.StartsWith($"/{Repository}/releases/", StringComparison.OrdinalIgnoreCase);
        return host is "objects.githubusercontent.com" or "release-assets.githubusercontent.com";
    }

    /// <summary>Ritardo del primo controllo dopo l'avvio, tra 2 e 30 minuti: gli accessi del mattino non controllano tutti insieme (A10).</summary>
    public static TimeSpan RitardoPrimoControllo(Random caso) =>
        RitardoMinimo + TimeSpan.FromSeconds(caso.NextDouble() * (RitardoMassimo - RitardoMinimo).TotalSeconds);

    /// <summary>Prossimo controllo: il primo possibile dopo l'avvio, poi 6 ore dopo l'ultimo (mai prima del primo possibile).</summary>
    public static DateTimeOffset ProssimoControllo(DateTimeOffset primoPossibile, DateTimeOffset? ultimoControllo)
    {
        var primo = primoPossibile;
        if (ultimoControllo is not { } ultimo) return primo;
        var dopo = ultimo + IntervalloControlli;
        return dopo > primo ? dopo : primo;
    }

    /// <summary>Si installa solo quando l'utente non vede il desktop e la finestra è chiusa (A4).</summary>
    public static bool MomentoBuono(SegnaliSistema s, bool finestraVisibile) =>
        !finestraVisibile && (s.DesktopCoperto || s.SchermoIntero || s.SessioneNonAttiva || s.MonitorSpenti);

    // ---------- stato.json ----------

    public static StatoAggiornamenti LeggiStato(string? json)
    {
        var vuoto = new StatoAggiornamenti(null, null, 0);
        if (string.IsNullOrWhiteSpace(json)) return vuoto;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.ValueKind != JsonValueKind.Object) return vuoto;
            DateTimeOffset? ultimo = r.TryGetProperty("ultimoControllo", out var u) && u.ValueKind == JsonValueKind.String
                && u.TryGetDateTimeOffset(out var d) ? d : null;
            InfoVersione? pronta = r.TryGetProperty("pronta", out var p) ? LeggiInfo(p) : null;
            int tentativi = pronta is not null && r.TryGetProperty("tentativi", out var t) && t.ValueKind == JsonValueKind.Number
                && t.TryGetInt32(out var n) && n >= 0 ? n : 0;
            return new StatoAggiornamenti(ultimo, pronta, tentativi);
        }
        catch (JsonException)
        {
            return vuoto;
        }
    }

    public static string ScriviStato(StatoAggiornamenti s) => Scrivi(w =>
    {
        if (s.UltimoControllo is { } u) w.WriteString("ultimoControllo", u); else w.WriteNull("ultimoControllo");
        if (s.Pronta is { } p)
        {
            w.WriteStartObject("pronta");
            ScriviInfo(w, p);
            w.WriteEndObject();
        }
        else w.WriteNull("pronta");
        w.WriteNumber("tentativi", s.Tentativi);
    });

    /// <summary>Il contenuto di versione.json (lo scrive anche il workflow di rilascio, con lo stesso formato).</summary>
    public static string ScriviInfo(InfoVersione info) => Scrivi(w => ScriviInfo(w, info));

    static void ScriviInfo(Utf8JsonWriter w, InfoVersione info)
    {
        w.WriteString("versione", info.Versione.ToString(3));
        w.WriteString("file", info.File);
        w.WriteString("sha256", info.Sha256);
    }

    // ---------- Messaggio per la finestra (spec 4.5) ----------

    public static string MessaggioVista(Version inUso, string stato, DateTimeOffset? ultimoControllo, Version? nuova, string? errore) => Scrivi(w =>
    {
        w.WriteString("tipo", "aggiornamenti");
        w.WriteString("versione", inUso.ToString(3));
        w.WriteString("stato", stato);
        if (ultimoControllo is { } u) w.WriteString("ultimoControllo", u); else w.WriteNull("ultimoControllo");
        if (nuova is not null) w.WriteString("versioneNuova", nuova.ToString(3)); else w.WriteNull("versioneNuova");
        if (errore is not null) w.WriteString("errore", errore); else w.WriteNull("errore");
    });

    static string? Testo(JsonElement o, string nome) =>
        o.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static string Scrivi(Action<Utf8JsonWriter> corpo)
    {
        using var flusso = new MemoryStream();
        using (var w = new Utf8JsonWriter(flusso, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartObject();
            corpo(w);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(flusso.ToArray());
    }
}
