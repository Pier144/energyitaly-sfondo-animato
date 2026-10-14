using System.Text.Json;

namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Regole pure della personalizzazione degli sfondi (spec personalizzazione 4.2).</summary>
public static class Personalizzazioni
{
    public const string Personalizzata = "personalizzata";

    /// <summary>Tavolozza di riserva se il manifest non ne ha di valide.</summary>
    public static readonly Tavolozza EnergyItaly = new("energy-italy", "Energy Italy", "#0f1e35", "#7ab52a", "#1e5b8e");

    /// <summary>"#rrggbb" (maiuscole o minuscole).</summary>
    public static bool ColoreValido(string? c) =>
        c is { Length: 7 } && c[0] == '#' && c.Skip(1).All(Uri.IsHexDigit);

    /// <summary>
    /// Porta una personalizzazione a una forma valida per quella voce: numeri al limite e al passo, campi che non
    /// valgono tolti, valori uguali al predefinito tolti. Ogni correzione aggiunge un avviso (solo per il log).
    /// </summary>
    public static Personalizzazione Normalizza(Personalizzazione p, Voce v, IReadOnlyList<Tavolozza> tavolozze, List<string> avvisi)
    {
        var dove = $"personalizzazione di '{v.Id}'";
        string? tavolozza = p.Tavolozza;
        string? fondo = p.Fondo?.ToLowerInvariant();
        string? accento = p.Accento?.ToLowerInvariant();
        var opzioni = new Dictionary<string, ValoreOpzione>();
        var riempimento = p.Riempimento;

        if (v.Tipo == TipoVoce.Animazione)
        {
            if (tavolozza is not null && tavolozza != Personalizzata && tavolozze.All(t => t.Id != tavolozza))
            {
                avvisi.Add($"{dove}: tavolozza '{tavolozza}' sconosciuta, tolta");
                tavolozza = null;
            }
            if (tavolozza == Personalizzata && (!ColoreValido(fondo) || !ColoreValido(accento)))
            {
                avvisi.Add($"{dove}: colori personalizzati mancanti o non validi, torna la tavolozza predefinita");
                tavolozza = null;
            }
            if (tavolozza != Personalizzata)
            {
                fondo = null;
                accento = null;
            }
            if (tavolozza is not null && tavolozze.Count > 0 && tavolozza == tavolozze[0].Id) tavolozza = null;

            foreach (var (id, valore) in p.Opzioni)
            {
                var o = v.Opzioni.FirstOrDefault(x => x.Id == id);
                if (o is null)
                {
                    avvisi.Add($"{dove}: l'opzione '{id}' non esiste più, tolta");
                    continue;
                }
                if (!valore.DelTipo(o.Tipo))
                {
                    avvisi.Add($"{dove}: valore del tipo sbagliato per '{id}', tolto");
                    continue;
                }
                var giusto = valore;
                if (o.Tipo == TipoOpzione.Numero)
                {
                    var allineato = Allinea(o, valore.Numero!.Value);
                    if (allineato != valore.Numero) avvisi.Add($"{dove}: '{id}' portato a {ValoreOpzione.DiNumero(allineato).PerUrl()}");
                    giusto = ValoreOpzione.DiNumero(allineato);
                }
                else if (o.Tipo == TipoOpzione.Scelta && o.Valori!.All(x => x.Valore != valore.Testo))
                {
                    avvisi.Add($"{dove}: valore '{valore.Testo}' non ammesso per '{id}', tolto");
                    continue;
                }
                if (giusto != o.Predefinito) opzioni[id] = giusto;
            }
        }
        else if (tavolozza is not null || fondo is not null || accento is not null || p.Opzioni.Count > 0)
        {
            avvisi.Add($"{dove}: colori e opzioni valgono solo per le animazioni, tolti");
            tavolozza = fondo = accento = null;
        }

        if (riempimento is not null && v.Tipo is not (TipoVoce.Immagine or TipoVoce.Video))
        {
            avvisi.Add($"{dove}: il riempimento vale solo per immagini e video, tolto");
            riempimento = null;
        }
        if (riempimento == Riempimento.Riempi) riempimento = null;

        return new Personalizzazione
        {
            Tavolozza = tavolozza,
            Fondo = fondo,
            Accento = accento,
            Opzioni = opzioni,
            ModoMonitor = p.ModoMonitor,
            Riempimento = riempimento
        };
    }

    /// <summary>Dentro i limiti e sul passo; arrotondato per togliere i resti della virgola mobile.</summary>
    static double Allinea(Opzione o, double n)
    {
        double min = o.Min!.Value, max = o.Max!.Value, passo = o.Passo!.Value;
        var x = min + Math.Round((Math.Clamp(n, min, max) - min) / passo) * passo;
        return Math.Round(Math.Clamp(x, min, max), 6);
    }

    /// <summary>
    /// Applica un comando "personalizza". Errore (da mostrare nella finestra, nulla cambia) se un campo non vale per
    /// quella voce o un valore non è valido; altrimenti il risultato è già normalizzato.
    /// </summary>
    public static (Personalizzazione Risultato, string? Errore) Applica(Personalizzazione? attuale, ModifichePersonalizzazione m,
        Voce v, IReadOnlyList<Tavolozza> tavolozze)
    {
        var p = attuale ?? new Personalizzazione();
        if (v.Tipo != TipoVoce.Animazione &&
            (m.Tavolozza is not null || m.Fondo is not null || m.Accento is not null || m.Opzioni is { Count: > 0 }))
            return (p, "Colori e opzioni valgono solo per le animazioni Energy Italy");
        if (m.Riempimento is not null && v.Tipo is not (TipoVoce.Immagine or TipoVoce.Video))
            return (p, "Il riempimento vale solo per immagini e video");
        if (m.Tavolozza is not null && m.Tavolozza != Personalizzata && tavolozze.All(t => t.Id != m.Tavolozza))
            return (p, "Tavolozza sconosciuta");
        if ((m.Fondo is not null && !ColoreValido(m.Fondo)) || (m.Accento is not null && !ColoreValido(m.Accento)))
            return (p, "Colore non valido");

        var opzioni = new Dictionary<string, ValoreOpzione>(p.Opzioni);
        foreach (var (id, valore) in m.Opzioni ?? new Dictionary<string, ValoreOpzione>())
        {
            var o = v.Opzioni.FirstOrDefault(x => x.Id == id);
            if (o is null) return (p, $"Opzione sconosciuta: {id}");
            if (!valore.DelTipo(o.Tipo) || (o.Tipo == TipoOpzione.Scelta && o.Valori!.All(x => x.Valore != valore.Testo)))
                return (p, $"Valore non valido per {o.Titolo}");
            opzioni[id] = valore;
        }

        var modo = p.ModoMonitor;
        if (m.ModoMonitor is not null)
        {
            if (m.ModoMonitor == "generale") modo = null;
            else if (NomiJson.LeggiModo(m.ModoMonitor) is { } letto) modo = letto;
            else return (p, "Modo monitor non valido");
        }
        var riempimento = p.Riempimento;
        if (m.Riempimento is not null)
        {
            if (NomiJson.LeggiRiempimento(m.Riempimento) is { } letto) riempimento = letto;
            else return (p, "Riempimento non valido");
        }

        string? tavolozza = p.Tavolozza, fondo = p.Fondo, accento = p.Accento;
        if (m.Tavolozza is not null)
        {
            if (m.Tavolozza == Personalizzata && tavolozza != Personalizzata)
            {
                // Si parte dai colori che si vedono ora.
                var (f, a, _) = Colori(p, tavolozze);
                fondo = f;
                accento = a;
            }
            tavolozza = m.Tavolozza;
        }
        if ((m.Fondo is not null || m.Accento is not null) && tavolozza != Personalizzata)
            return (p, "I colori si scelgono con la tavolozza Personalizzata");
        fondo = m.Fondo ?? fondo;
        accento = m.Accento ?? accento;

        var nuova = new Personalizzazione
        {
            Tavolozza = tavolozza,
            Fondo = fondo,
            Accento = accento,
            Opzioni = opzioni,
            ModoMonitor = modo,
            Riempimento = riempimento
        };
        return (Normalizza(nuova, v, tavolozze, []), null);
    }

    /// <summary>Toglie le voci di sfondi sconosciuti, normalizza le altre e toglie quelle rimaste vuote.</summary>
    public static IReadOnlyDictionary<string, Personalizzazione> Riconcilia(IReadOnlyDictionary<string, Personalizzazione> dati,
        IReadOnlyList<Voce> voci, IReadOnlyList<Tavolozza> tavolozze, List<string> avvisi)
    {
        var risultato = new Dictionary<string, Personalizzazione>();
        foreach (var (id, p) in dati)
        {
            var v = voci.FirstOrDefault(x => x.Id == id);
            if (v is null)
            {
                avvisi.Add($"impostazioni.json: personalizzazione di '{id}', sfondo sconosciuto, tolta");
                continue;
            }
            var giusta = Normalizza(p, v, tavolozze, avvisi);
            if (!giusta.Vuota) risultato[id] = giusta;
        }
        return risultato;
    }

    /// <summary>Colori da passare all'animazione; il secondario è null con "personalizzata" (lo ricava sfondo.js).</summary>
    public static (string Fondo, string Accento, string? Secondario) Colori(Personalizzazione? p, IReadOnlyList<Tavolozza> tavolozze)
    {
        if (p?.Tavolozza == Personalizzata && p.Fondo is not null && p.Accento is not null) return (p.Fondo, p.Accento, null);
        var t = tavolozze.FirstOrDefault(x => x.Id == p?.Tavolozza) ?? (tavolozze.Count > 0 ? tavolozze[0] : EnergyItaly);
        return (t.Fondo, t.Accento, t.Secondario);
    }

    /// <summary>URL dello sfondo con colori e opzioni effettive (spec personalizzazione 3.2), senza il parametro monitor.</summary>
    public static string Url(Voce v, Personalizzazione? p, IReadOnlyList<Tavolozza> tavolozze)
    {
        var url = Catalogo.UrlFile(v);
        if (v.Tipo != TipoVoce.Animazione) return url;
        var (fondo, accento, secondario) = Colori(p, tavolozze);
        var parti = new List<string> { "fondo=" + fondo[1..], "accento=" + accento[1..] };
        if (secondario is not null) parti.Add("secondario=" + secondario[1..]);
        foreach (var o in v.Opzioni)
        {
            var valore = p is not null && p.Opzioni.TryGetValue(o.Id, out var scelto) ? scelto : o.Predefinito;
            parti.Add($"{Uri.EscapeDataString(o.Id)}={Uri.EscapeDataString(valore.PerUrl())}");
        }
        return url + "?" + string.Join('&', parti);
    }

    public static ModoMonitor ModoEffettivo(Personalizzazione? p, ModoMonitor generale) => p?.ModoMonitor ?? generale;

    public static Riempimento RiempimentoEffettivo(Personalizzazione? p) => p?.Riempimento ?? Riempimento.Riempi;

    /// <summary>La personalizzazione come oggetto JSON, nella forma di impostazioni.json (spec 4.3) e dello stato.</summary>
    public static void Scrivi(Utf8JsonWriter w, Personalizzazione p)
    {
        w.WriteStartObject();
        if (p.Tavolozza is not null) w.WriteString("tavolozza", p.Tavolozza);
        if (p.Fondo is not null) w.WriteString("fondo", p.Fondo);
        if (p.Accento is not null) w.WriteString("accento", p.Accento);
        if (p.Opzioni.Count > 0)
        {
            w.WriteStartObject("opzioni");
            foreach (var (id, valore) in p.Opzioni) ScriviValore(w, id, valore);
            w.WriteEndObject();
        }
        if (p.ModoMonitor is { } modo) w.WriteString("modoMonitor", NomiJson.Di(modo));
        if (p.Riempimento is { } r) w.WriteString("riempimento", NomiJson.Di(r));
        w.WriteEndObject();
    }

    /// <summary>Un valore JSON come valore di opzione; null se il tipo non va bene o il numero non è rappresentabile.</summary>
    public static ValoreOpzione? ValoreDaJson(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Number => e.TryGetDouble(out var n) && double.IsFinite(n) ? ValoreOpzione.DiNumero(n) : null,
        JsonValueKind.String => ValoreOpzione.DiTesto(e.GetString()!),
        JsonValueKind.True => ValoreOpzione.DiBooleano(true),
        JsonValueKind.False => ValoreOpzione.DiBooleano(false),
        _ => null
    };

    public static void ScriviValore(Utf8JsonWriter w, string nome, ValoreOpzione v)
    {
        if (v.Numero is { } n) w.WriteNumber(nome, n);
        else if (v.Booleano is { } b) w.WriteBoolean(nome, b);
        else w.WriteString(nome, v.Testo);
    }
}
