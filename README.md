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

Le impostazioni predefinite aziendali stanno in `impostazioni-predefinite.json` accanto all'eseguibile. Ogni aggiornamento lo sostituisce con quello del repository: i valori predefiniti si cambiano qui, non a mano sui PC.

## Pubblicare una versione (aggiornamento automatico)

```bash
powershell -File strumenti/nuova-versione.ps1 1.1.0
```

Lo script porta `<Version>` di `Directory.Build.props` al nuovo numero, fa il commit, crea il tag `v1.1.0` e invia tutto. Il workflow `.github/workflows/rilascio.yml` esegue i test, compila, crea l'installer e pubblica la release con l'installer e `versione.json` (avanzamento nella scheda Actions, risultato in Releases).

I PC con il programma installato e "Aggiornamenti automatici" attivo controllano ogni 6 ore (il primo controllo tra 2 e 30 minuti dopo l'avvio), scaricano la nuova versione e la installano quando l'utente non guarda il desktop, oppure al successivo accesso a Windows. Con l'opzione spenta si aggiorna da Impostazioni con "Controlla ora" e "Installa ora". Dettagli e modello di fiducia: `docs/superpowers/specs/2026-10-14-aggiornamento-automatico-design.md`.

- La prima versione con l'aggiornamento automatico (1.1.0) va installata a mano una volta: le copie 1.0.0 non sanno aggiornarsi.
- Il repository deve essere pubblico, altrimenti i PC non possono scaricare le release.
- Prova ogni versione su un PC prima di pubblicarne un'altra: una versione che non parte arriva su tutti i PC e ne ferma anche gli aggiornamenti successivi.
- Solo la copia installata (`%LOCALAPPDATA%\Programs\Energy Italy Sfondo Animato`) si aggiorna; una copia avviata da `publish` dice soltanto se c'è una versione nuova.

## Dove sono i dati sul PC

- Impostazioni e libreria: `%APPDATA%\EnergyItaly\SfondoAnimato\`
- Log: `%LOCALAPPDATA%\EnergyItaly\SfondoAnimato\log\`
- Aggiornamenti scaricati, `stato.json` e registro dell'installer: `%LOCALAPPDATA%\EnergyItaly\SfondoAnimato\aggiornamenti\`
