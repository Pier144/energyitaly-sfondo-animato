using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Esito della valutazione di un file da importare.</summary>
public sealed record EsitoImport(bool Valido, TipoVoce Tipo, string Id, string NomeFile, string Titolo, string? Motivo)
{
    public static EsitoImport Scartato(string motivo) => new(false, TipoVoce.Immagine, "", "", "", motivo);
}

/// <summary>Regole per importare immagini, video e pagine HTML nella libreria.</summary>
public static class Importazione
{
    const long MB = 1024L * 1024;

    public const string FormatiAccettati = "immagini (jpg, png, webp, gif, bmp), video (mp4, webm) o pagine HTML";

    static readonly Dictionary<string, TipoVoce> Estensioni = new(StringComparer.OrdinalIgnoreCase)
    {
        ["jpg"] = TipoVoce.Immagine,
        ["jpeg"] = TipoVoce.Immagine,
        ["png"] = TipoVoce.Immagine,
        ["webp"] = TipoVoce.Immagine,
        ["gif"] = TipoVoce.Immagine,
        ["bmp"] = TipoVoce.Immagine,
        ["mp4"] = TipoVoce.Video,
        ["webm"] = TipoVoce.Video,
        ["html"] = TipoVoce.Html,
        ["htm"] = TipoVoce.Html
    };

    public static long Limite(TipoVoce tipo) => tipo switch
    {
        TipoVoce.Video => 2048 * MB,
        TipoVoce.Html => 5 * MB,
        _ => 50 * MB
    };

    public static EsitoImport Valuta(string nomeOriginale, long dimensione, Func<string>? suffisso = null)
    {
        var nome = Path.GetFileName(nomeOriginale ?? "");
        var estensione = Path.GetExtension(nome).TrimStart('.').ToLowerInvariant();
        if (!Estensioni.TryGetValue(estensione, out var tipo))
            return EsitoImport.Scartato($"Formato non supportato: usa {FormatiAccettati}");
        if (dimensione <= 0)
            return EsitoImport.Scartato("Il file è vuoto");
        if (dimensione > Limite(tipo))
            return EsitoImport.Scartato($"File troppo grande: il limite per {NomeTipo(tipo)} è {TestoLimite(tipo)}");

        var titolo = Path.GetFileNameWithoutExtension(nome).Trim();
        if (titolo.Length == 0) titolo = "Sfondo";
        var slug = Slug(titolo);
        var codice = (suffisso ?? SuffissoCasuale)();
        return new EsitoImport(true, tipo, $"u-{slug}-{codice}", $"{slug}-{codice}.{estensione}", titolo, null);
    }

    static string NomeTipo(TipoVoce t) => t switch
    {
        TipoVoce.Video => "i video",
        TipoVoce.Html => "le pagine HTML",
        _ => "le immagini"
    };

    static string TestoLimite(TipoVoce t) => t switch
    {
        TipoVoce.Video => "2 GB",
        TipoVoce.Html => "5 MB",
        _ => "50 MB"
    };

    /// <summary>Minuscole senza accenti, cifre e trattini; al massimo 40 caratteri.</summary>
    public static string Slug(string testo)
    {
        var scomposto = testo.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(scomposto.Length);
        bool ultimoTrattino = false;
        foreach (var c in scomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            var l = char.ToLowerInvariant(c);
            if (l is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                sb.Append(l);
                ultimoTrattino = false;
            }
            else if (!ultimoTrattino && sb.Length > 0)
            {
                sb.Append('-');
                ultimoTrattino = true;
            }
        }
        var s = sb.ToString().Trim('-');
        if (s.Length > 40) s = s[..40].Trim('-');
        return s.Length == 0 ? "sfondo" : s;
    }

    public static string SuffissoCasuale() =>
        RandomNumberGenerator.GetString("abcdefghijklmnopqrstuvwxyz0123456789", 6);
}
