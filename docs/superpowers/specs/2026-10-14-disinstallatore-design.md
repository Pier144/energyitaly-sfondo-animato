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
| U2 | Impostazioni › App lancia `unins000.exe /SILENT /GRAFICA=1`: l'installer riscrive `UninstallString` nella chiave di disinstallazione in `ssPostInstall` (dopo che Inno l'ha scritta). | La conferma e il messaggio finale di Inno compaiono solo senza `/SILENT` (verificato nel sorgente di Inno, `Setup.Uninstall.pas`) e non si possono togliere in altro modo. `/GRAFICA=1` distingue questo caso da `/SILENT` lanciato dall'IT (`QuietUninstallString`), che resta senza finestre nostre. |
| U3 | Casella "Conserva impostazioni e sfondi importati", **non spuntata** all'apertura. | Senza toccarla il risultato è quello di oggi (si cancella tutto). |
| U4 | La cartella dati (`%APPDATA%\EnergyItaly\SfondoAnimato`) si cancella nel `[Code]` (`usPostUninstall`), non in `[UninstallDelete]`. | Le condizioni di `[UninstallDelete]` si valutano all'installazione, non alla disinstallazione. |
| U6 | Con "Conserva" la cartella dati viene spostata in `SfondoAnimato.conserva` in `usUninstall` (dopo aver chiuso il programma) e rimessa al suo posto in `usPostUninstall`. | Inno aggiunge le istruzioni di ogni aggiornamento a quelle vecchie in `unins000.dat`: sui PC aggiornati dalla 1.0-1.2 la vecchia riga `[UninstallDelete]` dei dati c'è ancora e li cancellerebbe (verificato sul PC). |
| U7 | Serve Inno Setup 6.7 o successivo, e il ripiego `choco` della CI passa da 6.4.3 a 6.7.1. | La 6.7 ha cambiato `CreateCustomForm` (ora con le dimensioni) e tolto `SizeAndCenterOnShow`. Inoltre sul PC resta il disinstallatore della versione di Inno più recente fra quelle installate, che usa solo il `[Code]` compilato con la sua versione: una release compilata con un Inno più vecchio della 1.2.0 (6.7.1, quella del runner GitHub) farebbe partire `/SILENT /GRAFICA=1` con il codice vecchio, cioè una disinstallazione senza conferma. |
| U5 | Logo in bitmap 24 bit già composto sul fondo `#07111f`, cinque misure (100/125/150/175/200%, si usa la più vicina senza ridimensionare), installati in `{app}\disinstallazione` e caricati in memoria all'inizio della disinstallazione. | Bitmap: niente dipendenza dal supporto PNG della versione di Inno; il disinstallatore non può estrarre file dal setup, quindi le immagini devono essere già sul disco. |

## 3. Schermate

Comuni: fondo `#07111f`, barra del titolo scura (`DwmSetWindowAttribute`, `DWMWA_USE_IMMERSIVE_DARK_MODE`, come la finestra principale), titolo finestra "Disinstalla Energy Italy Sfondo Animato", logo bianco in alto a sinistra largo 128 DIP, testi Segoe UI: titoli `#e8eef6` grassetto, testo `#a9bbcf`, note `#5d7593`. Larghezza 480 DIP, centrata sullo schermo, non ridimensionabile. Le misure seguono la scala DPI di sistema (`GetDpiForSystem`; `ScaleX`/`ScaleY` scalano sul font di dialogo, non sui DPI).

Pulsanti a pillola alti 40 DIP (pannelli colorati con regione arrotondata, cursore a mano), come quelli del programma: principale fondo `#7AB52A` testo `#07111f`; secondario a contorno `#22395a` su fondo `#07111f`, testo `#e8eef6` (`.bottone-contorno`). Niente effetto al passaggio del mouse: negli script di Inno `TPanel` non ha eventi del mouse. Da tastiera si usano Invio ed Esc; i pulsanti non ricevono il fuoco e l'Assistente vocale non li legge come pulsanti.

### 3.1 Conferma (in `InitializeUninstall`)

- Titolo: "Disinstallare Sfondo Animato?"
- Testo: "Lo sfondo animato verrà fermato e il programma rimosso dal PC."
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
| Installazioni precedenti alla 1.3.0 | Il comando nuovo arriva con il primo aggiornamento automatico (che riesegue `ssPostInstall`); "Conserva" funziona anche con le vecchie istruzioni di cancellazione (U6) |
| Bitmap mancanti | Le schermate compaiono senza logo |

## 6. Verifica

- `strumenti/genera_logo_installer.py` produce i cinque bitmap; controllo visivo.
- Compilazione con Inno Setup 6.7.3 (la CI usa la 6.7.1 del runner).
- Provato sul PC (2026-10-14), partendo dalla release 1.2.0 pubblicata e aggiornando alla build nuova:
  - `UninstallString` diventa `unins000.exe /SILENT /GRAFICA=1`;
  - Esc sulla conferma: annullato, nulla toccato;
  - "Conserva" spuntata: programma rimosso, `impostazioni.json` e `libreria` rimasti; schermata finale con il testo giusto;
  - senza "Conserva" (Invio): programma, dati e cartelle `EnergyItaly` rimossi; chiave Run tolta;
  - `/VERYSILENT` e `/VERYSILENT /CONSERVA=1`: nessuna finestra, dati cancellati o conservati.
