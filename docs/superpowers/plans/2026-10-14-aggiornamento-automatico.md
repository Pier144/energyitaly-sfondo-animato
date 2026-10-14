# Aggiornamento automatico - Piano di implementazione

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** i PC con il programma installato scaricano e installano da soli ogni nuova versione pubblicata con un tag `vX.Y.Z`, salvo che l'utente spenga l'opzione "Aggiornamenti automatici".

**Architecture:** GitHub Actions pubblica la release (installer + `versione.json`) sul tag. Nel Core le regole pure (`Aggiornamenti`) e la nuova impostazione `AggiornamentiAutomatici` (stesso schema di `AvvioConWindows`). Nell'app `Aggiornatore` fa rete, file e avvio dell'installer; `AppSfondo` lo collega ai segnali di pausa e alla finestra; due righe nuove in Impostazioni.

**Tech Stack:** .NET 10 (WinForms, HttpClient, SHA256), xUnit, GitHub Actions (windows-latest), Inno Setup 6, PowerShell.

**Spec:** `docs/superpowers/specs/2026-10-14-aggiornamento-automatico-design.md`.

**Comandi:** test `dotnet test tests/EnergyItaly.SfondoAnimato.Core.Tests`; build `dotnet build src/EnergyItaly.SfondoAnimato -c Release`; publish come nel README.

## File

| File | Cosa |
|---|---|
| `src/EnergyItaly.SfondoAnimato.Core/Impostazioni.cs`, `ArchivioImpostazioni.cs`, `Messaggi.cs`, `Interfacce.cs`, `Coordinatore.cs` | Modifica: impostazione `AggiornamentiAutomatici` |
| `tests/.../ImpostazioniTests.cs`, `MessaggiTests.cs`, `CoordinatoreTests.cs`, `Falsi.cs` | Modifica: test dell'impostazione |
| `src/EnergyItaly.SfondoAnimato.Core/Aggiornamenti.cs` | Crea: regole pure |
| `tests/.../AggiornamentiTests.cs` | Crea |
| `src/EnergyItaly.SfondoAnimato/Percorsi.cs` | Modifica: cartella `aggiornamenti` |
| `src/EnergyItaly.SfondoAnimato/Aggiornatore.cs` | Crea: rete, file, installer |
| `src/EnergyItaly.SfondoAnimato/AppSfondo.cs`, `FinestraPrincipale.cs` | Modifica: collegamenti |
| `src/EnergyItaly.SfondoAnimato/impostazioni-predefinite.json` | Modifica: `aggiornamentiAutomatici` |
| `src/EnergyItaly.SfondoAnimato/web/app/index.html`, `app.js` | Modifica: righe in Impostazioni |
| `src/EnergyItaly.SfondoAnimato/web/sviluppo/prova-app.html` | Modifica: simulazione dei messaggi |
| `installer/SfondoAnimato.iss` | Modifica: versione dalla riga di comando |
| `.github/workflows/rilascio.yml` | Crea |
| `strumenti/nuova-versione.ps1` | Crea |
| `README.md` | Modifica: come si pubblica una versione |

## Chunk 1: Impostazione "Aggiornamenti automatici" (Core)

### Task 1: impostazione nel Core

- [ ] **Step 1: Test che falliscono.**
  - `ImpostazioniTests.Predefinite_SenzaFileAziendale_ValoriDelCodice`: aggiungere `Assert.True(i.AggiornamentiAutomatici);`.
  - `ImpostazioniTests.Carica_FileUtenteValido_Letto`: nel JSON aggiungere `"aggiornamentiAutomatici": false,` dopo `"avvioConWindows": false,` e l'asserzione `Assert.False(i.AggiornamentiAutomatici);`.
  - Nuovo test in `ImpostazioniTests`:

```csharp
    [Fact]
    public void AggiornamentiAutomatici_ValoreAziendaleEModifica()
    {
        var aziendale = ArchivioImpostazioni.Predefinite("""{ "versione": 2, "aggiornamentiAutomatici": false }""", Validi, Base, []);
        Assert.False(aziendale.AggiornamentiAutomatici);

        var (r, e) = Codice().Applica(new ModificheImpostazioni(AggiornamentiAutomatici: false));
        Assert.Null(e);
        Assert.False(r.AggiornamentiAutomatici);
        Assert.Contains("\"aggiornamentiAutomatici\": false", ArchivioImpostazioni.Scrivi(r));
        Assert.False(ArchivioImpostazioni.Carica(ArchivioImpostazioni.Scrivi(r), Codice(), Validi, []).AggiornamentiAutomatici);
    }
```

  - `CoordinatoreTests.Avvio_MostraLaPrimaQuandoIlPlayerEPronto`: aggiungere `Assert.True(sistema.AggiornamentiAutomatici);`. Nuovo test:

```csharp
    [Fact]
    public void AggiornamentiAutomatici_DallaFinestraArrivanoAlSistemaEAlloStato()
    {
        var c = Avvia();
        c.DaApp(Comando(new { tipo = "impostazioni", modifiche = new { aggiornamentiAutomatici = false } }));

        Assert.False(sistema.AggiornamentiAutomatici);
        Assert.False(app.Ultimo("stato").GetProperty("impostazioni").GetProperty("aggiornamentiAutomatici").GetBoolean());

        c.DaApp(Comando(new { tipo = "ripristina" }));
        Assert.True(sistema.AggiornamentiAutomatici);
    }
```

  (Se `AppFalsa` non ha `Ultimo(tipo)`, usare lo stesso accesso che usano gli altri test per leggere l'ultimo messaggio `stato`.)
  - `Falsi.cs`, `SistemaFalso`: `public bool? AggiornamentiAutomatici { get; private set; }` e `public void ImpostaAggiornamentiAutomatici(bool attivo) => AggiornamentiAutomatici = attivo;`.
- [ ] **Step 2: Eseguire i test**: errori di compilazione (proprietà e metodo mancanti).
- [ ] **Step 3: Implementazione** (stesso schema di `AvvioConWindows`, in ogni punto dove compare):
  - `Impostazioni`: `public bool AggiornamentiAutomatici { get; init; } = true;`; in `Applica`: `AggiornamentiAutomatici = m.AggiornamentiAutomatici ?? AggiornamentiAutomatici`; in `ModificheImpostazioni`: `bool? AggiornamentiAutomatici = null` in fondo.
  - `ArchivioImpostazioni.Leggi`: `AggiornamentiAutomatici = lettore.Booleano("aggiornamentiAutomatici", b.AggiornamentiAutomatici),`; `Scrivi`: `w.WriteBoolean("aggiornamentiAutomatici", i.AggiornamentiAutomatici);` dopo `avvioConWindows`.
  - `Messaggi.LeggiComandoApp`: `AggiornamentiAutomatici: Booleano(m, "aggiornamentiAutomatici")`; `Messaggi.Stato`: `w.WriteBoolean("aggiornamentiAutomatici", i.AggiornamentiAutomatici);`.
  - `ISistema`: `/// <summary>Accende o spegne gli aggiornamenti automatici (A9).</summary> void ImpostaAggiornamentiAutomatici(bool attivo);`.
  - `Coordinatore`: nei tre punti di `ImpostaAvvioConWindows` (avvio, `CambiaImpostazioni` con confronto vecchia/nuova, `Ripristina`) la chiamata gemella `sistema.ImpostaAggiornamentiAutomatici(...)` con lo stesso `try/catch` e messaggio `"Aggiornamenti automatici non impostati: ..."`.
  - `AppSfondo`: implementazione provvisoria `public void ImpostaAggiornamentiAutomatici(bool attivo) { }` (completata nel Task 4) per compilare.
  - `impostazioni-predefinite.json`: `"aggiornamentiAutomatici": true` dopo `avvioConWindows`.
  - `prova-app.html`: `aggiornamentiAutomatici: true` nei due oggetti `impostazioni`.
- [ ] **Step 4: Test** tutti verdi; build dell'app senza errori.
- [ ] **Step 5: Commit** `Nuova impostazione "Aggiornamenti automatici"`.

## Chunk 2: Regole e aggiornatore

### Task 2: `Aggiornamenti` (Core)

- [ ] **Step 1:** creare `tests/EnergyItaly.SfondoAnimato.Core.Tests/AggiornamentiTests.cs`:

```csharp
using System.Text.Json;
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

public class AggiornamentiTests
{
    const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    static readonly DateTimeOffset Avvio = new(2026, 10, 14, 9, 0, 0, TimeSpan.FromHours(2));

    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("V10.0.1", "10.0.1")]
    [InlineData("1.0.0+6d27f24a1be31360dc1db4a54dddb6ffd77dcf5d", "1.0.0")]
    [InlineData(" 1.1.0 ", "1.1.0")]
    public void VersioneDa_Valida(string testo, string attesa)
    {
        Assert.Equal(Version.Parse(attesa), Aggiornamenti.VersioneDa(testo));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("1.2.3-beta")]
    [InlineData("uno.due.tre")]
    [InlineData("v")]
    [InlineData("999999.0.0")]
    public void VersioneDa_NonValida(string? testo)
    {
        Assert.Null(Aggiornamenti.VersioneDa(testo));
    }

    [Fact]
    public void Leggi_FileValido()
    {
        var info = Aggiornamenti.Leggi($$"""{ "versione": "1.1.0", "file": "EnergyItaly-SfondoAnimato-1.1.0-setup.exe", "sha256": "{{Sha.ToUpperInvariant()}}" }""");

        Assert.Equal(new InfoVersione(new Version(1, 1, 0), "EnergyItaly-SfondoAnimato-1.1.0-setup.exe", Sha), info);
    }

    [Theory]
    [InlineData("{ rotto")]
    [InlineData("[]")]
    [InlineData("""{ "file": "a.exe", "sha256": "SHA" }""")]
    [InlineData("""{ "versione": "1.1", "file": "a.exe", "sha256": "SHA" }""")]
    [InlineData("""{ "versione": "v1.1.0", "file": "a.exe", "sha256": "SHA" }""")]
    [InlineData("""{ "versione": "1.1.0", "sha256": "SHA" }""")]
    [InlineData("""{ "versione": "1.1.0", "file": "..\\a.exe", "sha256": "SHA" }""")]
    [InlineData("""{ "versione": "1.1.0", "file": "cartella/a.exe", "sha256": "SHA" }""")]
    [InlineData("""{ "versione": "1.1.0", "file": "a.msi", "sha256": "SHA" }""")]
    [InlineData("""{ "versione": "1.1.0", "file": ".exe", "sha256": "SHA" }""")]
    [InlineData("""{ "versione": "1.1.0", "file": "a.exe" }""")]
    [InlineData("""{ "versione": "1.1.0", "file": "a.exe", "sha256": "abc" }""")]
    [InlineData("""{ "versione": "1.1.0", "file": "a.exe", "sha256": "zz23456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef" }""")]
    [InlineData("""{ "versione": 110, "file": "a.exe", "sha256": "SHA" }""")]
    public void Leggi_NonValido(string json)
    {
        Assert.Null(Aggiornamenti.Leggi(json.Replace("SHA", Sha)));
    }

    [Fact]
    public void PiuNuova_ConfrontaLeVersioni()
    {
        var info = new InfoVersione(new Version(1, 1, 0), "a.exe", Sha);
        Assert.True(Aggiornamenti.PiuNuova(info, new Version(1, 0, 0)));
        Assert.True(Aggiornamenti.PiuNuova(info, new Version(1, 0, 9)));
        Assert.False(Aggiornamenti.PiuNuova(info, new Version(1, 1, 0)));
        Assert.False(Aggiornamenti.PiuNuova(info, new Version(1, 2, 0)));
        Assert.False(Aggiornamenti.PiuNuova(null, new Version(1, 0, 0)));
    }

    [Fact]
    public void Indirizzi_DelleRelease()
    {
        Assert.Equal("https://github.com/Pier144/energyitaly-sfondo-animato/releases/latest/download/versione.json", Aggiornamenti.IndirizzoInfo.ToString());
        var info = new InfoVersione(new Version(1, 1, 0), "EnergyItaly-SfondoAnimato-1.1.0-setup.exe", Sha);
        Assert.Equal("https://github.com/Pier144/energyitaly-sfondo-animato/releases/download/v1.1.0/EnergyItaly-SfondoAnimato-1.1.0-setup.exe",
            Aggiornamenti.IndirizzoInstaller(info).ToString());
    }

    [Theory]
    [InlineData("https://github.com/Pier144/energyitaly-sfondo-animato/releases/latest/download/versione.json", true)]
    [InlineData("https://github.com/pier144/Energyitaly-Sfondo-Animato/releases/download/v1.1.0/a.exe", true)]
    [InlineData("https://objects.githubusercontent.com/github-production-release-asset-2e65be/123/abc?X=1", true)]
    [InlineData("https://release-assets.githubusercontent.com/github-production-release-asset/123/abc", true)]
    [InlineData("http://github.com/Pier144/energyitaly-sfondo-animato/releases/latest/download/versione.json", false)]
    [InlineData("https://github.com/altro/progetto/releases/download/v1.1.0/a.exe", false)]
    [InlineData("https://github.com/Pier144/energyitaly-sfondo-animato/archive/main.zip", false)]
    [InlineData("https://githubusercontent.com.esempio.it/a.exe", false)]
    [InlineData("https://raw.githubusercontent.com/Pier144/energyitaly-sfondo-animato/main/a.exe", false)]
    public void IndirizzoAffidabile(string indirizzo, bool atteso)
    {
        Assert.Equal(atteso, Aggiornamenti.IndirizzoAffidabile(new Uri(indirizzo)));
    }

    [Fact]
    public void RitardoPrimoControllo_TraDueETrentaMinuti()
    {
        var caso = new Random(3);
        for (int i = 0; i < 1000; i++)
        {
            var r = Aggiornamenti.RitardoPrimoControllo(caso);
            Assert.InRange(r, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(30));
        }
    }

    [Fact]
    public void ProssimoControllo_IlPrimoPossibileSenzaControlliPrecedenti()
    {
        var primo = Avvio.AddMinutes(7);
        Assert.Equal(primo, Aggiornamenti.ProssimoControllo(primo, null));
    }

    [Fact]
    public void ProssimoControllo_SeiOreDopoLUltimo()
    {
        Assert.Equal(Avvio.AddHours(5), Aggiornamenti.ProssimoControllo(Avvio.AddMinutes(7), Avvio.AddHours(-1)));
    }

    [Fact]
    public void ProssimoControllo_MaiPrimaDelPrimoPossibile()
    {
        var primo = Avvio.AddMinutes(7);
        Assert.Equal(primo, Aggiornamenti.ProssimoControllo(primo, Avvio.AddHours(-7)));
        Assert.Equal(primo, Aggiornamenti.ProssimoControllo(primo, Avvio.AddHours(-6).AddMinutes(1)));
    }

    [Theory]
    [InlineData(true, false, false, false, false, true)]
    [InlineData(false, true, false, false, false, true)]
    [InlineData(false, false, true, false, false, true)]
    [InlineData(false, false, false, true, false, true)]
    [InlineData(false, false, false, false, false, false)]
    [InlineData(true, false, false, false, true, false)]
    [InlineData(false, false, true, false, true, false)]
    public void MomentoBuono(bool coperto, bool schermoIntero, bool sessione, bool monitorSpenti, bool finestraVisibile, bool atteso)
    {
        var s = new SegnaliSistema(DesktopCoperto: coperto, SchermoIntero: schermoIntero, SessioneNonAttiva: sessione, MonitorSpenti: monitorSpenti);
        Assert.Equal(atteso, Aggiornamenti.MomentoBuono(s, finestraVisibile));
    }

    [Fact]
    public void MomentoBuono_BatteriaEPausaManualeNonBastano()
    {
        Assert.False(Aggiornamenti.MomentoBuono(new SegnaliSistema(ABatteria: true), finestraVisibile: false));
    }

    [Fact]
    public void Stato_ScrittoERiletto()
    {
        var stato = new StatoAggiornamenti(Avvio, new InfoVersione(new Version(1, 2, 0), "a.exe", Sha), 1);

        Assert.Equal(stato, Aggiornamenti.LeggiStato(Aggiornamenti.ScriviStato(stato)));
        var vuoto = new StatoAggiornamenti(null, null, 0);
        Assert.Equal(vuoto, Aggiornamenti.LeggiStato(Aggiornamenti.ScriviStato(vuoto)));
    }

    [Fact]
    public void Stato_TentativiSenzaAggiornamentoProntoValgonoZero()
    {
        Assert.Equal(0, Aggiornamenti.LeggiStato("""{ "ultimoControllo": null, "pronta": null, "tentativi": 3 }""").Tentativi);
        Assert.Equal(0, Aggiornamenti.LeggiStato($$"""{ "pronta": { "versione": "1.2.0", "file": "a.exe", "sha256": "{{Sha}}" }, "tentativi": -1 }""").Tentativi);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{ rotto")]
    [InlineData("[1]")]
    [InlineData("""{ "ultimoControllo": 5, "pronta": { "versione": "x" } }""")]
    public void Stato_IlleggibileDiventaVuoto(string? json)
    {
        Assert.Equal(new StatoAggiornamenti(null, null, 0), Aggiornamenti.LeggiStato(json));
    }

    [Fact]
    public void ScriviInfo_StessoFormatoDiVersioneJson()
    {
        var info = new InfoVersione(new Version(1, 1, 0), "a.exe", Sha);
        Assert.Equal(info, Aggiornamenti.Leggi(Aggiornamenti.ScriviInfo(info)));
    }

    [Fact]
    public void MessaggioVista_CampiPerLaFinestra()
    {
        var json = Aggiornamenti.MessaggioVista(new Version(1, 1, 0), "pronto", Avvio, new Version(1, 2, 0), null);

        var r = JsonDocument.Parse(json).RootElement;
        Assert.Equal("aggiornamenti", r.GetProperty("tipo").GetString());
        Assert.Equal("1.1.0", r.GetProperty("versione").GetString());
        Assert.Equal("pronto", r.GetProperty("stato").GetString());
        Assert.Equal(Avvio, r.GetProperty("ultimoControllo").GetDateTimeOffset());
        Assert.Equal("1.2.0", r.GetProperty("versioneNuova").GetString());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("errore").ValueKind);
    }
}
```

- [ ] **Step 2:** eseguire: non compila (`Aggiornamenti` mancante).
- [ ] **Step 3:** creare `src/EnergyItaly.SfondoAnimato.Core/Aggiornamenti.cs`:

```csharp
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
```

- [ ] **Step 4:** test tutti verdi.
- [ ] **Step 5: Commit** `Il Core conosce versioni, versione.json e le regole degli aggiornamenti`.

### Task 3: `Aggiornatore` (app)

- [ ] **Step 1:** `Percorsi.cs`: `public static string Aggiornamenti => Path.Combine(Locali, "aggiornamenti");`.
- [ ] **Step 2:** creare `src/EnergyItaly.SfondoAnimato/Aggiornatore.cs`:

```csharp
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using EnergyItaly.SfondoAnimato.Core;
using Microsoft.Win32;

namespace EnergyItaly.SfondoAnimato;

/// <summary>
/// Controllo, download e installazione degli aggiornamenti (spec 2026-10-14-aggiornamento-automatico,
/// 4.2-4.4). Le regole stanno in <see cref="Aggiornamenti"/>; qui rete, file e processi.
/// Vive sul thread dell'interfaccia: solo l'attesa di rete e disco è asincrona.
/// </summary>
sealed class Aggiornatore : IDisposable
{
    const string ChiaveDisinstallazione = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{6B2E7C1A-4F0D-4C55-9E7B-3A1D2F8C9E10}_is1";
    const long DimensioneMassima = 500L * 1024 * 1024;
    static readonly TimeSpan AttesaInfo = TimeSpan.FromSeconds(30);
    static readonly TimeSpan AttesaInstaller = TimeSpan.FromMinutes(10);
    static readonly TimeSpan DurataMomentoBuono = TimeSpan.FromSeconds(60);

    readonly ILog log;
    readonly Func<bool> momentoBuono;
    readonly HttpClient http;
    readonly DateTimeOffset primoControlloPossibile = DateTimeOffset.Now + Aggiornamenti.RitardoPrimoControllo(Random.Shared);
    readonly string fileStato = Path.Combine(Percorsi.Aggiornamenti, "stato.json");
    StatoAggiornamenti stato;
    bool automatici = true;
    bool inCorso;
    bool installazioneAvviata;
    bool chiusuraSessione;
    DateTimeOffset? inizioMomentoBuono;
    string? errore;
    Version? disponibileInSviluppo;

    /// <summary>Lo stato da mostrare nella finestra è cambiato.</summary>
    public event Action? Cambiato;

    /// <summary>L'installer è partito durante la sessione: il programma deve uscire.</summary>
    public event Action? InstallazioneAvviata;

    public Version InUso { get; } = Aggiornamenti.VersioneDa(Application.ProductVersion) ?? new Version(0, 0, 0);

    /// <summary>Solo la copia installata, nella build Release, scarica e installa (A7).</summary>
    public bool CopiaInstallata { get; } = EInstallata();

    /// <param name="momentoBuono">true se ora l'utente non vede il desktop e la finestra è chiusa (A4).</param>
    public Aggiornatore(ILog log, Func<bool> momentoBuono)
    {
        this.log = log;
        this.momentoBuono = momentoBuono;
        // Proxy di sistema con le credenziali dell'utente: nelle reti aziendali il proxy spesso le chiede.
        http = new HttpClient(new SocketsHttpHandler { DefaultProxyCredentials = CredentialCache.DefaultCredentials })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"EnergyItaly-SfondoAnimato/{InUso.ToString(3)}");
        stato = CopiaInstallata ? Aggiornamenti.LeggiStato(LeggiFile(fileStato)) : new StatoAggiornamenti(null, null, 0);
    }

    static bool EInstallata()
    {
#if DEBUG
        return false;
#else
        try
        {
            using var chiave = Registry.CurrentUser.OpenSubKey(ChiaveDisinstallazione);
            if (chiave?.GetValue("InstallLocation") is not string cartella || cartella.Length == 0) return false;
            return string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(cartella)), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
#endif
    }

    InfoVersione? Pronta => Aggiornamenti.PiuNuova(stato.Pronta, InUso) ? stato.Pronta : null;

    bool TentativiFiniti => stato.Tentativi >= Aggiornamenti.TentativiMassimi;

    bool PuoInstallareDaSolo => automatici && CopiaInstallata && !inCorso && !installazioneAvviata && !chiusuraSessione
        && Pronta is not null && !TentativiFiniti;

    /// <summary>Impostazione "Aggiornamenti automatici" (A9).</summary>
    public void ImpostaAutomatici(bool attivo)
    {
        if (automatici != attivo) log.Info($"Aggiornamento: automatici {(attivo ? "attivi" : "spenti")}");
        automatici = attivo;
        Valuta();
        Cambiato?.Invoke();
    }

    /// <summary>Windows sta chiudendo la sessione o spegnendo: nessuna installazione da qui in poi.</summary>
    public void SessioneInChiusura()
    {
        chiusuraSessione = true;
        inizioMomentoBuono = null;
    }

    /// <summary>
    /// All'avvio, prima di mostrare lo sfondo (4.2 "Avvio"). Pulisce sempre (copia installata) e, se l'avvio
    /// è automatico, installa l'aggiornamento pronto. true = l'installer è partito, il programma deve uscire.
    /// </summary>
    public bool AllAvvio(bool avvioAutomatico)
    {
        if (!CopiaInstallata) return false;
        foreach (var parziale in ElencaParziali()) CancellaFile(parziale);
        if (stato.Pronta is { } p)
        {
            if (!Aggiornamenti.PiuNuova(p, InUso))
            {
                if (p.Versione == InUso) log.Info($"Aggiornamento: installata la versione {InUso.ToString(3)}");
                Dimentica(p);
            }
            else if (stato.Tentativi > 0)
            {
                log.Avviso($"Aggiornamento: installazione della {p.Versione.ToString(3)} non riuscita (tentativo {stato.Tentativi}), dettagli in installazione.log");
            }
        }
        if (!avvioAutomatico || !PuoInstallareDaSolo) return false;
        return Installa(riapri: false);
    }

    /// <summary>Da chiamare ogni secondo: controllo programmato e installazione dopo 60 secondi di momento buono.</summary>
    public void Tick()
    {
        if (installazioneAvviata) return;
        if (automatici && !inCorso && DateTimeOffset.Now >= Aggiornamenti.ProssimoControllo(primoControlloPossibile, stato.UltimoControllo))
        {
            _ = ControllaAsync();
            return;
        }
        if (inizioMomentoBuono is { } inizio && DateTimeOffset.Now - inizio >= DurataMomentoBuono)
        {
            inizioMomentoBuono = null;
            if (PuoInstallareDaSolo && momentoBuono() && Installa(riapri: false)) InstallazioneAvviata?.Invoke();
        }
    }

    /// <summary>Segnali di pausa o finestra cambiati: comincia o annulla l'attesa di 60 secondi (A4).</summary>
    public void Valuta()
    {
        if (PuoInstallareDaSolo && momentoBuono()) inizioMomentoBuono ??= DateTimeOffset.Now;
        else inizioMomentoBuono = null;
    }

    /// <summary>"Installa ora" dalla finestra: subito, e il programma riparte con la finestra aperta.</summary>
    public void InstallaOra()
    {
        if (!CopiaInstallata || inCorso || installazioneAvviata || Pronta is null) return;
        if (Installa(riapri: true)) InstallazioneAvviata?.Invoke();
    }

    /// <summary>Controllo (programmato o da "Controlla ora"). Un solo controllo alla volta.</summary>
    public async Task ControllaAsync()
    {
        if (inCorso || installazioneAvviata) return;
        inCorso = true;
        inizioMomentoBuono = null;
        errore = null;
        Cambiato?.Invoke();
        try
        {
            var info = await LeggiInfoAsync();
            if (!Aggiornamenti.PiuNuova(info, InUso))
            {
                disponibileInSviluppo = null;
                log.Info($"Aggiornamento: nessuna versione più nuova della {InUso.ToString(3)}");
                if (CopiaInstallata && stato.Pronta is { } superata) Dimentica(superata);
            }
            else if (!CopiaInstallata)
            {
                // Copia di sviluppo: si dice soltanto che c'è (A7), senza scaricare né scrivere nulla.
                disponibileInSviluppo = info!.Versione;
                log.Info($"Aggiornamento: disponibile la {info.Versione.ToString(3)} (copia di sviluppo, non si scarica)");
            }
            else if (stato.Pronta == info && File.Exists(PercorsoInstaller(info!)))
            {
                log.Info($"Aggiornamento: la {info!.Versione.ToString(3)} è già scaricata");
            }
            else
            {
                await ScaricaAsync(info!);
            }
        }
        catch (Exception ex)
        {
            errore = Breve(ex);
            log.Avviso($"Aggiornamento: controllo non riuscito ({ex.Message})");
        }
        finally
        {
            inCorso = false;
            // Anche se non è riuscito: si riprova al turno successivo, non a raffica.
            stato = stato with { UltimoControllo = DateTimeOffset.Now };
            SalvaStato();
            Valuta();
            Cambiato?.Invoke();
        }
    }

    async Task<InfoVersione?> LeggiInfoAsync()
    {
        using var limite = new CancellationTokenSource(AttesaInfo);
        using var risposta = await http.GetAsync(Aggiornamenti.IndirizzoInfo, limite.Token);
        ControllaIndirizzo(risposta);
        risposta.EnsureSuccessStatusCode();
        var json = await risposta.Content.ReadAsStringAsync(limite.Token);
        return Aggiornamenti.Leggi(json) ?? throw new InvalidDataException("versione.json non valido");
    }

    async Task ScaricaAsync(InfoVersione info)
    {
        Directory.CreateDirectory(Percorsi.Aggiornamenti);
        var finale = PercorsoInstaller(info);
        var parziale = finale + ".part";
        log.Info($"Aggiornamento: scarico la versione {info.Versione.ToString(3)}");
        try
        {
            using var limite = new CancellationTokenSource(AttesaInstaller);
            using (var risposta = await http.GetAsync(Aggiornamenti.IndirizzoInstaller(info), HttpCompletionOption.ResponseHeadersRead, limite.Token))
            {
                ControllaIndirizzo(risposta);
                risposta.EnsureSuccessStatusCode();
                if (risposta.Content.Headers.ContentLength > DimensioneMassima) throw new InvalidDataException("installer troppo grande");
                await using var origine = await risposta.Content.ReadAsStreamAsync(limite.Token);
                await using var destinazione = File.Create(parziale);
                await origine.CopyToAsync(destinazione, limite.Token);
            }
            if (!await Task.Run(() => ImprontaGiusta(parziale, info)))
                throw new InvalidDataException("impronta SHA-256 diversa da versione.json");
            File.Move(parziale, finale, overwrite: true);
        }
        finally
        {
            CancellaFile(parziale, silenzioso: true);
        }
        if (stato.Pronta is { } vecchia && vecchia.File != info.File) CancellaFile(PercorsoInstaller(vecchia));
        stato = stato with { Pronta = info, Tentativi = 0 };
        SalvaStato();
        log.Info($"Aggiornamento: versione {info.Versione.ToString(3)} scaricata e verificata");
    }

    bool Installa(bool riapri)
    {
        var info = Pronta!;
        var percorso = PercorsoInstaller(info);
        try
        {
            if (!File.Exists(percorso) || !ImprontaGiusta(percorso, info))
            {
                log.Avviso($"Aggiornamento: installer della {info.Versione.ToString(3)} mancante o alterato, lo riscarico al prossimo controllo");
                Dimentica(info);
                Cambiato?.Invoke();
                return false;
            }
            stato = stato with { Tentativi = stato.Tentativi + 1 };
            SalvaStato();
            var registro = Path.Combine(Percorsi.Aggiornamenti, "installazione.log");
            var argomenti = $"/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /LOG=\"{registro}\"" + (riapri ? " /APRI=1" : "");
            log.Info($"Aggiornamento: avvio l'installazione della versione {info.Versione.ToString(3)} (tentativo {stato.Tentativi})");
            Process.Start(new ProcessStartInfo(percorso, argomenti) { UseShellExecute = false, WorkingDirectory = Percorsi.Aggiornamenti });
            installazioneAvviata = true;
            inizioMomentoBuono = null;
            return true;
        }
        catch (Exception ex)
        {
            errore = Breve(ex);
            log.Errore($"Aggiornamento: installazione non avviata ({ex.Message})");
            Cambiato?.Invoke();
            return false;
        }
    }

    /// <summary>Il messaggio "aggiornamenti" per la finestra (spec 4.6).</summary>
    public string MessaggioVista()
    {
        string s = inCorso ? "controllo"
            : !CopiaInstallata ? "sviluppo"
            : errore is not null ? "errore"
            : Pronta is not null && TentativiFiniti ? "fallito"
            : Pronta is not null ? "pronto"
            : "aggiornato";
        var nuova = CopiaInstallata ? Pronta?.Versione : disponibileInSviluppo;
        return Aggiornamenti.MessaggioVista(InUso, s, stato.UltimoControllo, nuova, errore);
    }

    static void ControllaIndirizzo(HttpResponseMessage risposta)
    {
        var finale = risposta.RequestMessage?.RequestUri;
        if (finale is null || !Aggiornamenti.IndirizzoAffidabile(finale))
            throw new InvalidDataException($"indirizzo non previsto ({finale?.Host})");
    }

    static bool ImprontaGiusta(string percorso, InfoVersione info)
    {
        using var flusso = File.OpenRead(percorso);
        return Convert.ToHexStringLower(SHA256.HashData(flusso)) == info.Sha256;
    }

    static string PercorsoInstaller(InfoVersione info) => Path.Combine(Percorsi.Aggiornamenti, info.File);

    static IEnumerable<string> ElencaParziali()
    {
        try { return Directory.Exists(Percorsi.Aggiornamenti) ? Directory.GetFiles(Percorsi.Aggiornamenti, "*.part") : []; }
        catch { return []; }
    }

    void Dimentica(InfoVersione info)
    {
        CancellaFile(PercorsoInstaller(info));
        stato = stato with { Pronta = null, Tentativi = 0 };
        SalvaStato();
    }

    void CancellaFile(string percorso, bool silenzioso = false)
    {
        try { File.Delete(percorso); }
        catch (Exception ex) { if (!silenzioso) log.Avviso($"Aggiornamento: {Path.GetFileName(percorso)} non cancellato ({ex.Message})"); }
    }

    // Scrittura atomica: un file temporaneo poi spostato sopra quello vecchio (un'interruzione non lo lascia a metà).
    void SalvaStato()
    {
        if (!CopiaInstallata) return;
        try
        {
            Directory.CreateDirectory(Percorsi.Aggiornamenti);
            var temporaneo = fileStato + ".tmp";
            File.WriteAllText(temporaneo, Aggiornamenti.ScriviStato(stato));
            File.Move(temporaneo, fileStato, overwrite: true);
        }
        catch (Exception ex) { log.Avviso($"Aggiornamento: stato.json non salvato ({ex.Message})"); }
    }

    static string? LeggiFile(string percorso)
    {
        try { return File.Exists(percorso) ? File.ReadAllText(percorso) : null; }
        catch { return null; }
    }

    // Testo breve per la finestra; il dettaglio va nel registro.
    static string Breve(Exception ex) => ex switch
    {
        HttpRequestException { StatusCode: HttpStatusCode.NotFound } => "nessuna versione pubblicata",
        HttpRequestException => "rete non raggiungibile",
        OperationCanceledException => "tempo scaduto",
        InvalidDataException d => d.Message,
        IOException => "errore sul disco",
        _ => "errore imprevisto"
    };

    public void Dispose() => http.Dispose();
}
```

- [ ] **Step 3:** build senza errori né avvisi.

### Task 4: collegamenti in `AppSfondo` e `FinestraPrincipale`

- [ ] **Step 1: `FinestraPrincipale`** - nuovo parametro del costruttore `Action<string> aggiornamenti` (salvato in un campo) e in `MessaggioRicevuto`:

```csharp
                case "controllaAggiornamenti":
                    aggiornamenti("controlla");
                    break;
                case "installaAggiornamento":
                    aggiornamenti("installa");
                    break;
                case "pronta":
                    daApp(json);
                    aggiornamenti("pronta");
                    break;
```

  Aggiungere anche `public bool Vista => Visible && WindowState != FormWindowState.Minimized;`.
- [ ] **Step 2: `AppSfondo`**:
  - campo `readonly Aggiornatore aggiornatore;` creato **prima** del `Coordinatore` (che all'avvio chiama `ImpostaAggiornamentiAutomatici`), subito dopo `icona = ...`:

```csharp
        aggiornatore = new Aggiornatore(log, () => Aggiornamenti.MomentoBuono(segnali, FinestraVista));
        aggiornatore.Cambiato += InviaAggiornamenti;
        aggiornatore.InstallazioneAvviata += () => contesto.Post(_ =>
        {
            log.Info("Uscita per aggiornamento");
            Esci();
        }, null);
        SystemEvents.SessionEnding += SessioneInChiusura;
```

  - `messaggi.ChiusuraRichiesta`: prima di `Esci();` aggiungere `aggiornatore.SessioneInChiusura();`;
  - in `pausa.Cambiati`, dopo `InviaSegnali();`: `aggiornatore.Valuta();`;
  - `orologio.Tick`: `orologio.Tick += (_, _) => { coord.Tick(); aggiornatore.Tick(); };`;
  - subito dopo `coord.Avvia(LettoreMonitor.Leggi());`:

```csharp
        // Aggiornamento scaricato in una sessione precedente: all'accesso a Windows si installa prima di
        // mostrare lo sfondo (A5). L'uscita va in coda: parte appena Application.Run avvia il ciclo dei messaggi.
        if (aggiornatore.AllAvvio(avvioAutomatico: !apriFinestra))
        {
            contesto.Post(_ => Esci(), null);
            return;
        }
```

    `attesaApri` diventa `RegisteredWaitHandle?` e in `Esci` si usa `attesaApri?.Unregister(null);` (in quel caso non è stato creato).
  - in `ApriFinestra`, alla creazione: passare `ComandoAggiornamenti` al costruttore e aggiungere `principale.VisibleChanged += (_, _) => aggiornatore.Valuta();` e `principale.Resize += (_, _) => aggiornatore.Valuta();` (ridotta a icona = non visibile).
  - in `Esci`: `SystemEvents.SessionEnding -= SessioneInChiusura;` e, prima di `icona.Dispose()`, `aggiornatore.Dispose();`.
  - metodi:

```csharp
    bool FinestraVista => principale is { IsDisposed: false } p && p.Vista;

    void InviaAggiornamenti() => principale?.Invia(aggiornatore.MessaggioVista());

    async void ComandoAggiornamenti(string tipo)
    {
        try
        {
            switch (tipo)
            {
                case "controlla": await aggiornatore.ControllaAsync(); break;
                case "installa": aggiornatore.InstallaOra(); break;
                case "pronta": InviaAggiornamenti(); break;
            }
        }
        catch (Exception ex) { log.Errore($"Aggiornamento: comando '{tipo}' non riuscito: {ex.Message}"); }
    }

    public void ImpostaAggiornamentiAutomatici(bool attivo) => aggiornatore.ImpostaAutomatici(attivo);

    void SessioneInChiusura(object? mittente, SessionEndingEventArgs e) => aggiornatore.SessioneInChiusura();
```
- [ ] **Step 3:** build senza errori né avvisi; test Core verdi.
- [ ] **Step 4: Commit** `Il programma controlla, scarica e installa gli aggiornamenti`.

## Chunk 3: Finestra e pubblicazione

### Task 5: righe in Impostazioni

- [ ] **Step 1: `index.html`**, gruppo SISTEMA, prima di "Avvia con Windows":

```html
        <div class="riga-imp">
          <div class="testi"><strong>Aggiornamenti automatici</strong><span class="tenue">Il programma si aggiorna da solo quando esce una nuova versione, mentre non stai guardando il desktop</span></div>
          <button type="button" role="switch" class="interruttore" id="sw-aggiornamenti" aria-checked="true" aria-label="Aggiornamenti automatici"><span></span></button>
        </div>
        <div class="riga-imp">
          <div class="testi"><strong id="titolo-versione">Versione</strong><span class="tenue" id="stato-versione" role="status"></span></div>
          <button type="button" class="bottone-contorno" id="btn-controlla">Controlla ora</button>
          <button type="button" class="bottone-verde" id="btn-installa" hidden>Installa ora</button>
        </div>
```

- [ ] **Step 2: `app.js`** (sei punti):

```js
// (1) Variabile di stato, accanto a `let stato = null;`
  let aggiornamenti = null;

// (2) Funzioni, prima di "// ---------- Collegamenti ----------"
  // ---------- Aggiornamenti (spec aggiornamento automatico 4.5) ----------
  function quando(iso) {
    const d = new Date(iso);
    const ora = d.toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' });
    const oggi = new Date();
    const ieri = new Date();
    ieri.setDate(oggi.getDate() - 1);
    if (d.toDateString() === oggi.toDateString()) return 'oggi alle ' + ora;
    if (d.toDateString() === ieri.toDateString()) return 'ieri alle ' + ora;
    return d.toLocaleDateString('it-IT', { day: 'numeric', month: 'long' }) + ' alle ' + ora;
  }

  function renderAggiornamenti() {
    const a = aggiornamenti;
    if (!a) return;
    const automatici = !stato || stato.impostazioni.aggiornamentiAutomatici;
    let testo;
    switch (a.stato) {
      case 'controllo': testo = 'Controllo in corso…'; break;
      case 'pronto':
        testo = automatici
          ? `Versione ${a.versioneNuova} pronta: si installa appena il desktop è coperto`
          : `Versione ${a.versioneNuova} disponibile`;
        break;
      case 'errore': testo = `Ultimo controllo non riuscito (${a.errore})`; break;
      case 'fallito': testo = `Installazione della ${a.versioneNuova} non riuscita: prova Installa ora o contatta l'IT`; break;
      case 'sviluppo': testo = 'Copia di sviluppo: non si installa nulla' + (a.versioneNuova ? ` · disponibile la ${a.versioneNuova}` : ''); break;
      default: testo = a.ultimoControllo ? 'Aggiornato · ultimo controllo ' + quando(a.ultimoControllo) : 'Nessun controllo ancora fatto';
    }
    $('#titolo-versione').textContent = 'Versione ' + a.versione;
    $('#stato-versione').textContent = testo;
    const installa = (a.stato === 'pronto' && !automatici) || a.stato === 'fallito';
    $('#btn-installa').hidden = !installa;
    $('#btn-controlla').hidden = installa;
    $('#btn-controlla').disabled = a.stato === 'controllo';
  }

// (3) In renderImpostazioni, dopo la riga di #sw-avvio:
    $('#sw-aggiornamenti').setAttribute('aria-checked', String(imp.aggiornamentiAutomatici));

// (4) In render(), dopo renderImpostazioni();
    renderAggiornamenti();

// (5) In collega(), dopo il gestore di #sw-avvio:
    $('#sw-aggiornamenti').addEventListener('click', conStato(() => modifica({ aggiornamentiAutomatici: !stato.impostazioni.aggiornamentiAutomatici })));
    $('#btn-controlla').addEventListener('click', () => ponte.invia({ tipo: 'controllaAggiornamenti' }));
    $('#btn-installa').addEventListener('click', () => ponte.invia({ tipo: 'installaAggiornamento' }));

// (6) In ponte.ascolta, dopo il ramo 'avviso':
    } else if (m.tipo === 'aggiornamenti') {
      aggiornamenti = m;
      renderAggiornamenti();
```

- [ ] **Step 3: `prova-app.html`**, nel gestore dei messaggi, prima di `default:`:

```js
      case 'pronta':
        invia({ tipo: 'aggiornamenti', versione: '1.1.0', stato: 'aggiornato', ultimoControllo: new Date().toISOString(), versioneNuova: null, errore: null });
        break;
      case 'controllaAggiornamenti':
        invia({ tipo: 'aggiornamenti', versione: '1.1.0', stato: 'controllo', ultimoControllo: null, versioneNuova: null, errore: null });
        setTimeout(() => invia({ tipo: 'aggiornamenti', versione: '1.1.0', stato: 'pronto', ultimoControllo: new Date().toISOString(), versioneNuova: '1.2.0', errore: null }), 800);
        break;
      case 'installaAggiornamento': invia({ tipo: 'avviso', testo: 'Nel programma partirebbe l\'installazione' }); break;
```

  (`case 'pronta': break;` esistente va sostituito da questo.)
- [ ] **Step 4: Verifica nel browser** (`sviluppo-web`, `/sviluppo/prova-app.html`, scheda Impostazioni): "Versione 1.1.0 · Aggiornato · ultimo controllo oggi alle HH:MM"; "Controlla ora" → "Controllo in corso…" con pulsante disattivato → "Versione 1.2.0 pronta: si installa appena il desktop è coperto"; spegnendo l'interruttore → "Versione 1.2.0 disponibile" e compare "Installa ora" al posto di "Controlla ora"; clic → avviso; gli stati "errore", "fallito" e "sviluppo" mandati a mano dalla console (`frames[0].postMessage({ allApp: {...} }, '*')`) mostrano i testi della spec 4.6, con "Installa ora" per "fallito"; console senza errori.
- [ ] **Step 5: Commit** `Impostazioni: aggiornamenti automatici e versione del programma`.

### Task 6: pubblicazione

- [ ] **Step 1: `installer/SfondoAnimato.iss`** (spec 3.3), cinque modifiche:

```
; (1) In cima: la versione può arrivare da ISCC /DVersione=X.Y.Z (workflow di rilascio)
#ifndef Versione
  #define Versione "1.0.0"
#endif

; (2) [Run]: la riga silenziosa
Filename: "{app}\{#Eseguibile}"; Parameters: "{code:ParametriRiavvio}"; Flags: nowait skipifnotsilent

; (3) [Code]: dopo il blocco const
var
  InstallazioneRiuscita: Boolean;

// "Installa ora" del programma passa /APRI=1: dopo l'aggiornamento riparte con la finestra aperta.
function ParametriRiavvio(Param: String): String;
begin
  if ExpandConstant('{param:APRI|0}') = '1' then
    Result := ''
  else
    Result := '--avvio';
end;

; (4) [Code]: prima riga di CurStepChanged
  if CurStep = ssPostInstall then
    InstallazioneRiuscita := True;

; (5) [Code]: in fondo
// Aggiornamento silenzioso non riuscito: il programma (già uscito per farsi aggiornare) riparte con la
// versione di prima, invece di restare spento fino al prossimo accesso.
procedure DeinitializeSetup();
var
  Codice: Integer;
begin
  if WizardSilent() and not InstallazioneRiuscita then
  begin
    try
      if FileExists(ExpandConstant('{app}\{#Eseguibile}')) then
        Exec(ExpandConstant('{app}\{#Eseguibile}'), '--avvio', '', SW_SHOWNORMAL, ewNoWait, Codice);
    except
      Log('Programma non rilanciato: ' + GetExceptionMessage);
    end;
  end;
end;
```

- [ ] **Step 2:** creare `.github/workflows/rilascio.yml`:

```yaml
# Pubblica una versione quando si invia un tag vX.Y.Z (spec 2026-10-14-aggiornamento-automatico, 3.2).
# I PC con il programma installato la trovano tramite versione.json e la installano da soli.
name: Rilascio

on:
  push:
    tags:
      - 'v*.*.*'

permissions:
  contents: write

jobs:
  rilascio:
    runs-on: windows-latest
    defaults:
      run:
        shell: pwsh
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json

      - name: Controlli sul tag
        id: versione
        run: |
          $tag = "${{ github.ref_name }}"
          if ($tag -notmatch '^v(\d+\.\d+\.\d+)$') { throw "Il tag '$tag' non è nel formato vX.Y.Z" }
          $versione = $Matches[1]
          [xml]$props = Get-Content Directory.Build.props
          $nelCodice = @($props.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ })[0]
          if ($versione -ne $nelCodice) { throw "Il tag $tag non corrisponde a <Version>$nelCodice</Version> di Directory.Build.props" }
          git merge-base --is-ancestor $env:GITHUB_SHA origin/main
          if ($LASTEXITCODE -ne 0) { throw "Il commit del tag $tag non è su main" }
          "versione=$versione" >> $env:GITHUB_OUTPUT

      - name: Test
        run: dotnet test tests/EnergyItaly.SfondoAnimato.Core.Tests -c Release

      - name: Publish
        run: dotnet publish src/EnergyItaly.SfondoAnimato/EnergyItaly.SfondoAnimato.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish

      - name: Installer
        run: |
          # ISCC.exe non è nel PATH: si cerca nella cartella di Inno Setup, installandolo (versione fissata) se manca.
          $iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
          if (-not (Test-Path $iscc)) {
            choco install innosetup --version=6.4.3 -y --no-progress
            if ($LASTEXITCODE -ne 0) { throw "Installazione di Inno Setup non riuscita" }
            $iscc = @(Get-ChildItem "${env:ProgramFiles(x86)}\Inno Setup*\ISCC.exe")[0].FullName
            if (-not $iscc) { throw "ISCC.exe non trovato dopo l'installazione di Inno Setup" }
          }
          & $iscc "/DVersione=${{ steps.versione.outputs.versione }}" installer\SfondoAnimato.iss
          if ($LASTEXITCODE -ne 0) { throw "Compilazione dell'installer non riuscita" }

      - name: versione.json
        run: |
          $versione = "${{ steps.versione.outputs.versione }}"
          $file = "EnergyItaly-SfondoAnimato-$versione-setup.exe"
          $percorso = "installer\Output\$file"
          if (-not (Test-Path $percorso)) { throw "Installer $percorso non trovato" }
          $sha = (Get-FileHash $percorso -Algorithm SHA256).Hash.ToLowerInvariant()
          [ordered]@{ versione = $versione; file = $file; sha256 = $sha } | ConvertTo-Json -Compress | Set-Content versione.json -Encoding utf8NoBOM
          Get-Content versione.json

      - name: Release
        env:
          GH_TOKEN: ${{ github.token }}
        run: |
          $versione = "${{ steps.versione.outputs.versione }}"
          gh release create "v$versione" "installer\Output\EnergyItaly-SfondoAnimato-$versione-setup.exe" versione.json `
            --title "Energy Italy Sfondo Animato $versione" --generate-notes
```

- [ ] **Step 3:** creare `strumenti/nuova-versione.ps1`:

```powershell
# Crea una nuova versione: aggiorna <Version> in Directory.Build.props, fa il commit, crea il tag vX.Y.Z
# e invia tutto. GitHub Actions (.github/workflows/rilascio.yml) pubblica poi la release e i PC si aggiornano.
#
# Uso: powershell -File strumenti\nuova-versione.ps1 1.1.0
param(
    [Parameter(Mandatory = $true)][string]$Versione
)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

function Ferma($testo) { Write-Host $testo -ForegroundColor Red; exit 1 }

if ($Versione -notmatch '^\d+\.\d+\.\d+$') { Ferma "La versione deve essere nel formato X.Y.Z (ad esempio 1.1.0)" }
$ramo = (git rev-parse --abbrev-ref HEAD).Trim()
if ($ramo -ne 'main') { Ferma "Sei sul ramo '$ramo': le versioni si creano solo da main" }
if (git status --porcelain) { Ferma "Ci sono modifiche non salvate: fai prima il commit o mettile da parte" }
git fetch --quiet origin
if ((git rev-parse HEAD) -ne (git rev-parse origin/main)) { Ferma "main non è allineato con origin/main: fai prima pull o push" }
if (git tag --list "v$Versione") { Ferma "Il tag v$Versione esiste già" }

$props = Get-Content Directory.Build.props -Raw
if ($props -notmatch '<Version>([^<]+)</Version>') { Ferma "<Version> non trovato in Directory.Build.props" }
$attuale = [version]$Matches[1]
if ([version]$Versione -le $attuale) { Ferma "La nuova versione ($Versione) deve essere più alta di quella attuale ($attuale)" }

$props = $props -replace '<Version>[^<]+</Version>', "<Version>$Versione</Version>"
[IO.File]::WriteAllText((Join-Path (Get-Location) 'Directory.Build.props'), $props)
git add Directory.Build.props
git commit -m "Versione $Versione"
git tag -a "v$Versione" -m "Versione $Versione"
git push --atomic origin main "v$Versione"
if ($LASTEXITCODE -ne 0) { Ferma "Invio non riuscito: il commit e il tag sono solo in locale, riprova con: git push --atomic origin main v$Versione" }
Write-Host "Versione $Versione inviata: la release la pubblica GitHub Actions (scheda Actions del repository)." -ForegroundColor Green
```

- [ ] **Step 4: Prova dello script sui rifiuti** (senza creare nulla): `powershell -File strumenti\nuova-versione.ps1 1.0` → formato; `... 1.0.0` → non più alta; con un file modificato → modifiche non salvate.
- [ ] **Step 5: README**, nuova sezione "Pubblicare una versione": `powershell -File strumenti\nuova-versione.ps1 X.Y.Z`; cosa fa il workflow; dove si vede (scheda Actions, Releases); che i PC si aggiornano entro 6 ore se l'opzione è attiva; che la prima versione con aggiornamento automatico va installata a mano.
- [ ] **Step 6: Commit** `Pubblicazione delle versioni con GitHub Actions`.

> **Nota di esecuzione (2026-10-14), dopo la seconda revisione:** `--dopo-aggiornamento` (Program.cs attende il mutex fino a 10 s e lo passa ad `AppSfondo`, che non installa all'avvio in quel caso; il `.iss` lo aggiunge in `DeinitializeSetup` insieme a `ParametriRiavvio`); `MessaggioVista` mette "fallito" e "pronto" prima di "errore" e l'errore di avvio dell'installer non usa più `errore`; limite di 500 MB contato durante la copia; `[NotNullWhen(true)]` su `PiuNuova`; azioni del workflow fissate per SHA, tag passato con `env:`, ricerca di ISCC tollerante; lo script si ferma se commit o tag falliscono. Prova dello script (Task 6 step 4): con `main` avanti rispetto a `origin/main` il rifiuto atteso per `1.0.0` è "main non è allineato", non "non più alta". Copia di sviluppo (Task 7 step 1): lo stato è sempre "sviluppo", con l'errore del controllo in coda al testo.

## Chunk 4: Consegna

### Task 7: verifica, repository pubblico, prima versione

- [ ] **Step 1:** test Core verdi; build Release senza avvisi; publish; prova del programma dalla cartella `publish` (copia di sviluppo): in Impostazioni "Copia di sviluppo"; "Controlla ora" con repository ancora privato → "Ultimo controllo non riuscito (nessuna versione pubblicata)" e riga nel registro.
- [ ] **Step 2: Controllo della storia prima di renderla pubblica** (spec 5): `git log --all -p` filtrato per `token|password|secret|apikey|gho_|ghp_|BEGIN .*PRIVATE KEY` e file `.log`; riportare all'utente cosa si è trovato.
- [ ] **Step 3:** push di `main`. **Chiedere all'utente** la conferma per rendere pubblico il repository (`gh repo edit Pier144/energyitaly-sfondo-animato --visibility public --accept-visibility-change-consequences`) e per creare la prima versione `1.1.0` con lo script. Senza conferma ci si ferma qui e si spiega come farlo.
- [ ] **Step 4 (dopo la conferma):** versione 1.1.0; seguire il workflow con `gh run watch`; controllare la release (due file) e che `versione.json` scaricato abbia l'impronta dell'installer scaricato.
- [ ] **Step 5:** dalla cartella `publish` (copia di sviluppo) "Controlla ora" → "Copia di sviluppo: non si installa nulla" senza "disponibile" (stessa versione). Elencare all'utente le prove da fare con la copia installata (spec 7).
