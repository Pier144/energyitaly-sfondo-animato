# PRD - Energy Italy Sfondo Animato

Documento dei requisiti per un programma Windows che mostra sfondi animati HTML a marchio Energy Italy sul desktop, distribuito sui PC aziendali.

- Stato: bozza 1, 13/10/2026
- Responsabile prodotto e IT: Pierangelo Pancera
- Cartella progetto: `Desktop\Programmi Dev\EnergyItaly_Sfondo_Animato`

---

## 1. Obiettivo

Avere sul desktop di ogni PC aziendale uno sfondo animato Energy Italy che:

1. si estende come **un'unica immagine su tutti i monitor** (niente sfondo doppiato su ogni schermo);
2. **ruota da solo** tra più animazioni, con una dissolvenza, a un intervallo configurabile;
3. pesa poco su CPU e GPU e si mette in pausa quando nessuno lo vede;
4. si installa e si aggiorna centralmente, senza Steam né account personali (scarto di Wallpaper Engine e Lively motivato in sezione 11).

Criterio di successo: sul PC di riferimento (2 monitor 1920x1080) lo sfondo gira tutto il giorno senza che l'utente noti rallentamenti, cambia animazione da solo e sopravvive a riavvii di Explorer, blocco sessione e collegamento o scollegamento di un monitor.

## 2. Utenti e contesto

- **Utenti finali:** dipendenti Energy Italy, PC Windows 10/11, configurazioni di monitor diverse (1, 2 o 3 monitor, risoluzioni e scale DPI diverse).
- **Amministratore:** Pierangelo (IT interno). Installa il programma, decide le animazioni disponibili e le impostazioni predefinite.
- **PC di riferimento per i test:** Windows 11 Pro, due monitor 1920x1080. Il principale (DISPLAY2) è a destra con origine (0,0); il secondario (DISPLAY1) è a sinistra con origine (-1920,7), quindi sfalsato di 7 px in verticale.

## 3. Ambito

### 3.1 Nella prima versione (MVP)

- Finestra di sfondo agganciata dietro le icone del desktop, estesa sull'intero schermo virtuale.
- Riproduzione delle animazioni HTML della cartella `animazioni/` (sono 5, già pronte e testate).
- Rotazione automatica con dissolvenza, ordine sequenziale o casuale.
- Pausa automatica (sezione 5.4).
- Icona nell'area di notifica con menu.
- Impostazioni salvate su file JSON.
- Avvio automatico con Windows.
- Installer per utente singolo, installabile anche in modo silenzioso.

### 3.2 Fuori dalla prima versione (fase 2)

- Finestra impostazioni grafica completa (nell'MVP basta il menu dell'icona più il file JSON).
- Aggiornamento centralizzato delle animazioni da cartella di rete o URL.
- Sfondo diverso per ogni monitor.
- Animazioni interattive (mouse, orologio, meteo).

## 4. Le animazioni (già pronte)

Cartella `animazioni/`, già funzionante e verificata nel browser:

| File | Titolo | Parametri (query string) |
|---|---|---|
| `onda-di-luce.html` | Onda di luce: celle solari che pulsano e si illuminano al passaggio di un'onda diagonale | `durata` (s, 16), `intensita` (0.2-1), `ampiezza` (lieve, media, forte; predefinita lieve) |
| `onde-di-energia.html` | Nastri di linee sinusoidali blu-verdi che scorrono | `durata` (s, 80), `linee` (28) |
| `paesaggio-rinnovabile.html` | Colline in parallasse con pale eoliche e pannelli con riflessi | `velocita` (1) |
| `circuito.html` | Piste da circuito stampato percorse da impulsi verdi | `tracce` (24 su 1080 px), `velocita` (1) |
| `aurora.html` | Macchie di luce e veli d'aurora che scorrono lenti | `velocita` (1), `veli` (1 o 0) |

File di supporto:

- `manifest.json`: elenco delle animazioni con id, titolo, file e parametri predefiniti. **Il programma legge questo file.**
- `comune/sfondo.js`: contratto comune (layout monitor, pausa e ripresa), vedi sezione 6.
- `comune/base.css`: stili di base.
- `img/brand/LogoEnergyItalyWhite.svg`: logo ufficiale (bianco, per fondi scuri).
- `anteprima.html`: **implementazione di riferimento del player** (rotazione con dissolvenza su due iframe, pausa, monitor simulati). Il player del programma deve comportarsi allo stesso modo. Per provarla serve un server HTTP locale (per esempio `python -m http.server 5599 --directory animazioni`), perché aperta direttamente dal disco il browser blocca alcune funzioni.

Tutte le animazioni:

- si adattano a qualunque dimensione di finestra e a qualunque disposizione dei monitor;
- mettono il logo in basso a sinistra del monitor più a sinistra, in scala con l'altezza di quel monitor;
- fanno loop senza stacchi;
- non usano rete né font esterni (funzionano offline).

## 5. Requisiti funzionali

Priorità: **M** = obbligatorio nell'MVP, **S** = desiderabile nell'MVP, **F** = fase 2.

### 5.1 Finestra di sfondo

- **RF-01 (M)** La finestra dello sfondo sta dietro le icone del desktop e sopra lo sfondo statico di Windows, senza bordi, senza comparire nella barra delle applicazioni né in Alt+Tab.
- **RF-02 (M)** La finestra copre l'intero schermo virtuale (bounding box di tutti i monitor, origine anche negativa). Sul PC di riferimento: da (-1920,0) a (1920,1087).
- **RF-03 (M)** Aggancio compatibile con Windows 10, Windows 11 e Windows 11 24H2 e successivi. In 24H2 Microsoft ha cambiato la struttura delle finestre del desktop (Progman, WorkerW, SHELLDLL_DefView): l'aggancio deve rilevare quale struttura è presente e gestire entrambe. Riferimento tecnico: il codice di Lively Wallpaper (open source, licenza GPL-3.0: **da studiare, non da copiare**, il programma aziendale non è GPL).
- **RF-04 (M)** Se Explorer si riavvia (messaggio registrato `TaskbarCreated`), la finestra si riaggancia da sola entro pochi secondi.
- **RF-05 (M)** Al cambio di configurazione dei monitor (collegamento, scollegamento, risoluzione, scala, orientamento) la finestra si ridimensiona e l'animazione riceve il nuovo layout senza riavviare il programma.
- **RF-06 (M)** All'uscita dal programma la finestra viene rimossa e il desktop ridisegnato, così torna visibile lo sfondo statico di Windows.
- **RF-07 (S)** Sfondo statico di riserva: un'immagine PNG panoramica (ricavata dall'animazione preferita) che il programma imposta come sfondo di Windows in modalità "estendi". Così, a programma chiuso o in pausa profonda, il desktop resta coerente.

### 5.2 Riproduzione

- **RF-10 (M)** Il motore è WebView2. Le pagine sono servite con `SetVirtualHostNameToFolderMapping` su un host virtuale unico (per esempio `https://sfondo.energyitaly/`), con le cartelle `player/` e `animazioni/` sotto la stessa radice: stessa origine, niente `file://`.
- **RF-11 (M)** `RasterizationScale` fissato a 1 e rilevamento automatico della scala disattivato, così 1 px CSS = 1 px fisico anche con monitor a DPI diversi. Le animazioni gestiscono già la scala in base all'altezza dei monitor.
- **RF-12 (M)** Il player (`player/player.html`, parte del programma) carica le animazioni in due iframe sovrapposti e passa da uno all'altro con una dissolvenza (predefinita 2 s), come `animazioni/anteprima.html`. L'iframe uscente viene svuotato (`about:blank`) a dissolvenza finita.
- **RF-13 (M)** L'URL di ogni animazione = file + colori della tavolozza + opzioni effettive (predefinite o personalizzate) + parametro `monitor` (sezione 6).
- **RF-14 (M)** Il colore di sfondo della pagina del player e della WebView è `#0f1e35`, così non c'è mai un lampo bianco al caricamento.
- **RF-15 (M)** Se un'animazione non si carica (file mancante, errore), il player passa alla successiva e il programma lo scrive nel log.

### 5.3 Rotazione

- **RF-20 (M)** Intervallo di cambio configurabile: 5, 15, 30 o 60 minuti, oppure "mai". Predefinito 30 minuti.
- **RF-21 (M)** Ordine sequenziale (predefinito) o casuale. In casuale la stessa animazione non si ripete due volte di fila.
- **RF-22 (M)** Si può scegliere quali animazioni sono in rotazione; ne resta attiva almeno una.
- **RF-23 (M)** Il tempo dell'intervallo scorre anche durante le pause. Se alla ripresa il cambio è scaduto, avviene subito con dissolvenza.
- **RF-24 (M)** Le finestre massimizzate **non** fermano la rotazione (differenza voluta rispetto a Wallpaper Engine).
- **RF-25 (M)** All'avvio parte l'animazione successiva all'ultima mostrata (memorizzata nelle impostazioni).

### 5.4 Pausa automatica

Due livelli:

- **Pausa leggera:** l'animazione si congela (messaggio `pausa`), la finestra resta visibile.
- **Pausa profonda:** in più la WebView viene nascosta e sospesa (`IsVisible = false` e `TrySuspendAsync`), per un consumo prossimo a zero.

| Requisito | Condizione | Livello |
|---|---|---|
| RF-30 (M) | Il desktop non è visibile su nessun monitor (finestre a schermo intero o massimizzate che coprono ogni monitor) | leggera |
| RF-31 (M) | App a schermo intero Direct3D o modalità presentazione (`SHQueryUserNotificationState`) | leggera |
| RF-32 (M) | Sessione bloccata, sessione remota disconnessa, cambio utente | profonda |
| RF-33 (M) | Monitor spenti (`GUID_CONSOLE_DISPLAY_STATE`) | profonda |
| RF-34 (S) | Alimentazione a batteria o risparmio energia attivo (configurabile, predefinito attivo) | leggera |
| RF-35 (M) | Pausa manuale dal menu | leggera |

RF-30 e RF-31 usano la pausa leggera, come Wallpaper Engine: con la WebView nascosta, quando il desktop riappare si vedrebbe per un istante lo sfondo di Windows e il movimento ripartirebbe in ritardo. Ferma sull'ultimo fotogramma e coperta, la WebView non consuma CPU (misurato: 0 ms in 8 s).

- **RF-36 (M)** Uscendo dalla pausa l'animazione riprende senza ricaricare la pagina (salvo dopo una sospensione WebView, se necessario).

### 5.5 Icona nell'area di notifica

- **RF-40 (M)** Icona Energy Italy con menu:
  - Animazione successiva
  - Pausa / Riprendi
  - Animazioni (sottomenu con una casella per ognuna)
  - Cambia ogni (5, 15, 30, 60 min, Mai)
  - Ordine casuale (casella)
  - Avvia con Windows (casella)
  - Apri cartella log
  - Esci
- **RF-41 (M)** Il suggerimento dell'icona mostra il nome dell'animazione in corso.
- **RF-42 (M)** Una sola istanza alla volta (mutex con nome). Il secondo avvio non apre una seconda copia.

### 5.6 Impostazioni e log

- **RF-50 (M)** Impostazioni in `%APPDATA%\EnergyItaly\SfondoAnimato\impostazioni.json`:

  ```json
  {
    "versione": 1,
    "intervalloMinuti": 30,
    "ordine": "sequenziale",
    "animazioniAttive": ["onda-di-luce", "onde-di-energia", "paesaggio-rinnovabile", "circuito", "aurora"],
    "dissolvenzaSecondi": 2,
    "pausaABatteria": true,
    "avvioConWindows": true,
    "ultimaAnimazione": "onda-di-luce"
  }
  ```

- **RF-51 (M)** Un file mancante o non valido non blocca il programma: si usano i valori predefiniti e si registra un avviso nel log.
- **RF-52 (S)** Impostazioni predefinite aziendali: un file `impostazioni-predefinite.json` accanto all'eseguibile, letto quando l'utente non ha ancora un file suo.
- **RF-53 (M)** Log a rotazione in `%LOCALAPPDATA%\EnergyItaly\SfondoAnimato\log\` (massimo 5 file da 1 MB): avvio, aggancio, cambi layout, pause, errori WebView.
- **RF-54 (M)** Avvio con Windows tramite `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

## 6. Contratto animazioni

Contratto tra il programma (player) e ogni animazione. È già implementato in `animazioni/comune/sfondo.js`.

1. Un'animazione è **una pagina HTML autonoma** che riempie la finestra (`100vw` per `100vh`), funziona offline e non usa risorse esterne.
2. **Layout monitor in ingresso:** query string `monitor=x,y,w,h;x,y,w,h;...` con un rettangolo per monitor, in px CSS, relativo all'angolo in alto a sinistra della finestra. Esempio per il PC di riferimento (finestra da (-1920,0) a (1920,1087)): `monitor=0,7,1920,1080;1920,0,1920,1080`. Senza parametro la finestra intera vale come un monitor.
3. **Messaggi dal player** (`postMessage`, accettati solo dalla finestra genitore):
   - `{ "tipo": "pausa" }`
   - `{ "tipo": "riprendi" }`
   - `{ "tipo": "layout", "monitor": [{ "x": 0, "y": 7, "w": 1920, "h": 1080 }] }`
4. **Comportamento richiesto:** al messaggio `layout` l'animazione ricostruisce la scena; alla `pausa` congela tutte le animazioni CSS. Le animazioni future che usano `requestAnimationFrame` o canvas devono fermare il ciclo.
5. **Logo:** in basso a sinistra del monitor più a sinistra, larghezza 420 px su un monitor alto 1080 px, in scala per altezze diverse (`Sfondo.posizionaLogo`).
6. **Manifest (versione 2):** ogni animazione va registrata in `manifest.json` con `id` (stabile, usato nelle impostazioni), `titolo` (mostrato nel menu), `file` e `opzioni`. Ogni opzione ha `id`, `titolo`, `tipo` (`numero` con `min`, `max`, `passo` e `unita` facoltativa; `scelta` con 2-4 `valori`; `interruttore`) e `predefinito`, che per i numeri deve cadere sul passo. In testa al manifest ci sono le `tavolozze` (`id`, `titolo`, `fondo`, `accento`, `secondario` come `#rrggbb`); la prima è la predefinita. Dettagli: spec `docs/superpowers/specs/2026-10-14-personalizzazione-sfondi-design.md`.
6a. **Colori e opzioni in ingresso:** query string `fondo`, `accento`, `secondario` (esadecimali senza `#`; con la tavolozza "Personalizzata" manca `secondario`, che `sfondo.js` ricava dal fondo) e una voce per ogni opzione, con i numeri in cultura invariante (punto decimale) e gli interruttori come `1`/`0`. `sfondo.js` offre i ruoli di colore con `Sfondo.colori()`, `Sfondo.tono(ruolo, luce, saturazione)`, `Sfondo.velo(luce)` (il velo scuro dietro il logo) e `Sfondo.rgba(colore, alfa)`, e come variabili CSS (`var(--accento-chiaro)`, `rgba(var(--accento-chiaro-rgb), 0.5)`); gli interruttori si leggono con `Sfondo.interruttore(nome, predefinito)`. Un'animazione non scrive colori fissi tranne il bianco del logo e dei dettagli.
7. Comunicazione programma-player: `CoreWebView2.PostWebMessageAsJson` verso il player; `window.chrome.webview.postMessage` dal player verso il programma (animazione cambiata, errori). Il player inoltra i messaggi di pausa e layout all'iframe attivo.

## 7. Requisiti non funzionali

- **RNF-01 Prestazioni:** sul PC di riferimento CPU media del processo (WebView compresa) **sotto il 5%** e memoria **sotto 300 MB** con l'animazione più pesante (`onda-di-luce`, circa 1150 celle animate). Se `onda-di-luce` supera la soglia, va riscritta su canvas mantenendo lo stesso aspetto.
- **RNF-02 Avvio:** sfondo visibile entro 3 secondi dal login.
- **RNF-03 Robustezza:** nessun crash per 8 ore di funzionamento continuo con cambio animazione ogni 5 minuti.
- **RNF-04 Installazione:** senza diritti di amministratore (installazione per utente).
- **RNF-05 Lingua:** interfaccia, messaggi e log in italiano.
- **RNF-06 Stile codice:** commenti e stringhe in italiano con accenti UTF-8 veri; mai em-dash né en-dash in codice, commenti e documenti.

## 8. Architettura proposta

- **Linguaggio e piattaforma:** C# su .NET 10 (LTS), Windows Forms (area di notifica e finestra senza bordi), controllo WebView2 (`Microsoft.Web.WebView2`), P/Invoke per le API Win32.
- **Consapevolezza DPI:** PerMonitorV2 nel manifest dell'applicazione; tutte le coordinate di finestra in px fisici.

Componenti (ognuno con un compito solo):

| Componente | Compito |
|---|---|
| `DesktopHost` | Trova la struttura del desktop (pre e post 24H2), aggancia e sgancia la finestra, gestisce `TaskbarCreated`. |
| `FinestraSfondo` | Form senza bordi che ospita la WebView2, coprendo lo schermo virtuale. |
| `LayoutMonitor` | Legge i monitor (`EnumDisplayMonitors`), calcola il bounding box e i rettangoli relativi alla finestra, produce la stringa `monitor=`. Logica pura, testabile. |
| `GestorePausa` | Ascolta sessione, alimentazione, display e finestra in primo piano; decide il livello di pausa. Le regole di decisione sono logica pura, testabile. |
| `Rotazione` | Timer, ordine, elenco attive, ultima mostrata. Logica pura, testabile. |
| `Impostazioni` | Lettura e scrittura JSON con valori predefiniti e validazione. |
| `Personalizzazioni` | Regole pure delle personalizzazioni degli sfondi: validazione, unione con i predefiniti, URL con colori e opzioni, modo dei monitor e riempimento effettivi. |
| `IconaNotifica` | Menu, suggerimento, azioni. |
| `player/player.html` | Iframe doppi, dissolvenza, inoltro messaggi; parte da `animazioni/anteprima.html`. |

Struttura del repository:

```
EnergyItaly_Sfondo_Animato/
  docs/PRD.md
  animazioni/                      (già presente)
  src/EnergyItaly.SfondoAnimato/   (applicazione .NET 10 WinForms)
    web/player/                    (player.html, player.js)
  tests/EnergyItaly.SfondoAnimato.Tests/  (xUnit: layout, rotazione, regole pausa, impostazioni)
  installer/                       (script Inno Setup)
```

In fase di build la cartella `animazioni/` viene copiata in `web/animazioni/` accanto a `web/player/`.

**Sviluppo:** TDD sulla logica pura (layout, rotazione, regole di pausa, impostazioni). Le parti Win32 e WebView2 si verificano a mano con la checklist di sezione 10.

## 9. Distribuzione

- `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`, con la cartella `web/` accanto all'eseguibile.
- Installer **Inno Setup** per utente (`PrivilegesRequired=lowest`), installazione in `%LOCALAPPDATA%\Programs\EnergyItaly Sfondo Animato`. Collegamento nel menu Start, voce di avvio automatico, disinstallazione pulita (chiave Run e impostazioni rimosse).
- Installazione silenziosa per la distribuzione massiva: `/VERYSILENT /SUPPRESSMSGBOXES`.
- WebView2 Runtime: già presente su Windows 11. Se manca (Windows 10), l'installer lancia il bootstrapper Evergreen di Microsoft.
- **Firma del codice (S):** con un certificato aziendale, per evitare gli avvisi di SmartScreen e dell'antivirus.

## 10. Piano di test con le animazioni

Prerequisito: sviluppare e testare sul PC di riferimento.

| # | Scenario | Esito atteso |
|---|---|---|
| T1 | Avvio sul PC di riferimento | Un'unica scena su 3840x1080; logo solo sul monitor sinistro, in basso a sinistra; icone del desktop sopra lo sfondo e cliccabili |
| T2 | Ciclo di tutte e 5 le animazioni (intervallo minimo, poi "Animazione successiva") | Dissolvenza pulita, nessun lampo bianco, nessun errore nel log |
| T3 | Confine tra i due monitor | Nessuno stacco visibile al passaggio tra i monitor; sfalsamento di 7 px gestito |
| T4 | Scollegare e ricollegare un monitor | Ridimensionamento automatico, logo che si sposta sul monitor rimasto |
| T5 | Scala 125% o 150% su un monitor | Proporzioni corrette, nessuna sfocatura, nessun ritaglio |
| T6 | Riavvio di `explorer.exe` | Riaggancio automatico entro pochi secondi |
| T7 | Finestra massimizzata su un solo monitor | L'animazione continua sull'altro monitor |
| T8 | Gioco o video a schermo intero su tutti i monitor | Pausa leggera; all'uscita nessuno stacco e movimento subito ripreso |
| T9 | Blocco sessione (Win+L) e sblocco | Pausa profonda e ripresa |
| T10 | Portatile a batteria | Pausa leggera (se attiva nelle impostazioni) |
| T11 | Misura risorse (Gestione attività, 10 minuti per animazione) | Entro RNF-01; annotare i valori per ogni animazione |
| T12 | 8 ore di funzionamento con cambio ogni 5 minuti | Nessun crash, memoria stabile |
| T13 | Installazione silenziosa su un PC pulito con utente standard | Avvio automatico al login successivo |
| T14 | Windows 10 (se ci sono ancora PC Windows 10 in azienda) | Aggancio e riproduzione funzionanti |

## 11. Alternative valutate

- **Lively Wallpaper:** gratuito, ma non ha la rotazione a tempo integrata (richiesta più volte su GitHub) e va installato e configurato su ogni PC.
- **Wallpaper Engine:** ha playlist a tempo ed estensione su più monitor, ma richiede Steam e una licenza per ogni utente, e di base ferma la playlist con le finestre massimizzate. Poco adatto alla distribuzione aziendale.
- **Programma proprio (scelto):** installazione unica gestita dall'IT, nessun account, aggiornamenti centralizzati, comportamento su misura. Costo: sviluppo iniziale e manutenzione dell'aggancio al desktop quando Windows cambia.

## 12. Rischi

| Rischio | Mitigazione |
|---|---|
| Un aggiornamento di Windows cambia di nuovo la struttura del desktop | Aggancio isolato in `DesktopHost`, log dettagliato, test a ogni aggiornamento importante di Windows |
| Consumo elevato di `onda-di-luce` | Misura in T11; eventuale riscrittura su canvas |
| WebView2 assente o vecchio su PC Windows 10 | Bootstrapper nell'installer |
| Monitor con DPI diversi | `RasterizationScale` fisso a 1, test T5 |
| Avvisi antivirus su eseguibile non firmato | Firma aziendale (sezione 9) |

## 13. Punti aperti

1. Nome definitivo del programma e dell'eseguibile (proposta: "Energy Italy Sfondo Animato", `SfondoAnimato.exe`).
2. Icona dell'area di notifica: ricavata dal simbolo del logo (cerchio con la E).
3. Intervallo predefinito aziendale (proposta: 30 minuti).
4. Disponibilità di un certificato di firma del codice.
5. Quanti PC hanno ancora Windows 10, per decidere se T14 è necessario.
6. In fase 2: aggiornamento delle animazioni da cartella di rete o da URL.

## 14. Come usare questo documento in una nuova chat

Aprire una nuova sessione di Claude Code nella cartella `EnergyItaly_Sfondo_Animato` e scrivere:

> Leggi `docs/PRD.md` e la cartella `animazioni/`. Voglio sviluppare il programma descritto, partendo dall'MVP. Prima chiariamo i punti aperti della sezione 13 e prepariamo il piano di implementazione; poi sviluppiamo con TDD sulla logica pura e testiamo con le 5 animazioni seguendo la sezione 10.

Prerequisiti da verificare sul PC prima di iniziare: .NET 10 SDK installato (oggi sul PC ci sono solo i runtime, l'SDK va installato; verifica con `dotnet --list-sdks`), WebView2 Runtime presente, Inno Setup (solo per l'installer).
