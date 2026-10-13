# Energy Italy Sfondo Animato - Piano di implementazione

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** programma Windows standalone (stile Lively/Wallpaper Engine) che mostra sfondi animati Energy Italy estesi o duplicati su tutti i monitor, con rotazione, pausa automatica, finestra galleria (design B "Vetrina"), importazione di immagini/video/HTML e installer per utente.

**Architecture:** un processo .NET 10 WinForms con due WebView2 (sfondo agganciato dietro le icone + finestra principale) che condividono un ambiente. Tutta la logica decisionale sta in `EnergyItaly.SfondoAnimato.Core` (net10.0, nessuna dipendenza Windows, TDD): layout monitor, catalogo, importazione, impostazioni, rotazione, regole di pausa, messaggi e il `Coordinatore` che tiene lo stato unico. Il progetto Windows è un sottile strato Win32/WebView2 che implementa le interfacce del Core. Le pagine web (player e app) sono HTML/CSS/JS senza dipendenze.

**Tech Stack:** C# / .NET 10 (SDK 10.0.401 per utente), WinForms, Microsoft.Web.WebView2 (CoreWebView2Controller), xUnit + Microsoft.Extensions.TimeProvider.Testing, HTML/CSS/JS vanilla, Python 3 (solo strumenti di sviluppo), Inno Setup 6.

**Spec:** `docs/superpowers/specs/2026-10-13-sfondo-animato-design.md` (prevale su `docs/PRD.md` dove differiscono). Design di riferimento: `docs/design/riferimento-vetrina.dc.html`.

## Global Constraints

- SDK: `"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe"` (installazione per utente 10.0.401; il `dotnet` nel PATH ha solo i runtime). Tutti i comandi .NET del piano usano quel percorso.
- Target: `net10.0` per Core e test, `net10.0-windows` per l'app; Windows 10 e 11; PerMonitorV2; nessun diritto di amministratore.
- Lingua: interfaccia, messaggi, log e commenti in italiano con accenti UTF-8 veri.
- **Mai** em-dash (U+2014) né en-dash (U+2013) in codice, commenti, stringhe, Markdown e messaggi di commit.
- Commit: nessuna riga `Co-Authored-By` né altre attribuzioni a Claude; autore = identità git configurata.
- Host virtuali: `https://sfondo.energyitaly/` (cartella `web/`) e `https://libreria.energyitaly/` (cartella libreria utente). Ogni altra richiesta di rete viene bloccata.
- Percorsi: impostazioni `%APPDATA%\EnergyItaly\SfondoAnimato\impostazioni.json`; libreria `%APPDATA%\EnergyItaly\SfondoAnimato\libreria.json` + file in `%APPDATA%\EnergyItaly\SfondoAnimato\libreria\`; log `%LOCALAPPDATA%\EnergyItaly\SfondoAnimato\log\` (5 file da 1 MB); dati WebView2 `%LOCALAPPDATA%\EnergyItaly\SfondoAnimato\WebView2\`. (Nota: `libreria.json` sta fuori dalla cartella servita, così le pagine importate non lo leggono; la spec lo indicava dentro, questo piano lo sposta.)
- Colori: sfondo player `#0f1e35`; token del design B: fondo `#07111f`, superficie `#0c1b31`, bordo `#16304f`, testo `#e8eef6`, tenue `#a9bbcf`, verde `#7AB52A`, verde chiaro `#b2e068`, blu `#1E5B8E`, arancio pausa `#f0b44c`.
- Intervallo: minuti 1-1440, ore 1-24, oppure "Mai"; dissolvenza 1, 2 o 4 s; predefiniti 30 minuti, in ordine, 2 s, Estendi, fine video attiva, pausa a batteria attiva, avvio con Windows attivo.
- Importazione: immagini jpg/jpeg/png/webp/gif/bmp fino a 50 MB, video mp4/webm fino a 2 GB, pagine html/htm fino a 5 MB.
- Nomi messaggi JSON: campo `tipo`; il tipo della voce si chiama `tipoVoce` (per non collidere con `tipo`).
- Ogni blocco di codice destinato a un file è preceduto da `<!-- file: percorso -->`: il contenuto del blocco è il file completo.

## Review Focus

1. **Nomi di file importati con accenti, spazi, apostrofi, maiuscole o solo simboli** (es. `Città è bella (1).JPG`, `###.png`): ci si aspetta uno slug pulito, un nome univoco e un URL correttamente codificato. Test in Task 4 (`Slug_*`, `Valuta_*`) e Task 3 (`UrlBase_CodificaIlNomeFile`).
2. **`impostazioni.json` modificato a mano con tipi o valori sbagliati, JSON rotto, versione diversa**: il programma parte sempre, corregge campo per campo e lo scrive nel log. Test in Task 5 (`Carica_*`).
3. **Monitor con origine negativa, dimensioni diverse, verticali, scollegati mentre il programma gira**: finestra e scene corrette, nessun crash con zero monitor. Test in Task 1 e Task 9 (`MonitorScollegato_*`).
4. **Voci in playlist il cui file è sparito o che il player non riesce a caricare**: vengono saltate e la rotazione non si blocca né va in loop. Test in Task 3 (`Carica_FileMancante_*`), Task 6 (`AggiornaVoci_EscludeNonDisponibili`) e Task 9 (`ErroriDiFila_*`).
5. **Intervalli lunghi e cambio di unità (24 ore che diventano minuti), video senza durata nota, "Mai" con un video**: conteggio corretto, nessun valore fuori intervallo, il video in "Mai" va in loop. Test in Task 5 (`Applica_*`), Task 6 (`Video_*`) e Task 9 (`Mai_ConVideo_*`).

---

## Struttura dei file

```
SfondoAnimato.slnx                     soluzione
global.json                            fissa l'SDK 10.0.401
Directory.Build.props                  impostazioni comuni dei progetti
.claude/launch.json                    server di sviluppo per l'anteprima nel browser
strumenti/server_sviluppo.py           serve web/ e animazioni/ sullo stesso host per le prove
strumenti/genera_icona.py              crea risorse/icona.ico dal simbolo del logo
src/EnergyItaly.SfondoAnimato.Core/
  Tipi.cs                              enum condivisi
  NomiJson.cs                          conversione enum <-> testo JSON
  Geometria.cs                         Rettangolo, Monitor, Scena, Disposizione
  LayoutMonitor.cs                     calcolo finestra e scene (Estendi/Duplica)
  CoperturaDesktop.cs                  regola "desktop coperto su tutti i monitor"
  Catalogo.cs                          Voce, libreria.json, manifest, URL
  Importazione.cs                      tipi, limiti, slug, nomi univoci
  Impostazioni.cs                      modello, modifiche e validazione
  ArchivioImpostazioni.cs              lettura tollerante e scrittura JSON v2
  MotoreRotazione.cs                   prossimo, precedente, storico, scadenze
  RegolePausa.cs                       livello e motivo della pausa
  Messaggi.cs                          protocollo JSON host <-> app/player
  Interfacce.cs                        IVistaPlayer, IVistaApp, IArchivio, ISistema, ILog
  Coordinatore.cs                      stato unico e orchestrazione
tests/EnergyItaly.SfondoAnimato.Core.Tests/
  (un file di test per ogni file del Core, più Falsi.cs per il Coordinatore)
src/EnergyItaly.SfondoAnimato/
  EnergyItaly.SfondoAnimato.csproj     WinExe, WebView2, copia web/ e animazioni/
  impostazioni-predefinite.json        predefiniti aziendali (RF-52)
  risorse/icona.ico                    icona del programma e dell'area di notifica
  Program.cs                           istanza unica, controllo WebView2, avvio
  Percorsi.cs                          tutti i percorsi su disco
  Win32.cs                             P/Invoke
  LogFile.cs                           log a rotazione
  ArchivioFile.cs                      IArchivio su disco
  AmbienteWebView.cs                   ambiente WebView2 condiviso
  HostWebView.cs                       controller WebView2 + isolamento di rete
  DesktopHost.cs                       aggancio dietro le icone (classico e 24H2)
  FinestraSfondo.cs                    finestra Win32 del player
  FinestraMessaggi.cs                  finestra nascosta per messaggi di sistema
  LettoreMonitor.cs                    monitor da Screen.AllScreens
  CoperturaFinestre.cs                 enumerazione finestre per la pausa
  GestorePausa.cs                      segnali di sistema -> SegnaliSistema
  IconaNotifica.cs                     icona e menu nell'area di notifica
  FinestraPrincipale.cs                finestra galleria con WebView2
  AppSfondo.cs                         composizione e ciclo di vita
  web/player/player.html|css|js        player degli sfondi
  web/app/index.html|app.css|app.js    finestra galleria (design B)
  web/app/font/                        Sora e IBM Plex Mono (OFL)
  web/sviluppo/prova-player.html       prova del player nel browser
  web/sviluppo/prova-app.html          prova della finestra con un finto programma
installer/SfondoAnimato.iss            installer Inno Setup per utente
```

---

### Task 1: Scheletro della soluzione, geometria e layout dei monitor

**Files:**
- Create: `global.json`, `Directory.Build.props`, `SfondoAnimato.slnx`
- Create: `src/EnergyItaly.SfondoAnimato.Core/EnergyItaly.SfondoAnimato.Core.csproj`
- Create: `src/EnergyItaly.SfondoAnimato.Core/Tipi.cs`, `Geometria.cs`, `LayoutMonitor.cs`
- Create: `tests/EnergyItaly.SfondoAnimato.Core.Tests/` (progetto xUnit) e `LayoutMonitorTests.cs`

**Interfaces:**
- Produces: `readonly record struct Rettangolo(int X, int Y, int L, int A)` con `Destra`, `Basso`; `record Monitor(string Nome, Rettangolo Area, bool Principale)`; `record Scena(Rettangolo Area, IReadOnlyList<Rettangolo> Monitor)` con `string ParametroMonitor()`; `record MonitorRelativo(string Nome, Rettangolo Area, bool Principale)`; `record Disposizione(Rettangolo Finestra, ModoMonitor ModoEffettivo, IReadOnlyList<MonitorRelativo> Monitor, IReadOnlyList<Scena> Scene)`; `static Disposizione LayoutMonitor.Calcola(IReadOnlyList<Monitor> monitor, ModoMonitor modo)`; enum in `Tipi.cs`: `TipoVoce { Animazione, Immagine, Video, Html }`, `UnitaTempo { Minuti, Ore }`, `Ordine { Sequenziale, Casuale }`, `ModoMonitor { Estendi, Duplica }`, `LivelloPausa { Nessuna, Leggera, Profonda }`, `MotivoPausa { Nessuno, ABatteria, Manuale, DesktopCoperto, SchermoIntero, MonitorSpenti, SessioneNonAttiva }`.

- [ ] **Step 1: Creare soluzione e progetti**

<!-- file: global.json -->
```json
{
  "sdk": {
    "version": "10.0.401",
    "rollForward": "latestFeature"
  }
}
```

<!-- file: Directory.Build.props -->
```xml
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <Company>Energy Italy SpA</Company>
    <Product>Energy Italy Sfondo Animato</Product>
    <Version>1.0.0</Version>
  </PropertyGroup>
</Project>
```

<!-- file: src/EnergyItaly.SfondoAnimato.Core/EnergyItaly.SfondoAnimato.Core.csproj -->
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>EnergyItaly.SfondoAnimato.Core</RootNamespace>
  </PropertyGroup>
</Project>
```

Run:
```bash
DN="$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe"
"$DN" new sln --name SfondoAnimato --format slnx
"$DN" new xunit -n EnergyItaly.SfondoAnimato.Core.Tests -o tests/EnergyItaly.SfondoAnimato.Core.Tests
rm -f tests/EnergyItaly.SfondoAnimato.Core.Tests/UnitTest1.cs
"$DN" add tests/EnergyItaly.SfondoAnimato.Core.Tests reference src/EnergyItaly.SfondoAnimato.Core/EnergyItaly.SfondoAnimato.Core.csproj
"$DN" add tests/EnergyItaly.SfondoAnimato.Core.Tests package Microsoft.Extensions.TimeProvider.Testing
"$DN" sln SfondoAnimato.slnx add src/EnergyItaly.SfondoAnimato.Core/EnergyItaly.SfondoAnimato.Core.csproj tests/EnergyItaly.SfondoAnimato.Core.Tests/EnergyItaly.SfondoAnimato.Core.Tests.csproj
```
Expected: comandi completati senza errori; nel csproj dei test compare il riferimento al Core e il pacchetto TimeProvider.Testing.

- [ ] **Step 2: Scrivere i test del layout (falliscono)**

<!-- file: tests/EnergyItaly.SfondoAnimato.Core.Tests/LayoutMonitorTests.cs -->
```csharp
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

public class LayoutMonitorTests
{
    // PC di riferimento: principale (DISPLAY2) a destra in (0,0), secondario (DISPLAY1) a sinistra sfalsato di 7 px.
    static readonly Monitor Principale = new("DISPLAY2", new Rettangolo(0, 0, 1920, 1080), true);
    static readonly Monitor Secondario = new("DISPLAY1", new Rettangolo(-1920, 7, 1920, 1080), false);

    [Fact]
    public void Estendi_PcDiRiferimento_FinestraSulloSchermoVirtuale()
    {
        var d = LayoutMonitor.Calcola([Principale, Secondario], ModoMonitor.Estendi);

        Assert.Equal(new Rettangolo(-1920, 0, 3840, 1087), d.Finestra);
        Assert.Equal(ModoMonitor.Estendi, d.ModoEffettivo);
        Assert.Equal(new[] { "DISPLAY1", "DISPLAY2" }, d.Monitor.Select(m => m.Nome));
        Assert.Equal(new Rettangolo(0, 7, 1920, 1080), d.Monitor[0].Area);
        Assert.Equal(new Rettangolo(1920, 0, 1920, 1080), d.Monitor[1].Area);
        Assert.True(d.Monitor[1].Principale);
        var scena = Assert.Single(d.Scene);
        Assert.Equal(new Rettangolo(0, 0, 3840, 1087), scena.Area);
        Assert.Equal("0,7,1920,1080;1920,0,1920,1080", scena.ParametroMonitor());
    }

    [Fact]
    public void Duplica_PcDiRiferimento_UnaScenaPerMonitor()
    {
        var d = LayoutMonitor.Calcola([Principale, Secondario], ModoMonitor.Duplica);

        Assert.Equal(ModoMonitor.Duplica, d.ModoEffettivo);
        Assert.Equal(2, d.Scene.Count);
        Assert.Equal(new Rettangolo(0, 7, 1920, 1080), d.Scene[0].Area);
        Assert.Equal(new Rettangolo(1920, 0, 1920, 1080), d.Scene[1].Area);
        Assert.Equal("0,0,1920,1080", d.Scene[0].ParametroMonitor());
        Assert.Equal("0,0,1920,1080", d.Scene[1].ParametroMonitor());
    }

    [Fact]
    public void Duplica_ConUnSoloMonitor_DiventaEstendi()
    {
        var d = LayoutMonitor.Calcola([Principale], ModoMonitor.Duplica);

        Assert.Equal(ModoMonitor.Estendi, d.ModoEffettivo);
        Assert.Equal(new Rettangolo(0, 0, 1920, 1080), d.Finestra);
        Assert.Equal("0,0,1920,1080", Assert.Single(d.Scene).ParametroMonitor());
    }

    [Fact]
    public void NessunMonitor_UsaUnMonitorDiRiserva()
    {
        var d = LayoutMonitor.Calcola([], ModoMonitor.Estendi);

        Assert.Equal(new Rettangolo(0, 0, 1920, 1080), d.Finestra);
        Assert.Single(d.Monitor);
    }

    [Fact]
    public void TreMonitorDiversiConUnoVerticale()
    {
        var verticale = new Monitor("DISPLAY3", new Rettangolo(-1080, -300, 1080, 1920), false);
        var grande = new Monitor("DISPLAY1", new Rettangolo(0, 0, 2560, 1440), true);
        var destro = new Monitor("DISPLAY2", new Rettangolo(2560, 200, 1920, 1080), false);

        var d = LayoutMonitor.Calcola([grande, destro, verticale], ModoMonitor.Estendi);

        Assert.Equal(new Rettangolo(-1080, -300, 5560, 1920), d.Finestra);
        Assert.Equal("0,0,1080,1920;1080,300,2560,1440;3640,500,1920,1080", Assert.Single(d.Scene).ParametroMonitor());
    }

    [Fact]
    public void MonitorConAreaVuota_Ignorato()
    {
        var vuoto = new Monitor("DISPLAY9", new Rettangolo(5000, 0, 0, 0), false);

        var d = LayoutMonitor.Calcola([Principale, vuoto], ModoMonitor.Estendi);

        Assert.Equal(new Rettangolo(0, 0, 1920, 1080), d.Finestra);
    }

    [Fact]
    public void Rettangolo_DestraEBasso()
    {
        var r = new Rettangolo(-10, 5, 30, 40);
        Assert.Equal(20, r.Destra);
        Assert.Equal(45, r.Basso);
    }
}
```

- [ ] **Step 3: Eseguire i test e verificare che falliscano**

Run: `"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" test tests/EnergyItaly.SfondoAnimato.Core.Tests`
Expected: FAIL in compilazione (`Rettangolo`, `LayoutMonitor` non esistono).

- [ ] **Step 4: Implementare tipi, geometria e layout**

<!-- file: src/EnergyItaly.SfondoAnimato.Core/Tipi.cs -->
```csharp
namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Che cosa mostra uno sfondo.</summary>
public enum TipoVoce { Animazione, Immagine, Video, Html }

/// <summary>Unità dell'intervallo di cambio.</summary>
public enum UnitaTempo { Minuti, Ore }

/// <summary>Ordine di rotazione della playlist.</summary>
public enum Ordine { Sequenziale, Casuale }

/// <summary>Con più monitor: un'unica scena estesa o la stessa scena su ognuno.</summary>
public enum ModoMonitor { Estendi, Duplica }

/// <summary>Leggera: animazione congelata. Profonda: anche WebView nascosta e sospesa.</summary>
public enum LivelloPausa { Nessuna, Leggera, Profonda }

/// <summary>Perché lo sfondo è in pausa, in ordine crescente di gravità.</summary>
public enum MotivoPausa { Nessuno, ABatteria, Manuale, DesktopCoperto, SchermoIntero, MonitorSpenti, SessioneNonAttiva }
```

<!-- file: src/EnergyItaly.SfondoAnimato.Core/Geometria.cs -->
```csharp
namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Rettangolo in pixel fisici: origine (X, Y), larghezza L, altezza A.</summary>
public readonly record struct Rettangolo(int X, int Y, int L, int A)
{
    public int Destra => X + L;
    public int Basso => Y + A;
}

/// <summary>Un monitor collegato, in coordinate dello schermo virtuale.</summary>
public sealed record Monitor(string Nome, Rettangolo Area, bool Principale);

/// <summary>Un monitor in coordinate relative alla finestra di sfondo.</summary>
public sealed record MonitorRelativo(string Nome, Rettangolo Area, bool Principale);

/// <summary>
/// Un'area della finestra di sfondo in cui gira un'animazione intera.
/// <paramref name="Monitor"/> sono i rettangoli dei monitor relativi all'area della scena.
/// </summary>
public sealed record Scena(Rettangolo Area, IReadOnlyList<Rettangolo> Monitor)
{
    /// <summary>Valore del parametro <c>monitor=</c> del contratto delle animazioni.</summary>
    public string ParametroMonitor() => string.Join(';', Monitor.Select(m => $"{m.X},{m.Y},{m.L},{m.A}"));
}

/// <summary>Dove sta la finestra di sfondo e quali scene contiene.</summary>
public sealed record Disposizione(
    Rettangolo Finestra,
    ModoMonitor ModoEffettivo,
    IReadOnlyList<MonitorRelativo> Monitor,
    IReadOnlyList<Scena> Scene);
```

<!-- file: src/EnergyItaly.SfondoAnimato.Core/LayoutMonitor.cs -->
```csharp
namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Calcola la finestra di sfondo e le scene a partire dai monitor collegati.</summary>
public static class LayoutMonitor
{
    /// <summary>Usato quando Windows non restituisce nessun monitor valido.</summary>
    public static readonly Monitor MonitorDiRiserva = new("DISPLAY1", new Rettangolo(0, 0, 1920, 1080), true);

    public static Disposizione Calcola(IReadOnlyList<Monitor> monitor, ModoMonitor modo)
    {
        var elenco = monitor
            .Where(m => m.Area.L > 0 && m.Area.A > 0)
            .OrderBy(m => m.Area.X).ThenBy(m => m.Area.Y)
            .ToList();
        if (elenco.Count == 0) elenco.Add(MonitorDiRiserva);

        int sinistra = elenco.Min(m => m.Area.X);
        int alto = elenco.Min(m => m.Area.Y);
        int destra = elenco.Max(m => m.Area.Destra);
        int basso = elenco.Max(m => m.Area.Basso);
        var finestra = new Rettangolo(sinistra, alto, destra - sinistra, basso - alto);

        var relativi = elenco
            .Select(m => new MonitorRelativo(m.Nome,
                new Rettangolo(m.Area.X - sinistra, m.Area.Y - alto, m.Area.L, m.Area.A), m.Principale))
            .ToList();

        var effettivo = relativi.Count == 1 ? ModoMonitor.Estendi : modo;
        IReadOnlyList<Scena> scene = effettivo == ModoMonitor.Estendi
            ? [new Scena(new Rettangolo(0, 0, finestra.L, finestra.A), relativi.Select(r => r.Area).ToList())]
            : relativi.Select(r => new Scena(r.Area, [new Rettangolo(0, 0, r.Area.L, r.Area.A)])).ToList();

        return new Disposizione(finestra, effettivo, relativi, scene);
    }
}
```

- [ ] **Step 5: Eseguire i test e verificare che passino**

Run: `"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" test tests/EnergyItaly.SfondoAnimato.Core.Tests`
Expected: PASS, 7 test superati.

- [ ] **Step 6: Commit**

```bash
git add global.json Directory.Build.props SfondoAnimato.slnx src/EnergyItaly.SfondoAnimato.Core tests/EnergyItaly.SfondoAnimato.Core.Tests
git commit -m "Aggiunge la soluzione, i tipi condivisi e il calcolo del layout dei monitor"
```

---

### Task 2: Regola del desktop coperto

**Files:**
- Create: `src/EnergyItaly.SfondoAnimato.Core/CoperturaDesktop.cs`
- Test: `tests/EnergyItaly.SfondoAnimato.Core.Tests/CoperturaDesktopTests.cs`

**Interfaces:**
- Consumes: `Rettangolo` (Task 1).
- Produces: `static bool CoperturaDesktop.TuttiCoperti(IReadOnlyList<Rettangolo> areeMonitor, IReadOnlyList<Rettangolo> finestre)`, `static bool CoperturaDesktop.Copre(Rettangolo finestra, Rettangolo area)`, `const int Tolleranza = 2`.

- [ ] **Step 1: Scrivere i test (falliscono)**

<!-- file: tests/EnergyItaly.SfondoAnimato.Core.Tests/CoperturaDesktopTests.cs -->
```csharp
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

public class CoperturaDesktopTests
{
    // Aree di lavoro (senza barra delle applicazioni) del PC di riferimento.
    static readonly Rettangolo Sinistro = new(-1920, 7, 1920, 1040);
    static readonly Rettangolo Destro = new(0, 0, 1920, 1040);

    [Fact]
    public void FinestreIngrandite_SuEntrambiIMonitor_DesktopCoperto()
    {
        Assert.True(CoperturaDesktop.TuttiCoperti([Sinistro, Destro], [Destro, Sinistro]));
    }

    [Fact]
    public void FinestraIngrandita_SoloSuUnMonitor_DesktopVisibile()
    {
        Assert.False(CoperturaDesktop.TuttiCoperti([Sinistro, Destro], [Destro]));
    }

    [Fact]
    public void SchermoIntero_CopreAncheLAreaDiLavoro()
    {
        var schermoIntero = new Rettangolo(0, 0, 1920, 1080);
        Assert.True(CoperturaDesktop.TuttiCoperti([Destro], [schermoIntero]));
    }

    [Fact]
    public void FinestraPiuPiccola_NonCopre()
    {
        Assert.False(CoperturaDesktop.TuttiCoperti([Destro], [new Rettangolo(100, 100, 800, 600)]));
    }

    [Fact]
    public void ScartoDiDuePixel_Tollerato()
    {
        Assert.True(CoperturaDesktop.Copre(new Rettangolo(2, 2, 1916, 1036), Destro));
        Assert.False(CoperturaDesktop.Copre(new Rettangolo(3, 0, 1917, 1040), Destro));
    }

    [Fact]
    public void NessunMonitor_NonConsideratoCoperto()
    {
        Assert.False(CoperturaDesktop.TuttiCoperti([], [Destro]));
    }
}
```

- [ ] **Step 2: Eseguire i test e verificare che falliscano**

Run: `"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" test tests/EnergyItaly.SfondoAnimato.Core.Tests --filter CoperturaDesktopTests`
Expected: FAIL in compilazione (`CoperturaDesktop` non esiste).

- [ ] **Step 3: Implementare**

<!-- file: src/EnergyItaly.SfondoAnimato.Core/CoperturaDesktop.cs -->
```csharp
namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Decide se il desktop non è visibile su nessun monitor (RF-30).</summary>
public static class CoperturaDesktop
{
    /// <summary>Pixel di scarto ammessi tra il bordo della finestra e l'area del monitor.</summary>
    public const int Tolleranza = 2;

    /// <summary>Vero se ogni area di lavoro è coperta interamente da almeno una finestra.</summary>
    public static bool TuttiCoperti(IReadOnlyList<Rettangolo> areeMonitor, IReadOnlyList<Rettangolo> finestre) =>
        areeMonitor.Count > 0 && areeMonitor.All(a => finestre.Any(f => Copre(f, a)));

    public static bool Copre(Rettangolo finestra, Rettangolo area) =>
        finestra.X <= area.X + Tolleranza &&
        finestra.Y <= area.Y + Tolleranza &&
        finestra.Destra >= area.Destra - Tolleranza &&
        finestra.Basso >= area.Basso - Tolleranza;
}
```

- [ ] **Step 4: Eseguire i test e verificare che passino**

Run: `"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" test tests/EnergyItaly.SfondoAnimato.Core.Tests --filter CoperturaDesktopTests`
Expected: PASS, 6 test.

- [ ] **Step 5: Commit**

```bash
git add src/EnergyItaly.SfondoAnimato.Core/CoperturaDesktop.cs tests/EnergyItaly.SfondoAnimato.Core.Tests/CoperturaDesktopTests.cs
git commit -m "Aggiunge la regola del desktop coperto per la pausa automatica"
```

---

### Task 3: Nomi JSON e catalogo degli sfondi

**Files:**
- Create: `src/EnergyItaly.SfondoAnimato.Core/NomiJson.cs`, `src/EnergyItaly.SfondoAnimato.Core/Catalogo.cs`
- Test: `tests/EnergyItaly.SfondoAnimato.Core.Tests/CatalogoTests.cs`

**Interfaces:**
- Consumes: enum di `Tipi.cs` (Task 1).
- Produces: `NomiJson.Di(TipoVoce|UnitaTempo|Ordine|ModoMonitor) -> string`; `NomiJson.LeggiTipo/LeggiUnita/LeggiOrdine/LeggiModo(string?) -> T?`; `record Voce(string Id, TipoVoce Tipo, string Titolo, bool Aziendale, string File, IReadOnlyDictionary<string,string> Parametri, int? DurataSecondi, bool Disponibile)`; `class VoceLibreria { Id, Tipo, Titolo, File, DurataSecondi, ImportataIl }`; `class DatiLibreria { Versione, Voci }`; `Catalogo.HostApp`, `Catalogo.HostLibreria`; `Catalogo.Carica(string manifestJson, DatiLibreria libreria, Func<string,bool> fileEsiste) -> (IReadOnlyList<Voce> Voci, IReadOnlyList<string> Avvisi)`; `Catalogo.LeggiLibreria(string? json, List<string> avvisi) -> DatiLibreria`; `Catalogo.ScriviLibreria(DatiLibreria) -> string`; `Catalogo.UrlBase(Voce) -> string`; `Catalogo.NomeFileSicuro(string?) -> bool`.

- [ ] **Step 1: Scrivere i test (falliscono)**

<!-- file: tests/EnergyItaly.SfondoAnimato.Core.Tests/CatalogoTests.cs -->
```csharp
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

public class CatalogoTests
{
    const string Manifest = """
        {
          "versione": 1,
          "animazioni": [
            { "id": "onda-di-luce", "titolo": "Onda di luce", "file": "onda-di-luce.html", "parametri": { "ampiezza": "lieve", "durata": 16 } },
            { "id": "aurora", "titolo": "Aurora", "file": "aurora.html", "parametri": { "velocita": 1, "veli": 1 } }
          ]
        }
        """;

    static DatiLibreria Libreria(params VoceLibreria[] voci) => new() { Voci = voci.ToList() };

    [Fact]
    public void Carica_SoloManifest_VociAziendaliConParametriTestuali()
    {
        var (voci, avvisi) = Catalogo.Carica(Manifest, new DatiLibreria(), _ => true);

        Assert.Empty(avvisi);
        Assert.Equal(new[] { "onda-di-luce", "aurora" }, voci.Select(v => v.Id));
        var onda = voci[0];
        Assert.Equal(TipoVoce.Animazione, onda.Tipo);
        Assert.True(onda.Aziendale);
        Assert.True(onda.Disponibile);
        Assert.Equal("lieve", onda.Parametri["ampiezza"]);
        Assert.Equal("16", onda.Parametri["durata"]);
    }

    [Fact]
    public void UrlBase_AnimazioneConParametri()
    {
        var (voci, _) = Catalogo.Carica(Manifest, new DatiLibreria(), _ => true);

        Assert.Equal("https://sfondo.energyitaly/animazioni/onda-di-luce.html?ampiezza=lieve&durata=16", Catalogo.UrlBase(voci[0]));
    }

    [Fact]
    public void Carica_VociUtente_DopoLeAziendali()
    {
        var lib = Libreria(
            new VoceLibreria { Id = "u-mare-abc123", Tipo = "immagine", Titolo = "Mare", File = "mare-abc123.jpg" },
            new VoceLibreria { Id = "u-clip-def456", Tipo = "video", Titolo = "Clip", File = "clip-def456.mp4", DurataSecondi = 160 });

        var (voci, avvisi) = Catalogo.Carica(Manifest, lib, _ => true);

        Assert.Empty(avvisi);
        Assert.Equal(4, voci.Count);
        Assert.Equal(TipoVoce.Immagine, voci[2].Tipo);
        Assert.False(voci[2].Aziendale);
        Assert.Equal(160, voci[3].DurataSecondi);
        Assert.Equal("https://libreria.energyitaly/clip-def456.mp4", Catalogo.UrlBase(voci[3]));
    }

    [Fact]
    public void UrlBase_CodificaIlNomeFile()
    {
        var v = new Voce("u-x-aaaaaa", TipoVoce.Html, "X", false, "pagina con spazi.html", new Dictionary<string, string>(), null, true);
        Assert.Equal("https://libreria.energyitaly/pagina%20con%20spazi.html", Catalogo.UrlBase(v));
    }

    [Fact]
    public void Carica_FileMancante_VoceNonDisponibileEAvviso()
    {
        var lib = Libreria(new VoceLibreria { Id = "u-mare-abc123", Tipo = "immagine", Titolo = "Mare", File = "mare-abc123.jpg" });

        var (voci, avvisi) = Catalogo.Carica(Manifest, lib, nome => nome != "mare-abc123.jpg");

        Assert.False(voci.Single(v => v.Id == "u-mare-abc123").Disponibile);
        Assert.Contains(avvisi, a => a.Contains("mare-abc123.jpg"));
    }

    [Theory]
    [InlineData("..\\fuori.jpg")]
    [InlineData("cartella/file.jpg")]
    [InlineData("C:x.jpg")]
    [InlineData("")]
    public void Carica_NomeFilePericoloso_VoceIgnorata(string file)
    {
        var lib = Libreria(new VoceLibreria { Id = "u-x-aaaaaa", Tipo = "immagine", Titolo = "X", File = file });

        var (voci, avvisi) = Catalogo.Carica(Manifest, lib, _ => true);

        Assert.DoesNotContain(voci, v => v.Id == "u-x-aaaaaa");
        Assert.NotEmpty(avvisi);
    }

    [Fact]
    public void Carica_TipoSconosciutoOIdRipetuto_Ignorati()
    {
        var lib = Libreria(
            new VoceLibreria { Id = "u-a-aaaaaa", Tipo = "audio", Titolo = "A", File = "a.mp3" },
            new VoceLibreria { Id = "aurora", Tipo = "immagine", Titolo = "B", File = "b.jpg" },
            new VoceLibreria { Id = "u-c-cccccc", Tipo = "html", Titolo = "C", File = "c.html" },
            new VoceLibreria { Id = "u-c-cccccc", Tipo = "html", Titolo = "C2", File = "c2.html" });

        var (voci, avvisi) = Catalogo.Carica(Manifest, lib, _ => true);

        Assert.Equal(new[] { "onda-di-luce", "aurora", "u-c-cccccc" }, voci.Select(v => v.Id));
        Assert.Equal(3, avvisi.Count);
    }

    [Fact]
    public void Carica_ManifestRotto_NessunaVoceEAvviso()
    {
        var (voci, avvisi) = Catalogo.Carica("{ non è json", new DatiLibreria(), _ => true);

        Assert.Empty(voci);
        Assert.Single(avvisi);
    }

    [Fact]
    public void Libreria_AndataERitorno()
    {
        var dati = Libreria(new VoceLibreria
        {
            Id = "u-mare-abc123", Tipo = "immagine", Titolo = "Mare all'alba", File = "mare-abc123.jpg",
            ImportataIl = new DateTimeOffset(2026, 10, 13, 16, 0, 0, TimeSpan.Zero)
        });

        var json = Catalogo.ScriviLibreria(dati);
        var letta = Catalogo.LeggiLibreria(json, []);

        Assert.Contains("\"voci\"", json);
        var v = Assert.Single(letta.Voci);
        Assert.Equal("Mare all'alba", v.Titolo);
        Assert.Equal(dati.Voci[0].ImportataIl, v.ImportataIl);
    }

    [Fact]
    public void LeggiLibreria_JsonRottoOVersioneDiversa_LibreriaVuotaConAvviso()
    {
        var avvisi = new List<string>();

        Assert.Empty(Catalogo.LeggiLibreria("[1,2", avvisi).Voci);
        Assert.Empty(Catalogo.LeggiLibreria("""{ "versione": 7, "voci": [] }""", avvisi).Voci);
        Assert.Empty(Catalogo.LeggiLibreria(null, avvisi).Voci);
        Assert.Equal(2, avvisi.Count);
    }

    [Theory]
    [InlineData(TipoVoce.Animazione, "animazione")]
    [InlineData(TipoVoce.Immagine, "immagine")]
    [InlineData(TipoVoce.Video, "video")]
    [InlineData(TipoVoce.Html, "html")]
    public void NomiJson_TipoAndataERitorno(TipoVoce tipo, string testo)
    {
        Assert.Equal(testo, NomiJson.Di(tipo));
        Assert.Equal(tipo, NomiJson.LeggiTipo(testo));
    }

    [Fact]
    public void NomiJson_ValoriSconosciuti_Null()
    {
        Assert.Null(NomiJson.LeggiTipo("Video"));
        Assert.Null(NomiJson.LeggiUnita("secondi"));
        Assert.Null(NomiJson.LeggiOrdine(null));
        Assert.Null(NomiJson.LeggiModo("specchio"));
        Assert.Equal(UnitaTempo.Ore, NomiJson.LeggiUnita("ore"));
        Assert.Equal(Ordine.Casuale, NomiJson.LeggiOrdine("casuale"));
        Assert.Equal(ModoMonitor.Duplica, NomiJson.LeggiModo("duplica"));
    }
}
```

- [ ] **Step 2: Eseguire i test e verificare che falliscano**

Run: `"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" test tests/EnergyItaly.SfondoAnimato.Core.Tests --filter CatalogoTests`
Expected: FAIL in compilazione (`Catalogo`, `NomiJson` non esistono).

- [ ] **Step 3: Implementare**

<!-- file: src/EnergyItaly.SfondoAnimato.Core/NomiJson.cs -->
```csharp
namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Nomi testuali dei valori enumerati nei file JSON e nei messaggi.</summary>
public static class NomiJson
{
    public static string Di(TipoVoce t) => t switch
    {
        TipoVoce.Animazione => "animazione",
        TipoVoce.Immagine => "immagine",
        TipoVoce.Video => "video",
        _ => "html"
    };

    public static string Di(UnitaTempo u) => u == UnitaTempo.Ore ? "ore" : "minuti";

    public static string Di(Ordine o) => o == Ordine.Casuale ? "casuale" : "sequenziale";

    public static string Di(ModoMonitor m) => m == ModoMonitor.Duplica ? "duplica" : "estendi";

    public static TipoVoce? LeggiTipo(string? s) => s switch
    {
        "animazione" => TipoVoce.Animazione,
        "immagine" => TipoVoce.Immagine,
        "video" => TipoVoce.Video,
        "html" => TipoVoce.Html,
        _ => null
    };

    public static UnitaTempo? LeggiUnita(string? s) => s switch
    {
        "minuti" => UnitaTempo.Minuti,
        "ore" => UnitaTempo.Ore,
        _ => null
    };

    public static Ordine? LeggiOrdine(string? s) => s switch
    {
        "sequenziale" => Ordine.Sequenziale,
        "casuale" => Ordine.Casuale,
        _ => null
    };

    public static ModoMonitor? LeggiModo(string? s) => s switch
    {
        "estendi" => ModoMonitor.Estendi,
        "duplica" => ModoMonitor.Duplica,
        _ => null
    };
}
```

<!-- file: src/EnergyItaly.SfondoAnimato.Core/Catalogo.cs -->
```csharp
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
}

/// <summary>Unisce le animazioni aziendali (manifest.json) e gli sfondi importati (libreria.json).</summary>
public static class Catalogo
{
    public const string HostApp = "sfondo.energyitaly";
    public const string HostLibreria = "libreria.energyitaly";

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
            if (!v.Id.StartsWith("u-", StringComparison.Ordinal) || usati.Contains(v.Id))
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

    public static DatiLibreria LeggiLibreria(string? json, List<string> avvisi)
    {
        if (string.IsNullOrWhiteSpace(json)) return new DatiLibreria();
        try
        {
            var dati = JsonSerializer.Deserialize<DatiLibreria>(json, Opzioni);
            if (dati is null || dati.Versione != 1)
            {
                avvisi.Add("libreria.json: versione non supportata, libreria vuota");
                return new DatiLibreria();
            }
            dati.Voci = (dati.Voci ?? []).Where(v => v is not null).ToList();
            return dati;
        }
        catch (JsonException ex)
        {
            avvisi.Add($"libreria.json non leggibile ({ex.Message}): libreria vuota");
            return new DatiLibreria();
        }
    }

    public static string ScriviLibreria(DatiLibreria dati) => JsonSerializer.Serialize(dati, Opzioni);

    /// <summary>URL della voce sugli host virtuali, senza il parametro monitor.</summary>
    public static string UrlBase(Voce v)
    {
        if (v.Tipo != TipoVoce.Animazione) return $"https://{HostLibreria}/{Uri.EscapeDataString(v.File)}";
        var url = $"https://{HostApp}/animazioni/{Uri.EscapeDataString(v.File)}";
        if (v.Parametri.Count == 0) return url;
        return url + "?" + string.Join('&', v.Parametri.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
    }
}
```

- [ ] **Step 4: Eseguire i test e verificare che passino**

Run: `"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" test tests/EnergyItaly.SfondoAnimato.Core.Tests --filter "CatalogoTests"`
Expected: PASS, 18 test (inclusi i casi delle Theory).

- [ ] **Step 5: Commit**

```bash
git add src/EnergyItaly.SfondoAnimato.Core/NomiJson.cs src/EnergyItaly.SfondoAnimato.Core/Catalogo.cs tests/EnergyItaly.SfondoAnimato.Core.Tests/CatalogoTests.cs
git commit -m "Aggiunge il catalogo che unisce animazioni aziendali e sfondi importati"
```

---

### Task 4: Importazione dei file

**Files:**
- Create: `src/EnergyItaly.SfondoAnimato.Core/Importazione.cs`
- Test: `tests/EnergyItaly.SfondoAnimato.Core.Tests/ImportazioneTests.cs`

**Interfaces:**
- Consumes: `TipoVoce` (Task 1).
- Produces: `record EsitoImport(bool Valido, TipoVoce Tipo, string Id, string NomeFile, string Titolo, string? Motivo)`; `Importazione.Valuta(string nomeOriginale, long dimensione, Func<string>? suffisso = null) -> EsitoImport`; `Importazione.Slug(string) -> string`; `Importazione.SuffissoCasuale() -> string` (6 caratteri a-z0-9); `Importazione.Limite(TipoVoce) -> long`; `Importazione.FormatiAccettati`.

- [ ] **Step 1: Scrivere i test (falliscono)**

<!-- file: tests/EnergyItaly.SfondoAnimato.Core.Tests/ImportazioneTests.cs -->
```csharp
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

public class ImportazioneTests
{
    const long MB = 1024L * 1024;
    static string Fisso() => "k3x9qa";

    [Theory]
    [InlineData("foto.jpg", TipoVoce.Immagine)]
    [InlineData("FOTO.JPEG", TipoVoce.Immagine)]
    [InlineData("a.png", TipoVoce.Immagine)]
    [InlineData("a.webp", TipoVoce.Immagine)]
    [InlineData("a.gif", TipoVoce.Immagine)]
    [InlineData("a.bmp", TipoVoce.Immagine)]
    [InlineData("clip.mp4", TipoVoce.Video)]
    [InlineData("clip.WebM", TipoVoce.Video)]
    [InlineData("pagina.html", TipoVoce.Html)]
    [InlineData("pagina.htm", TipoVoce.Html)]
    public void Valuta_TipiRiconosciuti(string nome, TipoVoce atteso)
    {
        var e = Importazione.Valuta(nome, 1000, Fisso);
        Assert.True(e.Valido);
        Assert.Equal(atteso, e.Tipo);
    }

    [Theory]
    [InlineData("programma.exe")]
    [InlineData("senza-estensione")]
    [InlineData("film.mkv")]
    public void Valuta_FormatoNonSupportato(string nome)
    {
        var e = Importazione.Valuta(nome, 1000, Fisso);
        Assert.False(e.Valido);
        Assert.StartsWith("Formato non supportato", e.Motivo);
    }

    [Fact]
    public void Valuta_NomiEIdPuliti()
    {
        var e = Importazione.Valuta(@"C:\Utenti\Mario\Montagne all'alba (1).JPG", 1000, Fisso);

        Assert.Equal("Montagne all'alba (1)", e.Titolo);
        Assert.Equal("u-montagne-all-alba-1-k3x9qa", e.Id);
        Assert.Equal("montagne-all-alba-1-k3x9qa.jpg", e.NomeFile);
    }

    [Fact]
    public void Valuta_LimitiEsatti()
    {
        Assert.True(Importazione.Valuta("a.jpg", 50 * MB, Fisso).Valido);
        Assert.False(Importazione.Valuta("a.jpg", 50 * MB + 1, Fisso).Valido);
        Assert.True(Importazione.Valuta("a.mp4", 2048 * MB, Fisso).Valido);
        Assert.False(Importazione.Valuta("a.mp4", 2048 * MB + 1, Fisso).Valido);
        var html = Importazione.Valuta("a.html", 5 * MB + 1, Fisso);
        Assert.False(html.Valido);
        Assert.Equal("File troppo grande: il limite per le pagine HTML è 5 MB", html.Motivo);
    }

    [Fact]
    public void Valuta_FileVuoto()
    {
        var e = Importazione.Valuta("a.png", 0, Fisso);
        Assert.False(e.Valido);
        Assert.Equal("Il file è vuoto", e.Motivo);
    }

    [Theory]
    [InlineData("Città è bella", "citta-e-bella")]
    [InlineData("###", "sfondo")]
    [InlineData("  spazi   multipli  ", "spazi-multipli")]
    [InlineData("Ünïcödé_ÀÉÎ", "unicode-aei")]
    public void Slug_Casi(string testo, string atteso)
    {
        Assert.Equal(atteso, Importazione.Slug(testo));
    }

    [Fact]
    public void Slug_LungoTroncatoA40SenzaTrattinoFinale()
    {
        var s = Importazione.Slug(new string('a', 39) + " bbbbbb");
        Assert.True(s.Length <= 40);
        Assert.False(s.EndsWith('-'));
    }

    [Fact]
    public void Valuta_SoloEstensione_TitoloSfondo()
    {
        var e = Importazione.Valuta(".png", 10, Fisso);
        Assert.True(e.Valido);
        Assert.Equal("Sfondo", e.Titolo);
        Assert.Equal("sfondo-k3x9qa.png", e.NomeFile);
    }

    [Fact]
    public void SuffissoCasuale_SeiCaratteriMinuscoliOCifre()
    {
        var s = Importazione.SuffissoCasuale();
        Assert.Equal(6, s.Length);
        Assert.All(s, c => Assert.True(c is >= 'a' and <= 'z' or >= '0' and <= '9'));
    }
}
```

- [ ] **Step 2: Eseguire i test e verificare che falliscano**

Run: `"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" test tests/EnergyItaly.SfondoAnimato.Core.Tests --filter ImportazioneTests`
Expected: FAIL in compilazione (`Importazione` non esiste).

- [ ] **Step 3: Implementare**

<!-- file: src/EnergyItaly.SfondoAnimato.Core/Importazione.cs -->
```csharp
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
```

- [ ] **Step 4: Eseguire i test e verificare che passino**

Run: `"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" test tests/EnergyItaly.SfondoAnimato.Core.Tests --filter ImportazioneTests`
Expected: PASS, 23 test.

- [ ] **Step 5: Commit**

```bash
git add src/EnergyItaly.SfondoAnimato.Core/Importazione.cs tests/EnergyItaly.SfondoAnimato.Core.Tests/ImportazioneTests.cs
git commit -m "Aggiunge le regole di importazione con nomi di file sicuri"
```

---

### Task 5: Impostazioni (modello, modifiche, lettura tollerante, scrittura)

**Files:**
- Create: `src/EnergyItaly.SfondoAnimato.Core/Impostazioni.cs`, `src/EnergyItaly.SfondoAnimato.Core/ArchivioImpostazioni.cs`
- Test: `tests/EnergyItaly.SfondoAnimato.Core.Tests/ImpostazioniTests.cs`

**Interfaces:**
- Consumes: `NomiJson` (Task 3), enum (Task 1).
- Produces: `record Impostazioni` con proprietà `IntervalloValore`, `IntervalloUnita`, `RotazioneAttiva`, `FineVideo`, `Ordine`, `Playlist (IReadOnlyList<string>)`, `DissolvenzaSecondi`, `ModoMonitor`, `PausaABatteria`, `AvvioConWindows`, `UltimaVoce (string?)`, calcolata `TimeSpan? Intervallo`, statiche `Massimo(UnitaTempo) -> int` e `DissolvenzeAmmesse`, metodo `Applica(ModificheImpostazioni) -> (Impostazioni Risultato, string? Errore)`; `record ModificheImpostazioni(int? IntervalloValore, string? IntervalloUnita, bool? RotazioneAttiva, bool? FineVideo, string? Ordine, int? DissolvenzaSecondi, string? ModoMonitor, bool? PausaABatteria, bool? AvvioConWindows)` (tutti con default null); `ArchivioImpostazioni.Predefinite(string? jsonAziendale, IReadOnlyCollection<string> idValidi, IReadOnlyList<string> playlistDiBase, List<string> avvisi)`, `ArchivioImpostazioni.Carica(string? jsonUtente, Impostazioni predefinite, IReadOnlyCollection<string> idValidi, List<string> avvisi)`, `ArchivioImpostazioni.Scrivi(Impostazioni) -> string`.

- [ ] **Step 1: Scrivere i test (falliscono)**

<!-- file: tests/EnergyItaly.SfondoAnimato.Core.Tests/ImpostazioniTests.cs -->
```csharp
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

public class ImpostazioniTests
{
    static readonly string[] Validi = ["onda-di-luce", "onde-di-energia", "aurora", "u-mare-abc123"];
    static readonly string[] Base = ["onda-di-luce", "onde-di-energia", "aurora"];

    static Impostazioni Codice() => ArchivioImpostazioni.Predefinite(null, Validi, Base, []);

    [Fact]
    public void Predefinite_SenzaFileAziendale_ValoriDelCodice()
    {
        var i = Codice();

        Assert.Equal(30, i.IntervalloValore);
        Assert.Equal(UnitaTempo.Minuti, i.IntervalloUnita);
        Assert.Equal(TimeSpan.FromMinutes(30), i.Intervallo);
        Assert.True(i.FineVideo);
        Assert.Equal(Ordine.Sequenziale, i.Ordine);
        Assert.Equal(Base, i.Playlist);
        Assert.Equal(2, i.DissolvenzaSecondi);
        Assert.Equal(ModoMonitor.Estendi, i.ModoMonitor);
        Assert.True(i.PausaABatteria);
        Assert.True(i.AvvioConWindows);
        Assert.Null(i.UltimaVoce);
    }

    [Fact]
    public void Predefinite_FileAziendale_SovrascriveIlCodice()
    {
        const string aziendale = """{ "versione": 2, "intervallo": { "valore": 15, "unita": "minuti" }, "ordine": "casuale", "playlist": ["aurora"] }""";

        var i = ArchivioImpostazioni.Predefinite(aziendale, Validi, Base, []);

        Assert.Equal(15, i.IntervalloValore);
        Assert.Equal(Ordine.Casuale, i.Ordine);
        Assert.Equal(new[] { "aurora" }, i.Playlist);
        Assert.Equal(2, i.DissolvenzaSecondi);
    }

    [Fact]
    public void Carica_FileUtenteValido_Letto()
    {
        const string utente = """
            {
              "versione": 2,
              "intervallo": { "valore": 2, "unita": "ore" },
              "rotazioneAttiva": true, "fineVideo": false, "ordine": "casuale",
              "playlist": ["u-mare-abc123", "aurora"], "dissolvenzaSecondi": 4,
              "modoMonitor": "duplica", "pausaABatteria": false, "avvioConWindows": false,
              "ultimaVoce": "aurora"
            }
            """;
        var avvisi = new List<string>();

        var i = ArchivioImpostazioni.Carica(utente, Codice(), Validi, avvisi);

        Assert.Empty(avvisi);
        Assert.Equal(TimeSpan.FromHours(2), i.Intervallo);
        Assert.False(i.FineVideo);
        Assert.Equal(Ordine.Casuale, i.Ordine);
        Assert.Equal(new[] { "u-mare-abc123", "aurora" }, i.Playlist);
        Assert.Equal(4, i.DissolvenzaSecondi);
        Assert.Equal(ModoMonitor.Duplica, i.ModoMonitor);
        Assert.False(i.PausaABatteria);
        Assert.False(i.AvvioConWindows);
        Assert.Equal("aurora", i.UltimaVoce);
    }

    [Theory]
    [InlineData("{ rotto")]
    [InlineData("[]")]
    [InlineData("""{ "versione": 1, "intervalloMinuti": 5 }""")]
    [InlineData("""{ "intervallo": { "valore": 5, "unita": "minuti" } }""")]
    public void Carica_FileIllegibileOVersioneDiversa_PredefiniteConAvviso(string json)
    {
        var avvisi = new List<string>();
        var predefinite = Codice();

        var i = ArchivioImpostazioni.Carica(json, predefinite, Validi, avvisi);

        Assert.Same(predefinite, i);
        Assert.Single(avvisi);
    }

    [Fact]
    public void Carica_CampiSbagliati_CorrettiUnoPerUno()
    {
        const string utente = """
            {
              "versione": 2,
              "intervallo": { "valore": 0, "unita": "ore" },
              "rotazioneAttiva": "si", "fineVideo": 1, "ordine": "boh",
              "dissolvenzaSecondi": 3, "modoMonitor": 2, "pausaABatteria": false
            }
            """;
        var avvisi = new List<string>();

        var i = ArchivioImpostazioni.Carica(utente, Codice(), Validi, avvisi);

        Assert.Equal(30, i.IntervalloValore);
        Assert.Equal(UnitaTempo.Minuti, i.IntervalloUnita);
        Assert.True(i.RotazioneAttiva);
        Assert.True(i.FineVideo);
        Assert.Equal(Ordine.Sequenziale, i.Ordine);
        Assert.Equal(2, i.DissolvenzaSecondi);
        Assert.Equal(ModoMonitor.Estendi, i.ModoMonitor);
        Assert.False(i.PausaABatteria);
        Assert.Equal(6, avvisi.Count);
    }

    [Fact]
    public void Carica_SoloUnita_ValoreRidottoAlMassimo()
    {
        var i = ArchivioImpostazioni.Carica("""{ "versione": 2, "intervallo": { "unita": "ore" } }""", Codice(), Validi, []);

        Assert.Equal(UnitaTempo.Ore, i.IntervalloUnita);
        Assert.Equal(24, i.IntervalloValore);
    }

    [Fact]
    public void Carica_PlaylistConSconosciutiEDoppioni_Filtrata()
    {
        var avvisi = new List<string>();

        var i = ArchivioImpostazioni.Carica("""{ "versione": 2, "playlist": ["aurora", "sparito", "aurora", 5, "onda-di-luce"] }""", Codice(), Validi, avvisi);

        Assert.Equal(new[] { "aurora", "onda-di-luce" }, i.Playlist);
        Assert.Equal(2, avvisi.Count);
    }

    [Fact]
    public void Carica_PlaylistTuttaSconosciuta_PredefinitaConAvviso()
    {
        var avvisi = new List<string>();

        var i = ArchivioImpostazioni.Carica("""{ "versione": 2, "playlist": ["sparito"] }""", Codice(), Validi, avvisi);

        Assert.Equal(Base, i.Playlist);
        Assert.Contains(avvisi, a => a.Contains("playlist vuota"));
    }

    [Fact]
    public void Carica_UltimaVoceSconosciuta_Ignorata()
    {
        var i = ArchivioImpostazioni.Carica("""{ "versione": 2, "ultimaVoce": "sparito" }""", Codice(), Validi, []);
        Assert.Null(i.UltimaVoce);
    }

    [Fact]
    public void Scrivi_AndataERitorno()
    {
        var originale = Codice() with
        {
            IntervalloValore = 3, IntervalloUnita = UnitaTempo.Ore, Ordine = Ordine.Casuale,
            Playlist = ["aurora", "u-mare-abc123"], ModoMonitor = ModoMonitor.Duplica, UltimaVoce = "aurora", FineVideo = false
        };

        var json = ArchivioImpostazioni.Scrivi(originale);
        var avvisi = new List<string>();
        var riletta = ArchivioImpostazioni.Carica(json, Codice(), Validi, avvisi);

        Assert.Empty(avvisi);
        Assert.Contains("\"versione\": 2", json);
        Assert.Equal(originale.Intervallo, riletta.Intervallo);
        Assert.Equal(originale.Ordine, riletta.Ordine);
        Assert.Equal(originale.Playlist, riletta.Playlist);
        Assert.Equal(originale.ModoMonitor, riletta.ModoMonitor);
        Assert.Equal(originale.UltimaVoce, riletta.UltimaVoce);
        Assert.False(riletta.FineVideo);
    }

    [Fact]
    public void Applica_ValoreFuoriIntervallo_Errore()
    {
        var i = Codice();

        var (r, errore) = i.Applica(new ModificheImpostazioni(IntervalloValore: 0));

        Assert.Same(i, r);
        Assert.Equal("Scrivi un numero intero da 1 a 1440 minuti", errore);
        Assert.Equal("Scrivi un numero intero da 1 a 24 ore", i.Applica(new ModificheImpostazioni(IntervalloValore: 30, IntervalloUnita: "ore")).Errore);
    }

    [Fact]
    public void Applica_CambioUnitaSenzaValore_ValoreRidotto()
    {
        var (r, errore) = Codice().Applica(new ModificheImpostazioni(IntervalloUnita: "ore"));

        Assert.Null(errore);
        Assert.Equal(UnitaTempo.Ore, r.IntervalloUnita);
        Assert.Equal(24, r.IntervalloValore);
        Assert.Equal(TimeSpan.FromHours(24), r.Intervallo);
    }

    [Fact]
    public void Applica_DueOre()
    {
        var (r, _) = Codice().Applica(new ModificheImpostazioni(IntervalloValore: 2, IntervalloUnita: "ore"));
        Assert.Equal(TimeSpan.FromHours(2), r.Intervallo);
    }

    [Fact]
    public void Applica_Mai_IntervalloNullo()
    {
        var (r, _) = Codice().Applica(new ModificheImpostazioni(RotazioneAttiva: false));
        Assert.Null(r.Intervallo);
        Assert.Equal(30, r.IntervalloValore);
    }

    [Fact]
    public void Applica_ValoriEnumerati()
    {
        var (r, e) = Codice().Applica(new ModificheImpostazioni(Ordine: "casuale", ModoMonitor: "duplica", DissolvenzaSecondi: 4, FineVideo: false, PausaABatteria: false, AvvioConWindows: false));

        Assert.Null(e);
        Assert.Equal(Ordine.Casuale, r.Ordine);
        Assert.Equal(ModoMonitor.Duplica, r.ModoMonitor);
        Assert.Equal(4, r.DissolvenzaSecondi);
        Assert.False(r.FineVideo);
        Assert.False(r.PausaABatteria);
        Assert.False(r.AvvioConWindows);
    }

    [Theory]
    [InlineData(null, null, null, 3, "Dissolvenza non valida: scegli 1, 2 o 4 secondi")]
    [InlineData("secondi", null, null, null, "Unità di tempo non valida")]
    [InlineData(null, "boh", null, null, "Ordine non valido")]
    [InlineData(null, null, "specchio", null, "Modo monitor non valido")]
    public void Applica_ValoriNonValidi_Errore(string? unita, string? ordine, string? modo, int? dissolvenza, string atteso)
    {
        var (_, errore) = Codice().Applica(new ModificheImpostazioni(IntervalloUnita: unita, Ordine: ordine, ModoMonitor: modo, DissolvenzaSecondi: dissolvenza));
        Assert.Equal(atteso, errore);
    }
}
```

- [ ] **Step 2: Eseguire i test e verificare che falliscano**

Run: `"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" test tests/EnergyItaly.SfondoAnimato.Core.Tests --filter ImpostazioniTests`
Expected: FAIL in compilazione (`Impostazioni`, `ArchivioImpostazioni` non esistono).

- [ ] **Step 3: Implementare il modello e le modifiche**

<!-- file: src/EnergyItaly.SfondoAnimato.Core/Impostazioni.cs -->
```csharp
namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Impostazioni dell'utente (impostazioni.json versione 2).</summary>
public sealed record Impostazioni
{
    public static readonly int[] DissolvenzeAmmesse = [1, 2, 4];

    public int IntervalloValore { get; init; } = 30;
    public UnitaTempo IntervalloUnita { get; init; } = UnitaTempo.Minuti;
    public bool RotazioneAttiva { get; init; } = true;
    public bool FineVideo { get; init; } = true;
    public Ordine Ordine { get; init; } = Ordine.Sequenziale;
    public IReadOnlyList<string> Playlist { get; init; } = [];
    public int DissolvenzaSecondi { get; init; } = 2;
    public ModoMonitor ModoMonitor { get; init; } = ModoMonitor.Estendi;
    public bool PausaABatteria { get; init; } = true;
    public bool AvvioConWindows { get; init; } = true;
    public string? UltimaVoce { get; init; }

    /// <summary>Durata dell'intervallo di cambio; null se la rotazione è spenta ("Mai").</summary>
    public TimeSpan? Intervallo => !RotazioneAttiva
        ? null
        : IntervalloUnita == UnitaTempo.Ore ? TimeSpan.FromHours(IntervalloValore) : TimeSpan.FromMinutes(IntervalloValore);

    public static int Massimo(UnitaTempo unita) => unita == UnitaTempo.Ore ? 24 : 1440;

    /// <summary>Applica le modifiche chieste dalla finestra; se un valore non è valido restituisce l'errore da mostrare.</summary>
    public (Impostazioni Risultato, string? Errore) Applica(ModificheImpostazioni m)
    {
        var unita = IntervalloUnita;
        if (m.IntervalloUnita is not null)
        {
            if (NomiJson.LeggiUnita(m.IntervalloUnita) is not { } u) return (this, "Unità di tempo non valida");
            unita = u;
        }
        int valore = m.IntervalloValore ?? (unita != IntervalloUnita ? Math.Min(IntervalloValore, Massimo(unita)) : IntervalloValore);
        if (valore < 1 || valore > Massimo(unita))
            return (this, $"Scrivi un numero intero da 1 a {Massimo(unita)} {NomiJson.Di(unita)}");

        var ordine = Ordine;
        if (m.Ordine is not null)
        {
            if (NomiJson.LeggiOrdine(m.Ordine) is not { } o) return (this, "Ordine non valido");
            ordine = o;
        }
        var modo = ModoMonitor;
        if (m.ModoMonitor is not null)
        {
            if (NomiJson.LeggiModo(m.ModoMonitor) is not { } mm) return (this, "Modo monitor non valido");
            modo = mm;
        }
        var dissolvenza = m.DissolvenzaSecondi ?? DissolvenzaSecondi;
        if (!DissolvenzeAmmesse.Contains(dissolvenza)) return (this, "Dissolvenza non valida: scegli 1, 2 o 4 secondi");

        return (this with
        {
            IntervalloUnita = unita,
            IntervalloValore = valore,
            Ordine = ordine,
            ModoMonitor = modo,
            DissolvenzaSecondi = dissolvenza,
            RotazioneAttiva = m.RotazioneAttiva ?? RotazioneAttiva,
            FineVideo = m.FineVideo ?? FineVideo,
            PausaABatteria = m.PausaABatteria ?? PausaABatteria,
            AvvioConWindows = m.AvvioConWindows ?? AvvioConWindows
        }, null);
    }
}

/// <summary>Campi cambiati dalla finestra principale; null = invariato.</summary>
public sealed record ModificheImpostazioni(
    int? IntervalloValore = null,
    string? IntervalloUnita = null,
    bool? RotazioneAttiva = null,
    bool? FineVideo = null,
    string? Ordine = null,
    int? DissolvenzaSecondi = null,
    string? ModoMonitor = null,
    bool? PausaABatteria = null,
    bool? AvvioConWindows = null);
```

- [ ] **Step 4: Implementare lettura e scrittura**

<!-- file: src/EnergyItaly.SfondoAnimato.Core/ArchivioImpostazioni.cs -->
```csharp
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

    /// <summary>Valori del codice, eventualmente sovrascritti da impostazioni-predefinite.json (RF-52).</summary>
    public static Impostazioni Predefinite(string? jsonAziendale, IReadOnlyCollection<string> idValidi,
        IReadOnlyList<string> playlistDiBase, List<string> avvisi)
    {
        var codice = new Impostazioni { Playlist = playlistDiBase.ToList() };
        return string.IsNullOrWhiteSpace(jsonAziendale)
            ? codice
            : Leggi(jsonAziendale, codice, idValidi, avvisi, "impostazioni-predefinite.json");
    }

    /// <summary>Impostazioni dell'utente sopra le predefinite.</summary>
    public static Impostazioni Carica(string? jsonUtente, Impostazioni predefinite,
        IReadOnlyCollection<string> idValidi, List<string> avvisi) =>
        string.IsNullOrWhiteSpace(jsonUtente)
            ? predefinite
            : Leggi(jsonUtente, predefinite, idValidi, avvisi, "impostazioni.json");

    static Impostazioni Leggi(string json, Impostazioni b, IReadOnlyCollection<string> idValidi, List<string> avvisi, string nome)
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
            UltimaVoce = ultima
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
            if (i.UltimaVoce is null) w.WriteNull("ultimaVoce"); else w.WriteString("ultimaVoce", i.UltimaVoce);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(flusso.ToArray());
    }
}
```

- [ ] **Step 5: Eseguire i test e verificare che passino**

Run: `"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" test tests/EnergyItaly.SfondoAnimato.Core.Tests --filter ImpostazioniTests`
Expected: PASS, tutti i test di `ImpostazioniTests` superati.

- [ ] **Step 6: Commit**

```bash
git add src/EnergyItaly.SfondoAnimato.Core/Impostazioni.cs src/EnergyItaly.SfondoAnimato.Core/ArchivioImpostazioni.cs tests/EnergyItaly.SfondoAnimato.Core.Tests/ImpostazioniTests.cs
git commit -m "Aggiunge le impostazioni v2 con lettura tollerante e validazione delle modifiche"
```

---

### Task 6: Motore di rotazione

**Files:**
- Create: `src/EnergyItaly.SfondoAnimato.Core/MotoreRotazione.cs`
- Test: `tests/EnergyItaly.SfondoAnimato.Core.Tests/MotoreRotazioneTests.cs`

**Interfaces:**
- Consumes: `Voce`, `TipoVoce`, `Ordine` (Task 1, 3).
- Produces: `record RegoleRotazione(TimeSpan? Intervallo, bool FineVideo, Ordine Ordine)`; `class MotoreRotazione(TimeProvider tempo, Random? casuale = null)` con `Voce? Corrente`, `TimeSpan? Totale`, `TimeSpan? Restante`, `bool Scaduta`, `bool InPausaManuale`, `bool StoricoVuoto`, `bool AttendeFineVideo`, `IReadOnlyList<Voce> Playlist`, `void AggiornaVoci(IReadOnlyList<Voce> tutte, IReadOnlyList<string> idPlaylist)`, `void AggiornaRegole(RegoleRotazione regole, bool riavvia)`, `void Avvia(string? ultima)`, `string? Prossima()`, `bool Mostra(string id)`, `bool TornaIndietro()`, `bool DeveAvanzareDopoVideo(string id)`, `void PausaManuale()`, `void RiprendiManuale()`, `void RiavviaConteggio()`, costanti `MargineVideo` (10 s) e `MassimoStorico` (20).

- [ ] **Step 1: Scrivere i test (falliscono)**

<!-- file: tests/EnergyItaly.SfondoAnimato.Core.Tests/MotoreRotazioneTests.cs -->
```csharp
using EnergyItaly.SfondoAnimato.Core;
using Microsoft.Extensions.Time.Testing;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

public class MotoreRotazioneTests
{
    static readonly Dictionary<string, string> Nessuno = [];
    static Voce Anim(string id) => new(id, TipoVoce.Animazione, id.ToUpperInvariant(), true, id + ".html", Nessuno, null, true);
    static Voce Video(string id, int? durata) => new(id, TipoVoce.Video, id, false, id + ".mp4", Nessuno, durata, true);

    readonly FakeTimeProvider tempo = new(new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero));

    MotoreRotazione Motore(IReadOnlyList<Voce> voci, IReadOnlyList<string> playlist, TimeSpan? intervallo = null, bool fineVideo = true, Ordine ordine = Ordine.Sequenziale, int seme = 1)
    {
        var m = new MotoreRotazione(tempo, new Random(seme));
        m.AggiornaVoci(voci, playlist);
        m.AggiornaRegole(new RegoleRotazione(intervallo ?? TimeSpan.FromMinutes(30), fineVideo, ordine), riavvia: true);
        return m;
    }

    static readonly Voce[] Tre = [Anim("a"), Anim("b"), Anim("c")];

    [Fact]
    public void Avvia_SenzaUltima_PrimaDellaPlaylist()
    {
        var m = Motore(Tre, ["a", "b", "c"]);
        m.Avvia(null);

        Assert.Equal("a", m.Corrente?.Id);
        Assert.Equal(TimeSpan.FromMinutes(30), m.Totale);
        Assert.True(m.StoricoVuoto);
    }

    [Theory]
    [InlineData("a", "b")]
    [InlineData("c", "a")]
    [InlineData("sparita", "a")]
    public void Avvia_DopoLUltimaMostrata(string ultima, string attesa)
    {
        var m = Motore(Tre, ["a", "b", "c"]);
        m.Avvia(ultima);
        Assert.Equal(attesa, m.Corrente?.Id);
    }

    [Fact]
    public void Prossima_CorrenteFuoriPlaylist_PrimaDellaPlaylist()
    {
        var m = Motore(Tre, ["b", "c"]);
        m.Avvia(null);
        m.Mostra("a");

        Assert.Equal("b", m.Prossima());
    }

    [Fact]
    public void Casuale_MaiLaStessaDueVolteDiFila()
    {
        var m = Motore(Tre, ["a", "b", "c"], ordine: Ordine.Casuale, seme: 42);
        m.Avvia(null);
        for (int i = 0; i < 200; i++)
        {
            var precedente = m.Corrente!.Id;
            var prossima = m.Prossima()!;
            Assert.NotEqual(precedente, prossima);
            m.Mostra(prossima);
        }
    }

    [Fact]
    public void Casuale_UnaSolaVoce_RestituisceQuella()
    {
        var m = Motore(Tre, ["b"], ordine: Ordine.Casuale);
        m.Avvia(null);
        Assert.Equal("b", m.Prossima());
    }

    [Fact]
    public void Scaduta_SoloDopoLIntervallo()
    {
        var m = Motore(Tre, ["a", "b", "c"]);
        m.Avvia(null);

        tempo.Advance(TimeSpan.FromMinutes(29) + TimeSpan.FromSeconds(59));
        Assert.False(m.Scaduta);
        Assert.Equal(TimeSpan.FromSeconds(1), m.Restante);
        tempo.Advance(TimeSpan.FromSeconds(1));
        Assert.True(m.Scaduta);
        Assert.Equal(TimeSpan.Zero, m.Restante);
    }

    [Fact]
    public void Mai_NessunaScadenza()
    {
        var m = new MotoreRotazione(tempo);
        m.AggiornaVoci(Tre, ["a", "b"]);
        m.AggiornaRegole(new RegoleRotazione(null, true, Ordine.Sequenziale), riavvia: true);
        m.Avvia(null);

        tempo.Advance(TimeSpan.FromDays(3));
        Assert.Null(m.Totale);
        Assert.Null(m.Restante);
        Assert.False(m.Scaduta);
    }

    [Fact]
    public void Video_ConDurataEFineVideo_RestaPerTuttoIlVideoPiuMargine()
    {
        var m = Motore([Anim("a"), Video("v", 160)], ["a", "v"], intervallo: TimeSpan.FromMinutes(1));
        m.Avvia("a");

        Assert.Equal("v", m.Corrente?.Id);
        Assert.Equal(TimeSpan.FromSeconds(160), m.Totale);
        Assert.True(m.AttendeFineVideo);
        Assert.True(m.DeveAvanzareDopoVideo("v"));
        tempo.Advance(TimeSpan.FromSeconds(169));
        Assert.False(m.Scaduta);
        tempo.Advance(TimeSpan.FromSeconds(1));
        Assert.True(m.Scaduta);
    }

    [Fact]
    public void Video_SenzaDurata_UsaLIntervallo()
    {
        var m = Motore([Video("v", null)], ["v"], intervallo: TimeSpan.FromMinutes(5));
        m.Avvia(null);

        Assert.Equal(TimeSpan.FromMinutes(5), m.Totale);
        Assert.False(m.AttendeFineVideo);
        Assert.True(m.DeveAvanzareDopoVideo("v"));
    }

    [Fact]
    public void Video_ConFineVideoSpenta_UsaLIntervallo()
    {
        var m = Motore([Video("v", 160)], ["v"], intervallo: TimeSpan.FromMinutes(5), fineVideo: false);
        m.Avvia(null);

        Assert.Equal(TimeSpan.FromMinutes(5), m.Totale);
        Assert.False(m.DeveAvanzareDopoVideo("v"));
    }

    [Fact]
    public void PausaManuale_FermaIlConteggio()
    {
        var m = Motore(Tre, ["a", "b", "c"]);
        m.Avvia(null);
        tempo.Advance(TimeSpan.FromMinutes(10));

        m.PausaManuale();
        tempo.Advance(TimeSpan.FromHours(1));
        Assert.True(m.InPausaManuale);
        Assert.False(m.Scaduta);
        Assert.Equal(TimeSpan.FromMinutes(20), m.Restante);

        m.RiprendiManuale();
        tempo.Advance(TimeSpan.FromMinutes(19));
        Assert.False(m.Scaduta);
        tempo.Advance(TimeSpan.FromMinutes(1));
        Assert.True(m.Scaduta);
    }

    [Fact]
    public void PausaAutomatica_IlTempoContinuaAScorrere()
    {
        // Le pause automatiche non toccano il motore: alla ripresa il cambio risulta già scaduto (RF-23).
        var m = Motore(Tre, ["a", "b", "c"]);
        m.Avvia(null);
        tempo.Advance(TimeSpan.FromMinutes(45));
        Assert.True(m.Scaduta);
    }

    [Fact]
    public void Storico_TornaIndietroNellOrdineMostrato()
    {
        var m = Motore(Tre, ["a", "b", "c"]);
        m.Avvia(null);
        m.Mostra("c");
        m.Mostra("b");

        Assert.True(m.TornaIndietro());
        Assert.Equal("c", m.Corrente?.Id);
        Assert.True(m.TornaIndietro());
        Assert.Equal("a", m.Corrente?.Id);
        Assert.True(m.StoricoVuoto);
        Assert.True(m.TornaIndietro());
        Assert.Equal("c", m.Corrente?.Id);
    }

    [Fact]
    public void Storico_AlMassimoVentiVoci()
    {
        var voci = Enumerable.Range(0, 30).Select(i => Anim("v" + i)).ToList();
        var m = Motore(voci, voci.Select(v => v.Id).ToList());
        m.Avvia(null);
        foreach (var v in voci.Skip(1)) m.Mostra(v.Id);

        int passi = 0;
        while (!m.StoricoVuoto) { m.TornaIndietro(); passi++; }
        Assert.Equal(MotoreRotazione.MassimoStorico, passi);
    }

    [Fact]
    public void Mostra_VoceNonDisponibileOSconosciuta_Falso()
    {
        var rotta = Anim("x") with { Disponibile = false };
        var m = Motore([.. Tre, rotta], ["a", "b"]);
        m.Avvia(null);

        Assert.False(m.Mostra("x"));
        Assert.False(m.Mostra("sconosciuta"));
        Assert.Equal("a", m.Corrente?.Id);
    }

    [Fact]
    public void AggiornaVoci_EscludeNonDisponibili()
    {
        var m = Motore([Anim("a"), Anim("b") with { Disponibile = false }, Anim("c")], ["a", "b", "c"]);
        m.Avvia(null);

        Assert.Equal(new[] { "a", "c" }, m.Playlist.Select(v => v.Id));
        Assert.Equal("c", m.Prossima());
    }

    [Fact]
    public void AggiornaVoci_DurataNuovaDelVideoCorrente_AggiornaIlTotaleSenzaRipartire()
    {
        var m = Motore([Video("v", null)], ["v"], intervallo: TimeSpan.FromMinutes(5));
        m.Avvia(null);
        tempo.Advance(TimeSpan.FromSeconds(30));

        m.AggiornaVoci([Video("v", 90)], ["v"]);

        Assert.Equal(TimeSpan.FromSeconds(90), m.Totale);
        Assert.Equal(TimeSpan.FromSeconds(60), m.Restante);
    }

    [Fact]
    public void AggiornaRegole_ConRiavvio_RipartIlConteggio()
    {
        var m = Motore(Tre, ["a", "b", "c"]);
        m.Avvia(null);
        tempo.Advance(TimeSpan.FromMinutes(20));

        m.AggiornaRegole(new RegoleRotazione(TimeSpan.FromHours(2), true, Ordine.Sequenziale), riavvia: true);

        Assert.Equal(TimeSpan.FromHours(2), m.Restante);
    }

    [Fact]
    public void AggiornaRegole_SoloOrdine_NonRiparte()
    {
        var m = Motore(Tre, ["a", "b", "c"]);
        m.Avvia(null);
        tempo.Advance(TimeSpan.FromMinutes(20));

        m.AggiornaRegole(new RegoleRotazione(TimeSpan.FromMinutes(30), true, Ordine.Casuale), riavvia: false);

        Assert.Equal(TimeSpan.FromMinutes(10), m.Restante);
    }

    [Fact]
    public void PlaylistVuota_NessunaProssima()
    {
        var m = Motore(Tre, []);
        m.Avvia("b");

        Assert.Equal("b", m.Corrente?.Id);
        Assert.Null(m.Prossima());
    }
}
```

- [ ] **Step 2: Eseguire i test e verificare che falliscano**

Run: `"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" test tests/EnergyItaly.SfondoAnimato.Core.Tests --filter MotoreRotazioneTests`
Expected: FAIL in compilazione (`MotoreRotazione` non esiste).

- [ ] **Step 3: Implementare**

<!-- file: src/EnergyItaly.SfondoAnimato.Core/MotoreRotazione.cs -->
```csharp
namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Regole di cambio: intervallo (null = "Mai"), fine video, ordine.</summary>
public sealed record RegoleRotazione(TimeSpan? Intervallo, bool FineVideo, Ordine Ordine);

/// <summary>
/// Decide quale sfondo mostrare e quando cambiarlo. La scadenza è un istante assoluto:
/// le pause automatiche non la spostano (RF-23), la pausa manuale sì (decisione D8).
/// </summary>
public sealed class MotoreRotazione
{
    /// <summary>Margine oltre la durata di un video prima di cambiare comunque (rete di sicurezza).</summary>
    public static readonly TimeSpan MargineVideo = TimeSpan.FromSeconds(10);
    public const int MassimoStorico = 20;

    readonly TimeProvider tempo;
    readonly Random casuale;
    readonly Dictionary<string, Voce> note = new(StringComparer.Ordinal);
    readonly List<string> storico = [];
    List<Voce> playlist = [];
    RegoleRotazione regole = new(TimeSpan.FromMinutes(30), true, Ordine.Sequenziale);
    DateTimeOffset inizio;
    DateTimeOffset? pausaDa;

    public MotoreRotazione(TimeProvider tempo, Random? casuale = null)
    {
        this.tempo = tempo;
        this.casuale = casuale ?? Random.Shared;
        inizio = tempo.GetUtcNow();
    }

    public Voce? Corrente { get; private set; }

    /// <summary>Permanenza della voce corrente; null = nessun cambio automatico.</summary>
    public TimeSpan? Totale { get; private set; }

    public bool InPausaManuale => pausaDa is not null;
    public bool StoricoVuoto => storico.Count == 0;
    public IReadOnlyList<Voce> Playlist => playlist;

    DateTimeOffset Adesso => tempo.GetUtcNow();

    /// <summary>Vero se la voce corrente è un video che resta fino alla fine (durata nota).</summary>
    public bool AttendeFineVideo =>
        regole.Intervallo is not null && regole.FineVideo && Corrente is { Tipo: TipoVoce.Video, DurataSecondi: > 0 };

    public TimeSpan? Restante
    {
        get
        {
            if (Totale is null) return null;
            var r = Totale.Value - ((pausaDa ?? Adesso) - inizio);
            return r < TimeSpan.Zero ? TimeSpan.Zero : r;
        }
    }

    public bool Scaduta =>
        pausaDa is null && Totale is not null &&
        Adesso >= inizio + Totale.Value + (AttendeFineVideo ? MargineVideo : TimeSpan.Zero);

    /// <summary>Aggiorna l'elenco di tutte le voci e la playlist (solo voci note e disponibili).</summary>
    public void AggiornaVoci(IReadOnlyList<Voce> tutte, IReadOnlyList<string> idPlaylist)
    {
        note.Clear();
        foreach (var v in tutte) note[v.Id] = v;
        playlist = idPlaylist
            .Where(id => note.TryGetValue(id, out var v) && v.Disponibile)
            .Select(id => note[id])
            .ToList();
        storico.RemoveAll(id => !note.ContainsKey(id));
        if (Corrente is not null && note.TryGetValue(Corrente.Id, out var aggiornata))
        {
            Corrente = aggiornata;
            Totale = Permanenza(aggiornata);
        }
    }

    public void AggiornaRegole(RegoleRotazione nuove, bool riavvia)
    {
        regole = nuove;
        if (riavvia) RiavviaConteggio();
        else Totale = Permanenza(Corrente);
    }

    /// <summary>All'avvio mostra la voce dopo l'ultima mostrata (RF-25).</summary>
    public void Avvia(string? ultima)
    {
        storico.Clear();
        Corrente = ultima is not null && note.TryGetValue(ultima, out var u) && u.Disponibile ? u : null;
        var id = Prossima();
        if (id is not null) Corrente = note[id];
        RiavviaConteggio();
    }

    public string? Prossima()
    {
        if (playlist.Count == 0) return null;
        if (regole.Ordine == Ordine.Casuale)
        {
            var altre = playlist.Where(v => v.Id != Corrente?.Id).ToList();
            return altre.Count == 0 ? playlist[0].Id : altre[casuale.Next(altre.Count)].Id;
        }
        int i = IndiceInPlaylist(Corrente?.Id);
        return playlist[(i + 1) % playlist.Count].Id;
    }

    public bool Mostra(string id)
    {
        if (!note.TryGetValue(id, out var v) || !v.Disponibile) return false;
        if (Corrente is not null && Corrente.Id != id)
        {
            storico.Add(Corrente.Id);
            if (storico.Count > MassimoStorico) storico.RemoveAt(0);
        }
        Corrente = v;
        RiavviaConteggio();
        return true;
    }

    /// <summary>Torna allo sfondo mostrato prima (D9); storico vuoto = voce precedente in playlist.</summary>
    public bool TornaIndietro()
    {
        while (storico.Count > 0)
        {
            var id = storico[^1];
            storico.RemoveAt(storico.Count - 1);
            if (note.TryGetValue(id, out var v) && v.Disponibile)
            {
                Corrente = v;
                RiavviaConteggio();
                return true;
            }
        }
        if (playlist.Count == 0) return false;
        int i = IndiceInPlaylist(Corrente?.Id);
        Corrente = i < 0 ? playlist[^1] : playlist[(i - 1 + playlist.Count) % playlist.Count];
        RiavviaConteggio();
        return true;
    }

    public bool DeveAvanzareDopoVideo(string id) =>
        pausaDa is null && regole.Intervallo is not null && regole.FineVideo &&
        Corrente is { Tipo: TipoVoce.Video } c && c.Id == id;

    public void PausaManuale() => pausaDa ??= Adesso;

    public void RiprendiManuale()
    {
        if (pausaDa is null) return;
        inizio += Adesso - pausaDa.Value;
        pausaDa = null;
    }

    public void RiavviaConteggio()
    {
        inizio = Adesso;
        if (pausaDa is not null) pausaDa = inizio;
        Totale = Permanenza(Corrente);
    }

    TimeSpan? Permanenza(Voce? v)
    {
        if (regole.Intervallo is null || v is null) return null;
        if (regole.FineVideo && v.Tipo == TipoVoce.Video && v.DurataSecondi is > 0)
            return TimeSpan.FromSeconds(v.DurataSecondi.Value);
        return regole.Intervallo;
    }

    int IndiceInPlaylist(string? id) => id is null ? -1 : playlist.FindIndex(v => v.Id == id);
}
```

- [ ] **Step 4: Eseguire i test e verificare che passino**

Run: `"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" test tests/EnergyItaly.SfondoAnimato.Core.Tests --filter MotoreRotazioneTests`
Expected: PASS, tutti i test di `MotoreRotazioneTests` superati.

- [ ] **Step 5: Commit**

```bash
git add src/EnergyItaly.SfondoAnimato.Core/MotoreRotazione.cs tests/EnergyItaly.SfondoAnimato.Core.Tests/MotoreRotazioneTests.cs
git commit -m "Aggiunge il motore di rotazione con storico, fine video e pausa manuale"
```

---

### Task 7: Regole di pausa

**Files:**
- Create: `src/EnergyItaly.SfondoAnimato.Core/RegolePausa.cs`
- Test: `tests/EnergyItaly.SfondoAnimato.Core.Tests/RegolePausaTests.cs`

**Interfaces:**
- Consumes: `LivelloPausa`, `MotivoPausa` (Task 1).
- Produces: `record SegnaliSistema(bool DesktopCoperto = false, bool SchermoIntero = false, bool SessioneNonAttiva = false, bool MonitorSpenti = false, bool ABatteria = false)`; `record DecisionePausa(LivelloPausa Livello, MotivoPausa Motivo)` con `static DecisionePausa Nessuna`; `RegolePausa.Valuta(SegnaliSistema s, bool pausaABatteria, bool pausaManuale) -> DecisionePausa`; `RegolePausa.Testo(MotivoPausa) -> string`.

- [ ] **Step 1: Scrivere i test (falliscono)**

<!-- file: tests/EnergyItaly.SfondoAnimato.Core.Tests/RegolePausaTests.cs -->
```csharp
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

public class RegolePausaTests
{
    [Fact]
    public void NessunSegnale_NessunaPausa()
    {
        Assert.Equal(DecisionePausa.Nessuna, RegolePausa.Valuta(new SegnaliSistema(), true, false));
    }

    [Theory]
    [InlineData(true, false, false, false, MotivoPausa.DesktopCoperto)]
    [InlineData(false, true, false, false, MotivoPausa.SchermoIntero)]
    [InlineData(false, false, true, false, MotivoPausa.SessioneNonAttiva)]
    [InlineData(false, false, false, true, MotivoPausa.MonitorSpenti)]
    public void SegnaliGravi_PausaProfonda(bool coperto, bool schermoIntero, bool sessione, bool spenti, MotivoPausa motivo)
    {
        var d = RegolePausa.Valuta(new SegnaliSistema(coperto, schermoIntero, sessione, spenti), true, false);

        Assert.Equal(LivelloPausa.Profonda, d.Livello);
        Assert.Equal(motivo, d.Motivo);
    }

    [Fact]
    public void ABatteria_LeggeraSoloSeAttivaNelleImpostazioni()
    {
        var s = new SegnaliSistema(ABatteria: true);

        Assert.Equal(new DecisionePausa(LivelloPausa.Leggera, MotivoPausa.ABatteria), RegolePausa.Valuta(s, true, false));
        Assert.Equal(DecisionePausa.Nessuna, RegolePausa.Valuta(s, false, false));
    }

    [Fact]
    public void Manuale_Leggera()
    {
        Assert.Equal(new DecisionePausa(LivelloPausa.Leggera, MotivoPausa.Manuale), RegolePausa.Valuta(new SegnaliSistema(), true, true));
    }

    [Fact]
    public void PiuMotivi_VinceIlPiuGrave()
    {
        var tutti = new SegnaliSistema(true, true, true, true, true);
        Assert.Equal(MotivoPausa.SessioneNonAttiva, RegolePausa.Valuta(tutti, true, true).Motivo);
        Assert.Equal(MotivoPausa.MonitorSpenti, RegolePausa.Valuta(tutti with { SessioneNonAttiva = false }, true, true).Motivo);
        Assert.Equal(MotivoPausa.Manuale, RegolePausa.Valuta(new SegnaliSistema(ABatteria: true), true, true).Motivo);
    }

    [Theory]
    [InlineData(MotivoPausa.SessioneNonAttiva, "SESSIONE BLOCCATA")]
    [InlineData(MotivoPausa.MonitorSpenti, "MONITOR SPENTI")]
    [InlineData(MotivoPausa.SchermoIntero, "APP A SCHERMO INTERO")]
    [InlineData(MotivoPausa.DesktopCoperto, "DESKTOP COPERTO")]
    [InlineData(MotivoPausa.ABatteria, "A BATTERIA")]
    [InlineData(MotivoPausa.Manuale, "")]
    public void Testo_Motivi(MotivoPausa m, string atteso)
    {
        Assert.Equal(atteso, RegolePausa.Testo(m));
    }
}
```

- [ ] **Step 2: Eseguire i test e verificare che falliscano**

Run: `"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" test tests/EnergyItaly.SfondoAnimato.Core.Tests --filter RegolePausaTests`
Expected: FAIL in compilazione.

- [ ] **Step 3: Implementare**

<!-- file: src/EnergyItaly.SfondoAnimato.Core/RegolePausa.cs -->
```csharp
namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Segnali raccolti dal sistema operativo (senza la pausa manuale e le impostazioni).</summary>
public sealed record SegnaliSistema(
    bool DesktopCoperto = false,
    bool SchermoIntero = false,
    bool SessioneNonAttiva = false,
    bool MonitorSpenti = false,
    bool ABatteria = false);

public sealed record DecisionePausa(LivelloPausa Livello, MotivoPausa Motivo)
{
    public static readonly DecisionePausa Nessuna = new(LivelloPausa.Nessuna, MotivoPausa.Nessuno);
}

/// <summary>Tabella di pausa del PRD (sezione 5.4) più la pausa manuale. Vince il motivo più grave.</summary>
public static class RegolePausa
{
    public static DecisionePausa Valuta(SegnaliSistema s, bool pausaABatteria, bool pausaManuale)
    {
        if (s.SessioneNonAttiva) return new(LivelloPausa.Profonda, MotivoPausa.SessioneNonAttiva);
        if (s.MonitorSpenti) return new(LivelloPausa.Profonda, MotivoPausa.MonitorSpenti);
        if (s.SchermoIntero) return new(LivelloPausa.Profonda, MotivoPausa.SchermoIntero);
        if (s.DesktopCoperto) return new(LivelloPausa.Profonda, MotivoPausa.DesktopCoperto);
        if (pausaManuale) return new(LivelloPausa.Leggera, MotivoPausa.Manuale);
        if (s.ABatteria && pausaABatteria) return new(LivelloPausa.Leggera, MotivoPausa.ABatteria);
        return DecisionePausa.Nessuna;
    }

    /// <summary>Testo mostrato dopo "IN PAUSA: " nella finestra; vuoto per la pausa manuale.</summary>
    public static string Testo(MotivoPausa motivo) => motivo switch
    {
        MotivoPausa.SessioneNonAttiva => "SESSIONE BLOCCATA",
        MotivoPausa.MonitorSpenti => "MONITOR SPENTI",
        MotivoPausa.SchermoIntero => "APP A SCHERMO INTERO",
        MotivoPausa.DesktopCoperto => "DESKTOP COPERTO",
        MotivoPausa.ABatteria => "A BATTERIA",
        _ => ""
    };
}
```

- [ ] **Step 4: Eseguire i test e verificare che passino**

Run: `"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" test tests/EnergyItaly.SfondoAnimato.Core.Tests --filter RegolePausaTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/EnergyItaly.SfondoAnimato.Core/RegolePausa.cs tests/EnergyItaly.SfondoAnimato.Core.Tests/RegolePausaTests.cs
git commit -m "Aggiunge le regole di pausa automatica e manuale"
```

---

### Task 8: Messaggi tra programma, finestra e player

**Files:**
- Create: `src/EnergyItaly.SfondoAnimato.Core/Messaggi.cs`
- Test: `tests/EnergyItaly.SfondoAnimato.Core.Tests/MessaggiTests.cs`

**Interfaces:**
- Consumes: `Voce`, `Catalogo.UrlBase` (Task 3), `Disposizione` (Task 1), `Impostazioni`, `ModificheImpostazioni` (Task 5), `NomiJson`.
- Produces: `record ComandoApp(string Tipo, string? Id, IReadOnlyList<string>? Ids, ModificheImpostazioni? Modifiche)`; `record EventoPlayer(string Tipo, string? Id, string? Messaggio, double? Secondi)`; `record StatoVista(IReadOnlyList<Voce> Voci, Impostazioni Impostazioni, string? Corrente, bool StoricoVuoto, bool InPausa, bool PausaManuale, string? MotivoPausa, long? RestanteMs, long? TotaleMs, bool ContoAttivo, bool AttendeFineVideo, Disposizione Disposizione)`; `Messaggi.LeggiComandoApp(string) -> ComandoApp?`; `Messaggi.LeggiEventoPlayer(string) -> EventoPlayer?`; `Messaggi.Mostra(Voce, Disposizione, int dissolvenzaMs, bool fineVideo) -> string`; `Messaggi.Layout(Disposizione) -> string`; `Messaggi.Semplice(string tipo) -> string`; `Messaggi.Avviso(string) -> string`; `Messaggi.Stato(StatoVista) -> string`.

Formato dei messaggi (riferimento per player e app):

- verso il player: `{"tipo":"mostra","id","tipoVoce","url","modo","scene":[{"area":{x,y,w,h},"monitor":[{x,y,w,h}]}],"dissolvenzaMs","fineVideo"}`, `{"tipo":"layout","modo","scene"}`, `{"tipo":"pausa"}`, `{"tipo":"riprendi"}`;
- dal player: `{"tipo":"pronto"}`, `{"tipo":"mostrato","id"}`, `{"tipo":"errore","id","messaggio"}`, `{"tipo":"videoFinito","id"}`, `{"tipo":"durataVideo","id","secondi"}`;
- verso l'app: `{"tipo":"stato","voci":[{id,tipoVoce,titolo,aziendale,url,durataSecondi,disponibile}],"playlist":[...],"impostazioni":{intervalloValore,intervalloUnita,rotazioneAttiva,fineVideo,ordine,dissolvenzaSecondi,modoMonitor,pausaABatteria,avvioConWindows},"corrente","storicoVuoto","inPausa","pausaManuale","motivoPausa","restanteMs","totaleMs","contoAttivo","attendeFineVideo","finestra":{w,h},"monitor":[{nome,x,y,w,h,principale}],"modo","scene":[...]}`, `{"tipo":"avviso","testo"}`;
- dall'app: `{"tipo":"pronta"}`, `{"tipo":"mostra","id"}`, `successivo`, `precedente`, `pausa`, `riprendi`, `{"tipo":"impostazioni","modifiche":{...}}`, `{"tipo":"playlist","ids":[...]}`, `importaDialogo`, `importaFile` (con oggetti File), `{"tipo":"elimina","id"}`, `apriLog`, `ripristina`.

- [ ] **Step 1: Scrivere i test (falliscono)**

<!-- file: tests/EnergyItaly.SfondoAnimato.Core.Tests/MessaggiTests.cs -->
```csharp
using System.Text.Json;
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

public class MessaggiTests
{
    static readonly Dictionary<string, string> Parametri = new() { ["durata"] = "16" };
    static readonly Voce Onda = new("onda-di-luce", TipoVoce.Animazione, "Onda di luce", true, "onda-di-luce.html", Parametri, null, true);
    static readonly Disposizione PcRiferimento = LayoutMonitor.Calcola(
        [new Monitor("DISPLAY2", new Rettangolo(0, 0, 1920, 1080), true), new Monitor("DISPLAY1", new Rettangolo(-1920, 7, 1920, 1080), false)],
        ModoMonitor.Estendi);

    static JsonElement Json(string s) => JsonDocument.Parse(s).RootElement;

    [Fact]
    public void LeggiComandoApp_Semplice()
    {
        var c = Messaggi.LeggiComandoApp("""{ "tipo": "mostra", "id": "aurora" }""");
        Assert.Equal(new ComandoApp("mostra", "aurora", null, null), c);
    }

    [Fact]
    public void LeggiComandoApp_PlaylistIgnoraNonStringhe()
    {
        var c = Messaggi.LeggiComandoApp("""{ "tipo": "playlist", "ids": ["a", 3, "b", null] }""");
        Assert.Equal(new[] { "a", "b" }, c!.Ids);
    }

    [Fact]
    public void LeggiComandoApp_Modifiche()
    {
        var c = Messaggi.LeggiComandoApp("""{ "tipo": "impostazioni", "modifiche": { "intervalloValore": 2, "intervalloUnita": "ore", "fineVideo": false, "ordine": 5 } }""");

        Assert.Equal(new ModificheImpostazioni(IntervalloValore: 2, IntervalloUnita: "ore", FineVideo: false), c!.Modifiche);
    }

    [Fact]
    public void LeggiComandoApp_NumeroNonIntero_DiventaZero()
    {
        var c = Messaggi.LeggiComandoApp("""{ "tipo": "impostazioni", "modifiche": { "intervalloValore": 1.5 } }""");
        Assert.Equal(0, c!.Modifiche!.IntervalloValore);
    }

    [Theory]
    [InlineData("non json")]
    [InlineData("[]")]
    [InlineData("""{ "id": "x" }""")]
    [InlineData("""{ "tipo": 4 }""")]
    public void LeggiComandoApp_NonValido_Null(string json)
    {
        Assert.Null(Messaggi.LeggiComandoApp(json));
    }

    [Fact]
    public void LeggiEventoPlayer()
    {
        Assert.Equal(new EventoPlayer("durataVideo", "v", null, 160.4), Messaggi.LeggiEventoPlayer("""{ "tipo": "durataVideo", "id": "v", "secondi": 160.4 }"""));
        Assert.Equal(new EventoPlayer("errore", "v", "boh", null), Messaggi.LeggiEventoPlayer("""{ "tipo": "errore", "id": "v", "messaggio": "boh" }"""));
        Assert.Null(Messaggi.LeggiEventoPlayer("{"));
    }

    [Fact]
    public void Mostra_ContieneVoceEScene()
    {
        var m = Json(Messaggi.Mostra(Onda, PcRiferimento, 2000, true));

        Assert.Equal("mostra", m.GetProperty("tipo").GetString());
        Assert.Equal("onda-di-luce", m.GetProperty("id").GetString());
        Assert.Equal("animazione", m.GetProperty("tipoVoce").GetString());
        Assert.Equal("https://sfondo.energyitaly/animazioni/onda-di-luce.html?durata=16", m.GetProperty("url").GetString());
        Assert.Equal("estendi", m.GetProperty("modo").GetString());
        Assert.Equal(2000, m.GetProperty("dissolvenzaMs").GetInt32());
        Assert.True(m.GetProperty("fineVideo").GetBoolean());
        var scena = Assert.Single(m.GetProperty("scene").EnumerateArray());
        Assert.Equal(3840, scena.GetProperty("area").GetProperty("w").GetInt32());
        Assert.Equal(7, scena.GetProperty("monitor")[0].GetProperty("y").GetInt32());
    }

    [Fact]
    public void Semplice_EAvviso()
    {
        Assert.Equal("""{"tipo":"pausa"}""", Messaggi.Semplice("pausa"));
        Assert.Equal("""{"tipo":"avviso","testo":"Aggiunto alla libreria: Città"}""", Messaggi.Avviso("Aggiunto alla libreria: Città"));
    }

    [Fact]
    public void Stato_CampiPrincipali()
    {
        var imp = new Impostazioni { Playlist = ["onda-di-luce"] };
        var s = new StatoVista([Onda], imp, "onda-di-luce", true, false, false, null, 60000, 1800000, true, false, PcRiferimento);

        var j = Json(Messaggi.Stato(s));

        Assert.Equal("stato", j.GetProperty("tipo").GetString());
        Assert.Equal("Onda di luce", j.GetProperty("voci")[0].GetProperty("titolo").GetString());
        Assert.Equal(JsonValueKind.Null, j.GetProperty("voci")[0].GetProperty("durataSecondi").ValueKind);
        Assert.Equal("onda-di-luce", j.GetProperty("playlist")[0].GetString());
        Assert.Equal("minuti", j.GetProperty("impostazioni").GetProperty("intervalloUnita").GetString());
        Assert.Equal("onda-di-luce", j.GetProperty("corrente").GetString());
        Assert.Equal(JsonValueKind.Null, j.GetProperty("motivoPausa").ValueKind);
        Assert.Equal(60000, j.GetProperty("restanteMs").GetInt64());
        Assert.Equal(3840, j.GetProperty("finestra").GetProperty("w").GetInt32());
        Assert.Equal("DISPLAY1", j.GetProperty("monitor")[0].GetProperty("nome").GetString());
        Assert.False(j.GetProperty("monitor")[0].GetProperty("principale").GetBoolean());
        Assert.Equal("estendi", j.GetProperty("modo").GetString());
        Assert.Single(j.GetProperty("scene").EnumerateArray());
    }
}
```

- [ ] **Step 2: Eseguire i test e verificare che falliscano**

Run: `"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" test tests/EnergyItaly.SfondoAnimato.Core.Tests --filter MessaggiTests`
Expected: FAIL in compilazione.

- [ ] **Step 3: Implementare**

<!-- file: src/EnergyItaly.SfondoAnimato.Core/Messaggi.cs -->
```csharp
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
                    AvvioConWindows: Booleano(m, "avvioConWindows"));
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
```

- [ ] **Step 4: Eseguire i test e verificare che passino**

Run: `"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" test tests/EnergyItaly.SfondoAnimato.Core.Tests --filter MessaggiTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/EnergyItaly.SfondoAnimato.Core/Messaggi.cs tests/EnergyItaly.SfondoAnimato.Core.Tests/MessaggiTests.cs
git commit -m "Aggiunge il protocollo dei messaggi tra programma, finestra e player"
```

---

### Task 9: Coordinatore (stato unico) con interfacce verso Windows

**Files:**
- Create: `src/EnergyItaly.SfondoAnimato.Core/Interfacce.cs`, `src/EnergyItaly.SfondoAnimato.Core/Coordinatore.cs`
- Test: `tests/EnergyItaly.SfondoAnimato.Core.Tests/Falsi.cs`, `tests/EnergyItaly.SfondoAnimato.Core.Tests/CoordinatoreTests.cs`

**Interfaces:**
- Consumes: tutto il Core dei Task 1-8.
- Produces:
  - `interface IVistaPlayer { void Invia(string json); void ImpostaArea(Rettangolo area); void ImpostaSospensione(bool sospesa); }`
  - `interface IVistaApp { void Invia(string json); }`
  - `interface IArchivio { string? LeggiImpostazioni(); void ScriviImpostazioni(string json); string? LeggiLibreria(); void ScriviLibreria(string json); bool EsisteInLibreria(string nomeFile); long Dimensione(string percorso); void CopiaInLibreria(string percorsoOrigine, string nomeFile); void CancellaDaLibreria(string nomeFile); }`
  - `interface ISistema { void ImpostaAvvioConWindows(bool attivo); void ApriCartellaLog(); void AggiornaIcona(string titolo, bool inPausa); }`
  - `interface ILog { void Info(string testo); void Avviso(string testo); void Errore(string testo); }`
  - `class Coordinatore(string manifestJson, string? predefiniteJson, IVistaPlayer player, IVistaApp app, IArchivio archivio, ISistema sistema, ILog log, TimeProvider tempo, Random? casuale = null)` con: `void Avvia(IReadOnlyList<Monitor>)`, `void Tick()`, `void DaPlayer(string json)`, `void DaApp(string json)`, `void ImportaFile(IReadOnlyList<string> percorsi)`, `void AggiornaMonitor(IReadOnlyList<Monitor>)`, `void AggiornaSegnali(SegnaliSistema)`, `void Successivo()`, `void AlternaPausaManuale()`, `void PlayerRicreato()`, proprietà `Impostazioni`, `Corrente`, `InPausaManuale`, `Pausa`, `Disposizione`.

- [ ] **Step 1: Scrivere i falsi per i test**

<!-- file: tests/EnergyItaly.SfondoAnimato.Core.Tests/Falsi.cs -->
```csharp
using System.Text.Json;
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

sealed class PlayerFalso : IVistaPlayer
{
    public List<string> Messaggi { get; } = [];
    public Rettangolo? Area { get; private set; }
    public bool Sospeso { get; private set; }
    public List<bool> Sospensioni { get; } = [];

    public void Invia(string json) => Messaggi.Add(json);
    public void ImpostaArea(Rettangolo area) => Area = area;
    public void ImpostaSospensione(bool sospesa) { Sospeso = sospesa; Sospensioni.Add(sospesa); }

    public IEnumerable<JsonElement> DiTipo(string tipo) =>
        Messaggi.Select(m => JsonDocument.Parse(m).RootElement).Where(e => e.GetProperty("tipo").GetString() == tipo);

    public JsonElement Ultimo(string tipo) => DiTipo(tipo).Last();
    public string? UltimoTipo => Messaggi.Count == 0 ? null : JsonDocument.Parse(Messaggi[^1]).RootElement.GetProperty("tipo").GetString();
}

sealed class AppFalsa : IVistaApp
{
    public List<string> Messaggi { get; } = [];
    public void Invia(string json) => Messaggi.Add(json);

    public JsonElement UltimoStato => Messaggi.Select(m => JsonDocument.Parse(m).RootElement).Last(e => e.GetProperty("tipo").GetString() == "stato");
    public IEnumerable<string> Avvisi => Messaggi.Select(m => JsonDocument.Parse(m).RootElement)
        .Where(e => e.GetProperty("tipo").GetString() == "avviso").Select(e => e.GetProperty("testo").GetString()!);
}

sealed class ArchivioFalso : IArchivio
{
    public string? Impostazioni { get; set; }
    public string? Libreria { get; set; }
    public HashSet<string> FileLibreria { get; } = [];
    public Dictionary<string, long> FileDaImportare { get; } = [];
    public List<(string Origine, string Nome)> Copie { get; } = [];
    public List<string> Cancellati { get; } = [];

    public string? LeggiImpostazioni() => Impostazioni;
    public void ScriviImpostazioni(string json) => Impostazioni = json;
    public string? LeggiLibreria() => Libreria;
    public void ScriviLibreria(string json) => Libreria = json;
    public bool EsisteInLibreria(string nomeFile) => FileLibreria.Contains(nomeFile);
    public long Dimensione(string percorso) => FileDaImportare.TryGetValue(percorso, out var d) ? d : throw new FileNotFoundException(percorso);
    public void CopiaInLibreria(string percorsoOrigine, string nomeFile) { Copie.Add((percorsoOrigine, nomeFile)); FileLibreria.Add(nomeFile); }
    public void CancellaDaLibreria(string nomeFile) { Cancellati.Add(nomeFile); FileLibreria.Remove(nomeFile); }
}

sealed class SistemaFalso : ISistema
{
    public bool? AvvioConWindows { get; private set; }
    public int CartelleLogAperte { get; private set; }
    public (string Titolo, bool InPausa) Icona { get; private set; }

    public void ImpostaAvvioConWindows(bool attivo) => AvvioConWindows = attivo;
    public void ApriCartellaLog() => CartelleLogAperte++;
    public void AggiornaIcona(string titolo, bool inPausa) => Icona = (titolo, inPausa);
}

sealed class LogFalso : ILog
{
    public List<string> Righe { get; } = [];
    public void Info(string testo) => Righe.Add("INFO " + testo);
    public void Avviso(string testo) => Righe.Add("AVVISO " + testo);
    public void Errore(string testo) => Righe.Add("ERRORE " + testo);
}
```

- [ ] **Step 2: Scrivere i test del coordinatore (falliscono)**

<!-- file: tests/EnergyItaly.SfondoAnimato.Core.Tests/CoordinatoreTests.cs -->
```csharp
using System.Text.Json;
using EnergyItaly.SfondoAnimato.Core;
using Microsoft.Extensions.Time.Testing;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

public class CoordinatoreTests
{
    const string Manifest = """
        { "versione": 1, "animazioni": [
          { "id": "a", "titolo": "A", "file": "a.html", "parametri": {} },
          { "id": "b", "titolo": "B", "file": "b.html", "parametri": {} },
          { "id": "c", "titolo": "C", "file": "c.html", "parametri": {} } ] }
        """;

    static readonly Monitor Principale = new("DISPLAY2", new Rettangolo(0, 0, 1920, 1080), true);
    static readonly Monitor Secondario = new("DISPLAY1", new Rettangolo(-1920, 7, 1920, 1080), false);

    readonly FakeTimeProvider tempo = new(new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero));
    readonly PlayerFalso player = new();
    readonly AppFalsa app = new();
    readonly ArchivioFalso archivio = new();
    readonly SistemaFalso sistema = new();
    readonly LogFalso log = new();

    Coordinatore Avvia(string? predefinite = null, params Monitor[] monitor)
    {
        var c = new Coordinatore(Manifest, predefinite, player, app, archivio, sistema, log, tempo, new Random(7));
        c.Avvia(monitor.Length == 0 ? [Principale, Secondario] : monitor);
        c.DaPlayer("""{"tipo":"pronto"}""");
        return c;
    }

    static string Comando(object o) => JsonSerializer.Serialize(o);

    [Fact]
    public void Avvio_MostraLaPrimaQuandoIlPlayerEPronto()
    {
        var c = Avvia();

        var m = player.Ultimo("mostra");
        Assert.Equal("a", m.GetProperty("id").GetString());
        Assert.Equal(0, m.GetProperty("dissolvenzaMs").GetInt32());
        Assert.Equal(new Rettangolo(-1920, 0, 3840, 1087), player.Area);
        Assert.Contains("\"ultimaVoce\": \"a\"", archivio.Impostazioni);
        Assert.True(sistema.AvvioConWindows);
        Assert.Equal(("A", false), sistema.Icona);
    }

    [Fact]
    public void Avvio_RiprendeDopoLUltimaVoce()
    {
        archivio.Impostazioni = """{ "versione": 2, "ultimaVoce": "b" }""";
        var c = Avvia();
        Assert.Equal("c", c.Corrente?.Id);
    }

    [Fact]
    public void Avvio_ImpostazioniRotte_LogEValoriPredefiniti()
    {
        archivio.Impostazioni = "{ rotto";
        var c = Avvia();

        Assert.Equal("a", c.Corrente?.Id);
        Assert.Contains(log.Righe, r => r.StartsWith("AVVISO impostazioni.json"));
    }

    [Fact]
    public void Tick_DopoLIntervallo_PassaAllaSuccessivaConDissolvenza()
    {
        var c = Avvia();
        tempo.Advance(TimeSpan.FromMinutes(29));
        c.Tick();
        Assert.Equal("a", c.Corrente?.Id);

        tempo.Advance(TimeSpan.FromMinutes(1));
        c.Tick();

        Assert.Equal("b", c.Corrente?.Id);
        Assert.Equal(2000, player.Ultimo("mostra").GetProperty("dissolvenzaMs").GetInt32());
        Assert.Equal("b", app.UltimoStato.GetProperty("corrente").GetString());
    }

    [Fact]
    public void PausaAutomatica_BloccaIlCambioERiprendeSubitoSeScaduto()
    {
        var c = Avvia();
        c.AggiornaSegnali(new SegnaliSistema(SessioneNonAttiva: true));

        Assert.Equal("pausa", player.UltimoTipo);
        Assert.True(player.Sospeso);
        Assert.Equal("SESSIONE BLOCCATA", app.UltimoStato.GetProperty("motivoPausa").GetString());

        tempo.Advance(TimeSpan.FromMinutes(31));
        c.Tick();
        Assert.Equal("a", c.Corrente?.Id);

        c.AggiornaSegnali(new SegnaliSistema());

        Assert.False(player.Sospeso);
        Assert.Equal("b", c.Corrente?.Id);
        Assert.Contains(player.Messaggi, m => m.Contains("\"riprendi\""));
    }

    [Fact]
    public void PausaManuale_FermaIlConteggio()
    {
        var c = Avvia();
        c.DaApp(Comando(new { tipo = "pausa" }));

        Assert.Equal("pausa", player.UltimoTipo);
        Assert.True(app.UltimoStato.GetProperty("pausaManuale").GetBoolean());
        Assert.False(app.UltimoStato.GetProperty("contoAttivo").GetBoolean());
        Assert.Equal(("A", true), sistema.Icona);

        tempo.Advance(TimeSpan.FromHours(2));
        c.Tick();
        Assert.Equal("a", c.Corrente?.Id);

        c.DaApp(Comando(new { tipo = "riprendi" }));
        Assert.Equal("riprendi", player.UltimoTipo);
        tempo.Advance(TimeSpan.FromMinutes(30));
        c.Tick();
        Assert.Equal("b", c.Corrente?.Id);
    }

    [Fact]
    public void Successivo_EPrecedente()
    {
        var c = Avvia();
        c.DaApp(Comando(new { tipo = "successivo" }));
        c.DaApp(Comando(new { tipo = "successivo" }));
        Assert.Equal("c", c.Corrente?.Id);

        c.DaApp(Comando(new { tipo = "precedente" }));
        Assert.Equal("b", c.Corrente?.Id);
        Assert.Contains("\"ultimaVoce\": \"b\"", archivio.Impostazioni);
    }

    [Fact]
    public void Impostazioni_ValoreNonValido_AvvisoESenzaSalvare()
    {
        var c = Avvia();
        var prima = archivio.Impostazioni;

        c.DaApp(Comando(new { tipo = "impostazioni", modifiche = new { intervalloValore = 0 } }));

        Assert.Contains("Scrivi un numero intero da 1 a 1440 minuti", app.Avvisi);
        Assert.Equal(prima, archivio.Impostazioni);
        Assert.Equal(30, c.Impostazioni.IntervalloValore);
    }

    [Fact]
    public void Impostazioni_DueOre_CambiaPermanenzaESalva()
    {
        var c = Avvia();
        c.DaApp(Comando(new { tipo = "impostazioni", modifiche = new { intervalloValore = 2, intervalloUnita = "ore" } }));

        Assert.Equal(7_200_000, app.UltimoStato.GetProperty("totaleMs").GetInt64());
        Assert.Contains("\"unita\": \"ore\"", archivio.Impostazioni);
    }

    [Fact]
    public void Mai_ConVideo_IlPlayerLoMetteInLoop()
    {
        archivio.Libreria = """{ "versione": 1, "voci": [ { "id": "u-v-aaaaaa", "tipo": "video", "titolo": "V", "file": "v.mp4", "durataSecondi": 60 } ] }""";
        archivio.FileLibreria.Add("v.mp4");
        archivio.Impostazioni = """{ "versione": 2, "rotazioneAttiva": false, "playlist": ["u-v-aaaaaa"] }""";
        var c = Avvia();

        Assert.Equal("u-v-aaaaaa", c.Corrente?.Id);
        Assert.False(player.Ultimo("mostra").GetProperty("fineVideo").GetBoolean());
        c.DaPlayer("""{"tipo":"videoFinito","id":"u-v-aaaaaa"}""");
        Assert.Equal("u-v-aaaaaa", c.Corrente?.Id);
    }

    [Fact]
    public void Playlist_Vuota_Rifiutata()
    {
        var c = Avvia();
        c.DaApp(Comando(new { tipo = "playlist", ids = Array.Empty<string>() }));

        Assert.Contains("Lascia almeno uno sfondo nella playlist", app.Avvisi);
        Assert.Equal(new[] { "a", "b", "c" }, c.Impostazioni.Playlist);
    }

    [Fact]
    public void Playlist_Riordinata_DecideLaSuccessiva()
    {
        var c = Avvia();
        c.DaApp(Comando(new { tipo = "playlist", ids = new[] { "a", "c", "b", "sconosciuto" } }));
        c.Successivo();

        Assert.Equal(new[] { "a", "c", "b" }, c.Impostazioni.Playlist);
        Assert.Equal("c", c.Corrente?.Id);
    }

    [Fact]
    public void ErrorePlayer_SullaCorrente_PassaAllaSuccessiva()
    {
        var c = Avvia();
        c.DaPlayer("""{"tipo":"errore","id":"a","messaggio":"timeout"}""");

        Assert.Equal("b", c.Corrente?.Id);
        Assert.Contains(log.Righe, r => r.StartsWith("ERRORE") && r.Contains("timeout"));
    }

    [Fact]
    public void ErroriDiFila_SuTutteLeVoci_SiFerma()
    {
        var c = Avvia();
        c.DaPlayer("""{"tipo":"errore","id":"a","messaggio":"x"}""");
        c.DaPlayer("""{"tipo":"errore","id":"b","messaggio":"x"}""");
        c.DaPlayer("""{"tipo":"errore","id":"c","messaggio":"x"}""");

        Assert.Equal("c", c.Corrente?.Id);
        Assert.Contains(log.Righe, r => r.Contains("Nessuno sfondo della playlist si carica"));
    }

    [Fact]
    public void ErroreDiUnaVoceVecchia_Ignorato()
    {
        var c = Avvia();
        c.DaPlayer("""{"tipo":"errore","id":"c","messaggio":"x"}""");
        Assert.Equal("a", c.Corrente?.Id);
    }

    [Fact]
    public void Import_FileValido_AggiuntoAllaLibreriaNonAllaPlaylist()
    {
        var c = Avvia();
        archivio.FileDaImportare[@"C:\Foto\Foto mare.JPG"] = 1000;

        c.ImportaFile([@"C:\Foto\Foto mare.JPG"]);

        var (origine, nome) = Assert.Single(archivio.Copie);
        Assert.Equal(@"C:\Foto\Foto mare.JPG", origine);
        Assert.StartsWith("foto-mare-", nome);
        Assert.EndsWith(".jpg", nome);
        Assert.Contains("Foto mare", archivio.Libreria);
        Assert.Contains("Aggiunto alla libreria: Foto mare", app.Avvisi);
        Assert.Equal(4, app.UltimoStato.GetProperty("voci").GetArrayLength());
        Assert.Equal(new[] { "a", "b", "c" }, c.Impostazioni.Playlist);
    }

    [Fact]
    public void Import_FormatoNonSupportatoEFileIllegibile_Avvisi()
    {
        var c = Avvia();
        archivio.FileDaImportare[@"C:\x\virus.exe"] = 10;

        c.ImportaFile([@"C:\x\virus.exe", @"C:\x\sparito.png"]);

        Assert.Empty(archivio.Copie);
        var avviso = Assert.Single(app.Avvisi);
        Assert.StartsWith("2 file non importati", avviso);
    }

    [Fact]
    public void Elimina_VoceImportataInOnda_CancellaFileEPassaAvanti()
    {
        archivio.Libreria = """{ "versione": 1, "voci": [ { "id": "u-m-aaaaaa", "tipo": "immagine", "titolo": "M", "file": "m.jpg" } ] }""";
        archivio.FileLibreria.Add("m.jpg");
        var c = Avvia();
        c.DaApp(Comando(new { tipo = "playlist", ids = new[] { "a", "u-m-aaaaaa" } }));
        c.DaApp(Comando(new { tipo = "mostra", id = "u-m-aaaaaa" }));

        c.DaApp(Comando(new { tipo = "elimina", id = "u-m-aaaaaa" }));

        Assert.Equal(new[] { "m.jpg" }, archivio.Cancellati);
        Assert.Equal("a", c.Corrente?.Id);
        Assert.Equal(new[] { "a" }, c.Impostazioni.Playlist);
        Assert.DoesNotContain("u-m-aaaaaa", archivio.Libreria);
        Assert.Contains("Eliminato dalla libreria: M", app.Avvisi);
    }

    [Fact]
    public void Elimina_VoceAziendale_Rifiutata()
    {
        var c = Avvia();
        c.DaApp(Comando(new { tipo = "elimina", id = "a" }));

        Assert.Empty(archivio.Cancellati);
        Assert.Contains("Si possono eliminare solo gli sfondi importati", app.Avvisi);
    }

    [Fact]
    public void VideoFinito_ConFineVideo_Avanza()
    {
        archivio.Libreria = """{ "versione": 1, "voci": [ { "id": "u-v-aaaaaa", "tipo": "video", "titolo": "V", "file": "v.mp4", "durataSecondi": 60 } ] }""";
        archivio.FileLibreria.Add("v.mp4");
        archivio.Impostazioni = """{ "versione": 2, "playlist": ["a", "u-v-aaaaaa"], "ultimaVoce": "a" }""";
        var c = Avvia();
        Assert.Equal("u-v-aaaaaa", c.Corrente?.Id);
        Assert.True(player.Ultimo("mostra").GetProperty("fineVideo").GetBoolean());

        c.DaPlayer("""{"tipo":"videoFinito","id":"u-v-aaaaaa"}""");

        Assert.Equal("a", c.Corrente?.Id);
    }

    [Fact]
    public void DurataVideo_SalvataInLibreria()
    {
        archivio.Libreria = """{ "versione": 1, "voci": [ { "id": "u-v-aaaaaa", "tipo": "video", "titolo": "V", "file": "v.mp4" } ] }""";
        archivio.FileLibreria.Add("v.mp4");
        var c = Avvia();

        c.DaPlayer("""{"tipo":"durataVideo","id":"u-v-aaaaaa","secondi":159.6}""");

        Assert.Contains("\"durataSecondi\": 160", archivio.Libreria);
    }

    [Fact]
    public void ModoDuplica_RimandaLoSfondoConUnaScenaPerMonitor()
    {
        var c = Avvia();
        c.DaApp(Comando(new { tipo = "impostazioni", modifiche = new { modoMonitor = "duplica" } }));

        var m = player.Ultimo("mostra");
        Assert.Equal("duplica", m.GetProperty("modo").GetString());
        Assert.Equal(2, m.GetProperty("scene").GetArrayLength());
        Assert.Equal("duplica", app.UltimoStato.GetProperty("modo").GetString());
    }

    [Fact]
    public void MonitorScollegato_RidimensionaEMandaIlLayout()
    {
        var c = Avvia();
        c.AggiornaMonitor([Principale]);

        Assert.Equal(new Rettangolo(0, 0, 1920, 1080), player.Area);
        Assert.Equal("layout", player.UltimoTipo);
        Assert.Equal(1, app.UltimoStato.GetProperty("monitor").GetArrayLength());
    }

    [Fact]
    public void MonitorIdentici_NessunMessaggio()
    {
        var c = Avvia();
        var quanti = player.Messaggi.Count;
        c.AggiornaMonitor([Secondario, Principale]);
        Assert.Equal(quanti, player.Messaggi.Count);
    }

    [Fact]
    public void Ripristina_TornaAllePredefiniteAziendali()
    {
        const string aziendali = """{ "versione": 2, "intervallo": { "valore": 15, "unita": "minuti" } }""";
        var c = Avvia(aziendali);
        c.DaApp(Comando(new { tipo = "impostazioni", modifiche = new { intervalloValore = 3, intervalloUnita = "ore", ordine = "casuale" } }));

        c.DaApp(Comando(new { tipo = "ripristina" }));

        Assert.Equal(15, c.Impostazioni.IntervalloValore);
        Assert.Equal(Ordine.Sequenziale, c.Impostazioni.Ordine);
        Assert.Contains("Impostazioni aziendali ripristinate", app.Avvisi);
    }

    [Fact]
    public void ComandiVari()
    {
        var c = Avvia();
        c.DaApp(Comando(new { tipo = "apriLog" }));
        c.DaApp(Comando(new { tipo = "pronta" }));
        c.DaApp("non json");

        Assert.Equal(1, sistema.CartelleLogAperte);
        Assert.Contains(log.Righe, r => r.StartsWith("AVVISO") && r.Contains("non riconosciuto"));
    }

    [Fact]
    public void PlayerRicreato_AspettaPronto()
    {
        var c = Avvia();
        c.PlayerRicreato();
        var quanti = player.DiTipo("mostra").Count();

        c.Successivo();
        Assert.Equal(quanti, player.DiTipo("mostra").Count());

        c.DaPlayer("""{"tipo":"pronto"}""");
        Assert.Equal("b", player.Ultimo("mostra").GetProperty("id").GetString());
    }
}
```

- [ ] **Step 3: Eseguire i test e verificare che falliscano**

Run: `"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" test tests/EnergyItaly.SfondoAnimato.Core.Tests --filter CoordinatoreTests`
Expected: FAIL in compilazione (`IVistaPlayer`, `Coordinatore` non esistono).

- [ ] **Step 4: Implementare le interfacce**

<!-- file: src/EnergyItaly.SfondoAnimato.Core/Interfacce.cs -->
```csharp
namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>La finestra di sfondo con il player.</summary>
public interface IVistaPlayer
{
    void Invia(string json);
    void ImpostaArea(Rettangolo area);
    void ImpostaSospensione(bool sospesa);
}

/// <summary>La finestra principale (galleria).</summary>
public interface IVistaApp
{
    void Invia(string json);
}

/// <summary>File su disco: impostazioni, libreria e file importati.</summary>
public interface IArchivio
{
    string? LeggiImpostazioni();
    void ScriviImpostazioni(string json);
    string? LeggiLibreria();
    void ScriviLibreria(string json);
    bool EsisteInLibreria(string nomeFile);
    long Dimensione(string percorso);
    void CopiaInLibreria(string percorsoOrigine, string nomeFile);
    void CancellaDaLibreria(string nomeFile);
}

/// <summary>Servizi di Windows: avvio automatico, cartella log, icona nell'area di notifica.</summary>
public interface ISistema
{
    void ImpostaAvvioConWindows(bool attivo);
    void ApriCartellaLog();
    void AggiornaIcona(string titolo, bool inPausa);
}

public interface ILog
{
    void Info(string testo);
    void Avviso(string testo);
    void Errore(string testo);
}
```

- [ ] **Step 5: Implementare il coordinatore**

<!-- file: src/EnergyItaly.SfondoAnimato.Core/Coordinatore.cs -->
```csharp
namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>
/// Tiene lo stato unico del programma e decide cosa mandare a player e finestra.
/// Tutti i metodi vanno chiamati dallo stesso thread (il thread dell'interfaccia).
/// </summary>
public sealed class Coordinatore
{
    readonly string manifestJson;
    readonly string? predefiniteJson;
    readonly IVistaPlayer player;
    readonly IVistaApp app;
    readonly IArchivio archivio;
    readonly ISistema sistema;
    readonly ILog log;
    readonly TimeProvider tempo;
    readonly MotoreRotazione motore;

    DatiLibreria libreria = new();
    IReadOnlyList<Voce> voci = [];
    Impostazioni predefinite = new();
    Impostazioni imp = new();
    IReadOnlyList<Monitor> monitor = [];
    Disposizione disposizione = LayoutMonitor.Calcola([], ModoMonitor.Estendi);
    SegnaliSistema segnali = new();
    DecisionePausa decisione = DecisionePausa.Nessuna;
    bool playerPronto;
    int erroriDiFila;

    public Coordinatore(string manifestJson, string? predefiniteJson, IVistaPlayer player, IVistaApp app,
        IArchivio archivio, ISistema sistema, ILog log, TimeProvider tempo, Random? casuale = null)
    {
        this.manifestJson = manifestJson;
        this.predefiniteJson = predefiniteJson;
        this.player = player;
        this.app = app;
        this.archivio = archivio;
        this.sistema = sistema;
        this.log = log;
        this.tempo = tempo;
        motore = new MotoreRotazione(tempo, casuale);
    }

    public Impostazioni Impostazioni => imp;
    public Voce? Corrente => motore.Corrente;
    public bool InPausaManuale => motore.InPausaManuale;
    public DecisionePausa Pausa => decisione;
    public Disposizione Disposizione => disposizione;

    // ---------- Avvio ----------

    public void Avvia(IReadOnlyList<Monitor> monitorIniziali)
    {
        var avvisi = new List<string>();
        libreria = Catalogo.LeggiLibreria(Leggi(archivio.LeggiLibreria, "libreria.json"), avvisi);
        RicaricaVoci(avvisi);
        var idValidi = voci.Select(v => v.Id).ToList();
        var playlistDiBase = voci.Where(v => v.Aziendale).Select(v => v.Id).ToList();
        predefinite = ArchivioImpostazioni.Predefinite(predefiniteJson, idValidi, playlistDiBase, avvisi);
        imp = ArchivioImpostazioni.Carica(Leggi(archivio.LeggiImpostazioni, "impostazioni.json"), predefinite, idValidi, avvisi);
        foreach (var a in avvisi) log.Avviso(a);

        monitor = monitorIniziali;
        disposizione = LayoutMonitor.Calcola(monitor, imp.ModoMonitor);
        log.Info(DescriviMonitor());
        player.ImpostaArea(disposizione.Finestra);

        motore.AggiornaVoci(voci, imp.Playlist);
        motore.AggiornaRegole(Regole(), riavvia: true);
        motore.Avvia(imp.UltimaVoce);
        if (motore.Corrente is { } c) log.Info($"Primo sfondo: {c.Titolo}");
        else log.Errore("Nessuno sfondo disponibile da mostrare");

        try { sistema.ImpostaAvvioConWindows(imp.AvvioConWindows); }
        catch (Exception ex) { log.Errore($"Avvio con Windows non impostato: {ex.Message}"); }
        SalvaUltima();
        AggiornaIcona();
    }

    string? Leggi(Func<string?> lettura, string nome)
    {
        try { return lettura(); }
        catch (Exception ex)
        {
            log.Errore($"{nome} non leggibile: {ex.Message}");
            return null;
        }
    }

    // ---------- Tempo ----------

    /// <summary>Da chiamare ogni secondo: cambia sfondo se il tempo è scaduto e non c'è pausa.</summary>
    public void Tick()
    {
        if (decisione.Livello == LivelloPausa.Nessuna && motore.Scaduta) Avanti();
    }

    public void Successivo() => Avanti();

    public void AlternaPausaManuale() => ImpostaPausaManuale(!motore.InPausaManuale);

    void Avanti()
    {
        var id = motore.Prossima();
        if (id is not null) MostraVoce(id);
    }

    void MostraVoce(string id)
    {
        if (!motore.Mostra(id))
        {
            InviaAvviso("Questo sfondo non è disponibile");
            return;
        }
        DopoCambio();
    }

    void DopoCambio()
    {
        SalvaUltima();
        MandaMostra(imp.DissolvenzaSecondi * 1000);
        if (motore.Corrente is { } c) log.Info($"Sfondo: {c.Titolo}");
        AggiornaIcona();
        InviaStato();
    }

    // ---------- Player ----------

    public void PlayerRicreato() => playerPronto = false;

    public void DaPlayer(string json)
    {
        var e = Messaggi.LeggiEventoPlayer(json);
        if (e is null)
        {
            log.Avviso("Messaggio dal player non riconosciuto");
            return;
        }
        switch (e.Tipo)
        {
            case "pronto":
                playerPronto = true;
                MandaMostra(0);
                if (decisione.Livello != LivelloPausa.Nessuna) player.Invia(Messaggi.Semplice("pausa"));
                break;
            case "mostrato":
                erroriDiFila = 0;
                break;
            case "errore":
                GestisciErrore(e);
                break;
            case "videoFinito":
                if (e.Id is not null && decisione.Livello == LivelloPausa.Nessuna && motore.DeveAvanzareDopoVideo(e.Id)) Avanti();
                break;
            case "durataVideo":
                AggiornaDurata(e);
                break;
            default:
                log.Avviso($"Evento del player non riconosciuto: {e.Tipo}");
                break;
        }
    }

    void GestisciErrore(EventoPlayer e)
    {
        log.Errore($"Lo sfondo '{e.Id}' non si carica: {e.Messaggio}");
        if (e.Id is null || e.Id != motore.Corrente?.Id) return;
        erroriDiFila++;
        if (erroriDiFila >= Math.Max(1, motore.Playlist.Count))
        {
            log.Errore("Nessuno sfondo della playlist si carica: resto sull'ultimo");
            return;
        }
        Avanti();
    }

    void AggiornaDurata(EventoPlayer e)
    {
        if (e.Id is null || e.Secondi is not > 0) return;
        var voce = libreria.Voci.Find(v => v.Id == e.Id);
        int secondi = (int)Math.Round(e.Secondi.Value);
        if (voce is null || voce.DurataSecondi == secondi) return;
        voce.DurataSecondi = secondi;
        SalvaLibreria();
        RicaricaVoci(null);
        motore.AggiornaVoci(voci, imp.Playlist);
        InviaStato();
    }

    void MandaMostra(int dissolvenzaMs)
    {
        if (!playerPronto || motore.Corrente is not { } c) return;
        bool fineVideo = imp.FineVideo && imp.RotazioneAttiva;
        player.Invia(Messaggi.Mostra(c, disposizione, dissolvenzaMs, fineVideo));
    }

    // ---------- Finestra principale ----------

    public void DaApp(string json)
    {
        var c = Messaggi.LeggiComandoApp(json);
        if (c is null)
        {
            log.Avviso("Comando della finestra non riconosciuto");
            return;
        }
        switch (c.Tipo)
        {
            case "pronta": InviaStato(); break;
            case "mostra": if (c.Id is not null) MostraVoce(c.Id); break;
            case "successivo": Avanti(); break;
            case "precedente": if (motore.TornaIndietro()) DopoCambio(); break;
            case "pausa": ImpostaPausaManuale(true); break;
            case "riprendi": ImpostaPausaManuale(false); break;
            case "impostazioni": if (c.Modifiche is not null) CambiaImpostazioni(c.Modifiche); break;
            case "playlist": CambiaPlaylist(c.Ids ?? []); break;
            case "elimina": if (c.Id is not null) Elimina(c.Id); break;
            case "apriLog": sistema.ApriCartellaLog(); break;
            case "ripristina": Ripristina(); break;
            default: log.Avviso($"Comando della finestra non riconosciuto: {c.Tipo}"); break;
        }
    }

    void ImpostaPausaManuale(bool attiva)
    {
        if (attiva == motore.InPausaManuale) return;
        if (attiva) motore.PausaManuale(); else motore.RiprendiManuale();
        log.Info(attiva ? "Pausa manuale" : "Ripresa manuale");
        RivalutaPausa();
        AggiornaIcona();
        InviaStato();
    }

    void CambiaImpostazioni(ModificheImpostazioni m)
    {
        var (nuova, errore) = imp.Applica(m);
        if (errore is not null)
        {
            InviaAvviso(errore);
            InviaStato();
            return;
        }
        var vecchia = imp;
        imp = nuova;
        SalvaImpostazioni();

        bool tempoCambiato = vecchia.Intervallo != nuova.Intervallo || vecchia.FineVideo != nuova.FineVideo;
        if (tempoCambiato || vecchia.Ordine != nuova.Ordine) motore.AggiornaRegole(Regole(), riavvia: tempoCambiato);
        if (vecchia.ModoMonitor != nuova.ModoMonitor) RicalcolaDisposizione(conDissolvenza: true);
        else if (vecchia.FineVideo != nuova.FineVideo || vecchia.RotazioneAttiva != nuova.RotazioneAttiva)
        {
            if (motore.Corrente?.Tipo == TipoVoce.Video) MandaMostra(0);
        }
        if (vecchia.PausaABatteria != nuova.PausaABatteria) RivalutaPausa();
        if (vecchia.AvvioConWindows != nuova.AvvioConWindows)
        {
            try { sistema.ImpostaAvvioConWindows(nuova.AvvioConWindows); }
            catch (Exception ex) { log.Errore($"Avvio con Windows non impostato: {ex.Message}"); }
        }
        InviaStato();
    }

    void CambiaPlaylist(IReadOnlyList<string> ids)
    {
        var valide = ids.Distinct().Where(id => voci.Any(v => v.Id == id)).ToList();
        if (valide.Count == 0)
        {
            InviaAvviso("Lascia almeno uno sfondo nella playlist");
            InviaStato();
            return;
        }
        imp = imp with { Playlist = valide };
        SalvaImpostazioni();
        motore.AggiornaVoci(voci, valide);
        InviaStato();
    }

    void Elimina(string id)
    {
        var voce = libreria.Voci.Find(v => v.Id == id);
        if (voce is null)
        {
            InviaAvviso("Si possono eliminare solo gli sfondi importati");
            return;
        }
        try { archivio.CancellaDaLibreria(voce.File); }
        catch (Exception ex) { log.Errore($"File '{voce.File}' non cancellato: {ex.Message}"); }
        libreria.Voci.Remove(voce);
        SalvaLibreria();
        RicaricaVoci(null);

        var playlist = imp.Playlist.Where(x => x != id).ToList();
        if (playlist.Count == 0) playlist = predefinite.Playlist.ToList();
        imp = imp with { Playlist = playlist };
        SalvaImpostazioni();
        motore.AggiornaVoci(voci, playlist);
        log.Info($"Eliminato dalla libreria: {voce.Titolo}");
        InviaAvviso($"Eliminato dalla libreria: {voce.Titolo}");
        if (motore.Corrente?.Id == id) Avanti();
        else InviaStato();
    }

    void Ripristina()
    {
        imp = predefinite with { UltimaVoce = imp.UltimaVoce };
        SalvaImpostazioni();
        motore.AggiornaVoci(voci, imp.Playlist);
        motore.AggiornaRegole(Regole(), riavvia: true);
        try { sistema.ImpostaAvvioConWindows(imp.AvvioConWindows); }
        catch (Exception ex) { log.Errore($"Avvio con Windows non impostato: {ex.Message}"); }
        RicalcolaDisposizione(conDissolvenza: true);
        RivalutaPausa();
        InviaAvviso("Impostazioni aziendali ripristinate");
        InviaStato();
    }

    // ---------- Importazione ----------

    public void ImportaFile(IReadOnlyList<string> percorsi)
    {
        var aggiunti = new List<string>();
        var scartati = new List<string>();
        foreach (var percorso in percorsi)
        {
            var nome = Path.GetFileName(percorso);
            long dimensione;
            try { dimensione = archivio.Dimensione(percorso); }
            catch (Exception ex)
            {
                log.Avviso($"Importazione: '{percorso}' non leggibile ({ex.Message})");
                scartati.Add($"{nome}: file non leggibile");
                continue;
            }
            var esito = Importazione.Valuta(nome, dimensione);
            if (!esito.Valido)
            {
                scartati.Add($"{nome}: {esito.Motivo}");
                continue;
            }
            try { archivio.CopiaInLibreria(percorso, esito.NomeFile); }
            catch (Exception ex)
            {
                log.Errore($"Importazione: copia di '{percorso}' non riuscita ({ex.Message})");
                scartati.Add($"{nome}: copia non riuscita");
                continue;
            }
            libreria.Voci.Add(new VoceLibreria
            {
                Id = esito.Id,
                Tipo = NomiJson.Di(esito.Tipo),
                Titolo = esito.Titolo,
                File = esito.NomeFile,
                ImportataIl = tempo.GetUtcNow()
            });
            aggiunti.Add(esito.Titolo);
            log.Info($"Importato '{nome}' come {esito.NomeFile}");
        }

        if (aggiunti.Count > 0)
        {
            SalvaLibreria();
            RicaricaVoci(null);
            motore.AggiornaVoci(voci, imp.Playlist);
        }
        var testo = aggiunti.Count switch
        {
            0 => "",
            1 => $"Aggiunto alla libreria: {aggiunti[0]}",
            _ => $"{aggiunti.Count} sfondi aggiunti alla libreria"
        };
        if (scartati.Count > 0)
        {
            var scarto = scartati.Count == 1 ? scartati[0] : $"{scartati.Count} file non importati. {scartati[0]}";
            testo = testo.Length > 0 ? $"{testo}. {scarto}" : scarto;
        }
        if (testo.Length > 0) InviaAvviso(testo);
        InviaStato();
    }

    // ---------- Monitor ----------

    public void AggiornaMonitor(IReadOnlyList<Monitor> nuovi)
    {
        monitor = nuovi;
        RicalcolaDisposizione(conDissolvenza: false);
    }

    void RicalcolaDisposizione(bool conDissolvenza)
    {
        var nuova = LayoutMonitor.Calcola(monitor, imp.ModoMonitor);
        if (Uguale(nuova, disposizione)) return;
        bool modoCambiato = nuova.ModoEffettivo != disposizione.ModoEffettivo;
        disposizione = nuova;
        log.Info(DescriviMonitor());
        player.ImpostaArea(nuova.Finestra);
        if (modoCambiato) MandaMostra(conDissolvenza ? imp.DissolvenzaSecondi * 1000 : 0);
        else if (playerPronto) player.Invia(Messaggi.Layout(nuova));
        InviaStato();
    }

    static bool Uguale(Disposizione a, Disposizione b) =>
        a.Finestra == b.Finestra && a.ModoEffettivo == b.ModoEffettivo && a.Monitor.SequenceEqual(b.Monitor);

    string DescriviMonitor()
    {
        var elenco = string.Join(", ", disposizione.Monitor.Select(m =>
            $"{m.Nome} {m.Area.L}x{m.Area.A} in ({m.Area.X},{m.Area.Y}){(m.Principale ? " principale" : "")}"));
        var f = disposizione.Finestra;
        return $"Monitor: {elenco}; finestra {f.L}x{f.A} da ({f.X},{f.Y}); modo {NomiJson.Di(disposizione.ModoEffettivo)}";
    }

    // ---------- Pausa ----------

    public void AggiornaSegnali(SegnaliSistema nuovi)
    {
        if (nuovi == segnali) return;
        segnali = nuovi;
        RivalutaPausa();
        InviaStato();
    }

    void RivalutaPausa()
    {
        var nuova = RegolePausa.Valuta(segnali, imp.PausaABatteria, motore.InPausaManuale);
        if (nuova == decisione) return;
        var vecchia = decisione;
        decisione = nuova;
        log.Info(nuova.Livello == LivelloPausa.Nessuna ? "Sfondo ripreso" : $"Pausa {nuova.Livello.ToString().ToLowerInvariant()}: {nuova.Motivo}");

        switch (nuova.Livello)
        {
            case LivelloPausa.Profonda:
                player.Invia(Messaggi.Semplice("pausa"));
                player.ImpostaSospensione(true);
                break;
            case LivelloPausa.Leggera:
                if (vecchia.Livello == LivelloPausa.Profonda) player.ImpostaSospensione(false);
                player.Invia(Messaggi.Semplice("pausa"));
                break;
            default:
                if (vecchia.Livello == LivelloPausa.Profonda) player.ImpostaSospensione(false);
                player.Invia(Messaggi.Semplice("riprendi"));
                Tick();
                break;
        }
    }

    // ---------- Supporto ----------

    RegoleRotazione Regole() => new(imp.Intervallo, imp.FineVideo, imp.Ordine);

    void RicaricaVoci(List<string>? avvisi)
    {
        var (nuove, a) = Catalogo.Carica(manifestJson, libreria, nome =>
        {
            try { return archivio.EsisteInLibreria(nome); }
            catch { return false; }
        });
        voci = nuove;
        if (avvisi is not null) avvisi.AddRange(a);
    }

    void SalvaUltima()
    {
        if (motore.Corrente is null) return;
        imp = imp with { UltimaVoce = motore.Corrente.Id };
        SalvaImpostazioni();
    }

    void SalvaImpostazioni()
    {
        try { archivio.ScriviImpostazioni(ArchivioImpostazioni.Scrivi(imp)); }
        catch (Exception ex) { log.Errore($"impostazioni.json non salvato: {ex.Message}"); }
    }

    void SalvaLibreria()
    {
        try { archivio.ScriviLibreria(Catalogo.ScriviLibreria(libreria)); }
        catch (Exception ex) { log.Errore($"libreria.json non salvato: {ex.Message}"); }
    }

    void AggiornaIcona() =>
        sistema.AggiornaIcona(motore.Corrente?.Titolo ?? "Energy Italy Sfondo Animato", motore.InPausaManuale);

    void InviaAvviso(string testo) => app.Invia(Messaggi.Avviso(testo));

    void InviaStato() => app.Invia(Messaggi.Stato(CreaStato()));

    StatoVista CreaStato() => new(
        voci,
        imp,
        motore.Corrente?.Id,
        motore.StoricoVuoto,
        decisione.Livello != LivelloPausa.Nessuna,
        motore.InPausaManuale,
        decisione.Motivo is MotivoPausa.Nessuno or MotivoPausa.Manuale ? null : RegolePausa.Testo(decisione.Motivo),
        Millisecondi(motore.Restante),
        Millisecondi(motore.Totale),
        motore.Totale is not null && !motore.InPausaManuale,
        motore.AttendeFineVideo,
        disposizione);

    static long? Millisecondi(TimeSpan? t) => t is null ? null : (long)t.Value.TotalMilliseconds;
}
```

- [ ] **Step 6: Eseguire tutti i test del Core e verificare che passino**

Run: `"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" test tests/EnergyItaly.SfondoAnimato.Core.Tests`
Expected: PASS, nessun test fallito.

- [ ] **Step 7: Commit**

```bash
git add src/EnergyItaly.SfondoAnimato.Core/Interfacce.cs src/EnergyItaly.SfondoAnimato.Core/Coordinatore.cs tests/EnergyItaly.SfondoAnimato.Core.Tests/Falsi.cs tests/EnergyItaly.SfondoAnimato.Core.Tests/CoordinatoreTests.cs
git commit -m "Aggiunge il coordinatore che tiene lo stato unico del programma"
```

---

### Task 10: Player web e strumenti di prova nel browser

**Files:**
- Create: `strumenti/server_sviluppo.py`, `.claude/launch.json`
- Create: `src/EnergyItaly.SfondoAnimato/web/player/player.html`, `player.css`, `player.js`
- Create: `src/EnergyItaly.SfondoAnimato/web/sviluppo/prova-player.html`

**Interfaces:**
- Consumes: formato dei messaggi del Task 8 (`mostra`, `layout`, `pausa`, `riprendi`) e il contratto delle animazioni (`animazioni/comune/sfondo.js`: parametro `monitor=`, messaggi `pausa`/`riprendi`/`layout` dal genitore).
- Produces: pagina `https://sfondo.energyitaly/player/player.html` che manda `pronto`, `mostrato`, `errore`, `videoFinito`, `durataVideo`. Senza `window.chrome.webview` accetta `{alPlayer: messaggio}` dalla finestra genitore e risponde con `{dalPlayer: messaggio}`.

- [ ] **Step 1: Server di sviluppo**

<!-- file: strumenti/server_sviluppo.py -->
```python
"""Server di sviluppo: serve la cartella web/ del programma e animazioni/ del repository sullo stesso host.

Uso: python strumenti/server_sviluppo.py [porta]   (predefinita 5599)
Pagine utili: /sviluppo/prova-player.html e /sviluppo/prova-app.html
"""
import http.server
import pathlib
import sys
import urllib.parse

RADICE = pathlib.Path(__file__).resolve().parent.parent
WEB = RADICE / 'src' / 'EnergyItaly.SfondoAnimato' / 'web'
ANIMAZIONI = RADICE / 'animazioni'


class Gestore(http.server.SimpleHTTPRequestHandler):
    def translate_path(self, path):
        percorso = urllib.parse.unquote(path.split('?', 1)[0].split('#', 1)[0])
        if percorso.startswith('/animazioni/'):
            base, resto = ANIMAZIONI, percorso[len('/animazioni/'):]
        else:
            base, resto = WEB, percorso.lstrip('/')
        destinazione = (base / resto).resolve()
        if destinazione != base and base not in destinazione.parents:
            return str(base / '__fuori_dalla_cartella__')
        return str(destinazione)

    def end_headers(self):
        self.send_header('Cache-Control', 'no-store')
        super().end_headers()


if __name__ == '__main__':
    porta = int(sys.argv[1]) if len(sys.argv) > 1 else 5599
    print(f'Server di sviluppo su http://127.0.0.1:{porta}/')
    http.server.ThreadingHTTPServer(('127.0.0.1', porta), Gestore).serve_forever()
```

<!-- file: .claude/launch.json -->
```json
{
  "version": "0.0.1",
  "configurations": [
    {
      "name": "sviluppo-web",
      "runtimeExecutable": "python",
      "runtimeArgs": ["strumenti/server_sviluppo.py", "5599"],
      "port": 5599
    }
  ]
}
```

- [ ] **Step 2: Player**

<!-- file: src/EnergyItaly.SfondoAnimato/web/player/player.html -->
```html
<!doctype html>
<html lang="it">
<head>
<meta charset="utf-8">
<title>Energy Italy Sfondo Animato - player</title>
<link rel="stylesheet" href="player.css">
</head>
<body>
<div id="palco"></div>
<script src="player.js"></script>
</body>
</html>
```

<!-- file: src/EnergyItaly.SfondoAnimato/web/player/player.css -->
```css
html, body {
  margin: 0;
  width: 100%;
  height: 100%;
  overflow: hidden;
  background: #0f1e35;
}

#palco {
  position: absolute;
  inset: 0;
}

.strato {
  position: absolute;
  inset: 0;
  opacity: 0;
  transition-property: opacity;
  transition-timing-function: ease;
}

.strato.visibile {
  opacity: 1;
}

.elemento {
  position: absolute;
  display: block;
  border: 0;
  overflow: hidden;
  background: #0f1e35;
}

.elemento img,
.elemento video {
  display: block;
  width: 100%;
  height: 100%;
  object-fit: cover;
}
```

<!-- file: src/EnergyItaly.SfondoAnimato/web/player/player.js -->
```js
/* Player degli sfondi: mostra una voce alla volta, in una o più scene, con dissolvenza (spec 5.1). */
(function () {
  'use strict';

  const ATTESA_MASSIMA_MS = 10000;
  const palco = document.getElementById('palco');
  const ponte = creaPonte();
  let corrente = null; // { dati, strato, elementi }
  let inPausa = false;

  function creaPonte() {
    const webview = window.chrome && window.chrome.webview;
    if (webview) {
      return {
        invia: (m) => webview.postMessage(m),
        ascolta: (cb) => webview.addEventListener('message', (e) => cb(e.data))
      };
    }
    // Pagina di prova nel browser: comandi dalla finestra genitore.
    return {
      invia: (m) => { if (window.parent !== window) window.parent.postMessage({ dalPlayer: m }, '*'); },
      ascolta: (cb) => window.addEventListener('message', (e) => {
        if (e.source === window.parent && e.data && e.data.alPlayer) cb(e.data.alPlayer);
      })
    };
  }

  function conMonitor(url, monitor) {
    const u = new URL(url, location.href);
    u.searchParams.set('monitor', monitor.map((m) => [m.x, m.y, m.w, m.h].join(',')).join(';'));
    return u.toString();
  }

  function posiziona(nodo, area) {
    nodo.style.left = area.x + 'px';
    nodo.style.top = area.y + 'px';
    nodo.style.width = area.w + 'px';
    nodo.style.height = area.h + 'px';
  }

  function creaElemento(dati, scena, primo) {
    let nodo;
    let pronto;
    if (dati.tipoVoce === 'animazione' || dati.tipoVoce === 'html') {
      nodo = document.createElement('iframe');
      nodo.className = 'elemento';
      nodo.title = 'Sfondo';
      nodo.setAttribute('scrolling', 'no');
      nodo.setAttribute('tabindex', '-1');
      if (dati.tipoVoce === 'html') nodo.setAttribute('sandbox', 'allow-scripts');
      pronto = new Promise((ok) => nodo.addEventListener('load', ok, { once: true }));
      nodo.src = dati.modo === 'estendi' ? conMonitor(dati.url, scena.monitor) : dati.url;
    } else if (dati.tipoVoce === 'immagine') {
      nodo = document.createElement('div');
      nodo.className = 'elemento';
      const img = document.createElement('img');
      img.alt = '';
      pronto = new Promise((ok, ko) => {
        img.onload = ok;
        img.onerror = () => ko(new Error('immagine non leggibile'));
      });
      img.src = dati.url;
      nodo.appendChild(img);
    } else {
      nodo = document.createElement('div');
      nodo.className = 'elemento';
      const video = document.createElement('video');
      video.muted = true;
      video.defaultMuted = true;
      video.playsInline = true;
      video.loop = !dati.fineVideo;
      video.preload = 'auto';
      pronto = new Promise((ok, ko) => {
        video.addEventListener('loadeddata', ok, { once: true });
        video.addEventListener('error', () => ko(new Error('video non riproducibile')), { once: true });
      });
      if (primo) {
        video.addEventListener('loadedmetadata', () => {
          if (isFinite(video.duration) && video.duration > 0) {
            ponte.invia({ tipo: 'durataVideo', id: dati.id, secondi: video.duration });
          }
        }, { once: true });
        video.addEventListener('ended', () => ponte.invia({ tipo: 'videoFinito', id: dati.id }));
      }
      video.src = dati.url;
      nodo.appendChild(video);
    }
    posiziona(nodo, scena.area);
    return { nodo, pronto };
  }

  function conLimite(promessa) {
    return Promise.race([
      promessa,
      new Promise((_, ko) => setTimeout(() => ko(new Error('tempo scaduto')), ATTESA_MASSIMA_MS))
    ]);
  }

  function applicaPausa(voce, pausa) {
    voce.elementi.forEach((nodo) => {
      if (nodo.tagName === 'IFRAME') {
        try { nodo.contentWindow.postMessage({ tipo: pausa ? 'pausa' : 'riprendi' }, '*'); } catch (e) { /* pagina non raggiungibile */ }
        return;
      }
      const video = nodo.querySelector('video');
      if (!video) return;
      if (pausa) video.pause();
      else video.play().catch(() => { /* riproduzione automatica rifiutata: riprova al prossimo riprendi */ });
    });
  }

  // Toglie dal palco tutto tranne la voce indicata (libera iframe e video, come RF-12).
  function pulisci(voce) {
    if (corrente !== voce) return;
    Array.from(palco.children).forEach((figlio) => { if (figlio !== voce.strato) figlio.remove(); });
  }

  function mostra(dati) {
    const strato = document.createElement('div');
    strato.className = 'strato';
    const durata = Math.max(0, Number(dati.dissolvenzaMs) || 0);
    strato.style.transitionDuration = durata + 'ms';
    const creati = dati.scene.map((s, i) => creaElemento(dati, s, i === 0));
    creati.forEach((c) => strato.appendChild(c.nodo));
    palco.appendChild(strato);
    const voce = { dati, strato, elementi: creati.map((c) => c.nodo) };
    const precedente = corrente;
    corrente = voce;

    conLimite(Promise.all(creati.map((c) => c.pronto))).then(() => {
      if (corrente !== voce) return;
      applicaPausa(voce, inPausa);
      if (durata === 0) strato.classList.add('visibile');
      else requestAnimationFrame(() => requestAnimationFrame(() => strato.classList.add('visibile')));
      setTimeout(() => pulisci(voce), durata + 100);
      ponte.invia({ tipo: 'mostrato', id: dati.id });
    }).catch((errore) => {
      strato.remove();
      if (corrente !== voce) return;
      corrente = precedente;
      ponte.invia({ tipo: 'errore', id: dati.id, messaggio: errore && errore.message ? errore.message : String(errore) });
    });
  }

  function layout(dati) {
    if (!corrente) return;
    const vecchi = corrente.dati;
    if (dati.modo !== vecchi.modo || dati.scene.length !== corrente.elementi.length) {
      mostra(Object.assign({}, vecchi, { modo: dati.modo, scene: dati.scene, dissolvenzaMs: 0 }));
      return;
    }
    corrente.dati = Object.assign({}, vecchi, { scene: dati.scene });
    corrente.elementi.forEach((nodo, i) => {
      posiziona(nodo, dati.scene[i].area);
      if (nodo.tagName !== 'IFRAME') return;
      try { nodo.contentWindow.postMessage({ tipo: 'layout', monitor: dati.scene[i].monitor }, '*'); } catch (e) { /* ignora */ }
    });
  }

  ponte.ascolta((m) => {
    if (!m || typeof m !== 'object') return;
    switch (m.tipo) {
      case 'mostra':
        mostra(m);
        break;
      case 'pausa':
        inPausa = true;
        if (corrente) applicaPausa(corrente, true);
        break;
      case 'riprendi':
        inPausa = false;
        if (corrente) applicaPausa(corrente, false);
        break;
      case 'layout':
        layout(m);
        break;
      default:
        break;
    }
  });

  ponte.invia({ tipo: 'pronto' });
})();
```

- [ ] **Step 3: Pagina di prova del player**

<!-- file: src/EnergyItaly.SfondoAnimato/web/sviluppo/prova-player.html -->
```html
<!doctype html>
<html lang="it">
<head>
<meta charset="utf-8">
<title>Prova del player</title>
<style>
body { margin: 0; padding: 16px; background: #07111f; color: #e8eef6; font: 14px "Segoe UI", sans-serif; display: flex; flex-direction: column; gap: 12px; }
h1 { margin: 0; font-size: 18px; }
#cornice { position: relative; width: 960px; height: 272px; overflow: hidden; outline: 1px solid #22395a; }
#player { position: absolute; top: 0; left: 0; width: 3840px; height: 1087px; border: 0; transform: scale(.25); transform-origin: 0 0; }
.comandi { display: flex; flex-wrap: wrap; gap: 8px; }
button { min-height: 36px; padding: 0 12px; border-radius: 8px; border: 1px solid #22395a; background: #0c1b31; color: inherit; cursor: pointer; }
#registro { font: 12px Consolas, monospace; white-space: pre-wrap; max-height: 220px; overflow: auto; background: #0c1b31; padding: 8px; border-radius: 8px; }
</style>
</head>
<body>
<h1>Prova del player (scala 1:4 del PC di riferimento, 3840x1087)</h1>
<div id="cornice"><iframe id="player" src="../player/player.html" title="Player"></iframe></div>
<div class="comandi" id="voci"></div>
<div class="comandi">
  <button type="button" data-modo="estendi">Estendi</button>
  <button type="button" data-modo="duplica">Duplica</button>
  <button type="button" id="pausa">Pausa</button>
  <button type="button" id="riprendi">Riprendi</button>
  <button type="button" id="un-monitor">Layout: un monitor</button>
  <button type="button" id="due-monitor">Layout: due monitor</button>
  <button type="button" id="rotto">Voce rotta</button>
</div>
<div id="registro"></div>
<script>
(async function () {
  'use strict';
  const player = document.getElementById('player');
  const registro = document.getElementById('registro');
  const DUE = [{ x: 0, y: 7, w: 1920, h: 1080 }, { x: 1920, y: 0, w: 1920, h: 1080 }];
  const UNO = [{ x: 0, y: 0, w: 1920, h: 1080 }];
  let modo = 'estendi';
  let monitor = DUE;
  let ultima = null;

  function modoEffettivo() { return monitor.length === 1 ? 'estendi' : modo; }
  function scene() {
    if (modoEffettivo() === 'estendi') {
      const w = Math.max(...monitor.map((m) => m.x + m.w));
      const h = Math.max(...monitor.map((m) => m.y + m.h));
      return [{ area: { x: 0, y: 0, w, h }, monitor }];
    }
    return monitor.map((m) => ({ area: m, monitor: [{ x: 0, y: 0, w: m.w, h: m.h }] }));
  }
  function scrivi(t) { registro.textContent = t + '\n' + registro.textContent; }
  function invia(m) { player.contentWindow.postMessage({ alPlayer: m }, '*'); scrivi('-> ' + JSON.stringify(m).slice(0, 180)); }
  function mostra(v) {
    ultima = v;
    invia({ tipo: 'mostra', id: v.id, tipoVoce: v.tipoVoce, url: v.url, modo: modoEffettivo(), scene: scene(), dissolvenzaMs: 2000, fineVideo: true });
  }

  const manifest = await (await fetch('/animazioni/manifest.json')).json();
  const voci = manifest.animazioni.map((a) => ({
    id: a.id, titolo: a.titolo, tipoVoce: 'animazione',
    url: '/animazioni/' + a.file + '?' + new URLSearchParams(Object.entries(a.parametri).map(([k, v]) => [k, String(v)]))
  }));
  voci.push({ id: 'logo', titolo: 'Immagine (logo)', tipoVoce: 'immagine', url: '/animazioni/img/brand/LogoEnergyItalyWhite.svg' });

  const contenitore = document.getElementById('voci');
  voci.forEach((v) => {
    const b = document.createElement('button');
    b.type = 'button';
    b.textContent = v.titolo;
    b.onclick = () => mostra(v);
    contenitore.appendChild(b);
  });
  document.querySelectorAll('[data-modo]').forEach((b) => {
    b.onclick = () => { modo = b.dataset.modo; if (ultima) invia({ tipo: 'layout', modo: modoEffettivo(), scene: scene() }); };
  });
  document.getElementById('pausa').onclick = () => invia({ tipo: 'pausa' });
  document.getElementById('riprendi').onclick = () => invia({ tipo: 'riprendi' });
  document.getElementById('un-monitor').onclick = () => { monitor = UNO; invia({ tipo: 'layout', modo: modoEffettivo(), scene: scene() }); };
  document.getElementById('due-monitor').onclick = () => { monitor = DUE; invia({ tipo: 'layout', modo: modoEffettivo(), scene: scene() }); };
  document.getElementById('rotto').onclick = () => mostra({ id: 'rotto', titolo: 'Rotto', tipoVoce: 'immagine', url: '/non-esiste.png' });

  window.addEventListener('message', (e) => {
    if (e.source !== player.contentWindow || !e.data || !e.data.dalPlayer) return;
    scrivi('<- ' + JSON.stringify(e.data.dalPlayer));
    if (e.data.dalPlayer.tipo === 'pronto') mostra(voci[0]);
  });
})();
</script>
</body>
</html>
```

- [ ] **Step 4: Verificare il player nel browser**

Avviare il server con `preview_start` (configurazione `sviluppo-web`) e aprire `http://127.0.0.1:5599/sviluppo/prova-player.html`.

Controlli attesi:
1. All'apertura il registro mostra `<- {"tipo":"pronto"}` e poi `<- {"tipo":"mostrato","id":"onda-di-luce"}`; la cornice mostra Onda di luce estesa su 3840x1087 con il logo solo in basso a sinistra.
2. Cliccare ognuna delle 5 animazioni: dissolvenza di 2 s, nessun lampo bianco, un solo `.strato` resta nel DOM dopo la dissolvenza (verifica con `document.getElementById('player').contentDocument.querySelectorAll('.strato').length` = 1).
3. "Duplica": due scene affiancate, ciascuna con il proprio logo. "Estendi": una scena sola.
4. "Pausa": l'animazione si ferma; "Riprendi": riparte senza ricaricare.
5. "Layout: un monitor": la scena si ridisegna su 1920x1080 senza ricaricare la pagina.
6. "Voce rotta": registro `<- {"tipo":"errore","id":"rotto",...}` e resta visibile lo sfondo precedente.
7. Nessun errore nella console (`read_console_messages` con `onlyErrors`).

- [ ] **Step 5: Commit**

```bash
git add strumenti/server_sviluppo.py .claude/launch.json src/EnergyItaly.SfondoAnimato/web/player src/EnergyItaly.SfondoAnimato/web/sviluppo/prova-player.html
git commit -m "Aggiunge il player degli sfondi e la pagina di prova nel browser"
```

---

### Task 11: Font e icona del programma

**Files:**
- Create: `src/EnergyItaly.SfondoAnimato/web/app/font/` (woff2, `font.css`, licenze OFL)
- Create: `strumenti/genera_icona.py`, `src/EnergyItaly.SfondoAnimato/risorse/icona.ico`

**Interfaces:**
- Produces: `web/app/font/font.css` con le famiglie `Sora` (400, 600, 700, 800) e `IBM Plex Mono` (400, 500, 600); `risorse/icona.ico` (16-256 px).

- [ ] **Step 1: Scaricare i font da npm (fonte ufficiale @fontsource) in una cartella temporanea isolata**

Run:
```bash
T="$(mktemp -d)"
cd "$T"
npm view @fontsource/sora time.modified version
npm view @fontsource/ibm-plex-mono time.modified version
npm pack @fontsource/sora @fontsource/ibm-plex-mono
mkdir sora plex
tar -xzf fontsource-sora-*.tgz -C sora
tar -xzf fontsource-ibm-plex-mono-*.tgz -C plex
echo "$T"
```
Expected: due archivi `.tgz` estratti. Se la versione più recente di un pacchetto ha meno di 14 giorni (`time.modified`), ripetere `npm pack` con la versione precedente (`npm view @fontsource/sora versions`).

- [ ] **Step 2: Copiare solo i woff2 latin necessari e le licenze**

Run (nella radice del repository, con `T` uguale al passo precedente):
```bash
D=src/EnergyItaly.SfondoAnimato/web/app/font
mkdir -p "$D"
for p in 400 600 700 800; do cp "$T/sora/package/files/sora-latin-$p-normal.woff2" "$D/"; done
for p in 400 500 600; do cp "$T/plex/package/files/ibm-plex-mono-latin-$p-normal.woff2" "$D/"; done
cp "$T/sora/package/LICENSE" "$D/OFL-Sora.txt"
cp "$T/plex/package/LICENSE" "$D/OFL-IBMPlexMono.txt"
ls "$D"
```
Expected: 7 file `.woff2` e 2 licenze.

<!-- file: src/EnergyItaly.SfondoAnimato/web/app/font/font.css -->
```css
/* Font inclusi nel programma (licenza SIL Open Font License, vedi OFL-*.txt): la WebView non usa la rete. */
@font-face { font-family: 'Sora'; font-style: normal; font-weight: 400; font-display: swap; src: url('sora-latin-400-normal.woff2') format('woff2'); }
@font-face { font-family: 'Sora'; font-style: normal; font-weight: 600; font-display: swap; src: url('sora-latin-600-normal.woff2') format('woff2'); }
@font-face { font-family: 'Sora'; font-style: normal; font-weight: 700; font-display: swap; src: url('sora-latin-700-normal.woff2') format('woff2'); }
@font-face { font-family: 'Sora'; font-style: normal; font-weight: 800; font-display: swap; src: url('sora-latin-800-normal.woff2') format('woff2'); }
@font-face { font-family: 'IBM Plex Mono'; font-style: normal; font-weight: 400; font-display: swap; src: url('ibm-plex-mono-latin-400-normal.woff2') format('woff2'); }
@font-face { font-family: 'IBM Plex Mono'; font-style: normal; font-weight: 500; font-display: swap; src: url('ibm-plex-mono-latin-500-normal.woff2') format('woff2'); }
@font-face { font-family: 'IBM Plex Mono'; font-style: normal; font-weight: 600; font-display: swap; src: url('ibm-plex-mono-latin-600-normal.woff2') format('woff2'); }
```

- [ ] **Step 3: Script dell'icona**

<!-- file: strumenti/genera_icona.py -->
```python
"""Crea src/EnergyItaly.SfondoAnimato/risorse/icona.ico dal simbolo del logo Energy Italy.

Il simbolo (cerchio con la E) occupa il riquadro 0..205.47 del viewBox del logo (larghezza 764.29).
Edge in modalità headless lo ritaglia su un quadrato blu del marchio; Pillow crea le dimensioni standard.
Uso: python strumenti/genera_icona.py
"""
import pathlib
import subprocess
import sys
import tempfile

from PIL import Image

RADICE = pathlib.Path(__file__).resolve().parent.parent
LOGO = RADICE / 'animazioni' / 'img' / 'brand' / 'LogoEnergyItalyWhite.svg'
DESTINAZIONE = RADICE / 'src' / 'EnergyItaly.SfondoAnimato' / 'risorse' / 'icona.ico'
EDGE = [
    pathlib.Path(r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'),
    pathlib.Path(r'C:\Program Files\Microsoft\Edge\Application\msedge.exe'),
]
LATO = 256
LATO_SIMBOLO_SVG = 205.47
LARGHEZZA_SVG = 764.29
SIMBOLO_PX = 196


def main():
    edge = next((e for e in EDGE if e.exists()), None)
    if edge is None:
        sys.exit('Microsoft Edge non trovato')
    larghezza_logo = SIMBOLO_PX * LARGHEZZA_SVG / LATO_SIMBOLO_SVG
    margine = (LATO - SIMBOLO_PX) / 2
    with tempfile.TemporaryDirectory() as cartella_temp:
        cartella = pathlib.Path(cartella_temp)
        pagina = cartella / 'icona.html'
        pagina.write_text(f'''<!doctype html><html><head><meta charset="utf-8"><style>
html, body {{ margin: 0; width: {LATO}px; height: {LATO}px; background: transparent; overflow: hidden; }}
.fondo {{ position: absolute; inset: 0; border-radius: 56px; background: #1E5B8E; }}
.ritaglio {{ position: absolute; left: {margine}px; top: {margine}px; width: {SIMBOLO_PX}px; height: {SIMBOLO_PX}px; overflow: hidden; }}
img {{ position: absolute; left: 0; top: 0; width: {larghezza_logo}px; height: {SIMBOLO_PX}px; }}
</style></head><body><div class="fondo"></div><div class="ritaglio"><img src="{LOGO.as_uri()}" alt=""></div></body></html>''', encoding='utf-8')
        png = cartella / 'icona.png'
        subprocess.run([
            str(edge), '--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run',
            f'--user-data-dir={cartella / "profilo"}', '--default-background-color=00000000',
            f'--window-size={LATO},{LATO}', f'--screenshot={png}', pagina.as_uri()
        ], check=True, timeout=90)
        immagine = Image.open(png).convert('RGBA')
        DESTINAZIONE.parent.mkdir(parents=True, exist_ok=True)
        immagine.save(DESTINAZIONE, format='ICO', sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    print('Creata', DESTINAZIONE)


if __name__ == '__main__':
    main()
```

- [ ] **Step 4: Generare e controllare l'icona**

Run: `python -I strumenti/genera_icona.py`
Expected: `Creata ...\risorse\icona.ico`. Aprire l'icona con lo strumento Read (immagine): quadrato blu arrotondato con il simbolo bianco centrato, bordi trasparenti. Se il simbolo risulta tagliato o decentrato, correggere `SIMBOLO_PX` o il ritaglio e rigenerare.

- [ ] **Step 5: Commit**

```bash
git add src/EnergyItaly.SfondoAnimato/web/app/font strumenti/genera_icona.py src/EnergyItaly.SfondoAnimato/risorse/icona.ico
git commit -m "Aggiunge i font del design e l'icona del programma"
```

---

### Task 12: Finestra galleria (design B "Vetrina")

**Files:**
- Create: `src/EnergyItaly.SfondoAnimato/web/app/index.html`, `app.css`, `app.js`
- Create: `src/EnergyItaly.SfondoAnimato/web/sviluppo/prova-app.html`

**Interfaces:**
- Consumes: messaggi `stato` e `avviso` del Task 8; font del Task 11; logo in `../animazioni/img/brand/LogoEnergyItalyWhite.svg`.
- Produces: comandi dell'app del Task 8. Senza `window.chrome.webview` la pagina parla con la finestra genitore usando `{dallApp: comando}` e `{allApp: messaggio}`; `importaFile` in quel caso porta `nomiFile` al posto degli oggetti File.

Regole di implementazione che i test manuali verificano:
- le miniature delle animazioni sono iframe 1920x1080 ridotti con `transform: scale()`, messi in pausa al caricamento e fatti ripartire al passaggio del mouse;
- le schede di playlist e libreria sono **persistenti per id**: si aggiornano sul posto e si riordinano con `moveBefore` quando disponibile, così gli iframe delle miniature non si ricaricano a ogni messaggio `stato`;
- il palco "In onda" si ricostruisce solo quando cambiano voce, URL, modo o monitor;
- il campo "Cambia ogni" non viene sovrascritto mentre ha il fuoco; il valore valido parte dopo 600 ms (o subito con Invio); un valore non valido mostra l'errore in linea e all'uscita dal campo torna l'ultimo valido.

- [ ] **Step 1: Pagina**

<!-- file: src/EnergyItaly.SfondoAnimato/web/app/index.html -->
```html
<!doctype html>
<html lang="it">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Energy Italy Sfondo Animato</title>
<link rel="stylesheet" href="font/font.css">
<link rel="stylesheet" href="app.css">
</head>
<body class="in-attesa">
<header class="testata">
  <img class="logo" src="../animazioni/img/brand/LogoEnergyItalyWhite.svg" alt="Energy Italy">
  <nav class="schede" aria-label="Sezioni">
    <button type="button" class="scheda" data-scheda="onda">In onda</button>
    <button type="button" class="scheda" data-scheda="libreria">Libreria</button>
    <button type="button" class="scheda" data-scheda="impostazioni">Impostazioni</button>
  </nav>
  <span class="stato-app"><span class="puntino" aria-hidden="true"></span><span id="testo-stato">AVVIO</span></span>
</header>

<main class="contenuto">
  <section id="vista-onda" class="vista" aria-label="In onda">
    <div class="riga-onda">
      <div class="palco">
        <div class="palco-testa">
          <span class="didascalia" id="didascalia-scena"></span>
          <div class="segmentato" id="modo-palco" role="group" aria-label="Con più monitor">
            <button type="button" class="pillola" data-modo="estendi">Estendi</button>
            <button type="button" class="pillola" data-modo="duplica">Duplica</button>
          </div>
        </div>
        <div id="monitor-riga"></div>
      </div>
      <div class="pannello-onda">
        <span class="etichetta-onda"><span class="puntino" aria-hidden="true"></span><span id="etichetta-onda-testo">IN ONDA</span></span>
        <div>
          <h1 id="titolo-corrente"></h1>
          <p class="tenue" id="posizione-corrente"></p>
        </div>
        <div class="conto">
          <div class="conto-riga"><span class="tempo" id="tempo">--:--</span><span class="tenue" id="nota-tempo"></span></div>
          <div class="barra"><div id="barra-avanzamento"></div></div>
        </div>
        <div class="comandi">
          <button type="button" class="tondo" id="btn-precedente" aria-label="Sfondo precedente"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M18 5l-9 7 9 7z"/><path d="M6 5v14"/></svg></button>
          <button type="button" class="tondo primario" id="btn-pausa" aria-label="Pausa"></button>
          <button type="button" class="tondo" id="btn-successivo" aria-label="Sfondo successivo"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M6 5l9 7-9 7z"/><path d="M18 5v14"/></svg></button>
        </div>
        <div class="poi" id="poi"></div>
      </div>
    </div>

    <section class="playlist" aria-labelledby="titolo-playlist">
      <div class="playlist-testa">
        <div>
          <h2 id="titolo-playlist">Playlist</h2>
          <p class="tenue" id="nota-playlist"></p>
        </div>
        <div class="controlli-playlist">
          <div class="riga-controlli">
            <div class="gruppo-intervallo" id="gruppo-intervallo" role="group" aria-label="Cambio sfondo">
              <label for="intervallo-valore" class="mini">CAMBIA OGNI</label>
              <input id="intervallo-valore" type="number" min="1" step="1" inputmode="numeric" aria-describedby="intervallo-errore">
              <label for="intervallo-unita" class="nascosto">Unità di tempo</label>
              <select id="intervallo-unita">
                <option value="minuti">minuti</option>
                <option value="ore">ore</option>
              </select>
              <button type="button" class="pillola" id="btn-mai" aria-pressed="false" title="Cambia sfondo solo a mano">Mai</button>
            </div>
            <div class="segmentato" role="group" aria-label="Ordine">
              <button type="button" class="pillola" data-ordine="sequenziale">In ordine</button>
              <button type="button" class="pillola" data-ordine="casuale"><svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M16 3h5v5"/><path d="M4 20L21 3"/><path d="M21 16v5h-5"/><path d="M15 15l6 6"/><path d="M4 4l5 5"/></svg>Casuale</button>
            </div>
          </div>
          <span id="intervallo-errore" class="errore" role="alert" hidden></span>
          <div class="riga-interruttore">
            <button type="button" role="switch" class="interruttore" id="sw-fine-video-playlist" aria-checked="true" aria-labelledby="etichetta-fine-video"><span></span></button>
            <span id="etichetta-fine-video">I video cambiano a fine riproduzione</span>
          </div>
        </div>
      </div>
      <div class="striscia" id="striscia"></div>
    </section>
  </section>

  <section id="vista-libreria" class="vista" aria-labelledby="titolo-libreria" hidden>
    <div class="libreria-testa">
      <div>
        <h2 id="titolo-libreria">Libreria</h2>
        <p class="tenue" id="nota-libreria"></p>
      </div>
      <div class="filtri">
        <button type="button" class="filtro" data-filtro="tutti">Tutti <span class="conteggio"></span></button>
        <button type="button" class="filtro" data-filtro="energy">Energy Italy <span class="conteggio"></span></button>
        <button type="button" class="filtro" data-filtro="miei">I miei <span class="conteggio"></span></button>
        <button type="button" class="bottone-verde" id="btn-importa"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 15V3"/><path d="M7 8l5-5 5 5"/><path d="M4 15v4a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-4"/></svg>Importa file</button>
      </div>
    </div>
    <div class="griglia" id="griglia"></div>
  </section>

  <section id="vista-impostazioni" class="vista impostazioni" aria-labelledby="titolo-impostazioni" hidden>
    <h2 id="titolo-impostazioni">Impostazioni</h2>

    <div class="gruppo-imp">
      <h3>MONITOR</h3>
      <div class="righe">
        <div class="riga-imp">
          <div class="mini-monitor" id="mini-monitor"></div>
          <div class="testi"><strong id="titolo-monitor"></strong><span class="tenue">Il programma li trova da solo all'avvio. Se colleghi o scolleghi un monitor, o cambi risoluzione, lo sfondo si adatta senza riavviare.</span></div>
        </div>
        <div class="riga-imp">
          <div class="testi"><strong>Con più monitor</strong><span class="tenue" id="descrizione-modo"></span></div>
          <div class="segmentato" role="group" aria-label="Con più monitor">
            <button type="button" class="pillola" data-modo="estendi">Estendi</button>
            <button type="button" class="pillola" data-modo="duplica">Duplica</button>
          </div>
        </div>
      </div>
    </div>

    <div class="gruppo-imp">
      <h3>RIPRODUZIONE</h3>
      <div class="righe">
        <div class="riga-imp">
          <div class="testi"><strong>Dissolvenza tra uno sfondo e l'altro</strong><span class="tenue">Provala con "Successivo" nella scheda In onda</span></div>
          <div class="segmentato" role="group" aria-label="Durata della dissolvenza">
            <button type="button" class="pillola" data-dissolvenza="1">1 s</button>
            <button type="button" class="pillola" data-dissolvenza="2">2 s</button>
            <button type="button" class="pillola" data-dissolvenza="4">4 s</button>
          </div>
        </div>
        <div class="riga-imp">
          <div class="testi"><strong>I video cambiano a fine riproduzione</strong><span class="tenue">Un video resta finché non finisce, anche se dura più dell'intervallo scelto</span></div>
          <button type="button" role="switch" class="interruttore" id="sw-fine-video" aria-checked="true" aria-label="I video cambiano a fine riproduzione"><span></span></button>
        </div>
      </div>
    </div>

    <div class="gruppo-imp">
      <h3>PAUSA AUTOMATICA</h3>
      <div class="righe">
        <div class="riga-imp">
          <div class="testi"><strong>Quando il PC va a batteria</strong><span class="tenue">L'animazione si ferma per risparmiare energia</span></div>
          <button type="button" role="switch" class="interruttore" id="sw-batteria" aria-checked="true" aria-label="Pausa quando il PC va a batteria"><span></span></button>
        </div>
        <div class="riga-imp">
          <div class="testi"><strong>Giochi, video o presentazioni a schermo intero</strong><span class="tenue">Anche con sessione bloccata o monitor spenti</span></div>
          <span class="etichetta-fissa">SEMPRE</span>
        </div>
        <div class="riga-imp">
          <div class="testi"><strong>Finestre ingrandite</strong><span class="tenue">La rotazione continua: lo sfondo si ferma solo se nessun monitor mostra il desktop</span></div>
          <span class="etichetta-fissa neutra">NON FERMANO</span>
        </div>
      </div>
    </div>

    <div class="gruppo-imp">
      <h3>SISTEMA</h3>
      <div class="righe">
        <div class="riga-imp">
          <div class="testi"><strong>Avvia con Windows</strong><span class="tenue">Lo sfondo parte da solo dopo l'accesso</span></div>
          <button type="button" role="switch" class="interruttore" id="sw-avvio" aria-checked="true" aria-label="Avvia con Windows"><span></span></button>
        </div>
        <div class="riga-imp">
          <div class="testi"><strong>Log diagnostici</strong><span class="tenue">Utili all'assistenza IT in caso di problemi</span></div>
          <button type="button" class="bottone-contorno" id="btn-log">Apri cartella</button>
        </div>
        <div class="riga-imp">
          <div class="testi"><strong>Ripristina impostazioni aziendali</strong><span class="tenue">Intervallo, ordine, monitor e opzioni tornano alle scelte dell'IT. La libreria resta com'è.</span></div>
          <button type="button" class="bottone-contorno" id="btn-ripristina">Ripristina</button>
        </div>
      </div>
    </div>
  </section>
</main>

<div class="avviso" id="avviso" role="status" hidden></div>

<div class="velo" id="conferma" hidden>
  <div class="dialogo" role="dialog" aria-modal="true" aria-labelledby="conferma-titolo" aria-describedby="conferma-testo">
    <h2 id="conferma-titolo"></h2>
    <p class="tenue" id="conferma-testo">Il file viene cancellato dalla libreria del programma. L'originale sul tuo PC non viene toccato.</p>
    <div class="dialogo-comandi">
      <button type="button" class="bottone" id="conferma-no">Annulla</button>
      <button type="button" class="bottone pericolo" id="conferma-si">Elimina</button>
    </div>
  </div>
</div>

<script src="app.js"></script>
</body>
</html>
```

- [ ] **Step 2: Stili**

<!-- file: src/EnergyItaly.SfondoAnimato/web/app/app.css -->
```css
/* Finestra galleria: design B "Vetrina" (docs/design/riferimento-vetrina.dc.html). */
:root {
  --fondo: #07111f;
  --superficie: #0c1b31;
  --superficie-alta: #0f2440;
  --palco: #0a1a30;
  --bordo: #16304f;
  --bordo-forte: #22395a;
  --testo: #e8eef6;
  --tenue: #a9bbcf;
  --tenue-chiaro: #c6d4e3;
  --spento: #5d7593;
  --verde: #7AB52A;
  --verde-chiaro: #b2e068;
  --verde-pallido: #d3f0a5;
  --blu: #1E5B8E;
  --arancio: #f0b44c;
  --errore: #ffb4a8;
  --pericolo: #ff9b8f;
  --scena: #0f1e35;
  --mono: 'IBM Plex Mono', Consolas, monospace;
  --sans: 'Sora', 'Segoe UI', sans-serif;
  color-scheme: dark;
}

* { box-sizing: border-box; }
html, body { margin: 0; min-height: 100%; background: var(--fondo); color: var(--testo); font-family: var(--sans); font-size: 14px; }
body.in-attesa .contenuto, body.in-attesa .schede { visibility: hidden; }
button, input, select { font: inherit; color: inherit; }
button:focus-visible, input:focus-visible, select:focus-visible, [tabindex]:focus-visible { outline: 2px solid var(--verde-chiaro); outline-offset: 2px; }
[hidden] { display: none !important; }
.nascosto { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
.tenue { margin: 0; color: var(--tenue); }
.mini { font-family: var(--mono); font-size: 11px; letter-spacing: .04em; color: var(--tenue); }
h1 { margin: 0 0 6px; font-size: 44px; line-height: 1.05; font-weight: 800; letter-spacing: -.02em; overflow-wrap: anywhere; }
h2 { margin: 0; font-size: 24px; font-weight: 800; letter-spacing: -.01em; }

/* Testata */
.testata { display: flex; flex-wrap: wrap; align-items: center; gap: 16px 32px; padding: 0 28px; min-height: 64px; border-bottom: 1px solid #142640; }
.logo { width: 128px; height: auto; }
.schede { display: flex; flex-wrap: wrap; gap: 4px; }
.scheda { min-height: 40px; padding: 0 16px; border: 0; border-radius: 20px; background: transparent; color: var(--tenue); font-weight: 600; cursor: pointer; }
.scheda:hover { color: var(--testo); }
.scheda[aria-current="page"] { background: var(--testo); color: var(--fondo); font-weight: 700; }
.stato-app { margin-left: auto; display: flex; align-items: center; gap: 8px; font-family: var(--mono); font-size: 12px; color: var(--tenue); }
.puntino { flex: none; width: 8px; height: 8px; border-radius: 4px; background: var(--verde); animation: battito 2s ease-in-out infinite; }
.in-pausa .puntino { background: var(--arancio); animation: none; }
@keyframes battito { 0%, 100% { opacity: 1; } 50% { opacity: .35; } }

.contenuto { display: flex; flex-direction: column; gap: 40px; max-width: 1440px; margin: 0 auto; padding: 32px 28px 48px; }
.vista { display: flex; flex-direction: column; gap: 40px; }

/* In onda */
.riga-onda { display: flex; flex-wrap: wrap; gap: 32px; align-items: stretch; }
.palco { flex: 999 1 620px; min-width: 0; display: flex; flex-direction: column; gap: 18px; padding: 24px 28px 28px; border-radius: 20px; border: 1px solid var(--bordo); background: radial-gradient(ellipse at 50% 0%, #11284a 0%, var(--palco) 70%); }
.palco-testa { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 12px; min-height: 44px; }
.didascalia { font-family: var(--mono); font-size: 12px; letter-spacing: .08em; color: var(--tenue); white-space: pre-wrap; }
.palco-schermi { display: flex; flex-direction: column; gap: 10px; }
.schermi { position: relative; width: 100%; background: var(--palco); }
.scena { position: absolute; overflow: hidden; background: var(--scena); }
.scena iframe { position: absolute; top: 0; left: 0; border: 0; transform-origin: 0 0; pointer-events: none; }
.scena img, .scena video { display: block; width: 100%; height: 100%; object-fit: cover; }
.maschera { position: absolute; background: var(--palco); }
.cornice { position: absolute; border: 5px solid #02070f; border-radius: 6px; box-shadow: 0 0 0 1px #1a2f4d; pointer-events: none; }
.cornice.principale { box-shadow: 0 0 0 1px #2f5a2a; }
.etichette-monitor { position: relative; height: 18px; }
.etichette-monitor span { position: absolute; top: 0; overflow: hidden; text-align: center; text-overflow: ellipsis; white-space: pre; font-family: var(--mono); font-size: 12px; color: var(--tenue); }

.pannello-onda { flex: 1 1 300px; display: flex; flex-direction: column; gap: 22px; padding: 8px 4px; min-width: 0; }
.etichetta-onda { display: flex; align-items: center; gap: 8px; font-family: var(--mono); font-size: 12px; font-weight: 600; letter-spacing: .1em; color: var(--verde-chiaro); }
.in-pausa .etichetta-onda { color: var(--arancio); }
.conto { display: flex; flex-direction: column; gap: 10px; }
.conto-riga { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 10px; }
.tempo { font-family: var(--mono); font-size: 40px; font-weight: 500; letter-spacing: -.02em; }
.barra { height: 4px; overflow: hidden; border-radius: 2px; background: #1a2f4d; }
.barra > div { width: 0; height: 100%; background: var(--verde); transition: width .9s linear; }
.barra.sottile { height: 3px; }
.comandi { display: flex; align-items: center; gap: 12px; }
.tondo { display: flex; align-items: center; justify-content: center; width: 52px; height: 52px; border: 1px solid var(--bordo-forte); border-radius: 26px; background: transparent; color: var(--testo); cursor: pointer; }
.tondo:hover { background: #10223c; }
.tondo.primario { width: 68px; height: 68px; border: 0; border-radius: 34px; background: var(--verde); color: var(--fondo); }
.tondo.primario:hover { background: #8cc63a; }
.poi { display: flex; align-items: center; gap: 12px; min-height: 74px; padding: 10px; border: 1px solid var(--bordo); border-radius: 12px; background: var(--superficie); }
.poi > .miniatura, .segnaposto-poi { flex: none; width: 96px; border-radius: 6px; }
.segnaposto-poi { display: flex; align-items: center; justify-content: center; aspect-ratio: 16 / 9; background: #13243c; color: var(--verde-chiaro); }
.poi-testi { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
.poi-testi .mini { letter-spacing: .08em; }
.poi-testi strong { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

/* Playlist */
.playlist { display: flex; flex-direction: column; gap: 18px; }
.playlist-testa { display: flex; flex-wrap: wrap; align-items: flex-start; justify-content: space-between; gap: 16px; }
.playlist-testa > div:first-child { display: flex; flex-direction: column; gap: 4px; }
.controlli-playlist { display: flex; flex-direction: column; align-items: flex-end; gap: 10px; }
.riga-controlli { display: flex; flex-wrap: wrap; align-items: center; justify-content: flex-end; gap: 12px; }
.gruppo-intervallo { display: flex; align-items: center; gap: 6px; padding: 4px 4px 4px 12px; border: 1px solid var(--bordo); border-radius: 12px; background: var(--superficie); }
.gruppo-intervallo.errato { border-color: #ff8a7a; }
.gruppo-intervallo input { width: 76px; min-height: 36px; padding: 0 8px; border: 1px solid var(--bordo-forte); border-radius: 8px; background: var(--fondo); font-weight: 700; text-align: right; }
.gruppo-intervallo.errato input { border-color: #ff8a7a; }
.gruppo-intervallo select { min-height: 36px; padding: 0 8px; border: 1px solid var(--bordo-forte); border-radius: 8px; background: var(--fondo); font-weight: 600; cursor: pointer; }
.gruppo-intervallo.spento input, .gruppo-intervallo.spento select { background: transparent; color: var(--spento); }
.segmentato { display: flex; align-items: center; gap: 4px; padding: 4px; border: 1px solid var(--bordo); border-radius: 12px; background: var(--superficie); }
.palco .segmentato, .riga-imp .segmentato { background: var(--fondo); }
.pillola { display: flex; align-items: center; gap: 6px; min-height: 36px; padding: 0 12px; border: 0; border-radius: 8px; background: transparent; color: var(--tenue-chiaro); font-weight: 600; cursor: pointer; }
.pillola[aria-pressed="true"] { background: var(--blu); color: #fff; font-weight: 700; }
.pillola:disabled { opacity: .45; cursor: default; }
.errore { font-size: 13px; color: var(--errore); }
.riga-interruttore { display: flex; align-items: center; gap: 10px; color: var(--tenue-chiaro); }
.interruttore { position: relative; flex: none; width: 46px; height: 26px; padding: 0; border: 0; border-radius: 13px; background: #2a3f5c; cursor: pointer; }
.interruttore span { position: absolute; top: 3px; left: 3px; width: 20px; height: 20px; border-radius: 10px; background: #fff; transition: left .2s; }
.interruttore[aria-checked="true"] { background: var(--verde); }
.interruttore[aria-checked="true"] span { left: 23px; }

.striscia { display: flex; gap: 14px; overflow-x: auto; padding-bottom: 8px; }
.voce-playlist { flex: none; display: flex; flex-direction: column; gap: 10px; width: 200px; padding: 10px; border: 1px solid var(--bordo); border-radius: 14px; background: var(--superficie); cursor: grab; transition: opacity .15s; }
.voce-playlist.attiva { border-color: var(--verde); background: var(--superficie-alta); }
.voce-playlist.trascinata { opacity: .4; }
.voce-testa { display: flex; align-items: center; justify-content: space-between; min-height: 32px; }
.numero { display: flex; align-items: center; gap: 6px; font-family: var(--mono); font-size: 12px; color: var(--tenue); }
.numero svg { color: var(--spento); }
.badge-onda { font-family: var(--mono); font-size: 11px; font-weight: 600; letter-spacing: .08em; color: var(--verde-chiaro); }
.togli { display: flex; align-items: center; justify-content: center; width: 32px; height: 32px; border: 0; border-radius: 16px; background: transparent; color: #8fa3ba; cursor: pointer; }
.togli:hover { background: #13243c; color: var(--testo); }
.anteprima-bottone { position: relative; display: block; width: 100%; padding: 0; overflow: hidden; border: 0; border-radius: 8px; background: var(--scena); cursor: pointer; }
.anteprima-bottone.grande { border: 1px solid var(--bordo); border-radius: 10px; }
.anteprima-bottone.in-onda { border-color: var(--verde); box-shadow: 0 0 0 1px var(--verde); }
.anteprima-bottone:disabled { cursor: default; }
.miniatura { position: relative; width: 100%; aspect-ratio: 16 / 9; overflow: hidden; background: var(--scena); }
.miniatura-iframe { position: absolute; top: 0; left: 0; border: 0; transform-origin: 0 0; pointer-events: none; }
.miniatura img, .miniatura video { display: block; width: 100%; height: 100%; object-fit: cover; }
.segnaposto { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; background: linear-gradient(135deg, #34506f, #1d2f47); color: var(--tenue-chiaro); font-size: 12px; }
.segnaposto.codice { background: linear-gradient(135deg, #21494f, #14303a); color: #c4e2df; }
.voce-testi { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
.voce-titolo { overflow: hidden; font-weight: 700; text-overflow: ellipsis; white-space: nowrap; }
.voce-meta { font-size: 12px; color: var(--tenue); }
.non-disponibile .voce-titolo, .non-disponibile .voce-meta { opacity: .6; }
.aggiungi-playlist { flex: none; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 8px; width: 200px; min-height: 200px; border: 1.5px dashed #2a4568; border-radius: 14px; background: transparent; color: var(--testo); font-weight: 700; cursor: pointer; }
.aggiungi-playlist svg { color: var(--verde-chiaro); }

/* Libreria */
.libreria-testa { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 16px; }
.libreria-testa > div:first-child { display: flex; flex-direction: column; gap: 4px; }
.libreria-testa h2, .impostazioni h2 { font-size: 28px; }
.filtri { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; }
.filtro { min-height: 40px; padding: 0 14px; border: 1px solid var(--bordo-forte); border-radius: 20px; background: transparent; color: var(--tenue); font-weight: 600; cursor: pointer; }
.filtro[aria-pressed="true"] { border-color: var(--testo); color: var(--testo); font-weight: 700; }
.bottone-verde { display: flex; align-items: center; gap: 8px; min-height: 44px; margin-left: 8px; padding: 0 18px; border: 0; border-radius: 22px; background: var(--verde); color: var(--fondo); font-weight: 800; cursor: pointer; }
.bottone-verde:hover { background: #8cc63a; }
.griglia { display: grid; grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); gap: 18px; }
.scheda-libreria { display: flex; flex-direction: column; gap: 10px; }
.scheda-piede { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.azioni-scheda { flex: none; display: flex; gap: 4px; }
.tondo-piccolo { display: flex; align-items: center; justify-content: center; width: 44px; height: 44px; border: 1px solid var(--bordo-forte); border-radius: 22px; background: transparent; color: var(--testo); cursor: pointer; }
.tondo-piccolo.attivo { border: 0; background: rgba(122, 181, 42, .18); color: var(--verde-chiaro); }
.tondo-piccolo.pericolo { border: 0; color: var(--pericolo); }
.badge-sovrapposto { position: absolute; top: 8px; left: 8px; display: flex; align-items: center; gap: 6px; padding: 4px 9px; border-radius: 12px; background: rgba(7, 17, 31, .85); color: var(--verde-pallido); font-family: var(--mono); font-size: 11px; font-weight: 600; letter-spacing: .06em; }
.puntino-verde { width: 6px; height: 6px; border-radius: 3px; background: var(--verde); }
.zona-import { display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 6px; min-height: 150px; padding: 16px; border: 1.5px dashed #2a4568; border-radius: 12px; color: var(--tenue); text-align: center; }
.zona-import.sopra { border-color: var(--verde); background: rgba(122, 181, 42, .06); }
.zona-import strong { color: var(--testo); }
.zona-import svg { color: var(--verde-chiaro); }

/* Impostazioni */
.impostazioni { max-width: 860px; gap: 28px; }
.gruppo-imp { display: flex; flex-direction: column; gap: 10px; }
.gruppo-imp h3 { margin: 0; font-family: var(--mono); font-size: 12px; font-weight: 600; letter-spacing: .1em; color: var(--tenue); }
.righe { display: flex; flex-direction: column; gap: 2px; overflow: hidden; border-radius: 14px; }
.riga-imp { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 12px 16px; min-height: 68px; padding: 10px 20px; background: var(--superficie); }
.riga-imp .testi { flex: 1 1 300px; display: flex; flex-direction: column; gap: 2px; }
.riga-imp .tenue { font-size: 13px; }
.etichetta-fissa { font-family: var(--mono); font-size: 12px; color: var(--verde-chiaro); }
.etichetta-fissa.neutra { color: var(--tenue); }
.mini-monitor { flex: none; display: flex; align-items: flex-start; gap: 6px; }
.mini-monitor div { display: flex; align-items: center; justify-content: center; width: 72px; height: 40px; border: 1.5px solid var(--spento); border-radius: 4px; font-family: var(--mono); font-size: 12px; color: var(--tenue); }
.mini-monitor div.principale { border-color: var(--verde); color: var(--verde-pallido); }
.bottone-contorno { min-height: 40px; padding: 0 16px; border: 1px solid var(--bordo-forte); border-radius: 20px; background: transparent; color: var(--testo); font-weight: 600; cursor: pointer; }
.bottone-contorno:hover { background: #10223c; }

/* Avvisi e dialogo */
.avviso { position: fixed; left: 50%; bottom: 28px; z-index: 20; max-width: min(720px, calc(100vw - 32px)); padding: 12px 20px; border-radius: 24px; background: var(--testo); color: var(--fondo); font-weight: 700; box-shadow: 0 10px 30px rgba(0, 0, 0, .4); transform: translateX(-50%); animation: comparsa .25s ease-out; }
@keyframes comparsa { from { opacity: 0; transform: translate(-50%, 12px); } to { opacity: 1; transform: translate(-50%, 0); } }
.velo { position: fixed; inset: 0; z-index: 30; display: flex; align-items: center; justify-content: center; padding: 16px; background: rgba(2, 7, 15, .7); }
.dialogo { display: flex; flex-direction: column; gap: 12px; width: min(460px, 100%); padding: 24px; border: 1px solid var(--bordo-forte); border-radius: 16px; background: var(--superficie); }
.dialogo h2 { font-size: 20px; overflow-wrap: anywhere; }
.dialogo-comandi { display: flex; justify-content: flex-end; gap: 8px; margin-top: 8px; }
.bottone { min-height: 44px; padding: 0 18px; border: 1px solid var(--bordo-forte); border-radius: 22px; background: transparent; color: var(--testo); font-weight: 700; cursor: pointer; }
.bottone.pericolo { border: 0; background: #c0392b; color: #fff; }

@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after { animation: none !important; transition: none !important; }
}
```

- [ ] **Step 3: Logica della finestra**

<!-- file: src/EnergyItaly.SfondoAnimato/web/app/app.js -->
```js
/* Finestra principale di Energy Italy Sfondo Animato.
   Disegna lo stato ricevuto dal programma e gli manda i comandi (spec sezioni 5.2 e 7). */
(function () {
  'use strict';

  const ICONE = {
    piu: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round"><path d="M12 5v14M5 12h14"/></svg>',
    spunta: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12l5 5 9-10"/></svg>',
    chiudi: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><path d="M6 6l12 12M18 6L6 18"/></svg>',
    cestino: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M4 7h16"/><path d="M9 7V4h6v3"/><path d="M6 7l1 13h10l1-13"/></svg>',
    maniglia: '<svg viewBox="0 0 24 24" fill="currentColor"><circle cx="9" cy="6" r="1.6"/><circle cx="15" cy="6" r="1.6"/><circle cx="9" cy="12" r="1.6"/><circle cx="15" cy="12" r="1.6"/><circle cx="9" cy="18" r="1.6"/><circle cx="15" cy="18" r="1.6"/></svg>',
    casuale: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M16 3h5v5"/><path d="M4 20L21 3"/><path d="M21 16v5h-5"/><path d="M15 15l6 6"/><path d="M4 4l5 5"/></svg>',
    codice: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d="M8 8l-4 4 4 4"/><path d="M16 8l4 4-4 4"/><path d="M13.5 5l-3 14"/></svg>',
    pausa: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round"><path d="M9 5v14M15 5v14"/></svg>',
    play: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M8 5l12 7-12 7z"/></svg>',
    successivo: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M6 5l9 7-9 7z"/><path d="M18 5v14"/></svg>'
  };

  // ---------- Ponte con il programma ----------
  const webview = window.chrome && window.chrome.webview;
  const ponte = {
    invia(m) {
      if (webview) webview.postMessage(m);
      else if (window.parent !== window) window.parent.postMessage({ dallApp: m }, '*');
    },
    inviaFile(m, file) {
      if (webview && webview.postMessageWithAdditionalObjects) webview.postMessageWithAdditionalObjects(m, file);
      else if (window.parent !== window) window.parent.postMessage({ dallApp: Object.assign({}, m, { nomiFile: Array.from(file, (f) => f.name) }) }, '*');
    },
    ascolta(cb) {
      if (webview) webview.addEventListener('message', (e) => cb(e.data));
      else window.addEventListener('message', (e) => { if (e.source === window.parent && e.data && e.data.allApp) cb(e.data.allApp); });
    }
  };

  // ---------- Stato locale ----------
  let stato = null;
  let ricevutoAlle = 0;
  let scheda = 'onda';
  let filtro = 'tutti';
  let trascinato = null;
  let attesaValore = null;
  let attesaAvviso = null;
  let chiavePalco = '';
  let daEliminare = null;
  const cacheMiniature = new Map();
  const schedePlaylist = new Map();
  const schedeLibreria = new Map();

  const $ = (selettore) => document.querySelector(selettore);

  // ---------- Utilità ----------
  function el(tag, attributi, ...figli) {
    const nodo = document.createElement(tag);
    if (attributi) {
      for (const [chiave, valore] of Object.entries(attributi)) {
        if (valore === null || valore === undefined || valore === false) continue;
        if (chiave === 'class') nodo.className = valore;
        else if (chiave === 'style') nodo.style.cssText = valore;
        else if (chiave.startsWith('on')) nodo.addEventListener(chiave.slice(2), valore);
        else nodo.setAttribute(chiave, valore === true ? '' : String(valore));
      }
    }
    for (const figlio of figli.flat()) {
      if (figlio === null || figlio === undefined || figlio === false) continue;
      nodo.append(figlio instanceof Node ? figlio : document.createTextNode(String(figlio)));
    }
    return nodo;
  }

  function icona(nome, lato) {
    const t = document.createElement('template');
    const l = lato || 16;
    t.innerHTML = ICONE[nome].replace('<svg ', `<svg width="${l}" height="${l}" aria-hidden="true" focusable="false" `);
    return t.content.firstElementChild;
  }

  function orologio(ms) {
    const s = Math.max(0, Math.round(ms / 1000));
    const h = Math.floor(s / 3600);
    const m = Math.floor((s % 3600) / 60);
    const x = s % 60;
    const due = (n) => String(n).padStart(2, '0');
    return h > 0 ? `${h}:${due(m)}:${due(x)}` : `${due(m)}:${due(x)}`;
  }

  function voce(id) {
    return (stato && stato.voci.find((v) => v.id === id)) || null;
  }

  function descrizione(v) {
    switch (v.tipoVoce) {
      case 'animazione': return 'Animazione Energy Italy';
      case 'immagine': return 'Immagine importata';
      case 'video': return 'Video importato' + (v.durataSecondi ? ', ' + orologio(v.durataSecondi * 1000) : '');
      default: return 'Pagina web importata';
    }
  }

  function metaBreve(v) {
    switch (v.tipoVoce) {
      case 'animazione': return 'Energy Italy';
      case 'immagine': return 'Immagine';
      case 'video': return 'Video' + (v.durataSecondi ? ', ' + orologio(v.durataSecondi * 1000) : '');
      default: return 'Pagina web';
    }
  }

  function conMonitor(url, monitor) {
    const u = new URL(url, location.href);
    u.searchParams.set('monitor', monitor.map((m) => [m.x, m.y, m.w, m.h].join(',')).join(';'));
    return u.toString();
  }

  function invia(iframe, tipo) {
    try { iframe.contentWindow.postMessage({ tipo }, '*'); } catch (e) { /* non ancora caricato */ }
  }

  function percento(r, W, H) {
    return `left: ${(r.x / W) * 100}%; top: ${(r.y / H) * 100}%; width: ${(r.w / W) * 100}%; height: ${(r.h / H) * 100}%`;
  }

  // Un iframe disegnato a grandezza naturale e ridotto per stare nel contenitore.
  function inScala(contenitore, iframe, larghezza, altezza) {
    iframe.style.width = larghezza + 'px';
    iframe.style.height = altezza + 'px';
    const aggiorna = () => { iframe.style.transform = `scale(${contenitore.clientWidth / larghezza})`; };
    new ResizeObserver(aggiorna).observe(contenitore);
    aggiorna();
  }

  function modifica(m) {
    ponte.invia({ tipo: 'impostazioni', modifiche: m });
  }

  // Riordina i figli senza ricaricare gli iframe (moveBefore) quando il browser lo permette.
  function riconcilia(contenitore, nodi) {
    nodi.forEach((nodo, i) => {
      const attuale = contenitore.children[i] || null;
      if (attuale === nodo) return;
      if (nodo.parentNode === contenitore && typeof contenitore.moveBefore === 'function') contenitore.moveBefore(nodo, attuale);
      else contenitore.insertBefore(nodo, attuale);
    });
    while (contenitore.children.length > nodi.length) contenitore.lastElementChild.remove();
  }

  // ---------- Miniature ----------
  function miniatura(contesto, v) {
    const chiave = contesto + '|' + v.id;
    const firma = v.url + '|' + v.disponibile;
    const memo = cacheMiniature.get(chiave);
    if (memo && memo.firma === firma) return memo.nodo;

    const nodo = el('div', { class: 'miniatura' });
    if (!v.disponibile) {
      nodo.append(el('div', { class: 'segnaposto' }, 'File non trovato'));
    } else if (v.tipoVoce === 'animazione') {
      const f = el('iframe', { class: 'miniatura-iframe', title: '', tabindex: '-1', 'aria-hidden': 'true' });
      f.addEventListener('load', () => invia(f, 'pausa'));
      nodo.addEventListener('mouseenter', () => invia(f, 'riprendi'));
      nodo.addEventListener('mouseleave', () => invia(f, 'pausa'));
      f.src = v.url;
      nodo.append(f);
      inScala(nodo, f, 1920, 1080);
    } else if (v.tipoVoce === 'immagine') {
      nodo.append(el('img', { src: v.url, alt: '', loading: 'lazy' }));
    } else if (v.tipoVoce === 'video') {
      const video = el('video', { preload: 'metadata', playsinline: true, muted: true });
      video.muted = true;
      video.src = v.url + '#t=1';
      nodo.append(video);
    } else {
      nodo.append(el('div', { class: 'segnaposto codice' }, icona('codice', 36)));
    }
    cacheMiniature.set(chiave, { nodo, firma, id: v.id });
    return nodo;
  }

  function potaCache() {
    const ids = new Set(stato.voci.map((v) => v.id));
    for (const [chiave, memo] of cacheMiniature) if (!ids.has(memo.id)) cacheMiniature.delete(chiave);
    for (const id of schedeLibreria.keys()) if (!ids.has(id)) schedeLibreria.delete(id);
    for (const id of schedePlaylist.keys()) if (!stato.playlist.includes(id)) schedePlaylist.delete(id);
  }

  // ---------- Disegno ----------
  function render() {
    if (!stato) return;
    document.body.classList.remove('in-attesa');
    document.body.classList.toggle('in-pausa', stato.inPausa);
    renderTestata();
    renderPalco();
    renderPannello();
    renderControlli();
    renderStriscia();
    renderLibreria();
    renderImpostazioni();
    potaCache();
    aggiornaConto();
  }

  function testoStato() {
    if (stato.inPausa) return stato.motivoPausa ? 'IN PAUSA: ' + stato.motivoPausa : 'IN PAUSA';
    const n = stato.monitor.length;
    return n === 1 ? 'ATTIVO SU 1 MONITOR' : `ATTIVO SU ${n} MONITOR`;
  }

  function renderTestata() {
    $('#testo-stato').textContent = testoStato();
    document.querySelectorAll('.scheda').forEach((b) => {
      if (b.dataset.scheda === scheda) b.setAttribute('aria-current', 'page');
      else b.removeAttribute('aria-current');
    });
    ['onda', 'libreria', 'impostazioni'].forEach((nome) => { $('#vista-' + nome).hidden = nome !== scheda; });
  }

  function apriScheda(nome) {
    scheda = nome;
    render();
  }

  function didascalia() {
    const n = stato.monitor.length;
    const f = stato.finestra;
    if (n === 1) return `IL TUO DESKTOP ADESSO  /  1 MONITOR  /  ${f.w} x ${f.h} px`;
    if (stato.modo === 'estendi') return `IL TUO DESKTOP ADESSO  /  ${n} MONITOR ESTESI  /  ${f.w} x ${f.h} px`;
    return `IL TUO DESKTOP ADESSO  /  STESSA SCENA SU ${n} MONITOR`;
  }

  function aggiornaModo() {
    const uno = stato.monitor.length < 2;
    document.querySelectorAll('[data-modo]').forEach((b) => {
      b.setAttribute('aria-pressed', String(b.dataset.modo === stato.impostazioni.modoMonitor));
      b.disabled = uno;
    });
  }

  function renderPalco() {
    $('#didascalia-scena').textContent = didascalia();
    $('#modo-palco').hidden = stato.monitor.length < 2;
    aggiornaModo();
    const v = voce(stato.corrente);
    const chiave = JSON.stringify([stato.corrente, v && v.url, v && v.disponibile, stato.modo, stato.finestra, stato.monitor, stato.scene]);
    const contenitore = $('#monitor-riga');
    if (chiave !== chiavePalco) {
      chiavePalco = chiave;
      contenitore.replaceChildren(costruisciPalco(v));
    }
    contenitore.querySelectorAll('iframe').forEach((f) => invia(f, stato.inPausa ? 'pausa' : 'riprendi'));
    contenitore.querySelectorAll('video').forEach((x) => {
      if (stato.inPausa) x.pause(); else x.play().catch(() => { /* ignora */ });
    });
  }

  function costruisciPalco(v) {
    const W = stato.finestra.w;
    const H = stato.finestra.h;
    const schermi = el('div', { class: 'schermi', style: `aspect-ratio: ${W} / ${H}` });
    if (v && v.disponibile) {
      stato.scene.forEach((s) => {
        const box = el('div', { class: 'scena', style: percento(s.area, W, H) });
        box.append(contenutoScena(v, s, box));
        schermi.append(box);
      });
    }
    mascherePalco(H).forEach((r) => schermi.append(el('div', { class: 'maschera', style: percento(r, W, H) })));
    stato.monitor.forEach((m) => schermi.append(el('div', { class: 'cornice' + (m.principale ? ' principale' : ''), style: percento(m, W, H) })));
    const etichette = el('div', { class: 'etichette-monitor' });
    stato.monitor.forEach((m) => etichette.append(el('span', { style: `left: ${(m.x / W) * 100}%; width: ${(m.w / W) * 100}%` },
      `${m.nome}  ${m.w}x${m.h}${m.principale ? '  PRINCIPALE' : ''}`)));
    return el('div', { class: 'palco-schermi' }, schermi, etichette);
  }

  function contenutoScena(v, s, box) {
    if (v.tipoVoce === 'immagine') return el('img', { src: v.url, alt: '' });
    if (v.tipoVoce === 'video') {
      const video = el('video', { autoplay: true, loop: true, playsinline: true, muted: true });
      video.muted = true;
      video.src = v.url;
      return video;
    }
    const f = el('iframe', { title: 'Anteprima dello sfondo in onda', tabindex: '-1', 'aria-hidden': 'true' });
    if (v.tipoVoce === 'html') f.setAttribute('sandbox', 'allow-scripts');
    f.addEventListener('load', () => invia(f, stato && stato.inPausa ? 'pausa' : 'riprendi'));
    f.src = stato.modo === 'estendi' ? conMonitor(v.url, s.monitor) : v.url;
    inScala(box, f, s.area.w, s.area.h);
    return f;
  }

  // Copre le parti dello schermo virtuale che non stanno su nessun monitor (es. lo sfalsamento di 7 px).
  function mascherePalco(H) {
    const m = stato.monitor;
    for (let i = 0; i < m.length; i++) {
      for (let j = i + 1; j < m.length; j++) {
        if (m[i].x < m[j].x + m[j].w && m[j].x < m[i].x + m[i].w) return [];
      }
    }
    const risultato = [];
    m.forEach((r) => {
      if (r.y > 0) risultato.push({ x: r.x, y: 0, w: r.w, h: r.y });
      const basso = r.y + r.h;
      if (basso < H) risultato.push({ x: r.x, y: basso, w: r.w, h: H - basso });
    });
    return risultato;
  }

  function prossimaVoce() {
    const pl = stato.playlist.map(voce).filter((v) => v && v.disponibile);
    if (pl.length === 0) return null;
    const i = pl.findIndex((v) => v.id === stato.corrente);
    return pl[(i + 1) % pl.length];
  }

  function renderPannello() {
    const v = voce(stato.corrente);
    $('#etichetta-onda-testo').textContent = stato.inPausa ? 'IN PAUSA' : 'IN ONDA';
    $('#titolo-corrente').textContent = v ? v.titolo : 'Nessuno sfondo';
    const pos = v ? stato.playlist.indexOf(v.id) : -1;
    $('#posizione-corrente').textContent = !v ? '' :
      descrizione(v) + (pos >= 0 ? `, ${pos + 1} di ${stato.playlist.length} nella playlist` : ', fuori dalla playlist');
    const bottone = $('#btn-pausa');
    bottone.replaceChildren(icona(stato.pausaManuale ? 'play' : 'pausa', 24));
    bottone.setAttribute('aria-label', stato.pausaManuale ? 'Riprendi' : 'Pausa');
    renderPoi();
  }

  function renderPoi() {
    const imp = stato.impostazioni;
    const poi = $('#poi');
    let figura;
    let testo;
    if (!imp.rotazioneAttiva) {
      figura = el('div', { class: 'segnaposto-poi' }, icona('successivo', 22));
      testo = 'Solo quando premi Successivo';
    } else if (imp.ordine === 'casuale') {
      figura = el('div', { class: 'segnaposto-poi' }, icona('casuale', 22));
      testo = 'A sorpresa dalla playlist';
    } else {
      const p = prossimaVoce();
      figura = p ? miniatura('poi', p) : el('div', { class: 'segnaposto-poi' });
      testo = p ? p.titolo : 'Nessuno';
    }
    const testi = el('div', { class: 'poi-testi' }, el('span', { class: 'mini' }, 'POI'), el('strong', null, testo));
    if (poi.firstElementChild === figura) poi.replaceChild(testi, poi.lastElementChild);
    else poi.replaceChildren(figura, testi);
  }

  // ---------- Controlli della playlist ----------
  function massimo(unita) {
    return unita === 'ore' ? 24 : 1440;
  }

  function mostraErrore(testo) {
    const errore = $('#intervallo-errore');
    errore.textContent = testo;
    errore.hidden = !testo;
    $('#gruppo-intervallo').classList.toggle('errato', !!testo);
  }

  function renderControlli() {
    const imp = stato.impostazioni;
    const campo = $('#intervallo-valore');
    const unita = $('#intervallo-unita');
    if (document.activeElement !== campo && $('#intervallo-errore').hidden) campo.value = imp.intervalloValore;
    if (document.activeElement !== unita) unita.value = imp.intervalloUnita;
    campo.max = massimo(unita.value);
    campo.disabled = !imp.rotazioneAttiva;
    unita.disabled = !imp.rotazioneAttiva;
    $('#gruppo-intervallo').classList.toggle('spento', !imp.rotazioneAttiva);
    $('#btn-mai').setAttribute('aria-pressed', String(!imp.rotazioneAttiva));
    document.querySelectorAll('[data-ordine]').forEach((b) => b.setAttribute('aria-pressed', String(b.dataset.ordine === imp.ordine)));
    $('#sw-fine-video-playlist').setAttribute('aria-checked', String(imp.fineVideo));
    $('#nota-playlist').textContent = `Trascina per cambiare l'ordine, clicca un'anteprima per mostrarla subito. Dissolvenza di ${imp.dissolvenzaSecondi} s.`;
  }

  function validaValore() {
    const campo = $('#intervallo-valore');
    const unita = $('#intervallo-unita').value;
    const testo = campo.value.trim();
    const n = Number(testo);
    const ok = testo !== '' && Number.isInteger(n) && n >= 1 && n <= massimo(unita);
    mostraErrore(ok ? '' : `Scrivi un numero intero da 1 a ${massimo(unita)} ${unita}`);
    return ok ? n : null;
  }

  function collegaIntervallo() {
    const campo = $('#intervallo-valore');
    const unita = $('#intervallo-unita');
    campo.addEventListener('input', () => {
      if (!stato) return;
      clearTimeout(attesaValore);
      const n = validaValore();
      if (n !== null && n !== stato.impostazioni.intervalloValore) attesaValore = setTimeout(() => modifica({ intervalloValore: n }), 600);
    });
    campo.addEventListener('keydown', (e) => {
      if (e.key !== 'Enter' || !stato) return;
      const n = validaValore();
      if (n === null) return;
      clearTimeout(attesaValore);
      if (n !== stato.impostazioni.intervalloValore) modifica({ intervalloValore: n });
    });
    campo.addEventListener('blur', () => {
      if (!stato || validaValore() !== null) return;
      clearTimeout(attesaValore);
      campo.value = stato.impostazioni.intervalloValore;
      mostraErrore('');
    });
    unita.addEventListener('change', () => {
      if (!stato) return;
      clearTimeout(attesaValore);
      const n = Math.max(1, Math.min(Math.round(Number(campo.value)) || stato.impostazioni.intervalloValore, massimo(unita.value)));
      campo.value = n;
      mostraErrore('');
      modifica({ intervalloUnita: unita.value, intervalloValore: n });
    });
  }

  // ---------- Striscia della playlist ----------
  function inviaPlaylist(ids) {
    stato.playlist = ids;
    render();
    ponte.invia({ tipo: 'playlist', ids });
  }

  function togliDallaPlaylist(id) {
    if (stato.playlist.length <= 1) {
      mostraAvviso('Lascia almeno uno sfondo nella playlist');
      return;
    }
    const v = voce(id);
    inviaPlaylist(stato.playlist.filter((x) => x !== id));
    if (v) mostraAvviso('Tolto dalla playlist: ' + v.titolo);
  }

  function aggiungiAllaPlaylist(id) {
    if (stato.playlist.includes(id)) return;
    const v = voce(id);
    inviaPlaylist(stato.playlist.concat([id]));
    if (v) mostraAvviso('Aggiunto alla playlist: ' + v.titolo);
  }

  function sposta(da, a) {
    if (!da || da === a) return;
    const pl = stato.playlist.slice();
    const i = pl.indexOf(da);
    const j = pl.indexOf(a);
    if (i < 0 || j < 0) return;
    pl.splice(i, 1);
    pl.splice(pl.indexOf(a) + (i < j ? 1 : 0), 0, da);
    inviaPlaylist(pl);
  }

  function spostaDi(id, passo) {
    const pl = stato.playlist.slice();
    const i = pl.indexOf(id);
    const j = i + passo;
    if (i < 0 || j < 0 || j >= pl.length) return;
    [pl[i], pl[j]] = [pl[j], pl[i]];
    inviaPlaylist(pl);
    const memo = schedePlaylist.get(id);
    if (memo) memo.nodo.focus();
  }

  function creaSchedaPlaylist(id) {
    const numero = el('span');
    const segnale = el('span');
    const testa = el('div', { class: 'voce-testa' }, el('span', { class: 'numero' }, icona('maniglia', 14), numero), segnale);
    const anteprima = el('button', { type: 'button', class: 'anteprima-bottone', onclick: () => ponte.invia({ tipo: 'mostra', id }) });
    const titolo = el('span', { class: 'voce-titolo' });
    const meta = el('span', { class: 'voce-meta' });
    const barra = el('div', { class: 'barra sottile' }, el('div', { class: 'avanzamento-voce' }));
    const nodo = el('div', { class: 'voce-playlist', draggable: 'true', tabindex: '0', 'data-id': id },
      testa, anteprima, el('div', { class: 'voce-testi' }, titolo, meta), barra);

    nodo.addEventListener('dragstart', (e) => {
      trascinato = id;
      nodo.classList.add('trascinata');
      e.dataTransfer.effectAllowed = 'move';
      try { e.dataTransfer.setData('text/plain', id); } catch (err) { /* ignora */ }
    });
    nodo.addEventListener('dragend', () => { trascinato = null; nodo.classList.remove('trascinata'); });
    nodo.addEventListener('dragover', (e) => { if (trascinato) e.preventDefault(); });
    nodo.addEventListener('drop', (e) => {
      if (!trascinato) return;
      e.preventDefault();
      const da = trascinato;
      trascinato = null;
      sposta(da, id);
    });
    nodo.addEventListener('keydown', (e) => {
      if (e.target !== nodo || !e.ctrlKey) return;
      if (e.key === 'ArrowLeft') { e.preventDefault(); spostaDi(id, -1); }
      if (e.key === 'ArrowRight') { e.preventDefault(); spostaDi(id, 1); }
    });

    return {
      nodo,
      aggiorna(v, i) {
        const attiva = v.id === stato.corrente;
        nodo.className = 'voce-playlist' + (attiva ? ' attiva' : '') + (v.disponibile ? '' : ' non-disponibile');
        nodo.setAttribute('aria-label', `${i + 1}. ${v.titolo}. Ctrl più freccia sinistra o destra per spostarlo`);
        numero.textContent = String(i + 1).padStart(2, '0');
        segnale.replaceChildren(attiva
          ? el('span', { class: 'badge-onda' }, 'IN ONDA')
          : el('button', {
            type: 'button', class: 'togli', title: 'Togli dalla playlist', 'aria-label': `Togli ${v.titolo} dalla playlist`,
            onclick: (e) => { e.stopPropagation(); togliDallaPlaylist(v.id); }
          }, icona('chiudi', 14)));
        anteprima.setAttribute('aria-label', `Mostra ${v.titolo} ora sul desktop`);
        anteprima.disabled = !v.disponibile;
        const m = miniatura('striscia', v);
        if (anteprima.firstElementChild !== m) anteprima.replaceChildren(m);
        titolo.textContent = v.titolo;
        meta.textContent = v.disponibile ? metaBreve(v) : 'File non trovato';
        barra.hidden = !attiva;
      }
    };
  }

  let bottoneAggiungi = null;

  function renderStriscia() {
    const voci = stato.playlist.map(voce).filter(Boolean);
    const nodi = voci.map((v, i) => {
      let memo = schedePlaylist.get(v.id);
      if (!memo) {
        memo = creaSchedaPlaylist(v.id);
        schedePlaylist.set(v.id, memo);
      }
      memo.aggiorna(v, i);
      return memo.nodo;
    });
    if (!bottoneAggiungi) {
      bottoneAggiungi = el('button', { type: 'button', class: 'aggiungi-playlist', onclick: () => apriScheda('libreria') },
        icona('piu', 22), el('span', null, 'Aggiungi alla playlist'));
    }
    nodi.push(bottoneAggiungi);
    riconcilia($('#striscia'), nodi);
  }

  // ---------- Libreria ----------
  function chiediConferma(v) {
    daEliminare = v.id;
    $('#conferma-titolo').textContent = `Eliminare ${v.titolo} dalla libreria?`;
    $('#conferma').hidden = false;
    $('#conferma-no').focus();
  }

  function chiudiConferma() {
    $('#conferma').hidden = true;
    daEliminare = null;
  }

  function creaSchedaLibreria(id) {
    const badge = el('span', { class: 'badge-sovrapposto' }, el('span', { class: 'puntino-verde' }), 'IN ONDA');
    const anteprima = el('button', { type: 'button', class: 'anteprima-bottone grande', onclick: () => ponte.invia({ tipo: 'mostra', id }) });
    const titolo = el('span', { class: 'voce-titolo' });
    const meta = el('span', { class: 'voce-meta' });
    const azioni = el('div', { class: 'azioni-scheda' });
    const nodo = el('div', { class: 'scheda-libreria' }, anteprima,
      el('div', { class: 'scheda-piede' }, el('div', { class: 'voce-testi' }, titolo, meta), azioni));
    return {
      nodo,
      aggiorna(v) {
        const inPlaylist = stato.playlist.includes(v.id);
        const inOnda = v.id === stato.corrente;
        nodo.className = 'scheda-libreria' + (v.disponibile ? '' : ' non-disponibile');
        anteprima.className = 'anteprima-bottone grande' + (inOnda ? ' in-onda' : '');
        anteprima.setAttribute('aria-label', `Mostra ${v.titolo} ora sul desktop`);
        anteprima.disabled = !v.disponibile;
        const m = miniatura('libreria', v);
        if (anteprima.firstElementChild !== m) anteprima.replaceChildren(m, badge);
        badge.hidden = !inOnda;
        titolo.textContent = v.titolo;
        meta.textContent = v.disponibile ? descrizione(v) : 'File non trovato';
        const bottoni = [];
        if (!v.aziendale) {
          bottoni.push(el('button', {
            type: 'button', class: 'tondo-piccolo pericolo', title: 'Elimina dalla libreria', 'aria-label': `Elimina ${v.titolo} dalla libreria`,
            onclick: () => chiediConferma(v)
          }, icona('cestino')));
        }
        bottoni.push(el('button', {
          type: 'button', class: 'tondo-piccolo' + (inPlaylist ? ' attivo' : ''), 'aria-pressed': String(inPlaylist),
          title: inPlaylist ? 'Togli dalla playlist' : 'Aggiungi alla playlist',
          'aria-label': inPlaylist ? `Togli ${v.titolo} dalla playlist` : `Aggiungi ${v.titolo} alla playlist`,
          onclick: () => (inPlaylist ? togliDallaPlaylist(v.id) : aggiungiAllaPlaylist(v.id))
        }, icona(inPlaylist ? 'spunta' : 'piu')));
        azioni.replaceChildren(...bottoni);
      }
    };
  }

  let zonaImport = null;

  function creaZonaImport() {
    const zona = el('div', { class: 'zona-import' }, icona('piu', 24), el('strong', null, 'Trascina qui un file'),
      el('span', null, 'Immagini, video o pagine HTML'));
    return zona;
  }

  function renderLibreria() {
    const tutte = stato.voci;
    const conteggi = {
      tutti: tutte.length,
      energy: tutte.filter((v) => v.aziendale).length,
      miei: tutte.filter((v) => !v.aziendale).length
    };
    document.querySelectorAll('[data-filtro]').forEach((b) => {
      b.setAttribute('aria-pressed', String(b.dataset.filtro === filtro));
      b.querySelector('.conteggio').textContent = conteggi[b.dataset.filtro];
    });
    $('#nota-libreria').textContent = `${tutte.length} sfondi, ${stato.playlist.length} nella playlist. Clicca un'anteprima per mostrarla subito.`;
    const visibili = tutte.filter((v) => filtro === 'tutti' || (filtro === 'miei' ? !v.aziendale : v.aziendale));
    const nodi = visibili.map((v) => {
      let memo = schedeLibreria.get(v.id);
      if (!memo) {
        memo = creaSchedaLibreria(v.id);
        schedeLibreria.set(v.id, memo);
      }
      memo.aggiorna(v);
      return memo.nodo;
    });
    if (filtro !== 'energy') {
      if (!zonaImport) zonaImport = creaZonaImport();
      nodi.push(zonaImport);
    }
    riconcilia($('#griglia'), nodi);
  }

  function contieneFile(e) {
    return e.dataTransfer && Array.from(e.dataTransfer.types || []).includes('Files');
  }

  function collegaTrascinamentoFile() {
    const vista = $('#vista-libreria');
    vista.addEventListener('dragover', (e) => {
      if (!contieneFile(e)) return;
      e.preventDefault();
      e.dataTransfer.dropEffect = 'copy';
      if (zonaImport) zonaImport.classList.add('sopra');
    });
    vista.addEventListener('dragleave', (e) => {
      if (zonaImport && !vista.contains(e.relatedTarget)) zonaImport.classList.remove('sopra');
    });
    vista.addEventListener('drop', (e) => {
      if (!contieneFile(e)) return;
      e.preventDefault();
      if (zonaImport) zonaImport.classList.remove('sopra');
      if (e.dataTransfer.files.length > 0) ponte.inviaFile({ tipo: 'importaFile' }, e.dataTransfer.files);
    });
    // Fuori dalla libreria un file trascinato non deve aprire una pagina.
    window.addEventListener('dragover', (e) => { if (contieneFile(e)) e.preventDefault(); });
    window.addEventListener('drop', (e) => { if (contieneFile(e)) e.preventDefault(); });
  }

  // ---------- Impostazioni ----------
  function renderImpostazioni() {
    const imp = stato.impostazioni;
    const n = stato.monitor.length;
    $('#titolo-monitor').textContent = n === 1 ? 'Rilevato 1 monitor' : `Rilevati ${n} monitor`;
    $('#mini-monitor').replaceChildren(...stato.monitor.map((m, i) => el('div', {
      class: m.principale ? 'principale' : '', title: m.nome, style: `margin-top: ${Math.min(20, Math.round(m.y / 4))}px`
    }, String(i + 1))));
    $('#descrizione-modo').textContent = n === 1
      ? 'Con un solo monitor lo sfondo lo riempie e basta.'
      : imp.modoMonitor === 'estendi'
        ? "Estendi: un'unica scena attraversa tutti i monitor, logo solo su quello più a sinistra."
        : 'Duplica: la stessa scena intera su ogni monitor, ognuna con il suo logo.';
    document.querySelectorAll('[data-dissolvenza]').forEach((b) =>
      b.setAttribute('aria-pressed', String(Number(b.dataset.dissolvenza) === imp.dissolvenzaSecondi)));
    $('#sw-fine-video').setAttribute('aria-checked', String(imp.fineVideo));
    $('#sw-batteria').setAttribute('aria-checked', String(imp.pausaABatteria));
    $('#sw-avvio').setAttribute('aria-checked', String(imp.avvioConWindows));
  }

  // ---------- Conto alla rovescia ----------
  function aggiornaConto() {
    if (!stato) return;
    const imp = stato.impostazioni;
    let ms = stato.restanteMs;
    if (ms !== null && stato.contoAttivo) ms = Math.max(0, ms - (performance.now() - ricevutoAlle));
    $('#tempo').textContent = ms === null ? '--:--' : orologio(ms);
    let nota = 'al prossimo cambio';
    if (!imp.rotazioneAttiva) nota = 'cambio solo a mano';
    else if (stato.inPausa) nota = 'in pausa';
    else if (stato.attendeFineVideo) nota = 'alla fine del video';
    $('#nota-tempo').textContent = nota;
    const p = ms === null || !stato.totaleMs ? 0 : Math.min(100, Math.max(0, (1 - ms / stato.totaleMs) * 100));
    $('#barra-avanzamento').style.width = p + '%';
    document.querySelectorAll('.avanzamento-voce').forEach((b) => { b.style.width = p + '%'; });
  }

  // ---------- Avvisi ----------
  function mostraAvviso(testo) {
    const avviso = $('#avviso');
    avviso.textContent = testo;
    avviso.hidden = false;
    avviso.style.animation = 'none';
    void avviso.offsetWidth;
    avviso.style.animation = '';
    clearTimeout(attesaAvviso);
    attesaAvviso = setTimeout(() => { avviso.hidden = true; }, 2600);
  }

  // ---------- Collegamenti ----------
  function collega() {
    const conStato = (azione) => () => { if (stato) azione(); };
    document.querySelectorAll('.scheda').forEach((b) => b.addEventListener('click', () => apriScheda(b.dataset.scheda)));
    $('#btn-precedente').addEventListener('click', () => ponte.invia({ tipo: 'precedente' }));
    $('#btn-successivo').addEventListener('click', () => ponte.invia({ tipo: 'successivo' }));
    $('#btn-pausa').addEventListener('click', conStato(() => ponte.invia({ tipo: stato.pausaManuale ? 'riprendi' : 'pausa' })));
    document.querySelectorAll('[data-modo]').forEach((b) => b.addEventListener('click', () => modifica({ modoMonitor: b.dataset.modo })));
    document.querySelectorAll('[data-ordine]').forEach((b) => b.addEventListener('click', () => modifica({ ordine: b.dataset.ordine })));
    document.querySelectorAll('[data-dissolvenza]').forEach((b) => b.addEventListener('click', () => modifica({ dissolvenzaSecondi: Number(b.dataset.dissolvenza) })));
    $('#btn-mai').addEventListener('click', conStato(() => modifica({ rotazioneAttiva: !stato.impostazioni.rotazioneAttiva })));
    const fineVideo = conStato(() => modifica({ fineVideo: !stato.impostazioni.fineVideo }));
    $('#sw-fine-video-playlist').addEventListener('click', fineVideo);
    $('#sw-fine-video').addEventListener('click', fineVideo);
    $('#sw-batteria').addEventListener('click', conStato(() => modifica({ pausaABatteria: !stato.impostazioni.pausaABatteria })));
    $('#sw-avvio').addEventListener('click', conStato(() => modifica({ avvioConWindows: !stato.impostazioni.avvioConWindows })));
    $('#btn-log').addEventListener('click', () => ponte.invia({ tipo: 'apriLog' }));
    $('#btn-ripristina').addEventListener('click', () => ponte.invia({ tipo: 'ripristina' }));
    $('#btn-importa').addEventListener('click', () => ponte.invia({ tipo: 'importaDialogo' }));
    document.querySelectorAll('[data-filtro]').forEach((b) => b.addEventListener('click', conStato(() => { filtro = b.dataset.filtro; render(); })));
    $('#conferma-no').addEventListener('click', chiudiConferma);
    $('#conferma-si').addEventListener('click', () => {
      if (daEliminare) ponte.invia({ tipo: 'elimina', id: daEliminare });
      chiudiConferma();
    });
    $('#conferma').addEventListener('keydown', (e) => { if (e.key === 'Escape') chiudiConferma(); });
    $('#conferma').addEventListener('click', (e) => { if (e.target === e.currentTarget) chiudiConferma(); });
    collegaIntervallo();
    collegaTrascinamentoFile();
  }

  ponte.ascolta((m) => {
    if (!m || typeof m !== 'object') return;
    if (m.tipo === 'stato') {
      stato = m;
      ricevutoAlle = performance.now();
      render();
    } else if (m.tipo === 'avviso') {
      mostraAvviso(m.testo);
    }
  });

  collega();
  setInterval(aggiornaConto, 500);
  ponte.invia({ tipo: 'pronta' });
})();
```

- [ ] **Step 4: Finto programma per provare la finestra nel browser**

<!-- file: src/EnergyItaly.SfondoAnimato/web/sviluppo/prova-app.html -->
```html
<!doctype html>
<html lang="it">
<head>
<meta charset="utf-8">
<title>Prova della finestra principale</title>
<style>
html, body { margin: 0; height: 100%; background: #000; }
iframe { display: block; width: 100%; height: 100%; border: 0; }
</style>
</head>
<body>
<iframe id="app" src="../app/index.html" title="Finestra principale"></iframe>
<script>
(async function () {
  'use strict';
  // Simula il programma C#: stessa forma dei messaggi "stato" e "avviso" (Task 8).
  // Parametro ?monitor=1|2|3 per provare disposizioni diverse.
  const app = document.getElementById('app');
  const manifest = await (await fetch('/animazioni/manifest.json')).json();
  const voci = manifest.animazioni.map((a) => ({
    id: a.id, tipoVoce: 'animazione', titolo: a.titolo, aziendale: true, durataSecondi: null, disponibile: true,
    url: '/animazioni/' + a.file + '?' + new URLSearchParams(Object.entries(a.parametri).map(([k, v]) => [k, String(v)]))
  }));
  voci.push({ id: 'u-logo-aaaaaa', tipoVoce: 'immagine', titolo: 'Logo come immagine', aziendale: false, url: '/animazioni/img/brand/LogoEnergyItalyWhite.svg', durataSecondi: null, disponibile: true });
  voci.push({ id: 'u-video-bbbbbb', tipoVoce: 'video', titolo: 'Timelapse ufficio', aziendale: false, url: '/non-esiste.mp4', durataSecondi: 160, disponibile: false });

  const disposizioni = {
    1: [{ nome: 'DISPLAY1', x: 0, y: 0, w: 1920, h: 1080, principale: true }],
    2: [{ nome: 'DISPLAY1', x: 0, y: 7, w: 1920, h: 1080, principale: false }, { nome: 'DISPLAY2', x: 1920, y: 0, w: 1920, h: 1080, principale: true }],
    3: [{ nome: 'DISPLAY1', x: 0, y: 7, w: 1920, h: 1080, principale: false }, { nome: 'DISPLAY2', x: 1920, y: 0, w: 1920, h: 1080, principale: true }, { nome: 'DISPLAY3', x: 3840, y: 3, w: 1920, h: 1080, principale: false }]
  };
  const monitor = disposizioni[new URLSearchParams(location.search).get('monitor') || '2'] || disposizioni[2];
  const finestra = { w: Math.max(...monitor.map((m) => m.x + m.w)), h: Math.max(...monitor.map((m) => m.y + m.h)) };

  const s = {
    tipo: 'stato', voci, playlist: voci.filter((v) => v.aziendale).map((v) => v.id),
    impostazioni: { intervalloValore: 30, intervalloUnita: 'minuti', rotazioneAttiva: true, fineVideo: true, ordine: 'sequenziale', dissolvenzaSecondi: 2, modoMonitor: 'estendi', pausaABatteria: true, avvioConWindows: true },
    corrente: voci[0].id, storicoVuoto: true, inPausa: false, pausaManuale: false, motivoPausa: null,
    restanteMs: 18 * 60000 + 42000, totaleMs: 30 * 60000, contoAttivo: true, attendeFineVideo: false,
    finestra, monitor, modo: 'estendi', scene: []
  };
  const storico = [];

  function totale() { return s.impostazioni.rotazioneAttiva ? s.impostazioni.intervalloValore * (s.impostazioni.intervalloUnita === 'ore' ? 3600000 : 60000) : null; }
  function scene() {
    s.modo = monitor.length === 1 ? 'estendi' : s.impostazioni.modoMonitor;
    s.scene = s.modo === 'estendi'
      ? [{ area: { x: 0, y: 0, w: finestra.w, h: finestra.h }, monitor: monitor.map(({ x, y, w, h }) => ({ x, y, w, h })) }]
      : monitor.map((m) => ({ area: { x: m.x, y: m.y, w: m.w, h: m.h }, monitor: [{ x: 0, y: 0, w: m.w, h: m.h }] }));
  }
  function invia(m) { app.contentWindow.postMessage({ allApp: m }, '*'); }
  function inviaStato() {
    scene();
    s.storicoVuoto = storico.length === 0;
    s.contoAttivo = s.totaleMs !== null && !s.pausaManuale;
    invia(JSON.parse(JSON.stringify(s)));
  }
  function mostra(id, registra) {
    if (registra !== false && s.corrente !== id) storico.push(s.corrente);
    s.corrente = id;
    s.totaleMs = totale();
    s.restanteMs = s.totaleMs;
  }
  function prossima() {
    const pl = s.playlist.filter((id) => voci.find((v) => v.id === id && v.disponibile));
    const i = pl.indexOf(s.corrente);
    if (s.impostazioni.ordine === 'casuale') {
      const altre = pl.filter((id) => id !== s.corrente);
      return altre[Math.floor(Math.random() * altre.length)] || pl[0];
    }
    return pl[(i + 1) % pl.length];
  }

  window.addEventListener('message', (e) => {
    if (e.source !== app.contentWindow || !e.data || !e.data.dallApp) return;
    const m = e.data.dallApp;
    switch (m.tipo) {
      case 'pronta': break;
      case 'mostra': mostra(m.id); break;
      case 'successivo': mostra(prossima()); break;
      case 'precedente': if (storico.length) mostra(storico.pop(), false); break;
      case 'pausa': s.pausaManuale = true; s.inPausa = true; break;
      case 'riprendi': s.pausaManuale = false; s.inPausa = false; break;
      case 'impostazioni': {
        const imp = Object.assign({}, s.impostazioni, m.modifiche);
        const max = imp.intervalloUnita === 'ore' ? 24 : 1440;
        if (!(imp.intervalloValore >= 1 && imp.intervalloValore <= max)) { invia({ tipo: 'avviso', testo: `Scrivi un numero intero da 1 a ${max} ${imp.intervalloUnita}` }); break; }
        const tempoCambiato = imp.intervalloValore !== s.impostazioni.intervalloValore || imp.intervalloUnita !== s.impostazioni.intervalloUnita || imp.rotazioneAttiva !== s.impostazioni.rotazioneAttiva;
        s.impostazioni = imp;
        if (tempoCambiato) { s.totaleMs = totale(); s.restanteMs = s.totaleMs; }
        break;
      }
      case 'playlist':
        if (!m.ids.length) invia({ tipo: 'avviso', testo: 'Lascia almeno uno sfondo nella playlist' });
        else s.playlist = m.ids;
        break;
      case 'importaDialogo': invia({ tipo: 'avviso', testo: 'Nella prova nel browser la finestra di selezione non c\'è: trascina un file nella libreria' }); break;
      case 'importaFile':
        (m.nomiFile || []).forEach((nome, i) => voci.push({ id: 'u-prova-' + Date.now() + i, tipoVoce: /\.(mp4|webm)$/i.test(nome) ? 'video' : /\.html?$/i.test(nome) ? 'html' : 'immagine', titolo: nome.replace(/\.[^.]+$/, ''), aziendale: false, url: '/non-esiste', durataSecondi: null, disponibile: false }));
        invia({ tipo: 'avviso', testo: (m.nomiFile || []).length + ' file simulati nella libreria' });
        break;
      case 'elimina': {
        const i = voci.findIndex((v) => v.id === m.id && !v.aziendale);
        if (i >= 0) { const [v] = voci.splice(i, 1); s.playlist = s.playlist.filter((x) => x !== m.id); invia({ tipo: 'avviso', testo: 'Eliminato dalla libreria: ' + v.titolo }); }
        break;
      }
      case 'apriLog': invia({ tipo: 'avviso', testo: 'Nel programma si apre la cartella dei log' }); break;
      case 'ripristina':
        s.impostazioni = { intervalloValore: 30, intervalloUnita: 'minuti', rotazioneAttiva: true, fineVideo: true, ordine: 'sequenziale', dissolvenzaSecondi: 2, modoMonitor: 'estendi', pausaABatteria: true, avvioConWindows: true };
        s.totaleMs = totale(); s.restanteMs = s.totaleMs;
        invia({ tipo: 'avviso', testo: 'Impostazioni aziendali ripristinate' });
        break;
      default: break;
    }
    inviaStato();
  });
})();
</script>
</body>
</html>
```

- [ ] **Step 5: Verificare la finestra nel browser**

Con il server `sviluppo-web` attivo, aprire `http://127.0.0.1:5599/sviluppo/prova-app.html` nel browser integrato (finestra larga almeno 1280 px).

Controlli attesi (confrontare con il design B, `docs/design/riferimento-vetrina.dc.html`):
1. In onda: due monitor disegnati con la scena unica di Onda di luce, logo solo sul sinistro, sfalsamento visibile; titolo, conto alla rovescia che scende da 18:42, barra, tre pulsanti, riquadro "Poi: Onde di energia".
2. Successivo/Precedente cambiano titolo, palco e scheda "IN ONDA"; Pausa mostra "IN PAUSA" arancione e il pulsante diventa "Riprendi".
3. Estendi/Duplica: in Duplica la didascalia dice "STESSA SCENA SU 2 MONITOR" e ogni monitor ha la sua scena con il logo.
4. "Cambia ogni": scrivere `0` mostra l'errore in linea e il bordo rosso; uscire dal campo riporta 30; scrivere `2` e scegliere "ore" dà un conto alla rovescia `2:00:00`; "Mai" disattiva campo e select e mostra "cambio solo a mano".
5. Trascinare una scheda della playlist su un'altra la sposta; con una scheda a fuoco Ctrl+freccia la sposta; le miniature non si ricaricano (nessun lampo).
6. Libreria: filtri con conteggi corretti, ✓/+ aggiorna la playlist con un avviso, il cestino apre il dialogo di conferma (Esc lo chiude), la voce "Timelapse ufficio" appare come "File non trovato".
7. Impostazioni: mini monitor, descrizione del modo, dissolvenza, interruttori che cambiano stato.
8. `?monitor=1`: il selettore Estendi/Duplica sparisce dal palco ed è disattivato nelle impostazioni; `?monitor=3`: tre monitor.
9. Larghezza 380 px (`resize_window` preset mobile): niente scorrimento orizzontale della pagina, il palco va sopra il pannello.
10. Nessun errore in console.

- [ ] **Step 6: Commit**

```bash
git add src/EnergyItaly.SfondoAnimato/web/app/index.html src/EnergyItaly.SfondoAnimato/web/app/app.css src/EnergyItaly.SfondoAnimato/web/app/app.js src/EnergyItaly.SfondoAnimato/web/sviluppo/prova-app.html
git commit -m "Aggiunge la finestra galleria nello stile del design B"
```

---

### Task 13: Progetto Windows e infrastruttura (log, archivio, WebView2 isolata)

**Files:**
- Create: `src/EnergyItaly.SfondoAnimato/EnergyItaly.SfondoAnimato.csproj`, `impostazioni-predefinite.json`
- Create: `src/EnergyItaly.SfondoAnimato/Percorsi.cs`, `Win32.cs`, `LogFile.cs`, `ArchivioFile.cs`, `AmbienteWebView.cs`, `HostWebView.cs`

**Interfaces:**
- Consumes: `ILog`, `IArchivio`, `Catalogo.HostApp`, `Catalogo.HostLibreria`, `Catalogo.NomeFileSicuro` (Core).
- Produces: `static class Percorsi` (`Impostazioni`, `FileLibreria`, `Libreria`, `Log`, `DatiWebView`, `Web`, `Manifest`, `PredefiniteAziendali`); `static class Win32` (P/Invoke e costanti); `sealed class LogFile(string cartella) : ILog`; `sealed class ArchivioFile : IArchivio`; `static class AmbienteWebView { Task<CoreWebView2Environment> OttieniAsync(); }`; `sealed class HostWebView(ILog log) : IDisposable` con `CoreWebView2Controller? Controller`, `CoreWebView2? Core`, `event Action<CoreWebView2WebMessageReceivedEventArgs>? Messaggio`, `event Action? ProcessoTerminato`, `Task InizializzaAsync(IntPtr finestra, Rectangle area, bool scalaFissa, Color sfondo, string url)`, `void ImpostaArea(Rectangle area)`.

- [ ] **Step 1: Progetto**

<!-- file: src/EnergyItaly.SfondoAnimato/EnergyItaly.SfondoAnimato.csproj -->
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <RootNamespace>EnergyItaly.SfondoAnimato</RootNamespace>
    <AssemblyName>SfondoAnimato</AssemblyName>
    <ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>
    <ApplicationIcon>risorse\icona.ico</ApplicationIcon>
    <SatelliteResourceLanguages>it</SatelliteResourceLanguages>
    <NoWarn>$(NoWarn);SYSLIB1054</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\EnergyItaly.SfondoAnimato.Core\EnergyItaly.SfondoAnimato.Core.csproj" />
  </ItemGroup>
  <ItemGroup>
    <None Remove="web\**" />
    <None Remove="risorse\**" />
    <None Remove="impostazioni-predefinite.json" />
    <EmbeddedResource Include="risorse\icona.ico" LogicalName="icona.ico" />
    <Content Include="web\**\*" Exclude="web\sviluppo\**" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" />
    <Content Include="..\..\animazioni\**\*" Link="web\animazioni\%(RecursiveDir)%(Filename)%(Extension)" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" />
    <Content Include="impostazioni-predefinite.json" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

<!-- file: src/EnergyItaly.SfondoAnimato/impostazioni-predefinite.json -->
```json
{
  "versione": 2,
  "intervallo": { "valore": 30, "unita": "minuti" },
  "rotazioneAttiva": true,
  "fineVideo": true,
  "ordine": "sequenziale",
  "playlist": ["onda-di-luce", "onde-di-energia", "paesaggio-rinnovabile", "circuito", "aurora"],
  "dissolvenzaSecondi": 2,
  "modoMonitor": "estendi",
  "pausaABatteria": true,
  "avvioConWindows": true
}
```

Run:
```bash
DN="$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe"
"$DN" add src/EnergyItaly.SfondoAnimato/EnergyItaly.SfondoAnimato.csproj package Microsoft.Web.WebView2
"$DN" sln SfondoAnimato.slnx add src/EnergyItaly.SfondoAnimato/EnergyItaly.SfondoAnimato.csproj
```
Expected: pacchetto WebView2 aggiunto (versione stabile più recente), progetto nella soluzione.

- [ ] **Step 2: Percorsi e P/Invoke**

<!-- file: src/EnergyItaly.SfondoAnimato/Percorsi.cs -->
```csharp
namespace EnergyItaly.SfondoAnimato;

/// <summary>Percorsi su disco del programma (spec 3.1; libreria.json sta fuori dalla cartella servita).</summary>
static class Percorsi
{
    static string Dati => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EnergyItaly", "SfondoAnimato");
    static string Locali => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EnergyItaly", "SfondoAnimato");

    public static string Impostazioni => Path.Combine(Dati, "impostazioni.json");
    public static string FileLibreria => Path.Combine(Dati, "libreria.json");
    public static string Libreria => Path.Combine(Dati, "libreria");
    public static string Log => Path.Combine(Locali, "log");
    public static string DatiWebView => Path.Combine(Locali, "WebView2");
    public static string Web => Path.Combine(AppContext.BaseDirectory, "web");
    public static string Manifest => Path.Combine(Web, "animazioni", "manifest.json");
    public static string PredefiniteAziendali => Path.Combine(AppContext.BaseDirectory, "impostazioni-predefinite.json");
}
```

<!-- file: src/EnergyItaly.SfondoAnimato/Win32.cs -->
```csharp
using System.Runtime.InteropServices;
using System.Text;

namespace EnergyItaly.SfondoAnimato;

/// <summary>Funzioni e costanti di Windows usate dal programma.</summary>
static class Win32
{
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;
    public const uint WS_POPUP = 0x80000000;
    public const uint WS_CHILD = 0x40000000;
    public const uint WS_CLIPSIBLINGS = 0x04000000;
    public const uint WS_CLIPCHILDREN = 0x02000000;
    public const int WS_EX_TRANSPARENT = 0x20;
    public const int WS_EX_TOOLWINDOW = 0x80;
    public const int WS_EX_LAYERED = 0x80000;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const uint SWP_NOSIZE = 0x1;
    public const uint SWP_NOMOVE = 0x2;
    public const uint SWP_NOZORDER = 0x4;
    public const uint SWP_NOACTIVATE = 0x10;
    public const int SW_HIDE = 0;
    public const int SW_SHOWNOACTIVATE = 4;
    public const uint LWA_ALPHA = 0x2;
    public const uint SMTO_NORMAL = 0x0;
    public const int WM_MOUSEACTIVATE = 0x21;
    public const int WM_NCDESTROY = 0x82;
    public const int MA_NOACTIVATE = 3;
    public const int WM_POWERBROADCAST = 0x218;
    public const int PBT_POWERSETTINGCHANGE = 0x8013;
    public const uint SPI_SETDESKWALLPAPER = 0x14;
    public const uint SPI_GETDESKWALLPAPER = 0x73;
    public const uint SPIF_SENDCHANGE = 0x2;
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    public const int DWMWA_CLOAKED = 14;
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    public static readonly Guid GUID_CONSOLE_DISPLAY_STATE = new("6FE69556-704A-47A0-8F24-C28D936FDA47");
    public static readonly Guid GUID_POWER_SAVING_STATUS = new("E00958C0-C213-4ACE-AC77-FECCED2EEEA5");

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct POWERBROADCAST_SETTING { public Guid PowerSetting; public uint DataLength; public uint Data; }

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string? classe, string? titolo);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr padre, IntPtr dopo, string? classe, string? titolo);
    [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr risultato);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] public static extern IntPtr SetParent(IntPtr figlia, IntPtr padre);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int indice);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int indice, IntPtr valore);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr dopo, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool ShowWindow(IntPtr hWnd, int comando);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int MapWindowPoints(IntPtr da, IntPtr a, ref POINT punti, int quanti);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint chiave, byte alfa, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder nome, int massimo);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rettangolo);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string nome);
    [DllImport("user32.dll")] public static extern IntPtr RegisterPowerSettingNotification(IntPtr destinatario, ref Guid impostazione, int flags);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool UnregisterPowerSettingNotification(IntPtr handle);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = CharSet.Unicode)][return: MarshalAs(UnmanagedType.Bool)] public static extern bool SystemParametersInfo(uint azione, uint parametro, StringBuilder? valore, uint winIni);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = CharSet.Unicode)][return: MarshalAs(UnmanagedType.Bool)] public static extern bool SystemParametersInfo(uint azione, uint parametro, string? valore, uint winIni);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attributo, out RECT valore, int dimensione);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attributo, out int valore, int dimensione);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hWnd, int attributo, ref int valore, int dimensione);
    [DllImport("shell32.dll")] public static extern int SHQueryUserNotificationState(out int stato);
}
```

- [ ] **Step 3: Log e archivio su disco**

<!-- file: src/EnergyItaly.SfondoAnimato/LogFile.cs -->
```csharp
using System.Text;
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato;

/// <summary>Log in italiano a rotazione: sfondo.log più 4 file precedenti da 1 MB (RF-53).</summary>
sealed class LogFile : ILog
{
    const long DimensioneMassima = 1024 * 1024;
    const int NumeroFile = 5;
    static readonly UTF8Encoding SenzaBom = new(false);

    readonly string cartella;
    readonly string percorso;
    readonly object blocco = new();

    public LogFile(string cartella)
    {
        this.cartella = cartella;
        Directory.CreateDirectory(cartella);
        percorso = Path.Combine(cartella, "sfondo.log");
    }

    public void Info(string testo) => Scrivi("INFO", testo);
    public void Avviso(string testo) => Scrivi("AVVISO", testo);
    public void Errore(string testo) => Scrivi("ERRORE", testo);

    void Scrivi(string livello, string testo)
    {
        var riga = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{livello}] {testo}{Environment.NewLine}";
        lock (blocco)
        {
            try
            {
                var info = new FileInfo(percorso);
                if (info.Exists && info.Length + riga.Length > DimensioneMassima) Ruota();
                File.AppendAllText(percorso, riga, SenzaBom);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        System.Diagnostics.Debug.Write(riga);
    }

    void Ruota()
    {
        string Nome(int i) => i == 0 ? percorso : Path.Combine(cartella, $"sfondo.{i}.log");
        File.Delete(Nome(NumeroFile - 1));
        for (int i = NumeroFile - 2; i >= 0; i--)
        {
            if (File.Exists(Nome(i))) File.Move(Nome(i), Nome(i + 1));
        }
    }
}
```

<!-- file: src/EnergyItaly.SfondoAnimato/ArchivioFile.cs -->
```csharp
using System.Text;
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato;

/// <summary>Impostazioni, libreria e file importati su disco. Le scritture passano da un file temporaneo.</summary>
sealed class ArchivioFile : IArchivio
{
    static readonly UTF8Encoding SenzaBom = new(false);

    public string? LeggiImpostazioni() => LeggiSeEsiste(Percorsi.Impostazioni);
    public void ScriviImpostazioni(string json) => ScriviSicuro(Percorsi.Impostazioni, json);
    public string? LeggiLibreria() => LeggiSeEsiste(Percorsi.FileLibreria);
    public void ScriviLibreria(string json) => ScriviSicuro(Percorsi.FileLibreria, json);

    public bool EsisteInLibreria(string nomeFile) => File.Exists(Path.Combine(Percorsi.Libreria, nomeFile));

    public long Dimensione(string percorso) => new FileInfo(percorso).Length;

    public void CopiaInLibreria(string percorsoOrigine, string nomeFile)
    {
        Directory.CreateDirectory(Percorsi.Libreria);
        File.Copy(percorsoOrigine, Path.Combine(Percorsi.Libreria, nomeFile), overwrite: false);
    }

    public void CancellaDaLibreria(string nomeFile)
    {
        var percorso = Path.Combine(Percorsi.Libreria, nomeFile);
        if (File.Exists(percorso)) File.Delete(percorso);
    }

    static string? LeggiSeEsiste(string percorso) => File.Exists(percorso) ? File.ReadAllText(percorso, Encoding.UTF8) : null;

    static void ScriviSicuro(string percorso, string testo)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(percorso)!);
        var temporaneo = percorso + ".tmp";
        File.WriteAllText(temporaneo, testo, SenzaBom);
        File.Move(temporaneo, percorso, overwrite: true);
    }
}
```

- [ ] **Step 4: WebView2 condivisa e isolata dalla rete**

<!-- file: src/EnergyItaly.SfondoAnimato/AmbienteWebView.cs -->
```csharp
using Microsoft.Web.WebView2.Core;

namespace EnergyItaly.SfondoAnimato;

/// <summary>Un solo ambiente WebView2 per sfondo e finestra principale: un solo processo browser.</summary>
static class AmbienteWebView
{
    static Task<CoreWebView2Environment>? ambiente;

    public static Task<CoreWebView2Environment> OttieniAsync() =>
        ambiente ??= CoreWebView2Environment.CreateAsync(null, Percorsi.DatiWebView,
            new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required"));
}
```

<!-- file: src/EnergyItaly.SfondoAnimato/HostWebView.cs -->
```csharp
using System.Drawing;
using EnergyItaly.SfondoAnimato.Core;
using Microsoft.Web.WebView2.Core;

namespace EnergyItaly.SfondoAnimato;

/// <summary>
/// WebView2 ospitata direttamente su una finestra Win32. Serve solo i due host virtuali,
/// blocca ogni altra richiesta di rete e consegna le pagine HTML importate con una
/// Content-Security-Policy senza rete (spec 3.1).
/// </summary>
sealed class HostWebView : IDisposable
{
    const string PoliticaPagineImportate =
        "default-src 'none'; script-src 'unsafe-inline' 'unsafe-eval' https://libreria.energyitaly; " +
        "style-src 'unsafe-inline' https://libreria.energyitaly; img-src data: blob: https://libreria.energyitaly; " +
        "media-src data: blob: https://libreria.energyitaly; font-src data: https://libreria.energyitaly; " +
        "connect-src 'none'; frame-src 'none'; form-action 'none'; base-uri 'none'";

    readonly ILog log;
    readonly HashSet<string> hostBloccati = new(StringComparer.OrdinalIgnoreCase);
    CoreWebView2Environment? ambiente;

    public HostWebView(ILog log) => this.log = log;

    public CoreWebView2Controller? Controller { get; private set; }
    public CoreWebView2? Core => Controller?.CoreWebView2;

    public event Action<CoreWebView2WebMessageReceivedEventArgs>? Messaggio;
    public event Action? ProcessoTerminato;

    public async Task InizializzaAsync(IntPtr finestra, Rectangle area, bool scalaFissa, Color sfondo, string url)
    {
        ambiente = await AmbienteWebView.OttieniAsync();
        var controller = await ambiente.CreateCoreWebView2ControllerAsync(finestra);
        Controller = controller;
        controller.DefaultBackgroundColor = sfondo;
        if (scalaFissa)
        {
            // RF-11: 1 px CSS = 1 px fisico anche con monitor a DPI diversi.
            controller.ShouldDetectMonitorScaleChanges = false;
            controller.RasterizationScale = 1.0;
        }
        controller.Bounds = area;

        var core = controller.CoreWebView2;
        var s = core.Settings;
        s.AreDefaultContextMenusEnabled = false;
        s.AreDefaultScriptDialogsEnabled = false;
        s.IsStatusBarEnabled = false;
        s.IsZoomControlEnabled = false;
        s.IsPinchZoomEnabled = false;
        s.IsSwipeNavigationEnabled = false;
        s.IsGeneralAutofillEnabled = false;
        s.IsPasswordAutosaveEnabled = false;
        s.AreBrowserAcceleratorKeysEnabled = false;
#if DEBUG
        s.AreDevToolsEnabled = true;
#else
        s.AreDevToolsEnabled = false;
#endif
        Directory.CreateDirectory(Percorsi.Libreria);
        core.SetVirtualHostNameToFolderMapping(Catalogo.HostApp, Percorsi.Web, CoreWebView2HostResourceAccessKind.Allow);
        core.SetVirtualHostNameToFolderMapping(Catalogo.HostLibreria, Percorsi.Libreria, CoreWebView2HostResourceAccessKind.Allow);
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.All);
        core.WebResourceRequested += FiltraRete;
        core.NewWindowRequested += (_, e) => e.Handled = true;
        core.DownloadStarting += (_, e) => e.Cancel = true;
        core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
        core.NavigationStarting += (_, e) =>
        {
            if (Consentito(e.Uri)) return;
            e.Cancel = true;
            log.Avviso($"Navigazione bloccata: {e.Uri}");
        };
        core.WebMessageReceived += (_, e) => Messaggio?.Invoke(e);
        core.ProcessFailed += (_, e) =>
        {
            log.Errore($"WebView2: processo terminato ({e.ProcessFailedKind}, {e.Reason})");
            // Il crash di una pagina importata in un iframe non richiede di ricreare tutto.
            if (e.ProcessFailedKind != CoreWebView2ProcessFailedKind.FrameRenderProcessExited) ProcessoTerminato?.Invoke();
        };
        core.Navigate(url);
    }

    public void ImpostaArea(Rectangle area)
    {
        if (Controller is not null) Controller.Bounds = area;
    }

    void FiltraRete(object? mittente, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri)) return;
        if (uri.Scheme is "data" or "blob" or "about") return;
        bool https = uri.Scheme == Uri.UriSchemeHttps;
        if (https && uri.Host.Equals(Catalogo.HostApp, StringComparison.OrdinalIgnoreCase)) return;
        if (https && uri.Host.Equals(Catalogo.HostLibreria, StringComparison.OrdinalIgnoreCase))
        {
            var estensione = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
            if (estensione is ".html" or ".htm") e.Response = PaginaImportataIsolata(uri);
            return;
        }
        lock (hostBloccati)
        {
            if (hostBloccati.Add(uri.Host)) log.Avviso($"Richiesta di rete bloccata verso {uri.Scheme}://{uri.Host}");
        }
        e.Response = ambiente!.CreateWebResourceResponse(null, 403, "Bloccato", "Content-Type: text/plain");
    }

    CoreWebView2WebResourceResponse PaginaImportataIsolata(Uri uri)
    {
        var nome = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        var percorso = Path.Combine(Percorsi.Libreria, nome);
        if (!Catalogo.NomeFileSicuro(nome) || !File.Exists(percorso))
            return ambiente!.CreateWebResourceResponse(null, 404, "Non trovato", "Content-Type: text/plain");
        var contenuto = new MemoryStream(File.ReadAllBytes(percorso));
        return ambiente!.CreateWebResourceResponse(contenuto, 200, "OK",
            $"Content-Type: text/html\nContent-Security-Policy: {PoliticaPagineImportate}\nCache-Control: no-store");
    }

    static bool Consentito(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) &&
        (u.Scheme == "about" ||
         (u.Scheme == Uri.UriSchemeHttps && (u.Host == Catalogo.HostApp || u.Host == Catalogo.HostLibreria)));

    public void Dispose()
    {
        try { Controller?.Close(); }
        catch (Exception ex) { log.Avviso($"Chiusura WebView2: {ex.Message}"); }
        Controller = null;
    }
}
```

- [ ] **Step 5: Compilare**

Run: `"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" build src/EnergyItaly.SfondoAnimato/EnergyItaly.SfondoAnimato.csproj`
Expected: errore solo per la mancanza di `Program`/`Main` (CS5001) oppure build riuscita se il generatore non lo richiede. Nessun altro errore. Se `AddWebResourceRequestedFilter` a tre argomenti non esiste nella versione del pacchetto, usare la versione a due argomenti `("*", CoreWebView2WebResourceContext.All)` e annotarlo: il Task 15 (T19) verifica che le richieste degli iframe siano filtrate comunque.

- [ ] **Step 6: Commit**

```bash
git add src/EnergyItaly.SfondoAnimato/EnergyItaly.SfondoAnimato.csproj src/EnergyItaly.SfondoAnimato/impostazioni-predefinite.json src/EnergyItaly.SfondoAnimato/Percorsi.cs src/EnergyItaly.SfondoAnimato/Win32.cs src/EnergyItaly.SfondoAnimato/LogFile.cs src/EnergyItaly.SfondoAnimato/ArchivioFile.cs src/EnergyItaly.SfondoAnimato/AmbienteWebView.cs src/EnergyItaly.SfondoAnimato/HostWebView.cs SfondoAnimato.slnx
git commit -m "Aggiunge il progetto Windows con log, archivio e WebView2 isolata dalla rete"
```

---

### Task 14: Finestre e segnali di sistema

**Files:**
- Create: `src/EnergyItaly.SfondoAnimato/DesktopHost.cs`, `FinestraSfondo.cs`, `FinestraMessaggi.cs`, `LettoreMonitor.cs`, `CoperturaFinestre.cs`, `GestorePausa.cs`, `IconaNotifica.cs`, `FinestraPrincipale.cs`

**Interfaces:**
- Consumes: Task 13; `Rettangolo`, `Monitor`, `SegnaliSistema`, `CoperturaDesktop`, `Messaggi.LeggiComandoApp`, `Catalogo.HostApp` (Core).
- Produces:
  - `DesktopHost(ILog)`: `bool Aggancia(IntPtr finestra, Rettangolo area)`, `void Posiziona(IntPtr finestra, Rettangolo area)`, `void Sgancia(IntPtr finestra)`, `static void RidisegnaSfondo()`.
  - `FinestraSfondo(ILog, DesktopHost) : NativeWindow`: `bool Crea(Rettangolo area)`, `Task InizializzaWebAsync()`, `void Invia(string json)`, `void ImpostaArea(Rettangolo)`, `void ImpostaSospensione(bool)`, `void Chiudi(bool ridisegna)`, eventi `Action<string> MessaggioDalPlayer`, `Action<bool> Persa` (true = crash WebView, false = finestra distrutta).
  - `FinestraMessaggi : NativeWindow, IDisposable`: eventi `Action ExplorerRiavviato`, `Action<bool> MonitorAccesi`, `Action<bool> RisparmioEnergetico`.
  - `static IReadOnlyList<Monitor> LettoreMonitor.Leggi()`.
  - `static bool CoperturaFinestre.DesktopCoperto()`.
  - `GestorePausa(FinestraMessaggi) : IDisposable`: `event Action<SegnaliSistema> Cambiati`, `void Valuta()`.
  - `IconaNotifica(Icon) : IDisposable`: eventi `Apri`, `Successivo`, `AlternaPausa`, `ApriLog`, `Esci`; `void Aggiorna(string titolo, bool inPausa)`.
  - `FinestraPrincipale(ILog, Icon, Action<string> daApp, Action<IReadOnlyList<string>> importa) : Form`: `void Invia(string json)`, `void MostraInPrimoPiano()`, `void ChiudiDefinitivamente()`.

- [ ] **Step 1: Aggancio al desktop**

<!-- file: src/EnergyItaly.SfondoAnimato/DesktopHost.cs -->
```csharp
using System.Text;
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato;

/// <summary>
/// Mette la finestra dello sfondo dietro le icone del desktop (RF-01..RF-06).
/// Due strutture: classica (WorkerW separato dietro le icone) e Windows 11 24H2 e successivi
/// (icone e WorkerW figli di Progman). Tecnica studiata su Lively Wallpaper, non copiata.
/// </summary>
sealed class DesktopHost
{
    readonly ILog log;
    IntPtr padre;

    public DesktopHost(ILog log) => this.log = log;

    public bool Aggancia(IntPtr finestra, Rettangolo area)
    {
        padre = IntPtr.Zero;
        var progman = Win32.FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
        {
            log.Avviso("Progman non trovato: il desktop non è ancora pronto");
            return false;
        }
        // Messaggio non documentato 0x052C: chiede a Explorer di creare il WorkerW dietro le icone.
        Win32.SendMessageTimeout(progman, 0x052C, new IntPtr(0xD), new IntPtr(0x1), Win32.SMTO_NORMAL, 1000, out _);
        Win32.SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, Win32.SMTO_NORMAL, 1000, out _);

        var icone = Win32.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (icone != IntPtr.Zero)
        {
            // Windows 11 24H2 e successivi: la finestra va dentro Progman, subito sotto le icone.
            RendiFiglia(finestra, progman, stratificata: true);
            Win32.SetWindowPos(finestra, icone, 0, 0, 0, 0, Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
            padre = progman;
            log.Info("Aggancio al desktop: struttura 24H2 (figlia di Progman, sotto le icone)");
        }
        else
        {
            IntPtr workerW = IntPtr.Zero;
            Win32.EnumWindows((alto, _) =>
            {
                if (Win32.FindWindowEx(alto, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                    workerW = Win32.FindWindowEx(IntPtr.Zero, alto, "WorkerW", null);
                return true;
            }, IntPtr.Zero);
            if (workerW == IntPtr.Zero)
            {
                log.Avviso("WorkerW non trovato: riprovo più tardi");
                return false;
            }
            RendiFiglia(finestra, workerW, stratificata: false);
            padre = workerW;
            log.Info("Aggancio al desktop: struttura classica (figlia di WorkerW)");
        }
        Posiziona(finestra, area);
        Win32.ShowWindow(finestra, Win32.SW_SHOWNOACTIVATE);
        return true;
    }

    static void RendiFiglia(IntPtr finestra, IntPtr nuovoPadre, bool stratificata)
    {
        long stile = Win32.GetWindowLongPtr(finestra, Win32.GWL_STYLE).ToInt64();
        stile = (stile & ~(long)Win32.WS_POPUP) | Win32.WS_CHILD;
        Win32.SetWindowLongPtr(finestra, Win32.GWL_STYLE, new IntPtr(stile));
        if (stratificata)
        {
            long esteso = Win32.GetWindowLongPtr(finestra, Win32.GWL_EXSTYLE).ToInt64() | Win32.WS_EX_LAYERED;
            Win32.SetWindowLongPtr(finestra, Win32.GWL_EXSTYLE, new IntPtr(esteso));
            Win32.SetLayeredWindowAttributes(finestra, 0, 255, Win32.LWA_ALPHA);
        }
        Win32.SetParent(finestra, nuovoPadre);
    }

    /// <summary>Porta la finestra sull'area indicata (coordinate dello schermo virtuale, px fisici).</summary>
    public void Posiziona(IntPtr finestra, Rettangolo area)
    {
        var p = new Win32.POINT { X = area.X, Y = area.Y };
        if (padre != IntPtr.Zero) Win32.MapWindowPoints(IntPtr.Zero, padre, ref p, 1);
        Win32.SetWindowPos(finestra, IntPtr.Zero, p.X, p.Y, area.L, area.A, Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
    }

    public void Sgancia(IntPtr finestra)
    {
        if (Win32.IsWindow(finestra)) Win32.ShowWindow(finestra, Win32.SW_HIDE);
        padre = IntPtr.Zero;
    }

    /// <summary>Riapplica lo sfondo statico di Windows così il desktop si ridisegna (RF-06).</summary>
    public static void RidisegnaSfondo()
    {
        var attuale = new StringBuilder(520);
        if (Win32.SystemParametersInfo(Win32.SPI_GETDESKWALLPAPER, (uint)attuale.Capacity, attuale, 0))
            Win32.SystemParametersInfo(Win32.SPI_SETDESKWALLPAPER, 0, attuale.ToString(), Win32.SPIF_SENDCHANGE);
    }
}
```

- [ ] **Step 2: Finestra dello sfondo**

<!-- file: src/EnergyItaly.SfondoAnimato/FinestraSfondo.cs -->
```csharp
using System.Drawing;
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato;

/// <summary>Finestra Win32 senza bordi, dietro le icone, che ospita il player (RF-01, RF-02, RF-14).</summary>
sealed class FinestraSfondo : NativeWindow
{
    public static readonly Color ColoreSfondo = Color.FromArgb(0x0f, 0x1e, 0x35);

    readonly ILog log;
    readonly DesktopHost desktop;
    readonly HostWebView web;
    Rettangolo area;
    bool chiusa;

    public event Action<string>? MessaggioDalPlayer;
    public event Action<bool>? Persa;

    public FinestraSfondo(ILog log, DesktopHost desktop)
    {
        this.log = log;
        this.desktop = desktop;
        web = new HostWebView(log);
        web.Messaggio += e =>
        {
            try { MessaggioDalPlayer?.Invoke(e.WebMessageAsJson); }
            catch (Exception ex) { log.Errore($"Messaggio del player non gestito: {ex.Message}"); }
        };
        web.ProcessoTerminato += () => Persa?.Invoke(true);
    }

    public bool Crea(Rettangolo area)
    {
        this.area = area;
        CreateHandle(new CreateParams
        {
            Caption = "Energy Italy Sfondo Animato",
            Style = unchecked((int)(Win32.WS_POPUP | Win32.WS_CLIPCHILDREN | Win32.WS_CLIPSIBLINGS)),
            ExStyle = Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE,
            X = area.X,
            Y = area.Y,
            Width = area.L,
            Height = area.A
        });
        return desktop.Aggancia(Handle, area);
    }

    public Task InizializzaWebAsync() =>
        web.InizializzaAsync(Handle, new Rectangle(0, 0, area.L, area.A), scalaFissa: true, ColoreSfondo,
            $"https://{Catalogo.HostApp}/player/player.html");

    public void Invia(string json)
    {
        try { web.Core?.PostWebMessageAsJson(json); }
        catch (Exception ex) { log.Avviso($"Messaggio al player non inviato: {ex.Message}"); }
    }

    public void ImpostaArea(Rettangolo nuova)
    {
        area = nuova;
        if (Handle == IntPtr.Zero) return;
        desktop.Posiziona(Handle, nuova);
        web.ImpostaArea(new Rectangle(0, 0, nuova.L, nuova.A));
    }

    public async void ImpostaSospensione(bool sospesa)
    {
        var controller = web.Controller;
        if (controller is null) return;
        try
        {
            if (sospesa)
            {
                controller.IsVisible = false;
                await controller.CoreWebView2.TrySuspendAsync();
            }
            else
            {
                controller.CoreWebView2.Resume();
                controller.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            log.Avviso($"Sospensione della WebView non riuscita: {ex.Message}");
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Win32.WM_MOUSEACTIVATE)
        {
            m.Result = (IntPtr)Win32.MA_NOACTIVATE;
            return;
        }
        base.WndProc(ref m);
        if (m.Msg == Win32.WM_NCDESTROY && !chiusa)
        {
            log.Avviso("La finestra dello sfondo è stata distrutta (Explorer riavviato?)");
            Persa?.Invoke(false);
        }
    }

    public void Chiudi(bool ridisegna)
    {
        chiusa = true;
        web.Dispose();
        if (Handle != IntPtr.Zero)
        {
            desktop.Sgancia(Handle);
            DestroyHandle();
        }
        if (ridisegna) DesktopHost.RidisegnaSfondo();
    }
}
```

- [ ] **Step 3: Messaggi di sistema, monitor e copertura**

<!-- file: src/EnergyItaly.SfondoAnimato/FinestraMessaggi.cs -->
```csharp
using System.Runtime.InteropServices;

namespace EnergyItaly.SfondoAnimato;

/// <summary>
/// Finestra nascosta di primo livello: riceve TaskbarCreated (Explorer riavviato, RF-04),
/// lo stato dei monitor (RF-33) e del risparmio energia (RF-34).
/// </summary>
sealed class FinestraMessaggi : NativeWindow, IDisposable
{
    static readonly int WmTaskbarCreated = (int)Win32.RegisterWindowMessage("TaskbarCreated");
    readonly List<IntPtr> registrazioni = [];

    public event Action? ExplorerRiavviato;
    public event Action<bool>? MonitorAccesi;
    public event Action<bool>? RisparmioEnergetico;

    public FinestraMessaggi()
    {
        CreateHandle(new CreateParams { Caption = "EnergyItaly.SfondoAnimato.Messaggi", ExStyle = Win32.WS_EX_TOOLWINDOW });
        foreach (var impostazione in new[] { Win32.GUID_CONSOLE_DISPLAY_STATE, Win32.GUID_POWER_SAVING_STATUS })
        {
            var guid = impostazione;
            var h = Win32.RegisterPowerSettingNotification(Handle, ref guid, 0);
            if (h != IntPtr.Zero) registrazioni.Add(h);
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmTaskbarCreated)
        {
            ExplorerRiavviato?.Invoke();
        }
        else if (m.Msg == Win32.WM_POWERBROADCAST && m.WParam.ToInt64() == Win32.PBT_POWERSETTINGCHANGE && m.LParam != IntPtr.Zero)
        {
            var dati = Marshal.PtrToStructure<Win32.POWERBROADCAST_SETTING>(m.LParam);
            if (dati.PowerSetting == Win32.GUID_CONSOLE_DISPLAY_STATE) MonitorAccesi?.Invoke(dati.Data != 0);
            else if (dati.PowerSetting == Win32.GUID_POWER_SAVING_STATUS) RisparmioEnergetico?.Invoke(dati.Data != 0);
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        foreach (var h in registrazioni) Win32.UnregisterPowerSettingNotification(h);
        registrazioni.Clear();
        if (Handle != IntPtr.Zero) DestroyHandle();
    }
}
```

<!-- file: src/EnergyItaly.SfondoAnimato/LettoreMonitor.cs -->
```csharp
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato;

/// <summary>Monitor collegati in pixel fisici (il processo è PerMonitorV2).</summary>
static class LettoreMonitor
{
    public static IReadOnlyList<Monitor> Leggi() =>
        Screen.AllScreens
            .Select(s => new Monitor(
                s.DeviceName.Replace(@"\\.\", "", StringComparison.Ordinal),
                new Rettangolo(s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height),
                s.Primary))
            .ToList();
}
```

<!-- file: src/EnergyItaly.SfondoAnimato/CoperturaFinestre.cs -->
```csharp
using System.Runtime.InteropServices;
using System.Text;
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato;

/// <summary>Raccoglie le finestre visibili e chiede al Core se coprono il desktop su tutti i monitor (RF-30).</summary>
static class CoperturaFinestre
{
    static readonly HashSet<string> ClassiDellaShell = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow"
    };

    public static bool DesktopCoperto()
    {
        var aree = Screen.AllScreens
            .Select(s => new Rettangolo(s.WorkingArea.X, s.WorkingArea.Y, s.WorkingArea.Width, s.WorkingArea.Height))
            .ToList();
        var finestre = new List<Rettangolo>();
        Win32.EnumWindows((h, _) =>
        {
            if (Rilevante(h)) finestre.Add(Area(h));
            return true;
        }, IntPtr.Zero);
        return CoperturaDesktop.TuttiCoperti(aree, finestre);
    }

    static bool Rilevante(IntPtr h)
    {
        if (!Win32.IsWindowVisible(h) || Win32.IsIconic(h)) return false;
        if (Win32.DwmGetWindowAttribute(h, Win32.DWMWA_CLOAKED, out int velata, sizeof(int)) == 0 && velata != 0) return false;
        long esteso = Win32.GetWindowLongPtr(h, Win32.GWL_EXSTYLE).ToInt64();
        if ((esteso & (Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_TRANSPARENT)) != 0) return false;
        var classe = new StringBuilder(64);
        Win32.GetClassName(h, classe, classe.Capacity);
        return !ClassiDellaShell.Contains(classe.ToString());
    }

    static Rettangolo Area(IntPtr h)
    {
        if (Win32.DwmGetWindowAttribute(h, Win32.DWMWA_EXTENDED_FRAME_BOUNDS, out Win32.RECT r, Marshal.SizeOf<Win32.RECT>()) != 0)
            Win32.GetWindowRect(h, out r);
        return new Rettangolo(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }
}
```

<!-- file: src/EnergyItaly.SfondoAnimato/GestorePausa.cs -->
```csharp
using EnergyItaly.SfondoAnimato.Core;
using Microsoft.Win32;

namespace EnergyItaly.SfondoAnimato;

/// <summary>Raccoglie i segnali di sistema per la pausa automatica (spec 6.2) e avvisa quando cambiano.</summary>
sealed class GestorePausa : IDisposable
{
    readonly System.Windows.Forms.Timer timer = new() { Interval = 2000 };
    readonly FinestraMessaggi messaggi;
    bool sessioneNonAttiva;
    bool monitorSpenti;
    bool risparmioEnergetico;
    SegnaliSistema ultimi = new();

    public event Action<SegnaliSistema>? Cambiati;

    public GestorePausa(FinestraMessaggi messaggi)
    {
        this.messaggi = messaggi;
        messaggi.MonitorAccesi += Accesi;
        messaggi.RisparmioEnergetico += Risparmio;
        SystemEvents.SessionSwitch += SessioneCambiata;
        SystemEvents.PowerModeChanged += AlimentazioneCambiata;
        timer.Tick += (_, _) => Valuta();
        timer.Start();
    }

    void Accesi(bool accesi) { monitorSpenti = !accesi; Valuta(); }

    void Risparmio(bool attivo) { risparmioEnergetico = attivo; Valuta(); }

    void SessioneCambiata(object? mittente, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
            case SessionSwitchReason.ConsoleDisconnect:
            case SessionSwitchReason.RemoteDisconnect:
            case SessionSwitchReason.SessionLogoff:
                sessioneNonAttiva = true;
                break;
            case SessionSwitchReason.SessionUnlock:
            case SessionSwitchReason.ConsoleConnect:
            case SessionSwitchReason.RemoteConnect:
            case SessionSwitchReason.SessionLogon:
                sessioneNonAttiva = false;
                break;
        }
        Valuta();
    }

    void AlimentazioneCambiata(object? mittente, PowerModeChangedEventArgs e) => Valuta();

    public void Valuta()
    {
        var segnali = new SegnaliSistema(
            DesktopCoperto: CoperturaFinestre.DesktopCoperto(),
            SchermoIntero: SchermoInteroAttivo(),
            SessioneNonAttiva: sessioneNonAttiva,
            MonitorSpenti: monitorSpenti,
            ABatteria: risparmioEnergetico || SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline);
        if (segnali == ultimi) return;
        ultimi = segnali;
        Cambiati?.Invoke(segnali);
    }

    // QUNS_BUSY (2), QUNS_RUNNING_D3D_FULL_SCREEN (3), QUNS_PRESENTATION_MODE (4): RF-31.
    static bool SchermoInteroAttivo() =>
        Win32.SHQueryUserNotificationState(out var stato) == 0 && stato is 2 or 3 or 4;

    public void Dispose()
    {
        timer.Stop();
        timer.Dispose();
        messaggi.MonitorAccesi -= Accesi;
        messaggi.RisparmioEnergetico -= Risparmio;
        SystemEvents.SessionSwitch -= SessioneCambiata;
        SystemEvents.PowerModeChanged -= AlimentazioneCambiata;
    }
}
```

- [ ] **Step 4: Icona nell'area di notifica**

<!-- file: src/EnergyItaly.SfondoAnimato/IconaNotifica.cs -->
```csharp
using System.Drawing;

namespace EnergyItaly.SfondoAnimato;

/// <summary>Icona e menu nell'area di notifica (RF-40, RF-41).</summary>
sealed class IconaNotifica : IDisposable
{
    const string NomeProgramma = "Energy Italy Sfondo Animato";
    readonly NotifyIcon icona;
    readonly ContextMenuStrip menu;
    readonly ToolStripMenuItem vocePausa;

    public event Action? Apri;
    public event Action? Successivo;
    public event Action? AlternaPausa;
    public event Action? ApriLog;
    public event Action? Esci;

    public IconaNotifica(Icon immagine)
    {
        menu = new ContextMenuStrip();
        var voceApri = new ToolStripMenuItem("Apri " + NomeProgramma, null, (_, _) => Apri?.Invoke());
        voceApri.Font = new Font(menu.Font, FontStyle.Bold);
        vocePausa = new ToolStripMenuItem("Pausa", null, (_, _) => AlternaPausa?.Invoke());
        menu.Items.AddRange(new ToolStripItem[]
        {
            voceApri,
            new ToolStripMenuItem("Sfondo successivo", null, (_, _) => Successivo?.Invoke()),
            vocePausa,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Apri cartella log", null, (_, _) => ApriLog?.Invoke()),
            new ToolStripMenuItem("Esci", null, (_, _) => Esci?.Invoke())
        });
        icona = new NotifyIcon
        {
            Icon = new Icon(immagine, SystemInformation.SmallIconSize),
            Text = NomeProgramma,
            ContextMenuStrip = menu,
            Visible = true
        };
        icona.MouseDoubleClick += (_, e) => { if (e.Button == MouseButtons.Left) Apri?.Invoke(); };
    }

    public void Aggiorna(string titolo, bool inPausa)
    {
        var testo = $"{NomeProgramma}\n{titolo}{(inPausa ? " (in pausa)" : "")}";
        icona.Text = testo.Length > 127 ? testo[..124] + "..." : testo;
        vocePausa.Text = inPausa ? "Riprendi" : "Pausa";
    }

    public void Dispose()
    {
        icona.Visible = false;
        icona.Dispose();
        menu.Dispose();
    }
}
```

- [ ] **Step 5: Finestra principale**

<!-- file: src/EnergyItaly.SfondoAnimato/FinestraPrincipale.cs -->
```csharp
using System.Drawing;
using EnergyItaly.SfondoAnimato.Core;
using Microsoft.Web.WebView2.Core;

namespace EnergyItaly.SfondoAnimato;

/// <summary>Finestra galleria (design B). Chiuderla la nasconde: il programma resta nell'area di notifica (D11).</summary>
sealed class FinestraPrincipale : Form
{
    static readonly Color Fondo = Color.FromArgb(0x07, 0x11, 0x1f);

    readonly ILog log;
    readonly HostWebView web;
    readonly Action<string> daApp;
    readonly Action<IReadOnlyList<string>> importa;
    bool inizializzata;
    bool chiusuraDefinitiva;

    public FinestraPrincipale(ILog log, Icon icona, Action<string> daApp, Action<IReadOnlyList<string>> importa)
    {
        this.log = log;
        this.daApp = daApp;
        this.importa = importa;
        web = new HostWebView(log);
        web.Messaggio += MessaggioRicevuto;
        Text = "Energy Italy Sfondo Animato";
        Icon = icona;
        BackColor = Fondo;
        StartPosition = FormStartPosition.CenterScreen;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int scuro = 1;
        Win32.DwmSetWindowAttribute(Handle, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE, ref scuro, sizeof(int));
        float k = DeviceDpi / 96f;
        var lavoro = Screen.FromHandle(Handle).WorkingArea;
        MinimumSize = new Size(Math.Min((int)(960 * k), lavoro.Width), Math.Min((int)(640 * k), lavoro.Height));
        Size = new Size(Math.Min((int)(1280 * k), (int)(lavoro.Width * 0.92)), Math.Min((int)(860 * k), (int)(lavoro.Height * 0.92)));
        CenterToScreen();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (inizializzata) return;
        inizializzata = true;
        try
        {
            await web.InizializzaAsync(Handle, ClientRectangle, scalaFissa: false, Fondo, $"https://{Catalogo.HostApp}/app/index.html");
            AggiornaWebView();
        }
        catch (Exception ex)
        {
            log.Errore($"Finestra principale: WebView2 non avviata ({ex.Message})");
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        AggiornaWebView();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        AggiornaWebView();
    }

    void AggiornaWebView()
    {
        var controller = web.Controller;
        if (controller is null) return;
        controller.Bounds = ClientRectangle;
        bool visibile = Visible && WindowState != FormWindowState.Minimized;
        if (controller.IsVisible != visibile) controller.IsVisible = visibile;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!chiusuraDefinitiva && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }

    public void MostraInPrimoPiano()
    {
        if (!Visible) Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    public void ChiudiDefinitivamente()
    {
        chiusuraDefinitiva = true;
        web.Dispose();
        Close();
    }

    public void Invia(string json)
    {
        try { web.Core?.PostWebMessageAsJson(json); }
        catch (Exception ex) { log.Avviso($"Messaggio alla finestra non inviato: {ex.Message}"); }
    }

    void MessaggioRicevuto(CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.WebMessageAsJson;
            switch (Messaggi.LeggiComandoApp(json)?.Tipo)
            {
                case "importaDialogo":
                    ApriDialogoImport();
                    break;
                case "importaFile":
                    var percorsi = e.AdditionalObjects?.OfType<CoreWebView2File>().Select(f => f.Path).ToList() ?? [];
                    importa(percorsi);
                    break;
                default:
                    daApp(json);
                    break;
            }
        }
        catch (Exception ex)
        {
            log.Errore($"Comando della finestra non gestito: {ex.Message}");
        }
    }

    void ApriDialogoImport()
    {
        using var dialogo = new OpenFileDialog
        {
            Title = "Importa sfondi",
            Multiselect = true,
            Filter = "Sfondi supportati|*.jpg;*.jpeg;*.png;*.webp;*.gif;*.bmp;*.mp4;*.webm;*.html;*.htm|" +
                     "Immagini|*.jpg;*.jpeg;*.png;*.webp;*.gif;*.bmp|Video|*.mp4;*.webm|Pagine web|*.html;*.htm"
        };
        if (dialogo.ShowDialog(this) == DialogResult.OK) importa(dialogo.FileNames);
    }
}
```

- [ ] **Step 6: Compilare**

Run: `"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" build src/EnergyItaly.SfondoAnimato/EnergyItaly.SfondoAnimato.csproj`
Expected: come nel Task 13, l'unico errore ammesso è la mancanza del punto d'ingresso (arriva nel Task 15).

- [ ] **Step 7: Commit**

```bash
git add src/EnergyItaly.SfondoAnimato/DesktopHost.cs src/EnergyItaly.SfondoAnimato/FinestraSfondo.cs src/EnergyItaly.SfondoAnimato/FinestraMessaggi.cs src/EnergyItaly.SfondoAnimato/LettoreMonitor.cs src/EnergyItaly.SfondoAnimato/CoperturaFinestre.cs src/EnergyItaly.SfondoAnimato/GestorePausa.cs src/EnergyItaly.SfondoAnimato/IconaNotifica.cs src/EnergyItaly.SfondoAnimato/FinestraPrincipale.cs
git commit -m "Aggiunge aggancio al desktop, finestre, icona e segnali di sistema"
```

---

### Task 15: Composizione del programma e prova sul PC di riferimento

**Files:**
- Create: `src/EnergyItaly.SfondoAnimato/Program.cs`, `src/EnergyItaly.SfondoAnimato/AppSfondo.cs`

**Interfaces:**
- Consumes: tutto il Core e i Task 13-14.
- Produces: `SfondoAnimato.exe` con argomento opzionale `--avvio` (avvio automatico senza finestra).

- [ ] **Step 1: Punto d'ingresso**

<!-- file: src/EnergyItaly.SfondoAnimato/Program.cs -->
```csharp
using Microsoft.Web.WebView2.Core;

namespace EnergyItaly.SfondoAnimato;

static class Program
{
    const string NomeMutex = @"Local\EnergyItaly.SfondoAnimato";
    const string NomeEventoApri = @"Local\EnergyItaly.SfondoAnimato.Apri";
    const string Titolo = "Energy Italy Sfondo Animato";

    [STAThread]
    static int Main(string[] args)
    {
        bool avvioAutomatico = args.Contains("--avvio", StringComparer.OrdinalIgnoreCase);
        using var mutex = new Mutex(true, NomeMutex, out bool primaIstanza);
        if (!primaIstanza)
        {
            // Una sola istanza (RF-42): la seconda chiede alla prima di aprire la finestra ed esce.
            if (!avvioAutomatico && EventWaitHandle.TryOpenExisting(NomeEventoApri, out var evento))
            {
                evento.Set();
                evento.Dispose();
            }
            return 0;
        }

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => RegistraErrore($"Errore non gestito: {e.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => RegistraErrore($"Errore non gestito: {e.ExceptionObject}");
        ApplicationConfiguration.Initialize();

        try
        {
            CoreWebView2Environment.GetAvailableBrowserVersionString();
        }
        catch (WebView2RuntimeNotFoundException)
        {
            RegistraErrore("WebView2 Runtime non installato");
            MessageBox.Show("Per mostrare lo sfondo animato serve Microsoft Edge WebView2 Runtime, che su questo PC non è installato.\n\nContatta l'assistenza IT.",
                Titolo, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        using var eventoApri = new EventWaitHandle(false, EventResetMode.AutoReset, NomeEventoApri);
        AppSfondo app;
        try
        {
            app = new AppSfondo(apriFinestra: !avvioAutomatico, eventoApri);
        }
        catch (Exception ex)
        {
            RegistraErrore($"Avvio non riuscito: {ex}");
            MessageBox.Show($"Il programma non è installato correttamente e non può partire.\n\n{ex.Message}\n\nContatta l'assistenza IT.",
                Titolo, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        Application.Run(app);
        return 0;
    }

    static void RegistraErrore(string testo)
    {
        try { new LogFile(Percorsi.Log).Errore(testo); }
        catch { /* niente log: non si può fare altro */ }
    }
}
```

- [ ] **Step 2: Composizione**

<!-- file: src/EnergyItaly.SfondoAnimato/AppSfondo.cs -->
```csharp
using System.Diagnostics;
using System.Drawing;
using EnergyItaly.SfondoAnimato.Core;
using Microsoft.Win32;

namespace EnergyItaly.SfondoAnimato;

/// <summary>
/// Compone il programma: coordinatore, finestra di sfondo (ricreata se Explorer si riavvia o la WebView
/// cade), finestra principale, icona nell'area di notifica e segnali di sistema.
/// </summary>
sealed class AppSfondo : ApplicationContext, IVistaPlayer, IVistaApp, ISistema
{
    const string NomeAvvio = "EnergyItaly.SfondoAnimato";
    static readonly TimeSpan IntervalloCrash = TimeSpan.FromMinutes(5);

    readonly SynchronizationContext contesto;
    readonly LogFile log;
    readonly DesktopHost desktop;
    readonly Icon iconaApp;
    readonly IconaNotifica icona;
    readonly FinestraMessaggi messaggi;
    readonly GestorePausa pausa;
    readonly Coordinatore coord;
    readonly System.Windows.Forms.Timer orologio = new() { Interval = 1000 };
    readonly System.Windows.Forms.Timer ritardoMonitor = new() { Interval = 600 };
    readonly System.Windows.Forms.Timer ricreazione = new() { Interval = 2000 };
    readonly Queue<DateTime> crash = new();
    readonly RegisteredWaitHandle attesaApri;
    FinestraSfondo? sfondo;
    FinestraPrincipale? principale;
    Rettangolo area = new(0, 0, 1920, 1080);
    bool sospeso;
    int tentativiAggancio;
    bool uscita;

    public AppSfondo(bool apriFinestra, EventWaitHandle eventoApri)
    {
        if (SynchronizationContext.Current is not WindowsFormsSynchronizationContext)
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        contesto = SynchronizationContext.Current!;

        log = new LogFile(Percorsi.Log);
        log.Info($"Avvio Energy Italy Sfondo Animato {Application.ProductVersion} su {Environment.OSVersion.VersionString}");
        desktop = new DesktopHost(log);
        iconaApp = CaricaIcona();
        icona = new IconaNotifica(iconaApp);

        var manifest = File.ReadAllText(Percorsi.Manifest);
        var predefinite = File.Exists(Percorsi.PredefiniteAziendali) ? File.ReadAllText(Percorsi.PredefiniteAziendali) : null;
        coord = new Coordinatore(manifest, predefinite, this, this, new ArchivioFile(), this, log, TimeProvider.System);

        icona.Apri += ApriFinestra;
        icona.Successivo += () => coord.Successivo();
        icona.AlternaPausa += () => coord.AlternaPausaManuale();
        icona.ApriLog += ApriCartellaLog;
        icona.Esci += Esci;

        messaggi = new FinestraMessaggi();
        messaggi.ExplorerRiavviato += () =>
        {
            log.Info("Explorer riavviato: riaggancio lo sfondo");
            ProgrammaRicreazione(1500);
        };
        pausa = new GestorePausa(messaggi);
        pausa.Cambiati += segnali => coord.AggiornaSegnali(segnali);

        SystemEvents.DisplaySettingsChanged += MonitorCambiati;
        ritardoMonitor.Tick += (_, _) =>
        {
            ritardoMonitor.Stop();
            coord.AggiornaMonitor(LettoreMonitor.Leggi());
        };
        ricreazione.Tick += (_, _) =>
        {
            ricreazione.Stop();
            _ = CreaSfondoAsync();
        };

        coord.Avvia(LettoreMonitor.Leggi());
        orologio.Tick += (_, _) => coord.Tick();
        orologio.Start();
        pausa.Valuta();
        _ = CreaSfondoAsync();
        if (apriFinestra) ApriFinestra();
        attesaApri = ThreadPool.RegisterWaitForSingleObject(eventoApri,
            (_, _) => contesto.Post(_ => ApriFinestra(), null), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    // ---------- Finestra di sfondo ----------

    async Task CreaSfondoAsync()
    {
        if (uscita) return;
        DistruggiSfondo(ridisegna: false);
        coord.PlayerRicreato();
        var finestra = new FinestraSfondo(log, desktop);
        finestra.MessaggioDalPlayer += json => coord.DaPlayer(json);
        finestra.Persa += crashWebView => contesto.Post(_ => SfondoPerso(finestra, crashWebView), null);
        sfondo = finestra;
        if (!finestra.Crea(area))
        {
            DistruggiSfondo(ridisegna: false);
            if (++tentativiAggancio <= 15) ProgrammaRicreazione(2000);
            else log.Errore("Impossibile agganciare lo sfondo al desktop dopo 15 tentativi");
            return;
        }
        tentativiAggancio = 0;
        try
        {
            await finestra.InizializzaWebAsync();
            if (sospeso && sfondo == finestra) finestra.ImpostaSospensione(true);
        }
        catch (Exception ex)
        {
            log.Errore($"WebView2 dello sfondo non avviata: {ex.Message}");
            SfondoPerso(finestra, crashWebView: true);
        }
    }

    void SfondoPerso(FinestraSfondo finestra, bool crashWebView)
    {
        if (uscita || finestra != sfondo) return;
        if (crashWebView)
        {
            var adesso = DateTime.UtcNow;
            crash.Enqueue(adesso);
            while (crash.Count > 0 && adesso - crash.Peek() > IntervalloCrash) crash.Dequeue();
            if (crash.Count >= 3)
            {
                log.Errore("Lo sfondo si è interrotto 3 volte in 5 minuti: resta spento fino al prossimo avvio");
                DistruggiSfondo(ridisegna: true);
                return;
            }
        }
        ProgrammaRicreazione(2000);
    }

    void ProgrammaRicreazione(int millisecondi)
    {
        ricreazione.Stop();
        ricreazione.Interval = millisecondi;
        ricreazione.Start();
    }

    void DistruggiSfondo(bool ridisegna)
    {
        var finestra = sfondo;
        sfondo = null;
        finestra?.Chiudi(ridisegna);
    }

    void MonitorCambiati(object? mittente, EventArgs e)
    {
        ritardoMonitor.Stop();
        ritardoMonitor.Start();
    }

    void IVistaPlayer.Invia(string json) => sfondo?.Invia(json);

    void IVistaPlayer.ImpostaArea(Rettangolo nuova)
    {
        area = nuova;
        sfondo?.ImpostaArea(nuova);
    }

    void IVistaPlayer.ImpostaSospensione(bool sospesa)
    {
        sospeso = sospesa;
        sfondo?.ImpostaSospensione(sospesa);
    }

    // ---------- Finestra principale ----------

    void IVistaApp.Invia(string json) => principale?.Invia(json);

    void ApriFinestra()
    {
        if (uscita) return;
        if (principale is null || principale.IsDisposed)
            principale = new FinestraPrincipale(log, iconaApp, json => coord.DaApp(json), percorsi => coord.ImportaFile(percorsi));
        principale.MostraInPrimoPiano();
    }

    // ---------- Servizi di sistema ----------

    public void ImpostaAvvioConWindows(bool attivo)
    {
#if DEBUG
        log.Info($"Avvio con Windows {(attivo ? "attivo" : "spento")}: ignorato nella build di sviluppo");
#else
        using var chiave = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (attivo) chiave.SetValue(NomeAvvio, $"\"{Environment.ProcessPath}\" --avvio");
        else chiave.DeleteValue(NomeAvvio, throwOnMissingValue: false);
#endif
    }

    public void ApriCartellaLog()
    {
        Directory.CreateDirectory(Percorsi.Log);
        Process.Start(new ProcessStartInfo { FileName = Percorsi.Log, UseShellExecute = true });
    }

    public void AggiornaIcona(string titolo, bool inPausa) => icona.Aggiorna(titolo, inPausa);

    static Icon CaricaIcona()
    {
        using var flusso = typeof(AppSfondo).Assembly.GetManifestResourceStream("icona.ico");
        return flusso is null ? (Icon)SystemIcons.Application.Clone() : new Icon(flusso);
    }

    // ---------- Uscita ----------

    void Esci()
    {
        if (uscita) return;
        uscita = true;
        log.Info("Uscita richiesta dall'utente");
        orologio.Stop();
        ricreazione.Stop();
        ritardoMonitor.Stop();
        attesaApri.Unregister(null);
        SystemEvents.DisplaySettingsChanged -= MonitorCambiati;
        pausa.Dispose();
        messaggi.Dispose();
        principale?.ChiudiDefinitivamente();
        DistruggiSfondo(ridisegna: true);
        icona.Dispose();
        ExitThread();
    }
}
```

- [ ] **Step 3: Compilare tutto e rieseguire i test**

Run:
```bash
DN="$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe"
"$DN" build SfondoAnimato.slnx
"$DN" test tests/EnergyItaly.SfondoAnimato.Core.Tests
```
Expected: build senza errori; tutti i test passano. Controllare che `src/EnergyItaly.SfondoAnimato/bin/Debug/net10.0-windows/web/` contenga `app/`, `player/`, `animazioni/` e non `sviluppo/`.

- [ ] **Step 4: Avviare il programma e controllare l'aggancio**

Run (in background):
```bash
"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" run --project src/EnergyItaly.SfondoAnimato
```
Poi leggere il log: `tail -n 40 "$LOCALAPPDATA/EnergyItaly/SfondoAnimato/log/sfondo.log"`.
Expected nel log: riga di avvio, `Monitor: DISPLAY1 1920x1080 in (-1920,7), DISPLAY2 1920x1080 in (0,0) principale; finestra 3840x1087 da (-1920,0); modo estendi`, riga `Aggancio al desktop: struttura ...`, `Primo sfondo: ...`, nessun `[ERRORE]`.

Catturare lo schermo per vedere sfondo e finestra principale (PowerShell, file nella scratchpad):
```powershell
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
$v = [System.Windows.Forms.SystemInformation]::VirtualScreen
$b = New-Object System.Drawing.Bitmap $v.Width, $v.Height
$g = [System.Drawing.Graphics]::FromImage($b)
$g.CopyFromScreen($v.Left, $v.Top, 0, 0, $b.Size)
$b.Save("$env:TEMP\claude\cattura-sfondo.png")
```
Aprire l'immagine con Read. Expected: finestra principale in primo piano come nel design B; dove il desktop è visibile si vede l'animazione dietro le icone. Se l'aggancio 24H2 non mostra lo sfondo (desktop statico visibile), provare nell'ordine: (a) senza `WS_EX_LAYERED`; (b) `SetWindowPos(finestra, workerW_figlio_di_Progman, ...)` per stare sopra il WorkerW; annotare nel log quale variante funziona e aggiornare `DesktopHost`.

- [ ] **Step 5: Prove manuali principali (PRD sezione 10 e spec sezione 12)**

Eseguire e annotare l'esito di ciascuna (chiedere all'utente di guardare il desktop dove serve):
- T1 scena unica 3840x1087, logo solo sul monitor sinistro, icone cliccabili;
- T2 "Sfondo successivo" su tutte e 5 le animazioni: dissolvenza pulita, nessun lampo bianco;
- T6 riavvio di Explorer (`taskkill /f /im explorer.exe` poi `start explorer.exe`, **solo con conferma dell'utente**): riaggancio entro pochi secondi, log `Explorer riavviato`;
- T7 finestra ingrandita su un solo monitor: la rotazione continua;
- T9 Win+L e sblocco (eseguito dall'utente): log `Pausa profonda: SessioneNonAttiva` e ripresa;
- T15 importare un'immagine, un video e una pagina HTML (pulsante e trascinamento): compaiono in "I miei" e si mostrano sui monitor;
- T16 video con fine riproduzione attiva e intervallo 1 minuto: resta fino alla fine;
- T17 Duplica: ogni monitor ha la scena intera con il suo logo;
- T18 intervallo 2 ore e valori non validi;
- T19 pagina HTML importata con `<img src="https://example.com/x.png">` e `fetch('https://example.com')`: log `Richiesta di rete bloccata verso https://example.com` (riga unica) e niente altro si rompe;
- T20 chiudere la finestra (resta l'icona), lanciare di nuovo l'eseguibile: si riapre la finestra della prima istanza;
- T21 riordino playlist con trascinamento e tastiera, ordine rispettato da "Sfondo successivo".
- "Esci" dal menu dell'icona: lo sfondo sparisce e torna lo sfondo statico di Windows;
- T11 risorse: per ogni animazione, dopo 2 minuti, sommare CPU e memoria di `SfondoAnimato` e dei processi `msedgewebview2` (`Get-Process`), con la finestra principale chiusa; annotare i valori (soglia RNF-01: CPU media sotto 5%, memoria sotto 300 MB).

Correggere ogni problema trovato con il ciclo di debug (superpowers:systematic-debugging), con test nel Core quando il difetto è nella logica pura.

- [ ] **Step 6: Commit**

```bash
git add src/EnergyItaly.SfondoAnimato/Program.cs src/EnergyItaly.SfondoAnimato/AppSfondo.cs
git commit -m "Compone il programma: sfondo, finestra galleria, icona e pause automatiche"
```

---

### Task 16: Pubblicazione, installer e istruzioni

**Files:**
- Create: `installer/SfondoAnimato.iss`, `README.md`

**Interfaces:**
- Consumes: progetto compilabile del Task 15.
- Produces: `publish/` (eseguibile singolo + `web/` + `impostazioni-predefinite.json`) e `installer/Output/EnergyItaly-SfondoAnimato-1.0.0-setup.exe`.

- [ ] **Step 1: Pubblicare**

Run:
```bash
"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" publish src/EnergyItaly.SfondoAnimato/EnergyItaly.SfondoAnimato.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
ls publish publish/web
```
Expected: `publish/SfondoAnimato.exe`, `publish/impostazioni-predefinite.json`, `publish/web/{app,player,animazioni}`; nessuna cartella `sviluppo`.

- [ ] **Step 2: Script dell'installer**

<!-- file: installer/SfondoAnimato.iss -->
```iss
; Installer per utente di Energy Italy Sfondo Animato (PRD sezione 9). Senza diritti di amministratore.
; Compilazione: ISCC.exe installer\SfondoAnimato.iss (dopo dotnet publish in .\publish)
; Installazione silenziosa per la distribuzione: setup.exe /VERYSILENT /SUPPRESSMSGBOXES

#define NomeApp "Energy Italy Sfondo Animato"
#define Versione "1.0.0"
#define Eseguibile "SfondoAnimato.exe"

[Setup]
AppId={{6B2E7C1A-4F0D-4C55-9E7B-3A1D2F8C9E10}
AppName={#NomeApp}
AppVersion={#Versione}
AppPublisher=Energy Italy SpA
DefaultDirName={localappdata}\Programs\{#NomeApp}
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=Output
OutputBaseFilename=EnergyItaly-SfondoAnimato-{#Versione}-setup
SetupIconFile=..\src\EnergyItaly.SfondoAnimato\risorse\icona.ico
UninstallDisplayIcon={app}\{#Eseguibile}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "it"; MessagesFile: "compiler:Languages\Italian.isl"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{userprograms}\{#NomeApp}"; Filename: "{app}\{#Eseguibile}"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "EnergyItaly.SfondoAnimato"; ValueData: """{app}\{#Eseguibile}"" --avvio"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#Eseguibile}"; Description: "Avvia {#NomeApp}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\{#Eseguibile}"; Parameters: "--avvio"; Flags: nowait skipifnotsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/IM {#Eseguibile} /F"; Flags: runhidden; RunOnceId: "ChiudiProgramma"

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\EnergyItaly\SfondoAnimato"
Type: filesandordirs; Name: "{localappdata}\EnergyItaly\SfondoAnimato"

[Code]
const
  ChiaveWebView2 = 'Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';

function VersioneValida(const Valore: String): Boolean;
begin
  Result := (Valore <> '') and (Valore <> '0.0.0.0');
end;

function WebView2Presente(): Boolean;
var
  Versione: String;
begin
  Result :=
    (RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\' + ChiaveWebView2, 'pv', Versione) and VersioneValida(Versione)) or
    (RegQueryStringValue(HKLM, 'SOFTWARE\' + ChiaveWebView2, 'pv', Versione) and VersioneValida(Versione)) or
    (RegQueryStringValue(HKCU, 'Software\' + ChiaveWebView2, 'pv', Versione) and VersioneValida(Versione));
end;

// Su Windows 10 senza WebView2 scarica ed esegue il bootstrapper Evergreen di Microsoft.
procedure CurStepChanged(CurStep: TSetupStep);
var
  Codice: Integer;
begin
  if (CurStep = ssPostInstall) and not WebView2Presente() then
  begin
    try
      DownloadTemporaryFile('https://go.microsoft.com/fwlink/p/?LinkId=2124703', 'MicrosoftEdgeWebview2Setup.exe', '', nil);
      Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, Codice);
    except
      Log('WebView2 non installato: ' + GetExceptionMessage);
    end;
  end;
end;
```

- [ ] **Step 3: Installare Inno Setup (per utente) e compilare l'installer**

Run:
```bash
winget install --id JRSoftware.InnoSetup -e --scope user --accept-package-agreements --accept-source-agreements
"$LOCALAPPDATA/Programs/Inno Setup 6/ISCC.exe" installer/SfondoAnimato.iss
```
Expected: `installer/Output/EnergyItaly-SfondoAnimato-1.0.0-setup.exe`. Se `winget --scope user` non è supportato dal pacchetto, ripetere senza `--scope user` (il programma di installazione di Inno Setup chiede se installare solo per l'utente) e cercare `ISCC.exe` anche in `C:\Program Files (x86)\Inno Setup 6\`.

- [ ] **Step 4: Istruzioni**

<!-- file: README.md -->
~~~markdown
# Energy Italy Sfondo Animato

Programma Windows che mostra sfondi animati Energy Italy sul desktop, estesi o duplicati su tutti i monitor, con rotazione automatica, pausa automatica e una finestra galleria per scegliere gli sfondi e importarne di propri.

- Requisiti: [docs/PRD.md](docs/PRD.md) e [spec](docs/superpowers/specs/2026-10-13-sfondo-animato-design.md)
- Design della finestra: [docs/design/riferimento-vetrina.dc.html](docs/design/riferimento-vetrina.dc.html)

## Struttura

- `animazioni/`: le animazioni HTML aziendali e `manifest.json` (copiate in `web/animazioni` in fase di build)
- `src/EnergyItaly.SfondoAnimato.Core/`: logica pura (layout, rotazione, pause, impostazioni, libreria, messaggi)
- `src/EnergyItaly.SfondoAnimato/`: programma WinForms + WebView2, pagine `web/app` (galleria) e `web/player`
- `tests/`: test xUnit della logica pura
- `installer/`: script Inno Setup

## Sviluppo

L'SDK .NET 10 è installato per utente in `%LOCALAPPDATA%\Microsoft\dotnet`.

```bash
"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" test tests/EnergyItaly.SfondoAnimato.Core.Tests
"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" run --project src/EnergyItaly.SfondoAnimato
```

Prove delle pagine web nel browser: `python strumenti/server_sviluppo.py` e poi `http://127.0.0.1:5599/sviluppo/prova-app.html` o `/sviluppo/prova-player.html`.

## Pubblicazione e installer

```bash
"$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe" publish src/EnergyItaly.SfondoAnimato/EnergyItaly.SfondoAnimato.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
"$LOCALAPPDATA/Programs/Inno Setup 6/ISCC.exe" installer/SfondoAnimato.iss
```

Installazione silenziosa sui PC aziendali: `EnergyItaly-SfondoAnimato-1.0.0-setup.exe /VERYSILENT /SUPPRESSMSGBOXES`.

Le impostazioni predefinite aziendali stanno in `impostazioni-predefinite.json` accanto all'eseguibile.

## Dove sono i dati sul PC

- Impostazioni e libreria: `%APPDATA%\EnergyItaly\SfondoAnimato\`
- Log: `%LOCALAPPDATA%\EnergyItaly\SfondoAnimato\log\`
~~~

- [ ] **Step 5: Commit**

```bash
git add installer/SfondoAnimato.iss README.md
git commit -m "Aggiunge l'installer per utente e le istruzioni di build"
```

Nota: non installare il setup sul PC dell'utente senza chiederglielo (aggiunge l'avvio automatico); la prova T13 si fa con la sua conferma o su un PC di test.

---

### Task 17: Verifica finale

**Files:**
- Modify: eventuali correzioni emerse.

- [ ] **Step 1: Test e build pulite**

Run:
```bash
DN="$LOCALAPPDATA/Microsoft/dotnet/dotnet.exe"
"$DN" build SfondoAnimato.slnx -c Release -warnaserror
"$DN" test tests/EnergyItaly.SfondoAnimato.Core.Tests
```
Expected: nessun avviso trattato come errore, tutti i test passano. Se `-warnaserror` segnala avvisi dei pacchetti esterni, correggere quelli del nostro codice e annotare gli altri.

- [ ] **Step 2: Niente trattini lunghi**

Run:
```bash
python -I -c "import pathlib;[print(p) for p in pathlib.Path('.').rglob('*') if p.is_file() and not set(p.parts)&{'.git','bin','obj','publish','Output'} and ('\u2014' in p.read_text(encoding='utf-8',errors='ignore') or '\u2013' in p.read_text(encoding='utf-8',errors='ignore'))]"
```
Expected: nessun file elencato.

- [ ] **Step 3: Revisione dell'intero ramo**

Richiedere una revisione del codice (superpowers:requesting-code-review) sull'intera serie di commit, correggere i problemi confermati e rieseguire gli Step 1-2.

- [ ] **Step 4: Riepilogo per l'utente**

Elencare: cosa funziona e come è stato verificato, prove manuali eseguite con esito, prove che richiedono l'utente (Win+L, riavvio Explorer, installazione, PC Windows 10), problemi noti.
