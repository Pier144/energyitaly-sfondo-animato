# Spec - Disinstallatore con grafica Energy Italy

- Data: 2026-10-14
- Riguarda: `installer/SfondoAnimato.iss`, nuovo `strumenti/genera_logo_installer.py`, bitmap in `installer/disinstallazione/`
- Modifica il PRD sezione 9 (disinstallazione pulita): i dati dell'utente si possono conservare.

## 1. Problema

Il disinstallatore di Inno Setup funziona, ma mostra le finestre grigie standard ("Sei sicuro di voler rimuovere completamente...?" e "... è stato rimosso con successo") e cancella sempre tutto, impostazioni e sfondi importati compresi. L'utente vuole un disinstallatore con l'aspetto Energy Italy.

## 2. Decisioni

| # | Decisione | Motivo |
|---|---|---|
| U1 | Le schermate sono finestre di Inno Setup (`[Code]`) colorate a mano, non una pagina WebView del programma. | Un solo processo: il programma non deve cancellare se stesso, Windows aggiorna l'elenco app alla fine, la disinstallazione silenziosa resta com'è. Una pagina WebView sarebbe identica all'app ma il programma dovrebbe chiudersi prima di essere cancellato (niente schermata finale) e servirebbe molto più codice. |
| U2 | Impostazioni › App lancia `unins000.exe /SILENT /GRAFICA=1`: l'installer riscrive `UninstallString` nella chiave di disinstallazione in `ssPostInstall` (dopo che Inno l'ha scritta). | La conferma e il messaggio finale di Inno compaiono solo senza `/SILENT` (verificato nel sorgente 6.4.3, `Setup.Uninstall.pas`) e non si possono togliere in altro modo. `/GRAFICA=1` distingue questo caso da `/SILENT` lanciato dall'IT (`QuietUninstallString`), che resta senza finestre nostre. |
| U3 | Casella "Conserva impostazioni e sfondi importati", **non spuntata** all'apertura. | Senza toccarla il risultato è quello di oggi (si cancella tutto). |
| U4 | La cartella dati (`%APPDATA%\EnergyItaly\SfondoAnimato`) si cancella nel `[Code]` (`usPostUninstall`), non in `[UninstallDelete]`. | Le condizioni di `[UninstallDelete]` si valutano all'installazione, non alla disinstallazione. |
| U5 | Logo in bitmap 32 bit con trasparenza, tre misure (100/150/200%), installati in `{app}\installer` e caricati in memoria all'inizio della disinstallazione. | Inno 6.4 non legge PNG né SVG; il disinstallatore non può estrarre file dal setup, quindi le immagini devono essere già sul disco. |

## 3. Schermate

Comuni: fondo `#07111f`, barra del titolo scura (`DwmSetWindowAttribute`, `DWMWA_USE_IMMERSIVE_DARK_MODE`, come la finestra principale), titolo finestra "Disinstalla Energy Italy Sfondo Animato", logo bianco in alto a sinistra largo 128 DIP, testi Segoe UI: titoli `#e8eef6` grassetto, testo `#a9bbcf`, note `#5d7593`. Larghezza 480 DIP, centrata sullo schermo, non ridimensionabile. Le misure seguono la scala DPI (`ScaleX`/`ScaleY`).

Pulsanti piatti (pannelli colorati, cursore a mano): principale fondo `#7AB52A` testo `#07111f`; secondario fondo `#0f2440` testo `#e8eef6`. Al passaggio del mouse principale `#b2e068`, secondario `#13243c`.

### 3.1 Conferma (in `InitializeUninstall`)

- Titolo: "Disinstallare Sfondo Animato?"
- Testo: "Lo sfondo animato verrà fermato e il programma rimosso da questo PC."
- Casella + etichetta cliccabile: "Conserva impostazioni e sfondi importati"; nota sotto: "Utile se pensi di reinstallarlo."
- Pulsanti in basso a destra: "Annulla" (secondario), "Disinstalla" (principale).
- Tastiera: Esc = Annulla, Invio = Disinstalla. Chiudere la finestra = Annulla.
- Annulla: `InitializeUninstall` restituisce False, non si tocca nulla.

### 3.2 Avanzamento (in `InitializeUninstallProgressForm`)

La finestra di avanzamento di Inno, ridipinta: stessi colori, logo, "Disinstallazione in corso..." al posto dell'intestazione bianca, barra di avanzamento di Windows; nascosti intestazione, linee, etichetta di stato e pulsante Annulla (che Inno tiene comunque disattivato).

### 3.3 Completata (in `usPostUninstall`, dopo la cancellazione dei dati)

- Titolo: "Sfondo Animato è stato disinstallato"
- Testo, secondo la scelta: "Impostazioni e sfondi importati sono stati conservati: li ritrovi se lo reinstalli." oppure "Sono state cancellate anche le impostazioni e gli sfondi importati."
- Se la cartella del programma esiste ancora: "Alcuni file non sono stati rimossi. Puoi cancellare a mano la cartella:" + percorso.
- Pulsante "Chiudi" (principale). Invio ed Esc chiudono.

## 4. Dati

| Cosa | Dove | Cancellato |
|---|---|---|
| Log, dati WebView2, aggiornamenti scaricati | `%LOCALAPPDATA%\EnergyItaly\SfondoAnimato` | Sempre (`[UninstallDelete]`, come oggi) |
| `impostazioni.json`, `libreria.json`, `libreria\` | `%APPDATA%\EnergyItaly\SfondoAnimato` | Se la casella non è spuntata, o senza interfaccia salvo `/CONSERVA=1` |

Dopo la cancellazione si toglie anche `EnergyItaly` se resta vuota (in entrambe le cartelle base).

## 5. Casi

| Caso | Comportamento |
|---|---|
| Impostazioni › App › Disinstalla | Schermate 3.1-3.3 |
| `unins000.exe /VERYSILENT` o `/SILENT` (IT) | Nessuna finestra nostra; dati cancellati come oggi (`/CONSERVA=1` per tenerli) |
| `unins000.exe` aperto a mano | Finestre standard di Inno; dati cancellati |
| Installazioni precedenti alla 1.3.0 | Il comando nuovo arriva con il primo aggiornamento automatico (che riesegue `ssPostInstall`) |
| Bitmap mancanti | Le schermate compaiono senza logo |

## 6. Verifica

- `strumenti/genera_logo_installer.py` produce i tre bitmap; controllo visivo.
- Compilazione con Inno Setup 6.4.3 (stessa versione della CI).
- Sul PC: installazione, `UninstallString` nel registro, disinstallazione da Impostazioni con e senza "Conserva", Annulla, Esc; controllo delle cartelle dati dopo ciascun caso; disinstallazione `/VERYSILENT`.
