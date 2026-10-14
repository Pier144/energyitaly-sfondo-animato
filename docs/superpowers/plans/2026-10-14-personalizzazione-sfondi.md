# Personalizzazione degli sfondi - Piano di implementazione

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** dalla Libreria si apre una barra laterale che personalizza ogni sfondo: tavolozza di colori, opzioni proprie dell'animazione, Estendi/Duplica per singolo sfondo, riempimento di immagini e video.

**Architecture:** il manifest (versione 2) dichiara tavolozze e opzioni; il Core le legge, valida le personalizzazioni (`Personalizzazioni`), le salva in `impostazioni.json` e costruisce l'URL dell'animazione con colori e opzioni; il coordinatore calcola la disposizione con il modo dello sfondo in onda e manda un solo `mostra` per volta (`AggiornaDesktop`). `sfondo.js` ricava le sfumature e le offre a JavaScript e CSS; le sette animazioni usano i ruoli invece dei colori fissi. Nella finestra un nuovo componente `barra.js` disegna il pannello.

**Tech Stack:** .NET 10 (Core senza dipendenze), xUnit, JavaScript senza librerie, HTML/CSS, WebView2.

**Spec:** `docs/superpowers/specs/2026-10-14-personalizzazione-sfondi-design.md` (leggerla prima di iniziare).

**Comandi:** test `dotnet test tests/EnergyItaly.SfondoAnimato.Core.Tests`; build `dotnet build src/EnergyItaly.SfondoAnimato`; server di sviluppo `python strumenti/server_sviluppo.py 5610` (oppure la configurazione `sviluppo-web` di `.claude/launch.json`), pagine `http://localhost:5610/sviluppo/prova-app.html`, `prova-player.html`, `prova-due-schermi.html`.

**Regole del progetto** (`docs/PRD.md` RNF-05/06): commenti, messaggi e log in italiano con accenti veri; mai trattini lunghi (em-dash, en-dash) in codice, commenti e documenti. I file C# del progetto usano CRLF: modificarli con l'Edit tool (che conserva gli a capo). Ogni commit termina con la riga `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## File

| File | Cosa |
|---|---|
| `src/EnergyItaly.SfondoAnimato.Core/Personalizzazione.cs` | Crea: tipi (`Opzione`, `ValoreOpzione`, `Tavolozza`, `Personalizzazione`, `ModifichePersonalizzazione`, enum) |
| `src/EnergyItaly.SfondoAnimato.Core/Personalizzazioni.cs` | Crea: regole pure (`Normalizza`, `Applica`, `Riconcilia`, `Url`, `Colori`, `ModoEffettivo`, `RiempimentoEffettivo`, scrittura JSON) |
| `src/EnergyItaly.SfondoAnimato.Core/Catalogo.cs` | Modifica: opzioni e tavolozze dal manifest 2; `Voce.Opzioni` al posto di `Parametri`; `UrlFile` al posto di `UrlBase` |
| `src/EnergyItaly.SfondoAnimato.Core/NomiJson.cs` | Modifica: `Riempimento`, `TipoOpzione` |
| `src/EnergyItaly.SfondoAnimato.Core/Impostazioni.cs`, `ArchivioImpostazioni.cs` | Modifica: campo `personalizzazioni` |
| `src/EnergyItaly.SfondoAnimato.Core/Messaggi.cs` | Modifica: comando `personalizza`, `mostra.riempimento`, campi nuovi dello stato |
| `src/EnergyItaly.SfondoAnimato.Core/Coordinatore.cs` | Modifica: `AggiornaDesktop`, modo per sfondo, comandi nuovi |
| `tests/EnergyItaly.SfondoAnimato.Core.Tests/PersonalizzazioniTests.cs` | Crea |
| `tests/.../CatalogoTests.cs`, `ImpostazioniTests.cs`, `MessaggiTests.cs`, `CoordinatoreTests.cs` | Modifica |
| `animazioni/comune/sfondo.js`, `animazioni/comune/base.css` | Modifica: colori e interruttori |
| `animazioni/manifest.json` | Modifica: versione 2 |
| `animazioni/*.html` (7 file) | Modifica: colori dai ruoli, opzioni |
| `src/EnergyItaly.SfondoAnimato/web/player/player.js`, `player.css` | Modifica: riempimento |
| `src/EnergyItaly.SfondoAnimato/web/app/barra.js` | Crea: pannello laterale |
| `src/EnergyItaly.SfondoAnimato/web/app/app.js`, `app.css`, `index.html` | Modifica |
| `src/EnergyItaly.SfondoAnimato/web/sviluppo/prova-app.html`, `prova-player.html` | Modifica: manifest 2 e comandi nuovi |
| `docs/PRD.md` | Modifica: contratto e requisiti |

## Chunk 1: Core, tipi e regole

### Task 1: tipi della personalizzazione

**Files:**
- Create: `src/EnergyItaly.SfondoAnimato.Core/Personalizzazione.cs`
- Modify: `src/EnergyItaly.SfondoAnimato.Core/NomiJson.cs`
- Test: `tests/EnergyItaly.SfondoAnimato.Core.Tests/PersonalizzazioniTests.cs` (nuovo)

- [ ] **Step 1: test che falliscono** in `PersonalizzazioniTests.cs`:

```csharp
using System.Globalization;
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

public class PersonalizzazioniTests
{
    [Fact]
    public void PerUrl_CulturaInvarianteEInterruttori()
    {
        var prima = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("it-IT");
        try
        {
            Assert.Equal("1.5", ValoreOpzione.DiNumero(1.5).PerUrl());
            Assert.Equal("2", ValoreOpzione.DiNumero(2).PerUrl());
            Assert.Equal("1", ValoreOpzione.DiBooleano(true).PerUrl());
            Assert.Equal("0", ValoreOpzione.DiBooleano(false).PerUrl());
            Assert.Equal("media", ValoreOpzione.DiTesto("media").PerUrl());
        }
        finally { CultureInfo.CurrentCulture = prima; }
    }

    [Fact]
    public void Riempimento_NomiJson()
    {
        Assert.Equal("adatta", NomiJson.Di(Riempimento.Adatta));
        Assert.Equal(Riempimento.Riempi, NomiJson.LeggiRiempimento("riempi"));
        Assert.Null(NomiJson.LeggiRiempimento("allunga"));
        Assert.Equal("interruttore", NomiJson.Di(TipoOpzione.Interruttore));
        Assert.Equal(TipoOpzione.Scelta, NomiJson.LeggiTipoOpzione("scelta"));
    }

    [Fact]
    public void Personalizzazione_Vuota()
    {
        Assert.True(new Personalizzazione().Vuota);
        Assert.False(new Personalizzazione { ModoMonitor = ModoMonitor.Duplica }.Vuota);
    }
}
```

- [ ] **Step 2:** `dotnet test tests/EnergyItaly.SfondoAnimato.Core.Tests` - atteso: errori di compilazione (tipi mancanti).
- [ ] **Step 3: implementazione.** `Personalizzazione.cs`:

```csharp
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

    /// <summary>Valore per la query string: numeri con il punto decimale, interruttori 1/0 (spec P12).</summary>
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
/// Le scelte dell'utente per uno sfondo, solo come differenze dai valori predefiniti (spec P7).
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
```

In `NomiJson.cs` aggiungere:

```csharp
    public static string Di(Riempimento r) => r == Riempimento.Adatta ? "adatta" : "riempi";

    public static string Di(TipoOpzione t) => t switch
    {
        TipoOpzione.Numero => "numero",
        TipoOpzione.Scelta => "scelta",
        _ => "interruttore"
    };

    public static Riempimento? LeggiRiempimento(string? s) => s switch
    {
        "riempi" => Riempimento.Riempi,
        "adatta" => Riempimento.Adatta,
        _ => null
    };

    public static TipoOpzione? LeggiTipoOpzione(string? s) => s switch
    {
        "numero" => TipoOpzione.Numero,
        "scelta" => TipoOpzione.Scelta,
        "interruttore" => TipoOpzione.Interruttore,
        _ => null
    };
```

- [ ] **Step 4:** test verdi (tutti, 263 + 3).
- [ ] **Step 5: commit** "Personalizzazione: tipi del Core".

### Task 2: manifest versione 2 nel catalogo

**Files:**
- Modify: `src/EnergyItaly.SfondoAnimato.Core/Catalogo.cs`
- Modify: `tests/EnergyItaly.SfondoAnimato.Core.Tests/CatalogoTests.cs`, `MessaggiTests.cs` (solo la costruzione di `Voce`)

- [ ] **Step 1: test.** In `CatalogoTests` sostituire la costante `Manifest` e i due test che usano `Parametri`/`UrlBase` con:

```csharp
    const string Manifest = """
        {
          "versione": 2,
          "tavolozze": [
            { "id": "energy-italy", "titolo": "Energy Italy", "fondo": "#0F1E35", "accento": "#7ab52a", "secondario": "#1e5b8e" },
            { "id": "notte", "titolo": "Notte", "fondo": "#05070d", "accento": "#4a90c8", "secondario": "#2b3a55" },
            { "id": "rotta", "titolo": "Rotta", "fondo": "blu", "accento": "#4a90c8", "secondario": "#2b3a55" }
          ],
          "animazioni": [
            { "id": "onda-di-luce", "titolo": "Onda di luce", "file": "onda-di-luce.html", "opzioni": [
              { "id": "ampiezza", "titolo": "Ampiezza", "tipo": "scelta", "predefinito": "lieve",
                "valori": [ { "valore": "lieve", "titolo": "Lieve" }, { "valore": "forte", "titolo": "Forte" } ] },
              { "id": "durata", "titolo": "Durata dell'onda", "tipo": "numero", "min": 8, "max": 30, "passo": 1, "predefinito": 16, "unita": "s" }
            ] },
            { "id": "aurora", "titolo": "Aurora", "file": "aurora.html", "opzioni": [
              { "id": "veli", "titolo": "Veli di luce", "tipo": "interruttore", "predefinito": true },
              { "id": "rotta", "titolo": "Rotta", "tipo": "numero", "min": 5, "max": 1, "passo": 1, "predefinito": 2 },
              { "id": "strana", "titolo": "Strana", "tipo": "colore", "predefinito": 1 }
            ] }
          ]
        }
        """;

    [Fact]
    public void Carica_Manifest2_OpzioniValideEScartate()
    {
        var (voci, avvisi) = Catalogo.Carica(Manifest, new DatiLibreria(), _ => true);

        Assert.Equal(new[] { "onda-di-luce", "aurora" }, voci.Select(v => v.Id));
        var onda = voci[0];
        Assert.Equal(new[] { "ampiezza", "durata" }, onda.Opzioni.Select(o => o.Id));
        Assert.Equal(TipoOpzione.Scelta, onda.Opzioni[0].Tipo);
        Assert.Equal("lieve", onda.Opzioni[0].Predefinito.Testo);
        Assert.Equal(2, onda.Opzioni[0].Valori!.Count);
        Assert.Equal(16, onda.Opzioni[1].Predefinito.Numero);
        Assert.Equal("s", onda.Opzioni[1].Unita);
        var aurora = voci[1];
        Assert.Equal(new[] { "veli" }, aurora.Opzioni.Select(o => o.Id));
        Assert.True(aurora.Opzioni[0].Predefinito.Booleano);
        Assert.Equal(2, avvisi.Count(a => a.Contains("aurora")));
    }

    [Fact]
    public void LeggiTavolozze_ScartaLeNonValideEMetteInMinuscolo()
    {
        var avvisi = new List<string>();
        var t = Catalogo.LeggiTavolozze(Manifest, avvisi);

        Assert.Equal(new[] { "energy-italy", "notte" }, t.Select(x => x.Id));
        Assert.Equal("#0f1e35", t[0].Fondo);
        Assert.Contains(avvisi, a => a.Contains("rotta"));
    }

    [Fact]
    public void LeggiTavolozze_NessunaValida_EnergyItalyDelCodice()
    {
        var t = Catalogo.LeggiTavolozze("""{ "versione": 2, "animazioni": [] }""", []);
        Assert.Equal(Personalizzazioni.EnergyItaly, Assert.Single(t));
    }

    [Fact]
    public void UrlFile_SoloIlFile()
    {
        var (voci, _) = Catalogo.Carica(Manifest, new DatiLibreria(), _ => true);
        Assert.Equal("https://sfondo.energyitaly/animazioni/onda-di-luce.html", Catalogo.UrlFile(voci[0]));
    }
```

  Negli altri test di `CatalogoTests` sostituire `Catalogo.UrlBase(` con `Catalogo.UrlFile(` e, nel test `UrlBase_CodificaIlNomeFile` (rinominarlo `UrlFile_CodificaIlNomeFile`), `new Dictionary<string, string>()` con `[]`. In `MessaggiTests` la voce `Onda` diventa:

```csharp
    static readonly Opzione Durata = new("durata", "Durata", TipoOpzione.Numero, ValoreOpzione.DiNumero(16), 8, 30, 1);
    static readonly Voce Onda = new("onda-di-luce", TipoVoce.Animazione, "Onda di luce", true, "onda-di-luce.html", [Durata], null, true);
```

  (eliminare il campo `Parametri`). I test di `MessaggiTests` che controllano l'URL si sistemano nel Task 6.
- [ ] **Step 2:** compilazione fallisce (`Opzioni`, `LeggiTavolozze`, `UrlFile`, `Personalizzazioni.EnergyItaly` mancanti). `Personalizzazioni.EnergyItaly` arriva nel Task 3: per ora aggiungere in `Personalizzazioni.cs` (nuovo file) solo

```csharp
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
}
```

- [ ] **Step 3: implementazione in `Catalogo.cs`.**
  - `Voce`: il campo `IReadOnlyDictionary<string, string> Parametri` diventa `IReadOnlyList<Opzione> Opzioni` (stessa posizione). Togliere `NessunParametro`; nelle voci della libreria passare `[]`.
  - In `LeggiManifest` sostituire il blocco `parametri` con:

```csharp
                var opzioni = new List<Opzione>();
                if (a.TryGetProperty("opzioni", out var elencoOpzioni) && elencoOpzioni.ValueKind == JsonValueKind.Array)
                {
                    foreach (var o in elencoOpzioni.EnumerateArray())
                    {
                        var opzione = LeggiOpzione(o, id!, avvisi);
                        if (opzione is null) continue;
                        if (opzioni.Any(x => x.Id == opzione.Id))
                        {
                            avvisi.Add($"manifest.json: opzione '{opzione.Id}' di '{id}' ripetuta, ignorata");
                            continue;
                        }
                        opzioni.Add(opzione);
                    }
                }
                voci.Add(new Voce(id!, TipoVoce.Animazione, Testo(a, "titolo") ?? id!, true, file!, opzioni, null, true));
```

  - Nuove funzioni:

```csharp
    /// <summary>Un'opzione del manifest; null (con avviso) se non è coerente (spec personalizzazione 3.1).</summary>
    static Opzione? LeggiOpzione(JsonElement o, string animazione, List<string> avvisi)
    {
        var id = Testo(o, "id");
        var tipo = NomiJson.LeggiTipoOpzione(Testo(o, "tipo"));
        string Scarta(string motivo)
        {
            avvisi.Add($"manifest.json: opzione '{id}' di '{animazione}' {motivo}, ignorata");
            return motivo;
        }
        if (string.IsNullOrWhiteSpace(id) || tipo is null)
        {
            Scarta("senza id o con tipo sconosciuto");
            return null;
        }
        var titolo = Testo(o, "titolo") ?? id;
        o.TryGetProperty("predefinito", out var p);
        switch (tipo)
        {
            case TipoOpzione.Numero:
            {
                double? min = Numero(o, "min"), max = Numero(o, "max"), passo = Numero(o, "passo");
                double? predefinito = p.ValueKind == JsonValueKind.Number ? p.GetDouble() : null;
                if (min is null || max is null || passo is null || predefinito is null || min >= max || passo <= 0 ||
                    predefinito < min || predefinito > max)
                {
                    Scarta("con limiti o predefinito non validi");
                    return null;
                }
                return new Opzione(id, titolo, TipoOpzione.Numero, ValoreOpzione.DiNumero(predefinito.Value),
                    min, max, passo, Testo(o, "unita"));
            }
            case TipoOpzione.Scelta:
            {
                var valori = new List<ValoreScelta>();
                if (o.TryGetProperty("valori", out var elenco) && elenco.ValueKind == JsonValueKind.Array)
                {
                    foreach (var v in elenco.EnumerateArray())
                    {
                        var valore = Testo(v, "valore");
                        if (valore is null || valori.Any(x => x.Valore == valore)) continue;
                        valori.Add(new ValoreScelta(valore, Testo(v, "titolo") ?? valore));
                    }
                }
                var predefinito = p.ValueKind == JsonValueKind.String ? p.GetString() : null;
                if (valori.Count is < 2 or > 4 || predefinito is null || valori.All(x => x.Valore != predefinito))
                {
                    Scarta("con valori o predefinito non validi");
                    return null;
                }
                return new Opzione(id, titolo, TipoOpzione.Scelta, ValoreOpzione.DiTesto(predefinito), Valori: valori);
            }
            default:
            {
                if (p.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    Scarta("con predefinito non valido");
                    return null;
                }
                return new Opzione(id, titolo, TipoOpzione.Interruttore, ValoreOpzione.DiBooleano(p.GetBoolean()));
            }
        }
    }

    static double? Numero(JsonElement e, string nome) =>
        e.TryGetProperty(nome, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    /// <summary>Tavolozze del manifest; se non ce n'è nessuna valida, solo Energy Italy (spec personalizzazione 3.1).</summary>
    public static IReadOnlyList<Tavolozza> LeggiTavolozze(string manifestJson, List<string> avvisi)
    {
        var risultato = new List<Tavolozza>();
        try
        {
            using var doc = JsonDocument.Parse(manifestJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("tavolozze", out var elenco) && elenco.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in elenco.EnumerateArray())
                {
                    var id = Testo(t, "id");
                    string? fondo = Testo(t, "fondo"), accento = Testo(t, "accento"), secondario = Testo(t, "secondario");
                    if (string.IsNullOrWhiteSpace(id) || id == Personalizzazioni.Personalizzata || risultato.Any(x => x.Id == id) ||
                        !Personalizzazioni.ColoreValido(fondo) || !Personalizzazioni.ColoreValido(accento) ||
                        !Personalizzazioni.ColoreValido(secondario))
                    {
                        avvisi.Add($"manifest.json: tavolozza '{id}' non valida, ignorata");
                        continue;
                    }
                    risultato.Add(new Tavolozza(id, Testo(t, "titolo") ?? id, fondo!.ToLowerInvariant(),
                        accento!.ToLowerInvariant(), secondario!.ToLowerInvariant()));
                }
            }
        }
        catch (JsonException) { /* l'avviso sul manifest rotto lo dà già Carica */ }
        return risultato.Count > 0 ? risultato : [Personalizzazioni.EnergyItaly];
    }
```

  - `UrlBase` diventa `UrlFile` e restituisce solo il file (togliere le ultime due righe dei parametri): l'URL completo lo costruisce `Personalizzazioni.Url` (Task 4). In `Messaggi.cs` sostituire per ora `Catalogo.UrlBase(v)` con `Catalogo.UrlFile(v)` (due punti): il Task 6 li cambia di nuovo.
- [ ] **Step 4:** test verdi. I test di `MessaggiTests` che si aspettano `?durata=16` nell'URL falliscono: sono attesi, li sistema il Task 6; annotare quali sono e proseguire solo se gli unici rossi sono quelli.
- [ ] **Step 5: commit** "Catalogo: tavolozze e opzioni dal manifest versione 2".

### Task 3: `Normalizza`, `Applica`, `Riconcilia`

**Files:**
- Modify: `src/EnergyItaly.SfondoAnimato.Core/Personalizzazioni.cs`
- Test: `tests/.../PersonalizzazioniTests.cs`

- [ ] **Step 1: test che falliscono** (aggiungere in `PersonalizzazioniTests`):

```csharp
    static readonly Opzione Velocita = new("velocita", "Velocità", TipoOpzione.Numero, ValoreOpzione.DiNumero(1), 0.5, 2, 0.25, "×");
    static readonly Opzione Ampiezza = new("ampiezza", "Ampiezza", TipoOpzione.Scelta, ValoreOpzione.DiTesto("lieve"),
        Valori: [new("lieve", "Lieve"), new("forte", "Forte")]);
    static readonly Opzione Veli = new("veli", "Veli di luce", TipoOpzione.Interruttore, ValoreOpzione.DiBooleano(true));
    static readonly Voce Anim = new("segnale", TipoVoce.Animazione, "Segnale", true, "segnale.html", [Velocita, Ampiezza, Veli], null, true);
    static readonly Voce Foto = new("u-foto-aaaaaa", TipoVoce.Immagine, "Foto", false, "foto.jpg", [], null, true);
    static readonly Voce Pagina = new("u-pag-bbbbbb", TipoVoce.Html, "Pagina", false, "p.html", [], null, true);
    static readonly Tavolozza Notte = new("notte", "Notte", "#05070d", "#4a90c8", "#2b3a55");
    static readonly IReadOnlyList<Tavolozza> Tav = [Personalizzazioni.EnergyItaly, Notte];

    static Dictionary<string, ValoreOpzione> Op(params (string Id, ValoreOpzione V)[] x) => x.ToDictionary(p => p.Id, p => p.V);

    [Fact]
    public void Normalizza_NumeroAlLimiteEAlPasso()
    {
        var avvisi = new List<string>();
        var p = Personalizzazioni.Normalizza(new() { Opzioni = Op(("velocita", ValoreOpzione.DiNumero(1.6))) }, Anim, Tav, avvisi);
        Assert.Equal(1.5, p.Opzioni["velocita"].Numero);

        p = Personalizzazioni.Normalizza(new() { Opzioni = Op(("velocita", ValoreOpzione.DiNumero(9))) }, Anim, Tav, avvisi);
        Assert.Equal(2, p.Opzioni["velocita"].Numero);
        Assert.Contains(avvisi, a => a.Contains("velocita"));
    }

    [Fact]
    public void Normalizza_ValoriUgualiAlPredefinitoETavolozzaPredefinitaTolti()
    {
        var p = Personalizzazioni.Normalizza(new()
        {
            Tavolozza = "energy-italy",
            Opzioni = Op(("velocita", ValoreOpzione.DiNumero(1)), ("veli", ValoreOpzione.DiBooleano(true))),
            Riempimento = Riempimento.Riempi
        }, Anim, Tav, []);
        Assert.True(p.Vuota);
    }

    [Fact]
    public void Normalizza_OpzioniSconosciuteTipoSbagliatoSceltaNonAmmessaTolte()
    {
        var avvisi = new List<string>();
        var p = Personalizzazioni.Normalizza(new()
        {
            Opzioni = Op(("sparita", ValoreOpzione.DiNumero(3)), ("velocita", ValoreOpzione.DiTesto("tanta")),
                ("ampiezza", ValoreOpzione.DiTesto("media")))
        }, Anim, Tav, avvisi);
        Assert.Empty(p.Opzioni);
        Assert.Equal(3, avvisi.Count);
    }

    [Fact]
    public void Normalizza_PersonalizzataSenzaColori_TornaAllaPredefinita()
    {
        var avvisi = new List<string>();
        var p = Personalizzazioni.Normalizza(new() { Tavolozza = "personalizzata", Fondo = "#102030" }, Anim, Tav, avvisi);
        Assert.True(p.Vuota);
        Assert.NotEmpty(avvisi);
    }

    [Fact]
    public void Normalizza_ColoriFuoriDaPersonalizzataTolti_ColoriInMinuscolo()
    {
        var p = Personalizzazioni.Normalizza(new() { Tavolozza = "notte", Fondo = "#102030" }, Anim, Tav, []);
        Assert.Equal("notte", p.Tavolozza);
        Assert.Null(p.Fondo);

        p = Personalizzazioni.Normalizza(new() { Tavolozza = "personalizzata", Fondo = "#A0B0C0", Accento = "#E0A040" }, Anim, Tav, []);
        Assert.Equal("#a0b0c0", p.Fondo);
        Assert.Equal("#e0a040", p.Accento);
    }

    [Fact]
    public void Normalizza_CampiNonAmmessiPerTipoDiVoce()
    {
        var avvisi = new List<string>();
        var foto = Personalizzazioni.Normalizza(new() { Tavolozza = "notte", Riempimento = Riempimento.Adatta }, Foto, Tav, avvisi);
        Assert.Null(foto.Tavolozza);
        Assert.Equal(Riempimento.Adatta, foto.Riempimento);

        var pagina = Personalizzazioni.Normalizza(new() { Riempimento = Riempimento.Adatta, ModoMonitor = ModoMonitor.Duplica }, Pagina, Tav, avvisi);
        Assert.Null(pagina.Riempimento);
        Assert.Equal(ModoMonitor.Duplica, pagina.ModoMonitor);
    }

    [Fact]
    public void Applica_ErroriPerCampiNonValidi()
    {
        Assert.NotNull(Personalizzazioni.Applica(null, new(Tavolozza: "notte"), Foto, Tav).Errore);
        Assert.NotNull(Personalizzazioni.Applica(null, new(Riempimento: "adatta"), Anim, Tav).Errore);
        Assert.NotNull(Personalizzazioni.Applica(null, new(Tavolozza: "sconosciuta"), Anim, Tav).Errore);
        Assert.NotNull(Personalizzazioni.Applica(null, new(Tavolozza: "personalizzata", Fondo: "blu"), Anim, Tav).Errore);
        Assert.NotNull(Personalizzazioni.Applica(null, new(Opzioni: Op(("nessuna", ValoreOpzione.DiNumero(1)))), Anim, Tav).Errore);
        Assert.NotNull(Personalizzazioni.Applica(null, new(Opzioni: Op(("veli", ValoreOpzione.DiNumero(1)))), Anim, Tav).Errore);
        Assert.NotNull(Personalizzazioni.Applica(null, new(Opzioni: Op(("ampiezza", ValoreOpzione.DiTesto("media")))), Anim, Tav).Errore);
        Assert.NotNull(Personalizzazioni.Applica(null, new(ModoMonitor: "ruota"), Anim, Tav).Errore);
        Assert.NotNull(Personalizzazioni.Applica(null, new(Fondo: "#102030"), Anim, Tav).Errore);
    }

    [Fact]
    public void Applica_UnisceENormalizza()
    {
        var (p, e) = Personalizzazioni.Applica(null, new(Opzioni: Op(("velocita", ValoreOpzione.DiNumero(1.4))), ModoMonitor: "duplica"), Anim, Tav);
        Assert.Null(e);
        Assert.Equal(1.5, p.Opzioni["velocita"].Numero);
        Assert.Equal(ModoMonitor.Duplica, p.ModoMonitor);

        (p, e) = Personalizzazioni.Applica(p, new(Opzioni: Op(("veli", ValoreOpzione.DiBooleano(false))), ModoMonitor: "generale"), Anim, Tav);
        Assert.Null(e);
        Assert.Equal(2, p.Opzioni.Count);
        Assert.Null(p.ModoMonitor);
    }

    [Fact]
    public void Applica_PassaggioAPersonalizzataPartendoDallaTavolozzaScelta()
    {
        var (p, _) = Personalizzazioni.Applica(new() { Tavolozza = "notte" }, new(Tavolozza: "personalizzata"), Anim, Tav);
        Assert.Equal("personalizzata", p.Tavolozza);
        Assert.Equal("#05070d", p.Fondo);
        Assert.Equal("#4a90c8", p.Accento);

        (p, _) = Personalizzazioni.Applica(p, new(Accento: "#E0A040"), Anim, Tav);
        Assert.Equal("#e0a040", p.Accento);

        (p, _) = Personalizzazioni.Applica(p, new(Tavolozza: "notte"), Anim, Tav);
        Assert.Equal("notte", p.Tavolozza);
        Assert.Null(p.Fondo);
    }

    [Fact]
    public void Riconcilia_TogliSfondiSconosciutiEVociVuote()
    {
        var avvisi = new List<string>();
        var dati = new Dictionary<string, Personalizzazione>
        {
            ["segnale"] = new() { Opzioni = Op(("velocita", ValoreOpzione.DiNumero(1))), ModoMonitor = ModoMonitor.Estendi },
            ["aurora"] = new() { Tavolozza = "notte" },
            ["u-foto-aaaaaa"] = new() { Tavolozza = "notte" }
        };
        var r = Personalizzazioni.Riconcilia(dati, [Anim, Foto], Tav, avvisi);

        Assert.Equal(new[] { "segnale" }, r.Keys);
        Assert.Empty(r["segnale"].Opzioni);
        Assert.Contains(avvisi, a => a.Contains("aurora"));
    }
```

- [ ] **Step 2:** compilazione fallisce (`Normalizza`, `Applica`, `Riconcilia`).
- [ ] **Step 3: implementazione** in `Personalizzazioni.cs` (aggiungere `using` necessari: nessuno oltre ai predefiniti):

```csharp
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
            if (tavolozza is not null && tavolozza == tavolozze[0].Id) tavolozza = null;

            foreach (var (id, valore) in p.Opzioni)
            {
                var o = v.Opzioni.FirstOrDefault(x => x.Id == id);
                if (o is null) { avvisi.Add($"{dove}: l'opzione '{id}' non esiste più, tolta"); continue; }
                if (!valore.DelTipo(o.Tipo)) { avvisi.Add($"{dove}: valore del tipo sbagliato per '{id}', tolto"); continue; }
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
            Tavolozza = tavolozza, Fondo = fondo, Accento = accento, Opzioni = opzioni,
            ModoMonitor = p.ModoMonitor, Riempimento = riempimento
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
        bool animazione = v.Tipo == TipoVoce.Animazione;
        if (!animazione && (m.Tavolozza is not null || m.Fondo is not null || m.Accento is not null || m.Opzioni is { Count: > 0 }))
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
            Tavolozza = tavolozza, Fondo = fondo, Accento = accento, Opzioni = opzioni,
            ModoMonitor = modo, Riempimento = riempimento
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
```

  `Colori` serve già qui: va scritto ora come in Task 4 (Step 3), poi il Task 4 aggiunge i suoi test.
- [ ] **Step 4:** test verdi (tranne i rossi attesi di `MessaggiTests` del Task 2).
- [ ] **Step 5: commit** "Personalizzazioni: normalizzazione, comandi e riconciliazione".

### Task 4: URL, colori, modo e riempimento effettivi; scrittura JSON

**Files:**
- Modify: `src/EnergyItaly.SfondoAnimato.Core/Personalizzazioni.cs`
- Test: `tests/.../PersonalizzazioniTests.cs`

- [ ] **Step 1: test** (aggiungere):

```csharp
    [Fact]
    public void Url_TavolozzaEOpzioniEffettive()
    {
        var p = new Personalizzazione { Tavolozza = "notte", Opzioni = Op(("velocita", ValoreOpzione.DiNumero(1.5)), ("veli", ValoreOpzione.DiBooleano(false))) };
        Assert.Equal("https://sfondo.energyitaly/animazioni/segnale.html?fondo=05070d&accento=4a90c8&secondario=2b3a55&velocita=1.5&ampiezza=lieve&veli=0",
            Personalizzazioni.Url(Anim, p, Tav));
        Assert.Equal("https://sfondo.energyitaly/animazioni/segnale.html?fondo=0f1e35&accento=7ab52a&secondario=1e5b8e&velocita=1&ampiezza=lieve&veli=1",
            Personalizzazioni.Url(Anim, null, Tav));
    }

    [Fact]
    public void Url_PersonalizzataSenzaSecondario_AltriTipiSenzaParametri()
    {
        var p = new Personalizzazione { Tavolozza = "personalizzata", Fondo = "#102030", Accento = "#e0a040" };
        Assert.StartsWith("https://sfondo.energyitaly/animazioni/segnale.html?fondo=102030&accento=e0a040&velocita=",
            Personalizzazioni.Url(Anim, p, Tav));
        Assert.Equal("https://libreria.energyitaly/foto.jpg",
            Personalizzazioni.Url(Foto, new() { Riempimento = Riempimento.Adatta }, Tav));
    }

    [Fact]
    public void ModoERiempimentoEffettivi()
    {
        Assert.Equal(ModoMonitor.Estendi, Personalizzazioni.ModoEffettivo(null, ModoMonitor.Estendi));
        Assert.Equal(ModoMonitor.Duplica, Personalizzazioni.ModoEffettivo(new() { ModoMonitor = ModoMonitor.Duplica }, ModoMonitor.Estendi));
        Assert.Equal(Riempimento.Riempi, Personalizzazioni.RiempimentoEffettivo(null));
        Assert.Equal(Riempimento.Adatta, Personalizzazioni.RiempimentoEffettivo(new() { Riempimento = Riempimento.Adatta }));
    }
```

- [ ] **Step 2:** compilazione fallisce.
- [ ] **Step 3: implementazione** (aggiungere `using System.Text.Json;` in testa a `Personalizzazioni.cs`):

```csharp
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

    public static void ScriviValore(Utf8JsonWriter w, string nome, ValoreOpzione v)
    {
        if (v.Numero is { } n) w.WriteNumber(nome, n);
        else if (v.Booleano is { } b) w.WriteBoolean(nome, b);
        else w.WriteString(nome, v.Testo);
    }
```

- [ ] **Step 4:** test verdi (tranne i rossi attesi del Task 2).
- [ ] **Step 5: commit** "Personalizzazioni: URL, colori e modo effettivi".

## Chunk 2: Core, impostazioni, messaggi e coordinatore

### Task 5: `personalizzazioni` in `impostazioni.json`

**Files:**
- Modify: `src/EnergyItaly.SfondoAnimato.Core/Impostazioni.cs`, `ArchivioImpostazioni.cs`
- Test: `tests/.../ImpostazioniTests.cs`

- [ ] **Step 1: test** (aggiungere in `ImpostazioniTests`):

```csharp
    [Fact]
    public void Personalizzazioni_AndataERitorno()
    {
        var originale = Codice() with
        {
            Personalizzazioni = new Dictionary<string, Personalizzazione>
            {
                ["aurora"] = new()
                {
                    Tavolozza = "personalizzata", Fondo = "#102030", Accento = "#e0a040",
                    Opzioni = new Dictionary<string, ValoreOpzione> { ["velocita"] = ValoreOpzione.DiNumero(1.5), ["veli"] = ValoreOpzione.DiBooleano(false), ["ampiezza"] = ValoreOpzione.DiTesto("forte") },
                    ModoMonitor = ModoMonitor.Duplica
                },
                ["u-mare-abc123"] = new() { Riempimento = Riempimento.Adatta }
            }
        };

        var json = ArchivioImpostazioni.Scrivi(originale);
        var avvisi = new List<string>();
        var riletta = ArchivioImpostazioni.Carica(json, Codice(), Validi, avvisi);

        Assert.Empty(avvisi);
        Assert.Contains("\"velocita\": 1.5", json);
        Assert.Contains("\"veli\": false", json);
        var a = riletta.Personalizzazioni["aurora"];
        Assert.Equal("personalizzata", a.Tavolozza);
        Assert.Equal("#e0a040", a.Accento);
        Assert.Equal(1.5, a.Opzioni["velocita"].Numero);
        Assert.False(a.Opzioni["veli"].Booleano);
        Assert.Equal("forte", a.Opzioni["ampiezza"].Testo);
        Assert.Equal(ModoMonitor.Duplica, a.ModoMonitor);
        Assert.Equal(Riempimento.Adatta, riletta.Personalizzazioni["u-mare-abc123"].Riempimento);
    }

    [Fact]
    public void Personalizzazioni_CampiDelTipoSbagliatoTolti()
    {
        var avvisi = new List<string>();
        var i = ArchivioImpostazioni.Carica("""
            { "versione": 2, "personalizzazioni": {
                "aurora": { "tavolozza": 3, "opzioni": { "velocita": [1] }, "modoMonitor": "ruota" },
                "onda-di-luce": "notte",
                "onde-di-energia": { "modoMonitor": "duplica" } } }
            """, Codice(), Validi, avvisi);

        Assert.Equal(new[] { "onde-di-energia" }, i.Personalizzazioni.Keys);
        Assert.Equal(4, avvisi.Count);
    }

    [Fact]
    public void Personalizzazioni_IgnorateNelFileAziendale()
    {
        var i = ArchivioImpostazioni.Predefinite("""{ "versione": 2, "personalizzazioni": { "aurora": { "tavolozza": "notte" } } }""", Validi, Base, []);
        Assert.Empty(i.Personalizzazioni);
    }
```

- [ ] **Step 2:** compilazione fallisce.
- [ ] **Step 3: implementazione.**
  - `Impostazioni`: `public IReadOnlyDictionary<string, Personalizzazione> Personalizzazioni { get; init; } = new Dictionary<string, Personalizzazione>();` (dopo `SfondiAziendaliVisti`).
  - `ArchivioImpostazioni.Leggi` riceve un ultimo parametro `bool conPersonalizzazioni`: `Predefinite` passa `false`, `Carica` passa `true`. Nel `b with { ... }` aggiungere `Personalizzazioni = conPersonalizzazioni ? LeggiPersonalizzazioni(r, nome, avvisi) : b.Personalizzazioni`.
  - Nuova funzione (lettura solo sintattica, la validazione la fa `Riconcilia`):

```csharp
    static IReadOnlyDictionary<string, Personalizzazione> LeggiPersonalizzazioni(JsonElement r, string nome, List<string> avvisi)
    {
        var risultato = new Dictionary<string, Personalizzazione>();
        if (!r.TryGetProperty("personalizzazioni", out var tutte)) return risultato;
        if (tutte.ValueKind != JsonValueKind.Object)
        {
            avvisi.Add($"{nome}: 'personalizzazioni' non valido, ignorato");
            return risultato;
        }
        foreach (var voce in tutte.EnumerateObject())
        {
            var dove = $"{nome}: personalizzazione di '{voce.Name}'";
            if (voce.Value.ValueKind != JsonValueKind.Object)
            {
                avvisi.Add($"{dove} non valida, tolta");
                continue;
            }
            var o = voce.Value;
            string? Testo(string campo)
            {
                if (!o.TryGetProperty(campo, out var x)) return null;
                if (x.ValueKind == JsonValueKind.String) return x.GetString();
                avvisi.Add($"{dove}: '{campo}' non valido, tolto");
                return null;
            }
            var opzioni = new Dictionary<string, ValoreOpzione>();
            if (o.TryGetProperty("opzioni", out var op))
            {
                if (op.ValueKind != JsonValueKind.Object) avvisi.Add($"{dove}: 'opzioni' non valido, tolto");
                else
                {
                    foreach (var kv in op.EnumerateObject())
                    {
                        ValoreOpzione? valore = kv.Value.ValueKind switch
                        {
                            JsonValueKind.Number => ValoreOpzione.DiNumero(kv.Value.GetDouble()),
                            JsonValueKind.String => ValoreOpzione.DiTesto(kv.Value.GetString()!),
                            JsonValueKind.True => ValoreOpzione.DiBooleano(true),
                            JsonValueKind.False => ValoreOpzione.DiBooleano(false),
                            _ => null
                        };
                        if (valore is null) avvisi.Add($"{dove}: opzione '{kv.Name}' non valida, tolta");
                        else opzioni[kv.Name] = valore;
                    }
                }
            }
            ModoMonitor? modo = null;
            if (Testo("modoMonitor") is { } m && (modo = NomiJson.LeggiModo(m)) is null) avvisi.Add($"{dove}: 'modoMonitor' non valido, tolto");
            Riempimento? riempimento = null;
            if (Testo("riempimento") is { } rr && (riempimento = NomiJson.LeggiRiempimento(rr)) is null) avvisi.Add($"{dove}: 'riempimento' non valido, tolto");
            var p = new Personalizzazione
            {
                Tavolozza = Testo("tavolozza"), Fondo = Testo("fondo"), Accento = Testo("accento"),
                Opzioni = opzioni, ModoMonitor = modo, Riempimento = riempimento
            };
            if (!p.Vuota) risultato[voce.Name] = p;
        }
        return risultato;
    }
```

  - `Scrivi`: dopo il blocco `sfondiAziendaliVisti`:

```csharp
            if (i.Personalizzazioni.Count > 0)
            {
                w.WritePropertyName("personalizzazioni");
                w.WriteStartObject();
                foreach (var (id, p) in i.Personalizzazioni)
                {
                    w.WritePropertyName(id);
                    Personalizzazioni.Scrivi(w, p);
                }
                w.WriteEndObject();
            }
```

  Nel test `Personalizzazioni_CampiDelTipoSbagliatoTolti` gli avvisi attesi sono 4: `tavolozza`, opzione `velocita`, `modoMonitor` di aurora (aurora resta vuota e sparisce) e la voce `onda-di-luce` non oggetto. Se il conteggio viene diverso, controllare il codice, non adattare il numero.
- [ ] **Step 4:** test verdi (tranne i rossi attesi del Task 2).
- [ ] **Step 5: commit** "Impostazioni: personalizzazioni degli sfondi".

### Task 6: messaggi

**Files:**
- Modify: `src/EnergyItaly.SfondoAnimato.Core/Messaggi.cs`
- Test: `tests/.../MessaggiTests.cs`

- [ ] **Step 1: test.** In `MessaggiTests`:
  - `Mostra_ContieneVoceEScene`: la chiamata diventa `Messaggi.Mostra(Onda, "https://x/onda.html?durata=16", Riempimento.Riempi, PcRiferimento, 2000, true)`; l'URL atteso diventa `https://x/onda.html?durata=16`; aggiungere `Assert.Equal("riempi", m.GetProperty("riempimento").GetString());`.
  - Nuovi test:

```csharp
    [Fact]
    public void LeggiComandoApp_Personalizza()
    {
        var c = Messaggi.LeggiComandoApp("""
            { "tipo": "personalizza", "id": "segnale", "modifiche": { "tavolozza": "personalizzata", "fondo": "#102030",
              "opzioni": { "velocita": 1.5, "veli": false, "ampiezza": "forte" }, "modoMonitor": "generale", "riempimento": "adatta" } }
            """)!;

        Assert.Equal("personalizza", c.Tipo);
        Assert.Equal("segnale", c.Id);
        Assert.Null(c.Modifiche);
        var p = c.Personalizzazione!;
        Assert.Equal("personalizzata", p.Tavolozza);
        Assert.Equal("#102030", p.Fondo);
        Assert.Null(p.Accento);
        Assert.Equal(1.5, p.Opzioni!["velocita"].Numero);
        Assert.False(p.Opzioni["veli"].Booleano);
        Assert.Equal("forte", p.Opzioni["ampiezza"].Testo);
        Assert.Equal("generale", p.ModoMonitor);
        Assert.Equal("adatta", p.Riempimento);
    }

    [Fact]
    public void Stato_VociConUrlPersonalizzatoOpzioniETavolozze()
    {
        var imp = new Impostazioni
        {
            Playlist = ["onda-di-luce"],
            Personalizzazioni = new Dictionary<string, Personalizzazione>
            {
                ["onda-di-luce"] = new() { Opzioni = new Dictionary<string, ValoreOpzione> { ["durata"] = ValoreOpzione.DiNumero(20) }, ModoMonitor = ModoMonitor.Duplica }
            }
        };
        var s = new StatoVista([Onda], imp, "onda-di-luce", true, false, false, null, 60000, 1800000, true, false, PcRiferimento,
            [Personalizzazioni.EnergyItaly]);

        var v = Json(Messaggi.Stato(s)).GetProperty("voci")[0];

        Assert.EndsWith("onda-di-luce.html?fondo=0f1e35&accento=7ab52a&secondario=1e5b8e&durata=20", v.GetProperty("url").GetString());
        Assert.Equal("durata", v.GetProperty("opzioni")[0].GetProperty("id").GetString());
        Assert.Equal("numero", v.GetProperty("opzioni")[0].GetProperty("tipo").GetString());
        Assert.Equal(16, v.GetProperty("opzioni")[0].GetProperty("predefinito").GetDouble());
        Assert.Equal(20, v.GetProperty("personalizzazione").GetProperty("opzioni").GetProperty("durata").GetDouble());
        Assert.Equal("duplica", v.GetProperty("modoMonitor").GetString());
        Assert.Equal("riempi", v.GetProperty("riempimento").GetString());
        Assert.Equal("energy-italy", Json(Messaggi.Stato(s)).GetProperty("tavolozze")[0].GetProperty("id").GetString());
    }
```

  - Nel test `Stato_CampiPrincipali` aggiungere `Assert.Equal(JsonValueKind.Null, j.GetProperty("voci")[0].GetProperty("personalizzazione").ValueKind);`.
- [ ] **Step 2:** compilazione fallisce.
- [ ] **Step 3: implementazione.**
  - `ComandoApp`: `public sealed record ComandoApp(string Tipo, string? Id, IReadOnlyList<string>? Ids, ModificheImpostazioni? Modifiche, ModifichePersonalizzazione? Personalizzazione = null);`
  - `StatoVista`: aggiungere in fondo `IReadOnlyList<Tavolozza>? Tavolozze = null` (null = solo Energy Italy).
  - `LeggiComandoApp`: se `tipo == "personalizza"` l'oggetto `modifiche` si legge con la funzione nuova e `ModificheImpostazioni` resta null; altrimenti come oggi.

```csharp
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
```

  - `Mostra(Voce v, string url, Riempimento riempimento, Disposizione d, int dissolvenzaMs, bool fineVideo)`: scrive `url` dato e `riempimento` (`NomiJson.Di`).
  - `Stato`: all'inizio `var tavolozze = s.Tavolozze ?? [Personalizzazioni.EnergyItaly];`. Per ogni voce:

```csharp
            var p = s.Impostazioni.Personalizzazioni.GetValueOrDefault(v.Id);
            w.WriteString("url", Personalizzazioni.Url(v, p, tavolozze));
            // ... campi esistenti (tolto il vecchio "url") ...
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
```

    e dopo l'array `voci`:

```csharp
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
```

    con

```csharp
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
```

  - In `Coordinatore.MandaMostra` per ora: `player.Invia(Messaggi.Mostra(c, Catalogo.UrlFile(c), Riempimento.Riempi, disposizione, dissolvenzaMs, fineVideo));` (il Task 7 lo sostituisce).
- [ ] **Step 4:** tutti i test verdi (anche i rossi attesi dei Task 2-5).
- [ ] **Step 5: commit** "Messaggi: comando personalizza, riempimento e stato delle personalizzazioni".

### Task 7: coordinatore, un solo punto che manda lo sfondo

Rifattorizzazione senza cambiare il comportamento: tutti i test esistenti devono restare verdi.

**Files:**
- Modify: `src/EnergyItaly.SfondoAnimato.Core/Coordinatore.cs`
- Test: `tests/.../CoordinatoreTests.cs`

- [ ] **Step 1: test** (comportamenti da tenere, aggiungere):

```csharp
    [Fact]
    public void CambioSfondo_UnSoloMostra()
    {
        var c = Avvia();
        var prima = player.DiTipo("mostra").Count();

        c.Successivo();

        Assert.Equal(prima + 1, player.DiTipo("mostra").Count());
    }

    [Fact]
    public void ModoGenerale_UnSoloMostra()
    {
        var c = Avvia();
        var prima = player.DiTipo("mostra").Count();

        c.DaApp(Comando(new { tipo = "impostazioni", modifiche = new { modoMonitor = "duplica" } }));

        Assert.Equal(prima + 1, player.DiTipo("mostra").Count());
    }
```

- [ ] **Step 2:** eseguire: dovrebbero già passare (fissano il comportamento prima del cambio).
- [ ] **Step 3: implementazione.**
  - Campo `IReadOnlyList<Tavolozza> tavolozze = [Personalizzazioni.EnergyItaly];` e costante `const int DissolvenzaPersonalizzazioneMs = 600;`.
  - Funzioni:

```csharp
    Personalizzazione? PersonalizzazioneDi(string? id) =>
        id is not null && imp.Personalizzazioni.TryGetValue(id, out var p) ? p : null;

    /// <summary>Disposizione con il modo dello sfondo in onda (spec personalizzazione P5); non manda niente.</summary>
    Disposizione CalcolaDisposizione() =>
        LayoutMonitor.Calcola(monitor, Personalizzazioni.ModoEffettivo(PersonalizzazioneDi(motore.Corrente?.Id), imp.ModoMonitor));

    /// <summary>Unico punto che manda lo sfondo al player: ricalcola la disposizione e manda un solo "mostra".</summary>
    void AggiornaDesktop(int dissolvenzaMs)
    {
        var nuova = CalcolaDisposizione();
        bool areaCambiata = nuova.Finestra != disposizione.Finestra;
        bool cambiata = !Uguale(nuova, disposizione);
        disposizione = nuova;
        if (cambiata) log.Info(DescriviMonitor());
        if (areaCambiata) player.ImpostaArea(nuova.Finestra);
        MandaMostra(dissolvenzaMs);
    }
```

  - `MandaMostra` resta privato e lo chiama solo `AggiornaDesktop`:

```csharp
    void MandaMostra(int dissolvenzaMs)
    {
        if (!playerPronto || motore.Corrente is not { } c) return;
        bool fineVideo = imp.FineVideo && imp.RotazioneAttiva;
        var p = PersonalizzazioneDi(c.Id);
        player.Invia(Messaggi.Mostra(c, Personalizzazioni.Url(c, p, tavolozze), Personalizzazioni.RiempimentoEffettivo(p),
            disposizione, dissolvenzaMs, fineVideo));
    }
```

  - Sostituzioni: in `DopoCambio` `MandaMostra(imp.DissolvenzaSecondi * 1000)` diventa `AggiornaDesktop(imp.DissolvenzaSecondi * 1000)`; nell'evento `pronto` `MandaMostra(0)` diventa `AggiornaDesktop(0)`; in `CambiaImpostazioni` (ramo video) `MandaMostra(0)` diventa `AggiornaDesktop(0)`.
  - `RicalcolaDisposizione`:

```csharp
    void RicalcolaDisposizione(bool conDissolvenza)
    {
        var nuova = CalcolaDisposizione();
        if (Uguale(nuova, disposizione)) return;
        if (nuova.ModoEffettivo != disposizione.ModoEffettivo)
        {
            AggiornaDesktop(conDissolvenza ? imp.DissolvenzaSecondi * 1000 : 0);
        }
        else
        {
            disposizione = nuova;
            log.Info(DescriviMonitor());
            player.ImpostaArea(nuova.Finestra);
            if (playerPronto) player.Invia(Messaggi.Layout(nuova));
        }
        InviaStato();
    }
```

  - `Avvia`: le righe `disposizione = LayoutMonitor.Calcola(monitor, imp.ModoMonitor); log.Info(DescriviMonitor()); player.ImpostaArea(disposizione.Finestra);` si spostano **dopo** `motore.Avvia(imp.UltimaVoce);` e il calcolo diventa `disposizione = CalcolaDisposizione();`. `monitor = monitorIniziali;` resta dov'è. Subito dopo `RicaricaVoci(avvisi);` aggiungere `tavolozze = Catalogo.LeggiTavolozze(manifestJson, avvisi);`; subito dopo `imp = ArchivioImpostazioni.Carica(...)` aggiungere `imp = imp with { Personalizzazioni = Personalizzazioni.Riconcilia(imp.Personalizzazioni, voci, tavolozze, avvisi) };` (prima del `foreach` che scrive gli avvisi).
  - `CreaStato` passa `tavolozze` come ultimo argomento di `StatoVista`.
- [ ] **Step 4:** tutti i test verdi.
- [ ] **Step 5: commit** "Coordinatore: un solo punto manda lo sfondo al player".

### Task 8: coordinatore, comandi di personalizzazione

**Files:**
- Modify: `src/EnergyItaly.SfondoAnimato.Core/Coordinatore.cs`
- Test: `tests/.../CoordinatoreTests.cs`

- [ ] **Step 1: test.** Sostituire la costante `Manifest` di `CoordinatoreTests` con una versione 2 (le voci restano a, b, c; tutti gli altri test devono restare verdi):

```csharp
    const string Manifest = """
        { "versione": 2,
          "tavolozze": [
            { "id": "energy-italy", "titolo": "Energy Italy", "fondo": "#0f1e35", "accento": "#7ab52a", "secondario": "#1e5b8e" },
            { "id": "notte", "titolo": "Notte", "fondo": "#05070d", "accento": "#4a90c8", "secondario": "#2b3a55" } ],
          "animazioni": [
          { "id": "a", "titolo": "A", "file": "a.html", "opzioni": [
            { "id": "velocita", "titolo": "Velocità", "tipo": "numero", "min": 0.5, "max": 2, "passo": 0.25, "predefinito": 1 } ] },
          { "id": "b", "titolo": "B", "file": "b.html" },
          { "id": "c", "titolo": "C", "file": "c.html" } ] }
        """;
```

  Nuovi test:

```csharp
    [Fact]
    public void Personalizza_SfondoInOnda_UnMostraDa600ms()
    {
        var c = Avvia();
        var prima = player.DiTipo("mostra").Count();

        c.DaApp(Comando(new { tipo = "personalizza", id = "a", modifiche = new { tavolozza = "notte", opzioni = new { velocita = 1.5 } } }));

        Assert.Equal(prima + 1, player.DiTipo("mostra").Count());
        var m = player.Ultimo("mostra");
        Assert.Equal(600, m.GetProperty("dissolvenzaMs").GetInt32());
        Assert.Contains("fondo=05070d", m.GetProperty("url").GetString());
        Assert.Contains("velocita=1.5", m.GetProperty("url").GetString());
        Assert.Contains("\"tavolozza\": \"notte\"", archivio.Impostazioni);
        Assert.NotEqual(JsonValueKind.Null, app.UltimoStato.GetProperty("voci")[0].GetProperty("personalizzazione").ValueKind);
    }

    [Fact]
    public void Personalizza_SfondoNonInOnda_NessunMostra()
    {
        var c = Avvia();
        var prima = player.DiTipo("mostra").Count();

        c.DaApp(Comando(new { tipo = "personalizza", id = "b", modifiche = new { modoMonitor = "duplica" } }));

        Assert.Equal(prima, player.DiTipo("mostra").Count());
        Assert.Equal("duplica", app.UltimoStato.GetProperty("voci")[1].GetProperty("modoMonitor").GetString());
    }

    [Fact]
    public void Personalizza_Errore_AvvisoENienteSalvato()
    {
        var c = Avvia();
        c.DaApp(Comando(new { tipo = "personalizza", id = "a", modifiche = new { tavolozza = "sconosciuta" } }));

        Assert.Contains("Tavolozza sconosciuta", app.Avvisi);
        Assert.DoesNotContain("personalizzazioni", archivio.Impostazioni);
    }

    [Fact]
    public void ModoDelloSfondo_SiApplicaQuandoVaInOnda()
    {
        var c = Avvia();
        c.DaApp(Comando(new { tipo = "personalizza", id = "b", modifiche = new { modoMonitor = "duplica" } }));

        c.Successivo();

        var m = player.Ultimo("mostra");
        Assert.Equal("b", m.GetProperty("id").GetString());
        Assert.Equal("duplica", m.GetProperty("modo").GetString());
        Assert.Equal(2, m.GetProperty("scene").GetArrayLength());

        c.Successivo();
        Assert.Equal("estendi", player.Ultimo("mostra").GetProperty("modo").GetString());
    }

    [Fact]
    public void Avvio_UltimaVoceConModoSuo_PrimoMostraGiusto()
    {
        archivio.Impostazioni = """{ "versione": 2, "ultimaVoce": "a", "personalizzazioni": { "b": { "modoMonitor": "duplica" } } }""";

        var c = Avvia();

        Assert.Equal("b", c.Corrente?.Id);
        Assert.Equal("duplica", player.Ultimo("mostra").GetProperty("modo").GetString());
    }

    [Fact]
    public void RipristinaSfondo_TornaIlPredefinito()
    {
        var c = Avvia();
        c.DaApp(Comando(new { tipo = "personalizza", id = "a", modifiche = new { tavolozza = "notte" } }));

        c.DaApp(Comando(new { tipo = "ripristinaSfondo", id = "a" }));

        Assert.Contains("fondo=0f1e35", player.Ultimo("mostra").GetProperty("url").GetString());
        Assert.Equal(JsonValueKind.Null, app.UltimoStato.GetProperty("voci")[0].GetProperty("personalizzazione").ValueKind);
    }

    [Fact]
    public void RipristinaAziendali_AzzeraLePersonalizzazioniEAggiornaIlDesktop()
    {
        var c = Avvia();
        c.DaApp(Comando(new { tipo = "personalizza", id = "a", modifiche = new { tavolozza = "notte" } }));
        var prima = player.DiTipo("mostra").Count();

        c.DaApp(Comando(new { tipo = "ripristina" }));

        Assert.Equal(prima + 1, player.DiTipo("mostra").Count());
        Assert.DoesNotContain("personalizzazioni", archivio.Impostazioni);
    }

    [Fact]
    public void EliminaSfondoImportato_TogliLaSuaPersonalizzazione()
    {
        archivio.Libreria = """{ "versione": 1, "voci": [ { "id": "u-f-aaaaaa", "tipo": "immagine", "titolo": "F", "file": "f.jpg" } ] }""";
        archivio.FileLibreria.Add("f.jpg");
        var c = Avvia();
        c.DaApp(Comando(new { tipo = "personalizza", id = "u-f-aaaaaa", modifiche = new { riempimento = "adatta" } }));
        Assert.Contains("u-f-aaaaaa", archivio.Impostazioni);

        c.DaApp(Comando(new { tipo = "elimina", id = "u-f-aaaaaa" }));

        Assert.DoesNotContain("u-f-aaaaaa", archivio.Impostazioni);
    }
```

  (Se la posizione di "b" nell'array `voci` dello stato non è 1, cercarla per `id` invece di usare l'indice. Se un test esistente confronta l'URL esatto di un'animazione nel messaggio `mostra` o nello stato, ora l'URL ha i parametri dei colori: aggiornare l'atteso, non il codice.)
- [ ] **Step 2:** i test nuovi falliscono (comandi non riconosciuti).
- [ ] **Step 3: implementazione.**
  - In `DaApp`: `case "personalizza": if (c.Id is not null && c.Personalizzazione is not null) Personalizza(c.Id, c.Personalizzazione); break;` e `case "ripristinaSfondo": if (c.Id is not null) RipristinaSfondo(c.Id); break;`.
  - Funzioni:

```csharp
    // ---------- Personalizzazione degli sfondi ----------

    /// <summary>Ciò che si vede ora: se cambia, il desktop va aggiornato.</summary>
    (string Url, Riempimento Riempimento, ModoMonitor Modo)? Impronta()
    {
        if (motore.Corrente is not { } c) return null;
        var p = PersonalizzazioneDi(c.Id);
        return (Personalizzazioni.Url(c, p, tavolozze), Personalizzazioni.RiempimentoEffettivo(p), CalcolaDisposizione().ModoEffettivo);
    }

    void ImpostaPersonalizzazione(string id, Personalizzazione p)
    {
        var tutte = new Dictionary<string, Personalizzazione>(imp.Personalizzazioni);
        if (p.Vuota) tutte.Remove(id);
        else tutte[id] = p;
        imp = imp with { Personalizzazioni = tutte };
    }

    void Personalizza(string id, ModifichePersonalizzazione m)
    {
        var v = voci.FirstOrDefault(x => x.Id == id);
        if (v is null)
        {
            InviaAvviso("Questo sfondo non è disponibile");
            InviaStato();
            return;
        }
        var prima = Impronta();
        var (nuova, errore) = Personalizzazioni.Applica(PersonalizzazioneDi(id), m, v, tavolozze);
        if (errore is not null)
        {
            InviaAvviso(errore);
            InviaStato();
            return;
        }
        ImpostaPersonalizzazione(id, nuova);
        SalvaImpostazioni();
        if (Impronta() != prima) AggiornaDesktop(DissolvenzaPersonalizzazioneMs);
        InviaStato();
    }

    void RipristinaSfondo(string id)
    {
        var prima = Impronta();
        ImpostaPersonalizzazione(id, new Personalizzazione());
        SalvaImpostazioni();
        if (Impronta() != prima) AggiornaDesktop(DissolvenzaPersonalizzazioneMs);
        InviaStato();
    }
```

  - `Ripristina()`: all'inizio `var prima = Impronta();`; togliere la chiamata `RicalcolaDisposizione(conDissolvenza: true);` e al suo posto `if (Impronta() != prima) AggiornaDesktop(imp.DissolvenzaSecondi * 1000);`. (`predefinite` non ha personalizzazioni, quindi `imp = predefinite with { ... }` le azzera.)
  - `Elimina(id)`: prima di `SalvaImpostazioni();` aggiungere `ImpostaPersonalizzazione(id, new Personalizzazione());`.
- [ ] **Step 4:** tutti i test verdi; `dotnet build src/EnergyItaly.SfondoAnimato` senza avvisi.
- [ ] **Step 5: commit** "Coordinatore: personalizza, ripristino per sfondo e modo per sfondo".

## Chunk 3: animazioni e player

### Task 9: colori e interruttori in `sfondo.js`, `base.css`

**Files:**
- Modify: `animazioni/comune/sfondo.js`, `animazioni/comune/base.css`

- [ ] **Step 1:** in `sfondo.js`, prima di `window.Sfondo = {`, aggiungere:

```js
  // ---------- Colori (spec personalizzazione 3.3) ----------
  // Tre colori base dalla query string (esadecimali senza #); sfumature ricavate cambiando la luminosità HSL.
  const ENERGY_ITALY = { fondo: '#0f1e35', accento: '#7ab52a', secondario: '#1e5b8e' };
  // Ruolo derivato: [colore base, punti di luminosità]. Con Energy Italy cadono sui colori usati fino alla 1.1.2.
  const RUOLI = {
    fondoScuro: ['fondo', -6],
    fondoChiaro: ['fondo', 8],
    accentoChiaro: ['accento', 21],
    accentoPallido: ['accento', 36],
    secondarioScuro: ['secondario', -5],
    secondarioMedio: ['secondario', 11],
    secondarioChiaro: ['secondario', 20]
  };

  const limita = (x) => Math.max(0, Math.min(100, x));

  function inRgb(hex) {
    return [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16));
  }

  function inHsl(hex) {
    const [r, g, b] = inRgb(hex).map((x) => x / 255);
    const max = Math.max(r, g, b);
    const min = Math.min(r, g, b);
    const l = (max + min) / 2;
    if (max === min) return [0, 0, l * 100];
    const d = max - min;
    const s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
    let h = max === r ? (g - b) / d + (g < b ? 6 : 0) : max === g ? (b - r) / d + 2 : (r - g) / d + 4;
    return [h * 60, s * 100, l * 100];
  }

  function daHsl(h, s, l) {
    s /= 100;
    l /= 100;
    const k = (n) => (n + h / 30) % 12;
    const a = s * Math.min(l, 1 - l);
    const f = (n) => l - a * Math.max(-1, Math.min(k(n) - 3, 9 - k(n), 1));
    return '#' + [0, 8, 4].map((n) => Math.round(f(n) * 255).toString(16).padStart(2, '0')).join('');
  }

  /** Lo stesso colore con luminosità (e saturazione) spostate di qualche punto. */
  function sposta(hex, luce, saturazione) {
    const [h, s, l] = inHsl(hex);
    return daHsl(h, limita(s + (saturazione || 0)), limita(l + luce));
  }

  function leggiColore(nome) {
    const v = new URLSearchParams(location.search).get(nome);
    return v && /^[0-9a-fA-F]{6}$/.test(v) ? '#' + v.toLowerCase() : null;
  }

  const colori = {
    fondo: leggiColore('fondo') || ENERGY_ITALY.fondo,
    accento: leggiColore('accento') || ENERGY_ITALY.accento
  };
  // Con "Personalizzata" arrivano solo fondo e accento: il secondario si ricava dal fondo (spec P4).
  colori.secondario = leggiColore('secondario') || (leggiColore('fondo') ? sposta(colori.fondo, 22, 20) : ENERGY_ITALY.secondario);
  for (const [ruolo, [base, luce]] of Object.entries(RUOLI)) colori[ruolo] = sposta(colori[base], luce);
  // Velo dietro il logo bianco: resta scuro anche con un fondo chiaro.
  colori.velo = daHsl(inHsl(colori.fondo)[0], inHsl(colori.fondo)[1], Math.min(inHsl(colori.fondo)[2] - 6, 10));

  const stileRadice = document.documentElement.style;
  for (const [ruolo, hex] of Object.entries(colori)) {
    const nome = '--' + ruolo.replace(/[A-Z]/g, (c) => '-' + c.toLowerCase());
    stileRadice.setProperty(nome, hex);
    stileRadice.setProperty(nome + '-rgb', inRgb(hex).join(', '));
  }
```

  e dentro `window.Sfondo = { ... }`:

```js
    /** I colori dei ruoli come "#rrggbb" (spec personalizzazione 3.3). */
    colori() {
      return Object.assign({}, colori);
    },

    /** Un ruolo con la luminosità spostata di qualche punto: per le sfumature che non hanno un ruolo proprio. */
    tono(ruolo, luce) {
      return sposta(colori[ruolo], luce || 0);
    },

    /** "rgba(...)" di un ruolo o di un colore "#rrggbb". */
    rgba(colore, alfa) {
      const hex = colore.startsWith('#') ? colore : colori[colore];
      return 'rgba(' + inRgb(hex).join(',') + ',' + alfa + ')';
    },

    /** Interruttore dalla query string: 1/0 (accetta anche true/false). */
    interruttore(nome, predefinito) {
      const v = new URLSearchParams(location.search).get(nome);
      if (v === '1' || v === 'true') return true;
      if (v === '0' || v === 'false') return false;
      return predefinito;
    },
```

  Aggiornare il commento in testa a `sfondo.js` con i parametri `fondo`, `accento`, `secondario`.
- [ ] **Step 2:** `base.css`: `background: #0f1e35;` diventa `background: var(--fondo, #0f1e35);`.
- [ ] **Step 3: verifica nel browser.** Avviare il server di sviluppo; aprire `http://localhost:5610/animazioni/segnale.html?fondo=05070d&accento=4a90c8` e nella console: `Sfondo.colori()` restituisce tutti i ruoli, `getComputedStyle(document.documentElement).getPropertyValue('--accento-chiaro')` non è vuoto, `Sfondo.tono('fondo', 0) === '#05070d'`. Senza parametri `Sfondo.colori().accentoChiaro` è vicino a `#b2e068` (scarto massimo 10 per componente).
- [ ] **Step 4: commit** "sfondo.js: colori dalla tavolozza, sfumature e interruttori".

### Task 10: `manifest.json` versione 2

**Files:**
- Modify: `animazioni/manifest.json`

- [ ] **Step 1:** sostituire il contenuto con:

```json
{
  "versione": 2,
  "tavolozze": [
    { "id": "energy-italy", "titolo": "Energy Italy", "fondo": "#0f1e35", "accento": "#7ab52a", "secondario": "#1e5b8e" },
    { "id": "notte", "titolo": "Notte", "fondo": "#05070d", "accento": "#4a90c8", "secondario": "#2b3a55" },
    { "id": "alba", "titolo": "Alba", "fondo": "#1d1530", "accento": "#f0a04c", "secondario": "#7a3b69" },
    { "id": "mare", "titolo": "Mare", "fondo": "#062a33", "accento": "#2ec4b6", "secondario": "#1b6f8a" },
    { "id": "bosco", "titolo": "Bosco", "fondo": "#0d1f14", "accento": "#9bd45a", "secondario": "#2f6b3f" }
  ],
  "animazioni": [
    { "id": "onda-di-luce", "titolo": "Onda di luce", "file": "onda-di-luce.html", "opzioni": [
      { "id": "ampiezza", "titolo": "Ampiezza", "tipo": "scelta", "predefinito": "lieve",
        "valori": [ { "valore": "lieve", "titolo": "Lieve" }, { "valore": "media", "titolo": "Media" }, { "valore": "forte", "titolo": "Forte" } ] },
      { "id": "durata", "titolo": "Durata dell'onda", "tipo": "numero", "min": 8, "max": 30, "passo": 1, "predefinito": 16, "unita": "s" },
      { "id": "intensita", "titolo": "Intensità", "tipo": "numero", "min": 0.4, "max": 1, "passo": 0.1, "predefinito": 1 }
    ] },
    { "id": "onde-di-energia", "titolo": "Onde di energia", "file": "onde-di-energia.html", "opzioni": [
      { "id": "durata", "titolo": "Durata", "tipo": "numero", "min": 40, "max": 160, "passo": 10, "predefinito": 80, "unita": "s" },
      { "id": "linee", "titolo": "Linee", "tipo": "numero", "min": 12, "max": 40, "passo": 2, "predefinito": 28 }
    ] },
    { "id": "paesaggio-rinnovabile", "titolo": "Paesaggio rinnovabile", "file": "paesaggio-rinnovabile.html", "opzioni": [
      { "id": "velocita", "titolo": "Velocità", "tipo": "numero", "min": 0.5, "max": 2, "passo": 0.25, "predefinito": 1, "unita": "×" }
    ] },
    { "id": "circuito", "titolo": "Circuito", "file": "circuito.html", "opzioni": [
      { "id": "tracce", "titolo": "Tracce", "tipo": "numero", "min": 12, "max": 40, "passo": 2, "predefinito": 24 },
      { "id": "velocita", "titolo": "Velocità", "tipo": "numero", "min": 0.5, "max": 2, "passo": 0.25, "predefinito": 1, "unita": "×" }
    ] },
    { "id": "aurora", "titolo": "Aurora", "file": "aurora.html", "opzioni": [
      { "id": "velocita", "titolo": "Velocità", "tipo": "numero", "min": 0.5, "max": 2, "passo": 0.25, "predefinito": 1, "unita": "×" },
      { "id": "veli", "titolo": "Veli di luce", "tipo": "interruttore", "predefinito": true }
    ] },
    { "id": "segnale", "titolo": "Segnale", "file": "segnale.html", "opzioni": [
      { "id": "velocita", "titolo": "Velocità", "tipo": "numero", "min": 0.5, "max": 2, "passo": 0.25, "predefinito": 1, "unita": "×" },
      { "id": "onde", "titolo": "Anelli per impulso", "tipo": "numero", "min": 0, "max": 4, "passo": 1, "predefinito": 2 }
    ] },
    { "id": "trama", "titolo": "Trama", "file": "trama.html", "opzioni": [
      { "id": "velocita", "titolo": "Velocità", "tipo": "numero", "min": 0.5, "max": 2, "passo": 0.25, "predefinito": 1, "unita": "×" },
      { "id": "distanza", "titolo": "Distanza tra i marchi", "tipo": "numero", "min": 90, "max": 200, "passo": 10, "predefinito": 130 }
    ] }
  ]
}
```

  Nota: `distanza` ha predefinito 130 (oggi 132): con passo 10 da 90 il predefinito deve cadere sul passo, altrimenti il cursore non lo mostra; la differenza non si vede.
- [ ] **Step 2:** `python -c "import json;json.load(open('animazioni/manifest.json',encoding='utf-8'))"` senza errori; `dotnet test` verde.
- [ ] **Step 3: commit** "Manifest versione 2: tavolozze e opzioni".

### Task 11: animazioni, colori dai ruoli (prima parte)

**Files:** `animazioni/onda-di-luce.html`, `animazioni/onde-di-energia.html`, `animazioni/circuito.html`, `animazioni/aurora.html`

**Regole per ogni animazione:**
1. Prima di un nuovo `Sfondo.avvia(...)` niente cambia nella struttura; si sostituiscono solo i colori.
2. In JavaScript: un esadecimale diventa `Sfondo.tono(ruolo, scarto)` (o `C.ruolo` con `const C = Sfondo.colori();` quando lo scarto è quello di un ruolo con nome); un `rgba(r,g,b,a)` diventa `Sfondo.rgba(Sfondo.tono(ruolo, scarto), a)` o `Sfondo.rgba('ruolo', a)`.
3. Nei blocchi `<style>`: per i ruoli con nome `var(--ruolo)` o `rgba(var(--ruolo-rgb), a)`; per gli altri scarti si definisce nello script una variabile della pagina (`document.documentElement.style.setProperty('--scia', Sfondo.tono('accento', 39))`) e la si usa nel CSS.
4. Il bianco resta bianco. Il velo scuro dietro il logo usa `velo` (`Sfondo.rgba('velo', a)`).
5. Corrispondenze (colore 1.1.2 -> ruolo e scarto di luminosità, calcolate sulla tavolozza Energy Italy):

| Animazione | Colori |
|---|---|
| onda-di-luce | `#0e1c32` fondo -1; `#0f1e35` fondo; `#132a46` fondo +4; `#173656` fondoChiaro; `rgb(15,30,53)` (velo del logo) velo; `rgb(122,181,42)` accento; `rgb(178,224,104)` accentoChiaro; nel codice delle celle i colori `[21,38,61]` (fondo +3), `[31,70,104]` (secondario -10) e `[122,181,42]` (accento) diventano `inRgb` di `Sfondo.tono(...)`: aggiungere in pagina una funzione `rgbDi(hex)` che restituisce `[r,g,b]` |
| onde-di-energia | `#0c182c` fondo -2; `#112743` fondo +3; `#163556` fondoChiaro; `rgb(12,24,44)` velo; `#1e5b8e`/`rgb(30,91,142)` secondario; `#2f78b5` secondarioMedio; `#4f8fc4` secondarioChiaro; `#7AB52A`/`rgb(122,181,42)` accento |
| circuito | `#0a1527`/`rgb(10,21,39)` velo; `#0c1a30` fondo -2; `#0e223d` fondo +1; `#132f52` fondo +6; `#214d7a` secondario -3; `#3d74a6` secondarioMedio; `#7AB52A`/`rgb(122,181,42)` accento; `#dcf7b0` accento +39; nel CSS `.alone` usa `var(--accento)`, `.nucleo` una variabile `--nucleo` impostata a `Sfondo.tono('accento', 39)` |
| aurora | `#07111f`/`rgb(7,17,31)` fondoScuro (il velo del logo: velo); `#0b1a30` fondo -2; `#10243f` fondo +2; `rgb(30,91,142)` secondario; `rgb(74,144,200)` secondarioChiaro; `rgb(122,181,42)` accento; `rgb(170,225,110)` accentoChiaro; `veli` si legge con `Sfondo.interruttore('veli', true)` |

- [ ] **Step 1:** applicare le regole a `onda-di-luce.html`.
- [ ] **Step 2: verifica nel browser** (server di sviluppo): aprire la pagina senza parametri accanto alla versione 1.1.2 (`git show v1.1.2:animazioni/onda-di-luce.html > %TEMP%\onda-112.html`, servita copiandola in `animazioni/` con un nome temporaneo da non committare, oppure confrontando con una cattura fatta prima delle modifiche). Nessuna differenza evidente (spec P10). Poi con `?fondo=05070d&accento=4a90c8&secondario=2b3a55`, `?fondo=1d1530&accento=f0a04c&secondario=7a3b69` e `?fondo=d8e4ee&accento=1e7a3a` (personalizzata chiara): leggibile, logo visibile. Nessun errore in console.
- [ ] **Step 3:** ripetere Step 1-2 per `onde-di-energia.html`, `circuito.html`, `aurora.html`.
- [ ] **Step 4:** cercare colori rimasti: `grep -n "#[0-9a-fA-F]\{6\}\|rgba\?(" animazioni/onda-di-luce.html animazioni/onde-di-energia.html animazioni/circuito.html animazioni/aurora.html`: devono restare solo bianchi (`255,255,255`) e i colori dentro le chiamate ai ruoli.
- [ ] **Step 5: commit** "Animazioni: colori dalla tavolozza (onda di luce, onde, circuito, aurora)".

### Task 12: animazioni, colori dai ruoli (seconda parte)

**Files:** `animazioni/paesaggio-rinnovabile.html`, `animazioni/segnale.html`, `animazioni/trama.html`

Stesse regole del Task 11.

| Animazione | Colori |
|---|---|
| paesaggio-rinnovabile | `#08142a` e `#081628`/`rgb(8,22,40)` fondo -4; `#0a172b` fondo -3; `#0f2747` fondo +4; `#102a4c` fondo +5; `#173a63` fondo +11; `#1b4a78` secondarioScuro; `#1e5b8e`/`rgb(30,91,142)` secondario; `#24608f` secondario +1; `#2a5f93` secondario +3; `#4f90c8` secondarioChiaro; `#6f93b8` secondario +24; `#9dbad6` secondario +39; `#a9c6e2` secondario +44; `rgb(190,225,255)` secondario +54; `#d5e4f2` secondario +55; `#dbe8f5` secondario +57; `#7AB52A`/`rgb(122,181,42)` accento; `rgb(214,245,160)` accentoPallido. Le sfumature chiare delle pale e del cielo nei blocchi `<style>` passano da variabili della pagina impostate nello script |
| segnale | `#081324`/`rgb(8,19,36)` velo; `#0d1f38` fondo; `#12304f` fondo +6; `rgb(74,144,200)` secondarioChiaro; `rgb(122,181,42)` accento; `#b2e068`/`rgb(178,224,104)` accentoChiaro; `#eefad9` accento +48. Nel CSS `.cerchio-onda` usa `rgba(var(--accento-chiaro-rgb), 0.6)`, `.carica` `var(--accento-chiaro)` |
| trama | `#0a1729`/`rgb(10,23,41)` fondo -3 (anche il velo del logo: velo); `#0f2240` fondo +2; `#132c4d` fondo +5; `rgb(30,91,142)` secondario; `rgb(122,181,42)` accento; `rgb(178,224,104)` accentoChiaro. Il parametro `passo` diventa `distanza` (`Sfondo.parametro('distanza', 130)`); aggiornare il commento dei parametri |

- [ ] **Step 1-3:** come Task 11, per le tre animazioni, con le stesse verifiche (anche `prova-due-schermi.html?animazione=segnale.html` con una tavolozza: `&fondo=05070d&accento=4a90c8&secondario=2b3a55`).
- [ ] **Step 4:** grep dei colori rimasti come nel Task 11.
- [ ] **Step 5: commit** "Animazioni: colori dalla tavolozza (paesaggio, segnale, trama)".

### Task 13: player, riempimento; pagine di prova del player

**Files:**
- Modify: `src/EnergyItaly.SfondoAnimato/web/player/player.js`, `player.css`, `src/EnergyItaly.SfondoAnimato/web/sviluppo/prova-player.html`

- [ ] **Step 1:** `player.css`, in fondo:

```css
/* Immagini e video con riempimento "adatta": interi, bande nere ai lati (spec personalizzazione 4.6). */
.elemento.adatta {
  background: #000;
}

.elemento.adatta img,
.elemento.adatta video {
  object-fit: contain;
}
```

- [ ] **Step 2:** `player.js`, in `creaElemento`, nei rami `immagine` e video, dopo `nodo.className = 'elemento';` aggiungere `if (dati.riempimento === 'adatta') nodo.classList.add('adatta');`.
- [ ] **Step 3:** `prova-player.html`: l'URL delle animazioni si costruisce dal manifest 2:

```js
  const t = manifest.tavolozze[0];
  const voci = manifest.animazioni.map((a) => ({
    id: a.id, titolo: a.titolo, tipoVoce: 'animazione',
    url: '/animazioni/' + a.file + '?' + new URLSearchParams([
      ['fondo', t.fondo.slice(1)], ['accento', t.accento.slice(1)], ['secondario', t.secondario.slice(1)],
      ...(a.opzioni || []).map((o) => [o.id, typeof o.predefinito === 'boolean' ? (o.predefinito ? '1' : '0') : String(o.predefinito)])
    ])
  }));
```

  e un pulsante "Immagine adattata" che manda la voce `logo` con `riempimento: 'adatta'` (aggiungere il campo `riempimento` al messaggio `mostra` in `mostra(v)`: `riempimento: v.riempimento || 'riempi'`).
- [ ] **Step 4: verifica** su `http://localhost:5610/sviluppo/prova-player.html`: tutte le animazioni si caricano; l'immagine adattata mostra il logo intero su fondo nero.
- [ ] **Step 5: commit** "Player: riempimento adatta per immagini e video".

## Chunk 4: finestra

### Task 14: pagina di prova della finestra

**Files:**
- Modify: `src/EnergyItaly.SfondoAnimato/web/sviluppo/prova-app.html`

Va fatto prima della barra, perché tutte le verifiche della finestra passano da qui.

- [ ] **Step 1:** simulare il Core (spec 4.8). Al posto della costruzione attuale di `voci`:

```js
  const tavolozze = manifest.tavolozze;
  const personalizzazioni = {};
  const perUrl = (v) => typeof v === 'boolean' ? (v ? '1' : '0') : String(v);
  function urlDi(base, v) {
    if (v.tipoVoce !== 'animazione') return base;
    const p = personalizzazioni[v.id] || {};
    const t = tavolozze.find((x) => x.id === p.tavolozza) || tavolozze[0];
    const parti = p.tavolozza === 'personalizzata'
      ? [['fondo', p.fondo.slice(1)], ['accento', p.accento.slice(1)]]
      : [['fondo', t.fondo.slice(1)], ['accento', t.accento.slice(1)], ['secondario', t.secondario.slice(1)]];
    v.opzioni.forEach((o) => parti.push([o.id, perUrl(p.opzioni && o.id in p.opzioni ? p.opzioni[o.id] : o.predefinito)]));
    return base + '?' + new URLSearchParams(parti);
  }
  function completa(v) {
    const p = personalizzazioni[v.id] || null;
    v.personalizzazione = p;
    v.url = urlDi(v.urlFile, v);
    v.modoMonitor = (p && p.modoMonitor) || s.impostazioni.modoMonitor;
    v.riempimento = (p && p.riempimento) || 'riempi';
  }
  const voci = manifest.animazioni.map((a) => ({
    id: a.id, tipoVoce: 'animazione', titolo: a.titolo, aziendale: true, durataSecondi: null, disponibile: true,
    urlFile: '/animazioni/' + a.file, opzioni: a.opzioni || []
  }));
  voci.push({ id: 'u-logo-aaaaaa', tipoVoce: 'immagine', titolo: 'Logo come immagine', aziendale: false, urlFile: '/animazioni/img/brand/LogoEnergyItalyWhite.svg', opzioni: [], durataSecondi: null, disponibile: true });
  voci.push({ id: 'u-video-bbbbbb', tipoVoce: 'video', titolo: 'Timelapse ufficio', aziendale: false, urlFile: '/non-esiste.mp4', opzioni: [], durataSecondi: 160, disponibile: false });
```

  - `s` riceve `tavolozze`; `inviaStato()` chiama `voci.forEach(completa)` prima di mandare; `scene()` usa il modo effettivo della voce in onda: `const v = voci.find((x) => x.id === s.corrente); s.modo = monitor.length === 1 ? 'estendi' : ((personalizzazioni[s.corrente] || {}).modoMonitor || s.impostazioni.modoMonitor);`.
  - Nuovi casi nello `switch`:

```js
      case 'personalizza': {
        const v = voci.find((x) => x.id === m.id);
        const p = Object.assign({}, personalizzazioni[m.id] || {});
        const mod = m.modifiche || {};
        if (mod.tavolozza !== undefined) {
          if (mod.tavolozza === 'personalizzata' && p.tavolozza !== 'personalizzata') {
            const t = tavolozze.find((x) => x.id === p.tavolozza) || tavolozze[0];
            p.fondo = t.fondo; p.accento = t.accento;
          }
          if (mod.tavolozza !== 'personalizzata') { delete p.fondo; delete p.accento; }
          p.tavolozza = mod.tavolozza === tavolozze[0].id ? undefined : mod.tavolozza;
        }
        if (mod.fondo) p.fondo = mod.fondo;
        if (mod.accento) p.accento = mod.accento;
        if (mod.opzioni) {
          p.opzioni = Object.assign({}, p.opzioni, mod.opzioni);
          v.opzioni.forEach((o) => { if (p.opzioni[o.id] === o.predefinito) delete p.opzioni[o.id]; });
          if (!Object.keys(p.opzioni).length) delete p.opzioni;
        }
        if (mod.modoMonitor) p.modoMonitor = mod.modoMonitor === 'generale' ? undefined : mod.modoMonitor;
        if (mod.riempimento) p.riempimento = mod.riempimento === 'riempi' ? undefined : mod.riempimento;
        Object.keys(p).forEach((k) => { if (p[k] === undefined) delete p[k]; });
        if (Object.keys(p).length) personalizzazioni[m.id] = p; else delete personalizzazioni[m.id];
        break;
      }
      case 'ripristinaSfondo': delete personalizzazioni[m.id]; break;
```

  - Nel caso `ripristina` aggiungere `Object.keys(personalizzazioni).forEach((k) => delete personalizzazioni[k]);`; nel caso `elimina` `delete personalizzazioni[m.id];`; le voci importate simulate ricevono `urlFile` e `opzioni: []`.
- [ ] **Step 2: verifica:** `http://localhost:5610/sviluppo/prova-app.html` si apre come prima (In onda, Libreria, Impostazioni) senza errori in console; le miniature delle animazioni si vedono.
- [ ] **Step 3: commit** "Prova della finestra: manifest 2 e personalizzazioni simulate".

### Task 15: componente `barra.js` e stili

**Files:**
- Create: `src/EnergyItaly.SfondoAnimato/web/app/barra.js`
- Modify: `src/EnergyItaly.SfondoAnimato/web/app/app.css`, `index.html`

- [ ] **Step 1:** `index.html`: la griglia della Libreria diventa `<div class="libreria-corpo"><div class="griglia" id="griglia"></div></div>`; prima di `<script src="app.js"></script>` aggiungere `<script src="barra.js"></script>`; la descrizione di "Ripristina impostazioni aziendali" diventa "Intervallo, ordine, monitor, opzioni e personalizzazioni degli sfondi tornano alle scelte dell'IT. La libreria resta com'è."; sotto il `div#modo-palco` aggiungere `<span class="nota-modo" id="nota-modo-palco" hidden>Scelto per questo sfondo nella Libreria</span>` (dentro `.palco-testa`).
- [ ] **Step 2:** `barra.js` (codice completo):

```js
/* Barra laterale della Libreria: personalizzazione di uno sfondo (spec personalizzazione 4.7).
   Riceve da app.js gli strumenti comuni e manda i comandi "personalizza", "ripristinaSfondo" e "mostra". */
window.BarraSfondo = function (d) {
  'use strict';
  const { el, invia, inScala, miniatura, icona } = d;

  const SPIEGA_MODO = {
    estendi: "Un'unica scena su tutti i monitor.",
    duplica: 'Ogni monitor mostra la sua copia completa.'
  };
  const SPIEGA_RIEMPIMENTO = {
    riempi: 'Riempie lo schermo tagliando i bordi.',
    adatta: 'Mostra tutto, con bande nere ai lati.'
  };

  let id = null;
  let stato = null;
  let costruitaPer = null;
  let bozza = {};
  let attesaAnteprima = null;
  let urlAnteprima = null;
  const parti = {};

  const nodo = el('aside', { class: 'pannello-sfondo', 'aria-labelledby': 'pannello-titolo', hidden: true });
  nodo.addEventListener('keydown', (e) => { if (e.key === 'Escape') chiudi(); });

  function voce() {
    return (stato && stato.voci.find((v) => v.id === id)) || null;
  }

  function tipoTesto(v) {
    return { animazione: 'Animazione Energy Italy', immagine: 'Immagine', video: 'Video' }[v.tipoVoce] || 'Pagina HTML';
  }

  function personalizza(modifiche) {
    invia({ tipo: 'personalizza', id, modifiche });
  }

  function valoreOpzione(v, o) {
    if (o.id in bozza) return bozza[o.id];
    const p = v.personalizzazione;
    return p && p.opzioni && o.id in p.opzioni ? p.opzioni[o.id] : o.predefinito;
  }

  function tavolozzaScelta(v) {
    return (v.personalizzazione && v.personalizzazione.tavolozza) || stato.tavolozze[0].id;
  }

  function coloriPersonalizzati(v) {
    const p = v.personalizzazione || {};
    if (p.tavolozza === 'personalizzata') return { fondo: bozza.fondo || p.fondo, accento: bozza.accento || p.accento };
    const t = stato.tavolozze.find((x) => x.id === tavolozzaScelta(v)) || stato.tavolozze[0];
    return { fondo: bozza.fondo || t.fondo, accento: bozza.accento || t.accento };
  }

  function etichetta(o, valore) {
    return String(valore).replace('.', ',') + (o.unita ? (o.unita === '×' ? '×' : ' ' + o.unita) : '');
  }

  // ---------- Anteprima ----------
  // Mentre si trascina un cursore o si sceglie un colore l'URL si ricostruisce qui (spec 4.7, "Invii e anteprima").
  function urlConBozza(v) {
    if (v.tipoVoce !== 'animazione' || !Object.keys(bozza).length) return v.url;
    const u = new URL(v.url, location.href);
    if (bozza.fondo || bozza.accento) {
      const c = coloriPersonalizzati(v);
      u.searchParams.set('fondo', c.fondo.slice(1));
      u.searchParams.set('accento', c.accento.slice(1));
      u.searchParams.delete('secondario');
    }
    Object.entries(bozza).forEach(([chiave, valore]) => {
      if (chiave === 'fondo' || chiave === 'accento') return;
      u.searchParams.set(chiave, typeof valore === 'boolean' ? (valore ? '1' : '0') : String(valore));
    });
    return u.toString();
  }

  function aggiornaAnteprima(v, subito) {
    clearTimeout(attesaAnteprima);
    const fai = () => {
      const palco = parti.palco;
      if (!v.disponibile) {
        palco.replaceChildren(el('div', { class: 'segnaposto' }, 'File non trovato'));
        urlAnteprima = null;
        return;
      }
      if (v.tipoVoce === 'animazione') {
        const url = urlConBozza(v);
        if (url === urlAnteprima) return;
        urlAnteprima = url;
        const f = el('iframe', { class: 'miniatura-iframe', title: 'Anteprima', tabindex: '-1', 'aria-hidden': 'true' });
        f.src = url;
        palco.replaceChildren(f);
        inScala(palco, f, 1920, 1080);
      } else if (v.tipoVoce === 'immagine' || v.tipoVoce === 'video') {
        const chiave = v.url + '|' + v.riempimento;
        if (chiave === urlAnteprima) return;
        urlAnteprima = chiave;
        const media = v.tipoVoce === 'immagine'
          ? el('img', { src: v.url, alt: '' })
          : el('video', { src: v.url, autoplay: true, loop: true, muted: true, playsinline: true });
        if (media.tagName === 'VIDEO') media.muted = true;
        media.className = v.riempimento === 'adatta' ? 'adatta' : '';
        palco.replaceChildren(media);
      } else if (urlAnteprima !== v.url) {
        urlAnteprima = v.url;
        palco.replaceChildren(miniatura('pannello', v));
      }
    };
    if (subito) fai(); else attesaAnteprima = setTimeout(fai, 250);
  }

  // ---------- Controlli ----------
  function pillole(etichettaGruppo, valori, scelto, alClic, disattivo) {
    return el('div', { class: 'segmentato', role: 'group', 'aria-label': etichettaGruppo },
      valori.map(([valore, testo]) => el('button', {
        type: 'button', class: 'pillola', 'aria-pressed': String(valore === scelto), disabled: disattivo,
        onclick: () => alClic(valore)
      }, testo)));
  }

  function gruppo(titolo, ...contenuto) {
    return el('section', { class: 'gruppo-pannello' }, el('h4', null, titolo), ...contenuto);
  }

  function sezioneColori(v, spento) {
    const scelta = tavolozzaScelta(v);
    const tavole = stato.tavolozze.map((t) => el('button', {
      type: 'button', class: 'tavolozza', 'aria-pressed': String(scelta === t.id), disabled: spento,
      onclick: () => personalizza({ tavolozza: t.id })
    }, el('span', { class: 'campione', style: `background: linear-gradient(135deg, ${t.fondo} 50%, ${t.accento} 50%)` }), t.titolo));
    tavole.push(el('button', {
      type: 'button', class: 'tavolozza', 'aria-pressed': String(scelta === 'personalizzata'), disabled: spento,
      onclick: () => personalizza({ tavolozza: 'personalizzata' })
    }, el('span', { class: 'campione arcobaleno' }), 'Personalizzata'));
    const contenuto = [el('div', { class: 'tavolozze' }, tavole)];
    if (scelta === 'personalizzata') {
      const c = coloriPersonalizzati(v);
      contenuto.push(el('div', { class: 'colori-liberi' },
        ['fondo', 'accento'].map((chiave) => {
          const campo = el('input', { type: 'color', value: c[chiave], disabled: spento, 'aria-label': chiave === 'fondo' ? 'Colore di fondo' : 'Colore di accento' });
          const codice = el('b', null, c[chiave].toUpperCase());
          campo.addEventListener('input', () => {
            bozza[chiave] = campo.value;
            codice.textContent = campo.value.toUpperCase();
            aggiornaAnteprima(v, false);
          });
          campo.addEventListener('change', () => {
            // La scelta è confermata: lo stato che arriva va disegnato, non scartato come "trascinamento in corso".
            delete bozza[chiave];
            personalizza({ [chiave]: campo.value });
          });
          parti['colore-' + chiave] = campo;
          return el('label', { class: 'colore' }, campo, el('span', null, chiave === 'fondo' ? 'Fondo' : 'Accento', codice));
        })));
    }
    return gruppo('COLORI', ...contenuto);
  }

  function controlloOpzione(v, o, spento) {
    const valore = valoreOpzione(v, o);
    if (o.tipo === 'numero') {
      const uscita = el('output', null, etichetta(o, valore));
      const cursore = el('input', { type: 'range', min: o.min, max: o.max, step: o.passo, value: valore, disabled: spento, 'aria-label': o.titolo });
      cursore.addEventListener('input', () => {
        bozza[o.id] = Number(cursore.value);
        uscita.textContent = etichetta(o, cursore.value);
        aggiornaAnteprima(v, false);
      });
      cursore.addEventListener('change', () => {
        delete bozza[o.id];
        personalizza({ opzioni: { [o.id]: Number(cursore.value) } });
      });
      parti['opzione-' + o.id] = cursore;
      return el('div', { class: 'opzione' }, el('div', { class: 'opzione-testa' }, el('span', null, o.titolo), uscita), cursore);
    }
    if (o.tipo === 'scelta') {
      return el('div', { class: 'opzione' }, el('div', { class: 'opzione-testa' }, el('span', null, o.titolo)),
        pillole(o.titolo, o.valori.map((x) => [x.valore, x.titolo]), valore, (x) => personalizza({ opzioni: { [o.id]: x } }), spento));
    }
    return el('div', { class: 'opzione opzione-riga' }, el('span', null, o.titolo),
      el('button', {
        type: 'button', role: 'switch', class: 'interruttore', 'aria-checked': String(valore), 'aria-label': o.titolo, disabled: spento,
        onclick: () => personalizza({ opzioni: { [o.id]: !valore } })
      }, el('span')));
  }

  function sezioneModo(v, spento) {
    const p = v.personalizzazione || {};
    const scelto = p.modoMonitor || 'generale';
    const generale = stato.impostazioni.modoMonitor === 'duplica' ? 'Duplica' : 'Estendi';
    let testo = scelto === 'generale' ? `Segue la scelta generale (ora: ${generale}).` : SPIEGA_MODO[scelto];
    if (stato.monitor.length < 2) testo += ' Vale quando colleghi altri monitor.';
    return gruppo('CON PIÙ MONITOR',
      pillole('Con più monitor', [['generale', 'Come in Impostazioni'], ['estendi', 'Estendi'], ['duplica', 'Duplica']], scelto,
        (x) => personalizza({ modoMonitor: x }), spento),
      el('p', { class: 'spiega' }, testo));
  }

  function costruisci(v) {
    costruitaPer = JSON.stringify([v.id, v.disponibile, v.personalizzazione, v.riempimento, stato.impostazioni.modoMonitor, stato.monitor.length, stato.corrente, stato.playlist.includes(v.id)]);
    const spento = !v.disponibile;
    parti.palco = el('div', { class: 'pannello-palco' });
    urlAnteprima = null;
    const corpo = [el('div', null, parti.palco, el('p', { class: 'mini' }, 'ANTEPRIMA · LE MODIFICHE SI SALVANO DA SOLE'))];
    if (v.tipoVoce === 'animazione') {
      corpo.push(sezioneColori(v, spento));
      if (v.opzioni.length) corpo.push(gruppo('FUNZIONAMENTO', v.opzioni.map((o) => controlloOpzione(v, o, spento))));
    }
    if (v.tipoVoce === 'immagine' || v.tipoVoce === 'video') {
      corpo.push(gruppo('RIEMPIMENTO',
        pillole('Riempimento', [['riempi', 'Riempi'], ['adatta', 'Adatta']], v.riempimento, (x) => personalizza({ riempimento: x }), spento),
        el('p', { class: 'spiega' }, SPIEGA_RIEMPIMENTO[v.riempimento])));
    }
    corpo.push(sezioneModo(v, spento));

    const inPlaylist = stato.playlist.includes(v.id);
    nodo.replaceChildren(
      el('div', { class: 'pannello-testa' },
        el('div', null,
          el('h3', { id: 'pannello-titolo' }, v.titolo),
          el('p', { class: 'tenue' }, tipoTesto(v) + (inPlaylist ? ' · in playlist' : ''))),
        el('button', { type: 'button', class: 'tondo-piccolo', 'aria-label': 'Chiudi', onclick: () => chiudi() }, icona('chiudi'))),
      el('div', { class: 'pannello-corpo' }, corpo),
      el('div', { class: 'pannello-piede' },
        el('button', {
          type: 'button', class: 'bottone-contorno', disabled: !v.personalizzazione,
          onclick: () => invia({ tipo: 'ripristinaSfondo', id })
        }, 'Ripristina questo sfondo'),
        el('button', {
          type: 'button', class: 'bottone-verde', disabled: !v.disponibile || stato.corrente === v.id,
          onclick: () => invia({ tipo: 'mostra', id })
        }, 'Mostra ora')));
    aggiornaAnteprima(v, true);
  }

  // ---------- Interfaccia per app.js ----------
  function apri(nuovoId) {
    id = nuovoId;
    bozza = {};
    nodo.hidden = false;
    costruitaPer = null;
    aggiorna(stato);
    const primo = nodo.querySelector('button, input');
    if (primo) primo.focus();
  }

  function chiudi() {
    if (id === null) return;
    const chiuso = id;
    id = null;
    nodo.hidden = true;
    nodo.replaceChildren();
    clearTimeout(attesaAnteprima);
    if (d.alChiudere) d.alChiudere(chiuso);
  }

  function aggiorna(nuovoStato) {
    stato = nuovoStato;
    if (id === null || !stato) return;
    const v = voce();
    if (!v) { chiudi(); return; }
    // Una modifica confermata è arrivata: la bozza ha fatto il suo lavoro.
    const firma = JSON.stringify([v.id, v.disponibile, v.personalizzazione, v.riempimento, stato.impostazioni.modoMonitor, stato.monitor.length, stato.corrente, stato.playlist.includes(v.id)]);
    if (firma === costruitaPer) return;
    // Non ricostruire sotto il dito: se si sta trascinando, si aspetta il prossimo stato.
    const attivo = document.activeElement;
    if (attivo && nodo.contains(attivo) && attivo.tagName === 'INPUT' && Object.keys(bozza).length) return;
    bozza = {};
    const fuoco = attivo && nodo.contains(attivo) ? attivo.getAttribute('aria-label') || attivo.textContent : null;
    costruisci(v);
    if (fuoco) {
      const torna = Array.from(nodo.querySelectorAll('button, input')).find((x) => (x.getAttribute('aria-label') || x.textContent) === fuoco);
      if (torna) torna.focus();
    }
  }

  return { nodo, apri, chiudi, aggiorna, idAperto: () => id };
};
```

- [ ] **Step 3:** `app.css`, prima di `/* Impostazioni */`:

```css
/* Libreria: barra laterale (spec personalizzazione 4.7) */
.libreria-corpo { display: flex; align-items: flex-start; gap: 24px; }
.libreria-corpo .griglia { flex: 1; min-width: 0; }
.anteprima-bottone.scelta { outline: 3px solid var(--verde-chiaro); outline-offset: 2px; }
.etichetta-personalizzato { position: absolute; top: 8px; right: 8px; padding: 4px 9px; border-radius: 12px; background: rgba(7, 17, 31, .85); color: var(--verde-chiaro); font-family: var(--mono); font-size: 11px; font-weight: 600; letter-spacing: .06em; }
.pannello-sfondo { position: sticky; top: 0; flex: none; display: flex; flex-direction: column; width: 400px; max-height: calc(100vh - 104px); border: 1px solid var(--bordo-forte); border-radius: 16px; background: var(--superficie); }
.pannello-testa { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px; padding: 18px 20px 10px; }
.pannello-testa h3 { margin: 0; font-size: 20px; font-weight: 800; overflow-wrap: anywhere; }
.pannello-testa .tenue { font-size: 12px; }
.pannello-corpo { flex: 1; min-height: 0; overflow-y: auto; display: flex; flex-direction: column; gap: 22px; padding: 0 20px 20px; }
.pannello-palco { position: relative; aspect-ratio: 16 / 9; overflow: hidden; border: 1px solid var(--bordo); border-radius: 10px; background: var(--scena); }
.pannello-palco img, .pannello-palco video { display: block; width: 100%; height: 100%; object-fit: cover; }
.pannello-palco .adatta { object-fit: contain; background: #000; }
.pannello-palco + .mini { margin: 6px 0 0; }
.gruppo-pannello { display: flex; flex-direction: column; gap: 10px; }
.gruppo-pannello h4 { margin: 0; font-family: var(--mono); font-size: 11px; font-weight: 600; letter-spacing: .1em; color: var(--tenue); }
.tavolozze { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px; }
.tavolozza { display: flex; align-items: center; gap: 8px; min-height: 40px; padding: 6px 8px; border: 1px solid var(--bordo-forte); border-radius: 10px; background: transparent; color: var(--tenue-chiaro); font-size: 12px; font-weight: 600; text-align: left; cursor: pointer; }
.tavolozza[aria-pressed="true"] { border-color: var(--verde-chiaro); background: rgba(122, 181, 42, .08); color: var(--testo); }
.campione { flex: none; width: 22px; height: 22px; border: 1px solid rgba(255, 255, 255, .25); border-radius: 11px; }
.campione.arcobaleno { background: conic-gradient(#f05a5a, #f0d04c, #5ad07a, #4a90c8, #b05ad0, #f05a5a); }
.colori-liberi { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
.colore { display: flex; align-items: center; gap: 10px; padding: 8px 10px; border: 1px solid var(--bordo-forte); border-radius: 10px; color: var(--tenue); font-size: 12px; cursor: pointer; }
.colore input { width: 32px; height: 32px; padding: 0; border: 0; background: none; cursor: pointer; }
.colore b { display: block; font-family: var(--mono); font-size: 12px; font-weight: 500; color: var(--testo); }
.opzione { display: flex; flex-direction: column; gap: 8px; }
.opzione-testa, .opzione-riga { display: flex; align-items: center; justify-content: space-between; gap: 12px; font-weight: 600; }
.opzione-testa output { font-family: var(--mono); font-size: 12px; color: var(--verde-chiaro); }
.opzione input[type=range] { width: 100%; accent-color: var(--verde); }
.gruppo-pannello .segmentato { background: var(--fondo); }
.gruppo-pannello .pillola { flex: 1; justify-content: center; }
.spiega { margin: 0; font-size: 12px; line-height: 1.45; color: var(--tenue); }
.pannello-piede { display: flex; gap: 10px; padding: 14px 20px; border-top: 1px solid var(--bordo); }
.pannello-piede button { flex: 1; justify-content: center; margin: 0; }
.pannello-piede button:disabled { opacity: .45; cursor: default; }
.nota-modo { font-size: 12px; color: var(--tenue); }
@media (max-width: 899px) {
  .pannello-sfondo { position: fixed; top: 76px; right: 12px; bottom: 12px; z-index: 15; max-height: none; width: min(400px, calc(100vw - 24px)); box-shadow: -18px 0 40px rgba(0, 0, 0, .45); }
}
```

- [ ] **Step 4:** nessuna verifica separata: il componente si prova con il Task 16.
- [ ] **Step 5: commit** "Finestra: componente della barra laterale".

### Task 16: collegamento della barra in `app.js`

**Files:**
- Modify: `src/EnergyItaly.SfondoAnimato/web/app/app.js`

- [ ] **Step 1:** creare la barra subito prima della chiamata finale `collega();` in fondo al file (le funzioni `el`, `icona`, `inScala`, `miniatura`, `renderLibreria` sono già definite; `render` gira solo dopo il primo `stato`, quindi `barra` esiste già quando serve):

```js
  const barra = window.BarraSfondo({
    el, icona, inScala, miniatura: (contesto, v) => miniatura(contesto, v),
    invia: (m) => ponte.invia(m),
    alChiudere: (id) => {
      const memo = schedeLibreria.get(id);
      if (memo) memo.nodo.querySelector('.anteprima-bottone').focus();
      renderLibreria();
    }
  });
```

  In `collega()` aggiungere `$('.libreria-corpo').append(barra.nodo);` (quindi `const barra = ...` va prima di `collega();`).
- [ ] **Step 2:** `creaSchedaLibreria`:
  - l'`onclick` dell'anteprima diventa `() => (barra.idAperto() === id ? barra.chiudi() : barra.apri(id))`;
  - in `aggiorna(v)`: `anteprima.setAttribute('aria-label', \`Personalizza ${v.titolo}\`)`, `anteprima.setAttribute('aria-expanded', String(barra.idAperto() === v.id))`, la classe aggiunge `' scelta'` se la barra è aperta su `v.id`, `anteprima.disabled = false` (anche un file sparito apre la barra, con i controlli spenti);
  - etichetta: creare una volta `const personalizzato = el('span', { class: 'etichetta-personalizzato' }, 'PERSONALIZZATO');`, aggiungerla accanto al badge (`anteprima.replaceChildren(m, badge, personalizzato)`) e `personalizzato.hidden = !v.personalizzazione;`.
- [ ] **Step 3:** `renderLibreria`: la nota diventa `` `${tutte.length} sfondi, ${stato.playlist.length} nella playlist. Clicca uno sfondo per personalizzarlo.` ``; in fondo `barra.aggiorna(stato);`.
- [ ] **Step 4:** miniature e palco con il riempimento:
  - `miniatura`: la `firma` diventa `v.url + '|' + v.disponibile + '|' + v.riempimento`; per immagini e video `object-fit` secondo `v.riempimento` (`img.style.objectFit = v.riempimento === 'adatta' ? 'contain' : 'cover'`, lo stesso per il video, e `nodo.style.background = '#000'` se `adatta`);
  - `contenutoScena`: stesso `object-fit` per immagine e video;
  - `renderPalco`: la `chiave` include `v && v.riempimento`.
- [ ] **Step 5:** pulsanti del modo:
  - `aggiornaModo` distingue i due gruppi:

```js
  function aggiornaModo() {
    const uno = stato.monitor.length < 2;
    const v = voce(stato.corrente);
    const suo = !!(v && v.personalizzazione && v.personalizzazione.modoMonitor);
    document.querySelectorAll('#modo-palco [data-modo]').forEach((b) => {
      b.setAttribute('aria-pressed', String(b.dataset.modo === stato.modo));
      b.disabled = uno || suo;
    });
    $('#nota-modo-palco').hidden = uno || !suo;
    document.querySelectorAll('.riga-imp [data-modo]').forEach((b) => {
      b.setAttribute('aria-pressed', String(b.dataset.modo === stato.impostazioni.modoMonitor));
      b.disabled = uno;
    });
  }
```

  - `renderImpostazioni`: alla `descrizione-modo` con più monitor aggiungere `' Ogni sfondo può avere la sua scelta nella Libreria.'`.
- [ ] **Step 6: verifica nel browser** (`prova-app.html?monitor=2`), con la checklist:
  1. Libreria: clic su Segnale apre la barra, contorno verde sulla scheda; secondo clic o ✕ o Esc la chiudono e il fuoco torna sulla scheda.
  2. Tavolozza "Notte": l'anteprima cambia colori, compare "PERSONALIZZATO" sulla scheda, la miniatura della scheda si aggiorna.
  3. "Personalizzata": compaiono Fondo e Accento con i colori di Notte; scegliendo un colore l'anteprima cambia mentre si sceglie; alla chiusura del selettore la scheda si aggiorna.
  4. Cursore Velocità: il valore cambia mentre si trascina, l'anteprima si ricarica al massimo ogni 250 ms, al rilascio parte un solo `personalizza` (controllare con un `console.log` temporaneo o dai messaggi nella pagina di prova).
  5. Aurora: interruttore "Veli di luce" spento, l'anteprima perde i veli.
  6. Estendi/Duplica: la spiegazione cambia; con lo sfondo in onda forzato su Duplica, nella pagina In onda il palco mostra Duplica, i pulsanti sono disattivati e c'è la nota.
  7. Immagine "Logo come immagine": Riempimento Adatta, anteprima intera su nero; niente sezione Colori.
  8. Video non disponibile: la barra si apre con controlli spenti e "File non trovato".
  9. "Ripristina questo sfondo" toglie l'etichetta; "Ripristina impostazioni aziendali" le toglie tutte.
  10. Finestra sotto i 900 px (vista ridotta): la barra si apre sopra la griglia.
  11. Nessun errore in console.
- [ ] **Step 7: commit** "Libreria: barra laterale per personalizzare gli sfondi".

### Task 17: PRD, build e prova nel programma

**Files:**
- Modify: `docs/PRD.md`

- [ ] **Step 1: PRD** (spec 3.4): sezione 6 "Contratto animazioni" con tavolozze, opzioni, query string (`fondo`, `accento`, `secondario`, opzioni con numeri in cultura invariante e interruttori 1/0) e `Sfondo.colori`/`tono`/`rgba`/`interruttore`; togliere da 3.2 "Parametri delle singole animazioni modificabili dall'utente"; RF-13 diventa "L'URL di ogni animazione = file + colori della tavolozza + opzioni effettive + parametro `monitor` (sezione 6)."; nella tabella dei componenti della sezione 8 aggiungere `Personalizzazioni` ("Regole pure delle personalizzazioni: validazione, unione con i predefiniti, URL, modo e riempimento effettivi").
- [ ] **Step 2:** `dotnet test tests/EnergyItaly.SfondoAnimato.Core.Tests` verde; `dotnet build src/EnergyItaly.SfondoAnimato -c Release` senza avvisi; nessun trattino lungo (caratteri U+2014 e U+2013) nei file toccati: cercarli con `grep -rnP "\x{2014}|\x{2013}"`.
- [ ] **Step 3: prova nel programma vero** (build Debug avviata da `src/EnergyItaly.SfondoAnimato/bin/Debug/.../SfondoAnimato.exe` o con `dotnet run --project src/EnergyItaly.SfondoAnimato`): la finestra si apre, la Libreria mostra le miniature, una personalizzazione dello sfondo in onda cambia il desktop con dissolvenza breve, `%LOCALAPPDATA%` `impostazioni.json` contiene `personalizzazioni`; il log non ha errori. Con due monitor: uno sfondo forzato su Duplica e il successivo su Estendi cambiano disposizione al passaggio.
- [ ] **Step 4: commit** "PRD: personalizzazione degli sfondi".
