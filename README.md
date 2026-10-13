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
