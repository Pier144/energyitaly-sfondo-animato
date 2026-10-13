# Spec - Energy Italy Sfondo Animato (MVP con finestra galleria)

- Data: 13/10/2026
- Base: `docs/PRD.md` (bozza 1). Questa spec **prevale sul PRD** dove i due differiscono; tutto ciò che qui non è detto resta come nel PRD.
- Design di riferimento: direzione **B "Vetrina"** del canvas Claude Design "Energy Italy Sfondo Animato - Galleria". Copia locale del sorgente: `docs/design/riferimento-vetrina.dc.html` (finestra) e `docs/design/riferimento-anteprima.dc.html` (miniature).

## 1. Obiettivo

Programma Windows standalone, simile a Lively Wallpaper e Wallpaper Engine, distribuito dall'IT sui PC aziendali. Mostra sul desktop sfondi animati estesi su tutti i monitor (o duplicati su ciascuno), li fa ruotare da solo e offre una **finestra principale** per scegliere e ordinare gli sfondi, importarne di propri e cambiare le impostazioni.

Criterio di successo: quello del PRD (sezione 1) più i test nuovi della sezione 12 di questa spec.

## 2. Decisioni prese nel brainstorming

| # | Decisione | Differenza dal PRD |
|---|---|---|
| D1 | La finestra principale con galleria entra nell'MVP, nello stile del design B | Il PRD la metteva in fase 2 |
| D2 | La finestra la usano i dipendenti liberamente; l'IT fissa solo i predefiniti (`impostazioni-predefinite.json`) | Nessuna |
| D3 | L'utente può importare **immagini, video e pagine HTML** proprie | Nuovo |
| D4 | Intervallo di cambio **scritto dall'utente**: numero intero + unità (minuti 1-1440, ore 1-24), oppure "Mai" | Il PRD aveva 5/15/30/60/Mai |
| D5 | Opzione "I video cambiano a fine riproduzione" (predefinita attiva): un video resta finché non finisce, a prescindere dall'intervallo | Nuovo |
| D6 | Monitor rilevati in automatico; con più monitor l'utente sceglie **Estendi** (un'unica scena) o **Duplica** (la stessa scena intera su ogni monitor, ognuna col suo logo) | Il PRD aveva solo Estendi |
| D7 | La "rotazione" diventa una **playlist ordinata** (riordinabile trascinando); deve contenere almeno uno sfondo | Il PRD aveva una lista di caselle |
| D8 | **Pausa manuale** ferma anche il conto alla rovescia; le **pause automatiche** no (RF-23 resta valido per quelle) | Chiarimento |
| D9 | "Precedente" torna allo sfondo mostrato prima (storico, massimo 20), non a quello prima in playlist | Nuovo |
| D10 | Barra del titolo **nativa di Windows** in tema scuro; l'intestazione HTML non disegna i pulsanti della finestra | Il design B li disegnava in HTML |
| D11 | Chiudere la finestra la nasconde: il programma resta nell'area di notifica. Si esce solo da "Esci" nel menu dell'icona | Nuovo |
| D12 | Sfondo statico di riserva (RF-07) rimandato a una fase successiva | Era "S" nel PRD |

## 3. Architettura

Un solo processo .NET 10 WinForms, due finestre con WebView2 che condividono lo stesso `CoreWebView2Environment` (un solo browser process, meno memoria):

1. **Finestra di sfondo** (`FinestraSfondo`): senza bordi, agganciata dietro le icone, estesa sull'intero schermo virtuale. Carica `player/player.html`.
2. **Finestra principale** (`FinestraPrincipale`): finestra normale ridimensionabile. Carica `app/index.html`.

Il C# è la **fonte di verità** dello stato (impostazioni, libreria, playlist, sfondo corrente, pausa, scadenza). Le due pagine web sono visualizzatori: ricevono lo stato e mandano comandi.

```
                 +-------------------------------------------+
                 |        Processo SfondoAnimato.exe         |
                 |                                           |
  Win32 eventi ->|  GestorePausa   DesktopHost   Monitor     |
                 |        \            |          /         |
                 |         +------ Coordinatore ---+         |
                 |        /        (stato unico)    \        |
                 |  FinestraSfondo                FinestraPrincipale
                 |   WebView2: player.html         WebView2: app/index.html
                 |  IconaNotifica   Impostazioni   Libreria  |
                 +-------------------------------------------+
```

### 3.1 Host virtuali e cartelle

| Host virtuale | Cartella | Contenuto |
|---|---|---|
| `https://sfondo.energyitaly/` | `web/` accanto all'eseguibile | `app/`, `player/`, `animazioni/` (copiata in build da `animazioni/` del repo) |
| `https://libreria.energyitaly/` | `%APPDATA%\EnergyItaly\SfondoAnimato\libreria\` | immagini e video importati dall'utente |
| `https://pagine.energyitaly/` | nessuna (servito dal programma) | pagine HTML importate, sempre con la Content-Security-Policy senza rete |

Origini diverse: una pagina HTML importata non può leggere né toccare il player o l'app. Le pagine HTML hanno un host non mappato su cartella perché WebView2 non lascia intercettare le richieste agli host mappati: solo così il programma può aggiungere la CSP a ogni pagina. `libreria.json` sta in `%APPDATA%\EnergyItaly\SfondoAnimato\`, fuori dalla cartella servita.

**Isolamento di rete (entrambe le WebView):** un filtro `WebResourceRequested` su `*` lascia passare solo i due host virtuali (e gli schemi `data:` e `blob:`); ogni altra richiesta riceve una risposta 403 e il log registra l'host bloccato (una riga per host per sessione). Le animazioni aziendali sono già offline; per le pagine importate questo realizza la promessa "senza rete".

Altri percorsi:

- Impostazioni: `%APPDATA%\EnergyItaly\SfondoAnimato\impostazioni.json`
- Libreria utente: `%APPDATA%\EnergyItaly\SfondoAnimato\libreria\libreria.json` + i file
- Log: `%LOCALAPPDATA%\EnergyItaly\SfondoAnimato\log\` (5 file da 1 MB)
- Dati WebView2: `%LOCALAPPDATA%\EnergyItaly\SfondoAnimato\WebView2\`

### 3.2 Progetti

```
EnergyItaly_Sfondo_Animato/
  SfondoAnimato.slnx
  animazioni/                                (già presente, invariata)
  src/EnergyItaly.SfondoAnimato.Core/        (net10.0, logica pura, nessuna dipendenza Windows)
  src/EnergyItaly.SfondoAnimato/             (net10.0-windows, WinForms + WebView2 + Win32)
    web/app/                                 (index.html, app.css, app.js)
    web/player/                              (player.html, player.js)
    web/sviluppo/                            (pagine di prova da browser, escluse dal publish)
  tests/EnergyItaly.SfondoAnimato.Core.Tests/ (xUnit)
  installer/SfondoAnimato.iss                (Inno Setup)
```

## 4. Componenti

### 4.1 Core (logica pura, testata con TDD)

| Componente | Compito | Interfaccia essenziale |
|---|---|---|
| `LayoutMonitor` | Dai rettangoli fisici dei monitor calcola il bounding box (origine anche negativa), i rettangoli relativi alla finestra, la stringa `monitor=` e le "scene" per modo: Estendi = 1 scena con tutti i rettangoli; Duplica = 1 scena per monitor. | `Calcola(IReadOnlyList<Monitor>, ModoMonitor) -> Disposizione` |
| `Catalogo` | Unisce `manifest.json` (aziendali, sola lettura) e `libreria.json` (utente) in un elenco di `Voce`. | `Carica(manifestJson, libreriaJson) -> IReadOnlyList<Voce>` |
| `Importazione` | Riconosce il tipo da estensione, controlla i limiti, genera id e nome file sicuri e univoci. | `Valuta(nomeFile, dimensione) -> EsitoImport` |
| `Rotazione` | Decide il prossimo e il precedente, tiene lo storico, calcola la permanenza di una voce e la scadenza. Usa `TimeProvider` (testabile). | vedi 6.1 |
| `RegolePausa` | Dai segnali di sistema decide livello (Nessuna, Leggera, Profonda) e motivo. | `Valuta(SegnaliPausa) -> DecisionePausa` |
| `Impostazioni` | Modello v2, valori predefiniti, validazione e correzione, serializzazione JSON. | `Leggi(json, predefinite) -> (Impostazioni, avvisi)`, `Scrivi(Impostazioni) -> json` |
| `Messaggi` | Tipi dei messaggi JSON tra host, app e player (sezione 7) e loro serializzazione. | record + `JsonSerializerOptions` comuni |

### 4.2 App Windows (verificata a mano)

| Componente | Compito |
|---|---|
| `Program` | Avvio, mutex con nome (una sola istanza). Il secondo avvio segnala al primo di aprire la finestra (`EventWaitHandle` con nome) ed esce. Argomento `--avvio` (usato dalla voce Run): non apre la finestra. |
| `Coordinatore` | Tiene lo stato unico, applica i comandi, chiama Rotazione e RegolePausa, salva impostazioni e libreria, manda lo stato a player e app. Unico timer (1 s) per la scadenza. |
| `DesktopHost` | Come PRD (aggancio pre e post 24H2, `TaskbarCreated`, ridisegno all'uscita). |
| `FinestraSfondo` | Form senza bordi sullo schermo virtuale; WebView2 con `RasterizationScale = 1` e rilevamento scala disattivato; sfondo `#0f1e35`; niente menu contestuale, zoom, tasti acceleratori e DevTools. Pausa profonda: `IsVisible = false` + `TrySuspendAsync`. |
| `FinestraPrincipale` | Form ridimensionabile (predefinita 1280x860, minima 960x640, centrata), barra del titolo scura (`DWMWA_USE_IMMERSIVE_DARK_MODE`), icona Energy Italy. Chiusura = nasconde. Quando è nascosta o ridotta a icona la WebView va in `IsVisible = false`. |
| `MonitorWatcher` | Legge i monitor (`EnumDisplayMonitors` + `GetMonitorInfo`, nome DISPLAYn, principale) all'avvio e a `WM_DISPLAYCHANGE` / `WM_DPICHANGED` / `WM_SETTINGCHANGE`; notifica il Coordinatore. |
| `GestorePausa` | Raccoglie i segnali (sessione, alimentazione, `GUID_CONSOLE_DISPLAY_STATE`, `SHQueryUserNotificationState`, copertura del desktop per monitor) e li passa a `RegolePausa`. |
| `ArchivioLibreria` | Copia i file importati nella cartella libreria, aggiorna `libreria.json`, cancella file e voci. |
| `IconaNotifica` | Menu: **Apri Energy Italy Sfondo Animato** (predefinita, anche con doppio clic), Sfondo successivo, Pausa / Riprendi, separatore, Apri cartella log, Esci. Suggerimento = titolo dello sfondo corrente (RF-41). |
| `Log` | Log a rotazione, in italiano. |

## 5. Pagine web

### 5.1 Player (`web/player/`)

Evoluzione del vecchio `anteprima.html` (cancellato su richiesta, la sua logica è descritta qui).

- Riceve `mostra` con la voce da mostrare, il modo e i rettangoli delle scene. Crea un nuovo **strato** sopra il vecchio:
  - **Estendi:** un elemento che copre tutta la finestra.
  - **Duplica:** un elemento per monitor, posizionato sul rettangolo del monitor.
- Elemento per tipo:
  - `animazione` (aziendale): `iframe` con URL = file + parametri del manifest + `monitor=` (in Estendi tutti i rettangoli della scena; in Duplica nessun parametro, l'iframe è già grande quanto il monitor).
  - `html` (importata): `iframe` sull'host libreria con `sandbox="allow-scripts"`; riceve anch'essa `monitor=` in Estendi.
  - `immagine`: `img` con `object-fit: cover`.
  - `video`: `video` muto, `autoplay`, `playsinline`, `object-fit: cover`; `loop` solo se la fine riproduzione è disattivata. In Duplica solo il primo video segnala la fine.
- Quando l'elemento è pronto (`load`, `loadeddata`, o 10 s di timeout = errore) lo strato nuovo passa da opacità 0 a 1 in `dissolvenzaMs`; a dissolvenza finita il vecchio strato viene rimosso (libera iframe e video, equivale a RF-12).
- Errore di caricamento o timeout: messaggio `errore` all'host, che passa alla voce successiva e scrive nel log (RF-15).
- `pausa` / `riprendi`: inoltrati a ogni iframe (contratto PRD sezione 6) e `pause()` / `play()` sui video.
- `layout`: in Estendi inoltra il messaggio `layout` agli iframe senza ricaricare; in Duplica riposiziona gli elementi e manda a ogni iframe il proprio rettangolo `{x:0, y:0, w, h}`. Cambiare modo (Estendi/Duplica) ricrea lo strato con dissolvenza.
- Il colore di sfondo della pagina è `#0f1e35` (RF-14).
- Senza `window.chrome.webview` il player accetta comandi da `window.parent` (serve alla pagina di prova `web/sviluppo/prova-player.html`).

### 5.2 App (`web/app/`)

HTML, CSS e JavaScript senza framework né dipendenze esterne, fedele al design B (colori, font, spaziature, componenti). I font Sora e IBM Plex Mono vanno **inclusi localmente** (`web/app/font/`, licenza OFL) perché la WebView non ha rete.

Intestazione: logo, schede **In onda / Libreria / Impostazioni**, stato ("ATTIVO SU n MONITOR" o "IN PAUSA" con il motivo per le pause automatiche, per esempio "IN PAUSA: SESSIONE BLOCCATA").

**In onda**
- Riquadro scena: i monitor rilevati disegnati come nel design (sfalsamenti in scala, etichetta "DISPLAYn 1920x1080 PRINCIPALE"), con dentro la scena **dal vivo**: iframe della voce corrente in scala, con la vera disposizione e il modo scelto. Didascalia con dimensioni totali. Selettore Estendi/Duplica se i monitor sono più di uno.
- Pannello: titolo, tipo e posizione in playlist, conto alla rovescia (`MM:SS`, `H:MM:SS` sopra l'ora, "alla fine del video", "cambio solo a mano", "in pausa"), barra di avanzamento, Precedente / Pausa-Riprendi / Successivo, riquadro "Poi".
- Playlist: schede numerate in orizzontale, riordinabili trascinando (e con tastiera: frecce sinistra e destra con la scheda a fuoco + Ctrl), clic sull'anteprima = mostra subito, × = togli (bloccato sull'ultima con avviso "Lascia almeno uno sfondo nella playlist"). Campo **Cambia ogni** (numero + minuti/ore + Mai) con messaggio di errore in linea, ordine In ordine/Casuale, interruttore "I video cambiano a fine riproduzione".

**Libreria**
- Filtri Tutti / Energy Italy / I miei con conteggi, griglia di schede, tasto ✓/+ per la playlist, cestino solo per le voci importate (con conferma), etichetta "IN ONDA".
- **Importa file**: apre la finestra di selezione nativa (comando all'host). Trascinamento di file da Esplora risorse sulla zona tratteggiata: i `File` arrivano all'host con `chrome.webview.postMessageWithAdditionalObjects` e l'host ne legge il percorso (`CoreWebView2File.Path`).

**Impostazioni**
- Monitor (rilevati, disposizione disegnata, Estendi/Duplica con descrizione), Riproduzione (dissolvenza 1/2/4 s, fine video), Pausa automatica (a batteria: interruttore; schermo intero, sessione bloccata, monitor spenti: "SEMPRE"; finestre ingrandite: "NON FERMANO"), Sistema (Avvia con Windows, Apri cartella log, Ripristina impostazioni aziendali).

**Miniature**
- Animazioni aziendali: iframe della pagina vera in 1920x1080 ridotto con `transform: scale()`, messo in `pausa` subito dopo il caricamento; riparte al passaggio del mouse. Così le anteprime sono fedeli e quasi non consumano.
- Immagini: l'immagine stessa. Video: `video` con `preload="metadata"` sul primo secondo. HTML importate: icona (niente esecuzione nella galleria).

Avvisi temporanei ("Aggiunto alla playlist: ...") in basso al centro, 2,6 s, `role="status"`.

## 6. Regole

### 6.1 Rotazione

- **Permanenza** di una voce: se la rotazione è spenta ("Mai") = infinita; se la voce è un video e "fine riproduzione" è attiva = fino al messaggio `videoFinito` (con rete di sicurezza: durata nota + 10 s; durata ignota = intervallo); altrimenti = intervallo.
- **Scadenza** come istante assoluto (`TimeProvider`). Le pause automatiche non la spostano: se alla ripresa è passata, il cambio avviene subito con dissolvenza (RF-23). La **pausa manuale** salva il tempo restante e alla ripresa ricalcola la scadenza (D8).
- **Prossimo:** in ordine = voce dopo la corrente nella playlist (se la corrente non è in playlist, la prima); casuale = a caso tra le altre voci della playlist, mai la stessa due volte di fila (RF-21).
- **Precedente:** ultima voce dello storico (D9); storico vuoto = voce prima in playlist.
- Cambiare intervallo, unità, "Mai" o "fine video" riparte il conteggio della voce corrente con la nuova regola.
- Se una voce in playlist non esiste più (file mancante), viene saltata e segnalata nel log.
- All'avvio: la voce dopo `ultimaVoce` (RF-25).

### 6.2 Pausa

Tabella PRD 5.4 invariata, più:

| Condizione | Livello | Conteggio |
|---|---|---|
| Pausa manuale (finestra o menu) | leggera | fermo (D8) |
| Tutte le pause automatiche | come PRD | continua |

Con più motivi attivi vale il livello più alto. La finestra principale mostra il motivo.

### 6.3 Monitor

- Rilevamento automatico all'avvio e a ogni cambio (RF-05), senza riavvio.
- **Estendi:** una scena con tutti i rettangoli; il logo va sul monitor più a sinistra (`sfondo.js` già lo fa).
- **Duplica:** una scena per monitor; ogni animazione si vede intera con il suo logo. Immagini e video riempiono ogni monitor (`cover`).
- Con un monitor solo il modo non conta e il selettore è nascosto.

### 6.4 Importazione

| Tipo | Estensioni | Limite |
|---|---|---|
| Immagine | jpg, jpeg, png, webp, gif, bmp | 50 MB |
| Video | mp4, webm | 2 GB |
| Pagina web | html, htm | 5 MB (solo il file; risorse relative accanto non vengono copiate) |

- Il file viene **copiato** nella cartella libreria con nome sicuro e univoco (`<slug>-<6 caratteri casuali>.<ext>`); l'originale non viene toccato.
- Titolo = nome del file senza estensione. Id = `u-<slug>-<6 caratteri>`.
- Le voci importate entrano nella libreria, **non** in automatico nella playlist.
- File non supportati o troppo grandi: avviso con il motivo, nessun errore bloccante.
- Durata dei video: la legge il player al primo caricamento e la manda all'host (`durataVideo`), che la salva in `libreria.json`.
- Eliminare una voce importata cancella il file, la voce e la sua presenza in playlist e nello storico; se era in onda si passa alla successiva.

## 7. Messaggi

JSON con campo `tipo`. Host verso pagine con `PostWebMessageAsJson`; pagine verso host con `chrome.webview.postMessage`.

**App -> host:** `pronta`, `mostra {id}`, `successivo`, `precedente`, `pausa`, `riprendi`, `impostazioni {modifiche}` (solo i campi cambiati), `playlist {ids}`, `importaDialogo`, `importaFile` (con oggetti `File` aggiuntivi), `elimina {id}`, `apriLog`, `ripristina`.

**Host -> app:** `stato {...}` completo a ogni cambiamento (voci, playlist, impostazioni, corrente, storicoVuoto, inPausa, pausaManuale, motivoPausa, restanteMs, totaleMs, contoAttivo, attendeFineVideo, finestra, monitor, modo, scene), `avviso {testo}`. L'app conta i secondi da sola tra uno `stato` e l'altro (solo se `contoAttivo`). Il formato esatto è in `Messaggi.cs`.

**Host -> player:** `mostra {id, tipoVoce, url, modo, scene, dissolvenzaMs, fineVideo}`, `pausa`, `riprendi`, `layout {modo, scene}`.

**Player -> host:** `pronto`, `mostrato {id}`, `errore {id, messaggio}`, `videoFinito {id}`, `durataVideo {id, secondi}`.

Messaggi sconosciuti o malformati: ignorati e scritti nel log.

## 8. Dati

### 8.1 `impostazioni.json` (versione 2)

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
  "avvioConWindows": true,
  "ultimaVoce": "onda-di-luce"
}
```

Validazione (ogni correzione genera un avviso nel log, mai un blocco, RF-51): valori fuori intervallo riportati al predefinito; `unita` in {minuti, ore}; `dissolvenzaSecondi` in {1, 2, 4}; `ordine` in {sequenziale, casuale}; `modoMonitor` in {estendi, duplica}; id sconosciuti tolti dalla playlist; playlist vuota = playlist predefinita. File con `versione` diversa da 2 = predefinite. Precedenza: file utente, poi `impostazioni-predefinite.json` accanto all'eseguibile (RF-52), poi valori nel codice. "Ripristina" riapplica le predefinite aziendali tranne la libreria.

### 8.2 `libreria.json`

```json
{
  "versione": 1,
  "voci": [
    { "id": "u-montagne-alba-k3x9qa", "tipo": "immagine", "titolo": "Montagne all'alba", "file": "montagne-alba-k3x9qa.jpg", "importataIl": "2026-10-13T16:00:00Z" },
    { "id": "u-timelapse-p0r2mv", "tipo": "video", "titolo": "Timelapse ufficio", "file": "timelapse-p0r2mv.mp4", "durataSecondi": 160, "importataIl": "2026-10-13T16:02:00Z" }
  ]
}
```

Voci il cui file manca: caricate ma segnate "non disponibile" nella galleria e saltate in rotazione.

### 8.3 `manifest.json`

Invariato (PRD sezione 6). Le voci del manifest hanno tipo `animazione` e origine aziendale.

## 9. Errori e log

- Mai finestre di errore bloccanti, salvo WebView2 Runtime assente all'avvio: messaggio chiaro in italiano con indicazione di contattare l'IT, poi uscita.
- Log in italiano: avvio, versione, monitor rilevati e modo, aggancio, pause e motivi, cambi sfondo, importazioni, errori del player, richieste di rete bloccate, avvisi di validazione.
- Crash del processo WebView2 (`ProcessFailed`): ricreare la WebView e rimostrare la voce corrente; dopo 3 crash in 5 minuti, pausa profonda e riga di errore nel log.

## 10. Non funzionali

Invariati dal PRD (RNF-01..06). In più: con la finestra principale chiusa il consumo deve restare quello del solo sfondo; con la finestra aperta le miniature sono in pausa salvo quella sotto il mouse e la scena "In onda".

## 11. Fuori da questa versione

Sfondo statico di riserva (RF-07); aggiornamento centralizzato delle animazioni; parametri delle animazioni modificabili dall'utente; sfondo diverso per ogni monitor; animazioni interattive; audio dei video; importazione di cartelle HTML con risorse; firma del codice (dipende dal certificato).

## 12. Test

**Automatici (xUnit su Core, TDD):** LayoutMonitor (PC di riferimento con origine negativa e sfalsamento 7 px, 1 e 3 monitor, Estendi e Duplica, DPI diversi in px fisici); Rotazione (in ordine, casuale senza ripetizione, corrente fuori playlist, storico, permanenza video, "Mai", pausa manuale vs automatica con `FakeTimeProvider`, scadenza passata alla ripresa); RegolePausa (ogni riga della tabella e combinazioni); Impostazioni (predefinite, correzioni, versione sbagliata, JSON rotto, precedenza dei file); Importazione (tipi, limiti, nomi sicuri e univoci, caratteri strani); Catalogo (unione, file mancanti); Messaggi (andata e ritorno JSON).

**Manuali:** la checklist T1-T14 del PRD, più:

| # | Scenario | Esito atteso |
|---|---|---|
| T15 | Importare un'immagine, un video e una pagina HTML (pulsante e trascinamento) | Compaiono in "I miei"; si mostrano correttamente estesi sui monitor |
| T16 | Video in playlist con "fine riproduzione" attiva e intervallo 1 minuto, video da 2 minuti | Il video resta fino alla fine, poi dissolvenza |
| T17 | Duplica su 2 monitor | Ogni monitor mostra la scena intera con il suo logo |
| T18 | Intervallo 2 ore, poi valore non valido (0, 30 ore, testo) | Conto alla rovescia `1:59:59`; errore in linea e ritorno al valore valido |
| T19 | Pagina HTML importata che prova a caricare una risorsa da internet | Richiesta bloccata, riga nel log, la pagina non si rompe il resto |
| T20 | Chiudere la finestra, rilanciare dal menu Start | Il programma resta attivo; la seconda istanza apre la finestra della prima |
| T21 | Riordinare la playlist trascinando e con tastiera | Ordine salvato e rispettato dalla rotazione |

## 13. Prerequisiti di sviluppo

- .NET 10 SDK (oggi assente: installazione per utente con lo script ufficiale Microsoft `dotnet-install.ps1`, senza diritti di amministratore).
- WebView2 Runtime (presente, v154).
- Inno Setup 6 solo per l'ultimo passo (installer).
- Font Sora e IBM Plex Mono (OFL) da includere in `web/app/font/`.
