# Spec - Personalizzazione degli sfondi

- Data: 2026-10-14
- Riguarda: barra laterale nella pagina "Libreria", colori e opzioni di ogni animazione, Estendi/Duplica per singolo sfondo, riempimento di immagini e video, contratto delle animazioni (`sfondo.js`, `base.css`, `manifest.json`), pagine di prova.
- Scelte dell'utente: barra per **tutti** gli sfondi, con opzioni diverse per tipo; colori con **tavolozze pronte più "Personalizzata"** (fondo e accento); **anteprima nella barra**, il desktop cambia solo quando lo sfondo è in onda; "Ripristina impostazioni aziendali" **cancella anche** le personalizzazioni; approccio A (opzioni dichiarate nel manifest, colori comuni a tutte le animazioni).
- Revisione: la prima stesura è stata rivista; questa versione fissa i formati (numeri, interruttori, colori nei CSS), le interfacce del Core, l'ordine degli invii al player, il debounce della barra, i pulsanti del palco e le pagine di prova.

## 1. Obiettivo

Dalla Libreria si sceglie uno sfondo e lo si adatta: colori, funzionamento (velocità e opzioni proprie di ogni animazione) e comportamento con più monitor. Le scelte valgono solo su quel PC, si salvano da sole e si annullano con un clic.

## 2. Decisioni

| # | Decisione | Motivo |
|---|---|---|
| P1 | Clic su una scheda della Libreria: si apre la barra laterale di quello sfondo (non va più subito in onda). Per mandarlo in onda c'è "Mostra ora" nella barra. | Il clic serve a scegliere cosa personalizzare; "Mostra ora" resta a un clic di distanza. |
| P2 | Le opzioni di ogni animazione sono dichiarate in `manifest.json` (tipo, limiti, valore predefinito, titolo). La barra si costruisce da lì. | Un'animazione nuova ottiene la barra senza toccare il codice del programma. |
| P3 | I colori sono comuni a tutte le animazioni: arrivano nella query string; `sfondo.js` ne ricava le sfumature e le espone sia in JavaScript sia come variabili CSS. Le tavolozze pronte stanno in `manifest.json`. | Una sola regola per tutte le animazioni, anche per i colori scritti nei fogli di stile. |
| P4 | "Personalizzata" fa scegliere fondo e accento; il secondario lo ricava `sfondo.js` dal fondo. | Due selettori bastano; il calcolo sta in un posto solo. |
| P5 | Con più monitor ogni sfondo può seguire la scelta generale (Impostazioni) oppure forzare Estendi o Duplica. La disposizione dipende dallo sfondo in onda. | Richiesta dell'utente. |
| P6 | Immagini e video importati: Estendi/Duplica e riempimento ("Riempi" taglia i bordi, "Adatta" lascia bande nere). Pagine HTML importate: solo Estendi/Duplica. | Colori e opzioni non hanno senso per file che il programma non controlla. |
| P7 | Le personalizzazioni stanno in `impostazioni.json` (campo `personalizzazioni`), solo come differenze dai valori predefiniti. Uno sfondo senza differenze non ha voce. | Il file resta piccolo; "personalizzato" significa "ha una voce". |
| P8 | Ogni modifica confermata (cursore rilasciato, colore scelto, pulsante premuto) si salva subito. Se lo sfondo è in onda, il desktop si aggiorna con una dissolvenza di 600 ms; altrimenti il desktop non cambia. Mentre si trascina, cambia solo l'anteprima della barra. | Nessun pulsante "Salva"; niente salti né ricaricamenti continui del desktop. |
| P9 | "Ripristina questo sfondo" toglie la sua voce. "Ripristina impostazioni aziendali" toglie tutte le voci. | Scelta dell'utente. |
| P10 | Con la tavolozza Energy Italy e le opzioni predefinite ogni animazione deve sembrare quella della 1.1.2: si confrontano fianco a fianco nel browser e non deve esserci una differenza evidente. | La personalizzazione non deve cambiare l'aspetto aziendale di chi non tocca niente. |
| P11 | La logica (manifest, validazione, unione con i predefiniti, URL, disposizione per sfondo) sta nel Core come funzioni pure, provata con i test. | Stessa architettura delle altre funzioni. |
| P12 | Numeri sempre in cultura invariante (punto decimale) nell'URL e nei file; interruttori come `1`/`0` nell'URL e `true`/`false` nei file. | Su Windows in italiano `ToString()` scriverebbe "1,5". |

## 3. Contratto delle animazioni

### 3.1 `manifest.json` (versione 2)

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
    {
      "id": "segnale", "titolo": "Segnale", "file": "segnale.html",
      "opzioni": [
        { "id": "velocita", "titolo": "Velocità", "tipo": "numero", "min": 0.5, "max": 2, "passo": 0.25, "predefinito": 1, "unita": "×" },
        { "id": "onde", "titolo": "Anelli per impulso", "tipo": "numero", "min": 0, "max": 4, "passo": 1, "predefinito": 2 }
      ]
    }
  ]
}
```

- La prima tavolozza è la predefinita. I valori di Notte, Alba, Mare e Bosco si possono ritoccare in implementazione guardando le animazioni.
- Tipi di opzione:
  - `numero`: `min`, `max`, `passo`, `predefinito` (numeri), `unita` facoltativa. Nella barra: cursore con il valore accanto.
  - `scelta`: `valori` come elenco di `{ "valore": "lieve", "titolo": "Lieve" }` (da 2 a 4), `predefinito` (uno dei valori). Nella barra: pulsanti a pillola.
  - `interruttore`: `predefinito` true/false. Nella barra: interruttore come quelli di Impostazioni.
- `parametri` (versione 1) sparisce: ogni valore che oggi sta in `parametri` diventa un'opzione con quel predefinito. Opzioni:

| Animazione | Opzioni (id: tipo, limiti, predefinito) |
|---|---|
| Onda di luce | `ampiezza`: scelta lieve/media/forte, lieve; `durata`: numero 8-30 passo 1, 16, unità "s"; `intensita`: numero 0.4-1 passo 0.1, 1 |
| Onde di energia | `durata`: numero 40-160 passo 10, 80, unità "s"; `linee`: numero 12-40 passo 2, 28 |
| Paesaggio rinnovabile | `velocita` |
| Circuito | `tracce`: numero 12-40 passo 2, 24; `velocita` |
| Aurora | `velocita`; `veli`: interruttore, true (titolo "Veli di luce") |
| Segnale | `velocita`; `onde`: numero 0-4 passo 1, 2 (titolo "Anelli per impulso") |
| Trama | `velocita`; `distanza`: numero 90-200 passo 10, 130 (titolo "Distanza tra i marchi"; oggi si chiama `passo`) |

  `velocita` è sempre: numero 0.5-2, passo 0.25, predefinito 1, unità "×", titolo "Velocità".
- Validazione nel Core (tollerante): un'opzione con tipo sconosciuto, limiti incoerenti, `predefinito` del tipo sbagliato o fuori limiti viene scartata con un avviso nel log; l'animazione resta disponibile. Una tavolozza con colori non validi (non `#rrggbb`) o id ripetuto viene scartata; se non ne resta nessuna vale Energy Italy scritta nel codice.

### 3.2 Query string dell'animazione

`segnale.html?fondo=0f1e35&accento=7ab52a&secondario=1e5b8e&velocita=1.5&onde=2&monitor=...`

- Colori esadecimali senza `#`. Con una tavolozza si passano tutti e tre; con "Personalizzata" solo `fondo` e `accento`.
- Si passano sempre tutte le opzioni con il valore effettivo (predefinito o personalizzato): numeri in cultura invariante, interruttori `1`/`0`, scelte come testo.

### 3.3 `sfondo.js` e `base.css`

`sfondo.js`, appena caricato e prima di costruire la scena:

- legge `fondo`, `accento`, `secondario` (Energy Italy se mancano o non sono validi; se manca solo il secondario lo ricava dal fondo: stessa tinta, saturazione +20, luminosità +22 se il fondo è scuro (sotto 50) o -30 se è chiaro, limitate a 0-100);
- calcola in HSL i ruoli: `fondo`, `fondoScuro`, `fondoChiaro`, `accento`, `accentoChiaro`, `accentoPallido`, `secondario`, `secondarioScuro`, `secondarioMedio`, `secondarioChiaro`, più `velo` (la tinta del fondo con luminosità al massimo 12, per il velo dietro il logo bianco: resta scuro anche con un fondo chiaro; ogni animazione può chiederlo un po' più scuro con `Sfondo.velo(scarto)`). Gli scarti di luminosità si scelgono in implementazione perché, con Energy Italy, i ruoli cadano sui colori usati oggi (per esempio `accentoChiaro` circa `#b2e068`, `accentoPallido` circa `#d6f5a0`, `secondarioChiaro` circa `#4a90c8`, `secondarioMedio` circa `#2f78b5`, `secondarioScuro` circa `#173a63`, `fondoScuro` circa `#07111f`);
- scrive su `:root` una variabile CSS per ruolo, `--fondo`, `--fondo-scuro`, ..., e la versione a componenti per le trasparenze, `--fondo-rgb: 15, 30, 53` (uso: `rgba(var(--accento-rgb), 0.5)`);
- offre in JavaScript `Sfondo.colori()` (oggetto con i ruoli come `#rrggbb`) e `Sfondo.rgba(ruolo, alfa)`;
- aggiunge `Sfondo.interruttore(nome, predefinito)`, che legge `1`/`0` (e accetta `true`/`false`).

`comune/base.css` usa `var(--fondo, #0f1e35)` al posto del colore fisso. Ogni animazione sostituisce i colori scritti nel codice, nei blocchi `<style>` e negli script, con i ruoli. Il logo resta bianco. Il fondo del player (`player.css`) resta `#0f1e35`: si vede solo prima che lo sfondo sia caricato.

### 3.4 PRD

In `docs/PRD.md`: la sezione 6 "Contratto animazioni" si aggiorna con 3.1-3.3; da 3.2 "Fuori dalla prima versione" si toglie "Parametri delle singole animazioni modificabili dall'utente"; RF-13 diventa "file + colori + opzioni effettive + `monitor`"; la tabella dei componenti della sezione 8 aggiunge `Personalizzazione`.

## 4. Nel programma

### 4.1 Core: tipi

Nuovo file `Personalizzazione.cs`:

```csharp
public enum TipoOpzione { Numero, Scelta, Interruttore }
public enum Riempimento { Riempi, Adatta }

/// Valore di un'opzione: uno solo dei tre campi è valorizzato, secondo il tipo.
public sealed record ValoreOpzione(double? Numero = null, string? Testo = null, bool? Booleano = null)
{
    public string PerUrl();   // cultura invariante; interruttore "1"/"0"
}

public sealed record ValoreScelta(string Valore, string Titolo);

public sealed record Opzione(string Id, string Titolo, TipoOpzione Tipo, ValoreOpzione Predefinito,
    double? Min = null, double? Max = null, double? Passo = null, string? Unita = null,
    IReadOnlyList<ValoreScelta>? Valori = null);

public sealed record Tavolozza(string Id, string Titolo, string Fondo, string Accento, string Secondario);

public sealed record Personalizzazione
{
    public string? Tavolozza { get; init; }       // id di tavolozza o "personalizzata"; null = la predefinita
    public string? Fondo { get; init; }           // "#rrggbb", solo con "personalizzata"
    public string? Accento { get; init; }
    public IReadOnlyDictionary<string, ValoreOpzione> Opzioni { get; init; }   // solo quelle diverse dal predefinito
    public ModoMonitor? ModoMonitor { get; init; }      // null = come in Impostazioni
    public Riempimento? Riempimento { get; init; }      // solo immagini e video; null = Riempi
    public bool Vuota { get; }
}

/// Comando "personalizza": null = campo non toccato.
public sealed record ModifichePersonalizzazione(
    string? Tavolozza = null, string? Fondo = null, string? Accento = null,
    IReadOnlyDictionary<string, ValoreOpzione>? Opzioni = null,
    string? ModoMonitor = null,      // "generale" | "estendi" | "duplica"
    string? Riempimento = null);     // "riempi" | "adatta"
```

- `Voce` perde `Parametri` e guadagna `Opzioni` (`IReadOnlyList<Opzione>`; vuota per le voci importate).
- `Catalogo.Carica` restituisce anche le tavolozze.

### 4.2 Core: regole (classe statica `Personalizzazioni`)

Una sola normalizzazione, usata sia al caricamento sia per i comandi:

- **`Normalizza(Personalizzazione, Voce, tavolozze, avvisi)`**: un numero fuori limiti si porta al limite e si arrotonda al passo; un'opzione che non esiste più o ha il tipo sbagliato si toglie; una scelta non più ammessa si toglie; una tavolozza sconosciuta si toglie; `Fondo`/`Accento` si tengono solo con "personalizzata" e solo se validi, e una "personalizzata" rimasta senza uno dei due colori torna alla tavolozza predefinita; `Riempimento` si tiene solo per immagini e video; colori e opzioni solo per le animazioni; un valore uguale al predefinito si toglie. Ogni correzione aggiunge un avviso (solo nel log).
- **`Applica(Personalizzazione?, ModifichePersonalizzazione, Voce, tavolozze)`** restituisce `(Personalizzazione Risultato, string? Errore)`. È un errore (avviso mostrato nella finestra, nulla cambia) un campo che non vale per quel tipo di voce, un'opzione sconosciuta, un valore del tipo sbagliato, una scelta non ammessa, un colore non valido, un modo o riempimento sconosciuto. Altrimenti si applicano i campi e si chiama `Normalizza`. Scegliere una tavolozza diversa da "personalizzata" toglie `Fondo` e `Accento`. Passando a "personalizzata" senza colori si usano i colori della tavolozza effettiva fino a quel momento.
- **`Riconcilia(dizionario, voci, tavolozze, avvisi)`**: toglie le voci di sfondi sconosciuti, normalizza le altre, toglie quelle vuote. Si chiama all'avvio e quando cambiano le voci (importazione, eliminazione).
- **`Url(Voce, Personalizzazione?, tavolozze)`**: sostituisce `Catalogo.UrlBase`. Per le animazioni aggiunge colori e tutte le opzioni effettive (3.2); per gli altri tipi l'URL non cambia.
- **`ModoEffettivo(Personalizzazione?, ModoMonitor generale)`** e **`RiempimentoEffettivo(Personalizzazione?)`**.

### 4.3 Core: `impostazioni.json`

```json
"personalizzazioni": {
  "segnale": { "tavolozza": "personalizzata", "fondo": "#102030", "accento": "#e0a040", "opzioni": { "velocita": 1.5 }, "modoMonitor": "estendi" },
  "aurora": { "opzioni": { "veli": false } },
  "u-i-ab12cd": { "modoMonitor": "duplica", "riempimento": "adatta" }
}
```

- I valori delle opzioni si scrivono con il loro tipo JSON (numero, testo, booleano).
- `ArchivioImpostazioni` legge il campo in modo solo sintattico (tipi JSON giusti, altrimenti campo tolto con avviso) e lo scrive; la validazione contro manifest e voci la fa `Riconcilia` nel coordinatore. Firma di `Carica` invariata.
- `impostazioni-predefinite.json` non legge questo campo (sezione 6).
- Il formato resta `versione: 2`: il campo è nuovo e facoltativo.

### 4.4 Core: messaggi

- `ComandoApp` guadagna `ModifichePersonalizzazione? Personalizzazione`. `LeggiComandoApp` legge l'oggetto `modifiche` come `ModificheImpostazioni` se `tipo` è `impostazioni` e come `ModifichePersonalizzazione` se `tipo` è `personalizza` (numeri JSON in `Numero`, stringhe in `Testo`, booleani in `Booleano`).
- `mostra`: l'URL contiene già colori e opzioni; nuovo campo `riempimento` (`"riempi"` o `"adatta"`) per immagini e video.
- `stato`: per ogni voce, `url` con la personalizzazione applicata, `opzioni` (lo schema), `personalizzazione` (oggetto come in `impostazioni.json`, o null), `modoMonitor` effettivo e `riempimento` effettivo. Una volta per messaggio: `tavolozze`. La scelta generale resta in `impostazioni.modoMonitor`; `modo` diventa quello effettivo dello sfondo in onda.

### 4.5 Core: coordinatore

- Tiene le tavolozze e `imp.Personalizzazioni`, riconciliate all'avvio.
- **Calcolo della disposizione**: `CalcolaDisposizione()` usa `ModoEffettivo` dello sfondo in onda e non manda niente. Un solo punto manda al player:
  - `AggiornaDesktop(int dissolvenzaMs)`: ricalcola la disposizione; se cambia l'area chiama `player.ImpostaArea`; poi manda **un solo** `mostra` con la dissolvenza data. Lo usano il cambio di sfondo (`DopoCambio`, dissolvenza delle impostazioni) e le personalizzazioni (600 ms).
  - `RicalcolaDisposizione` resta per monitor e scelta generale: se il modo effettivo dello sfondo in onda cambia passa per `AggiornaDesktop`, altrimenti manda `layout` come oggi.
- **Comandi nuovi**:
  - `personalizza`: `Applica`; errore -> avviso e stato; altrimenti salva, poi `RinfrescaSeInOnda(id)`.
  - `ripristinaSfondo`: toglie la voce, salva, `RinfrescaSeInOnda(id)`.
  - `RinfrescaSeInOnda(id)`: se `id` è in onda e sono cambiati URL, riempimento o modo effettivo, `AggiornaDesktop(600)`. Sempre `InviaStato`.
- **Avvio**: la disposizione si calcola dopo `motore.Avvia`, quando lo sfondo in onda è noto. L'evento `pronto` del player e il percorso di `FineVideo` in `CambiaImpostazioni` passano da `AggiornaDesktop(0)` invece che da `MandaMostra`.
- `Ripristina()` (impostazioni aziendali) azzera anche le personalizzazioni, poi fa **un solo** confronto prima/dopo dello sfondo in onda (URL, riempimento, modo effettivo): se qualcosa è cambiato, `AggiornaDesktop` con la dissolvenza delle impostazioni; altrimenti, se è cambiata solo l'area, `layout`. Non chiama più `RicalcolaDisposizione(conDissolvenza: true)` a parte.
- Eliminando uno sfondo importato si toglie anche la sua voce; importando, `Riconcilia` non cambia nulla per le voci nuove.

### 4.6 Player

- `mostra.riempimento`: per immagini e video `object-fit: cover` con "riempi" (come oggi) e `contain` con "adatta", su fondo `#000` per l'elemento.
- Nient'altro cambia: colori e opzioni arrivano nell'URL; il player già cambia scene a ogni `mostra`.

### 4.7 Finestra: Libreria e barra laterale

**Griglia**
- Clic sull'anteprima di una scheda apre la barra (contorno verde `--verde-chiaro` di 3 px sulla scheda); clic sulla stessa scheda, ✕ o Esc la chiudono e il fuoco torna sull'anteprima. I pulsanti della scheda (playlist, elimina) restano e non aprono la barra.
- Una scheda con personalizzazione ha in alto a destra l'etichetta "PERSONALIZZATO" (carattere mono, verde chiaro su fondo scuro).
- La nota sotto il titolo diventa: "N sfondi, M nella playlist. Clicca uno sfondo per personalizzarlo."

**Barra**: larghezza 400 px a destra della griglia, che si stringe; sotto i 900 px di larghezza della finestra si apre sopra la griglia con un'ombra. Dall'alto:
1. titolo dello sfondo, sotto "Animazione Energy Italy", "Immagine", "Video" o "Pagina HTML", più " · in playlist" se lo è; pulsante ✕;
2. anteprima 16:9: l'animazione su un monitor solo; immagini e video con il riempimento scelto; per le pagine HTML la miniatura attuale. Sotto, in mono: "ANTEPRIMA · le modifiche si salvano da sole";
3. **COLORI** (solo animazioni): griglia di 3 colonne con le tavolozze del manifest più "Personalizzata"; ogni tavolozza ha un cerchio diviso fondo/accento; quella scelta ha il bordo verde. "Personalizzata" mostra sotto due campi `<input type="color">`, "Fondo" e "Accento", con il valore esadecimale;
4. **FUNZIONAMENTO** (solo animazioni con opzioni): per ogni opzione il titolo, il valore con l'unità e il controllo (3.1);
5. **RIEMPIMENTO** (immagini e video): pillole "Riempi" / "Adatta", con la spiegazione "Riempie lo schermo tagliando i bordi" / "Mostra tutto, con bande nere ai lati";
6. **CON PIÙ MONITOR**: pillole "Come in Impostazioni" / "Estendi" / "Duplica" e una riga di spiegazione ("Segue la scelta generale (ora: Estendi)", "Un'unica scena su tutti i monitor", "Ogni monitor mostra la sua copia completa"). Con un monitor solo la riga aggiunge "Vale quando colleghi altri monitor";
7. piede: "Ripristina questo sfondo" (contorno; disattivato se lo sfondo non è personalizzato) e "Mostra ora" (verde; disattivato se è già in onda o non è disponibile).

**Invii e anteprima**
- Mentre si trascina un cursore o si sceglie un colore, la barra aggiorna il valore mostrato e l'anteprima ricostruendo da sola l'URL: prende l'`url` della voce e sostituisce i parametri interessati (`URLSearchParams`); l'iframe si ricarica al massimo ogni 250 ms.
- `personalizza` parte solo alla conferma: rilascio del cursore (`change`), chiusura del selettore di colore (`change`), clic su tavolozza, pillola o interruttore. L'anteprima poi segue l'`url` dello `stato`.
- Uno sfondo non disponibile (file importato sparito) apre la barra con i controlli disattivati e il messaggio "File non trovato".
- Se lo sfondo della barra viene eliminato, la barra si chiude.

**Pagina "In onda"**
- Il palco e le miniature usano `url`, `modoMonitor` e `riempimento` effettivi della voce (`contenutoScena` applica `object-fit` secondo il riempimento).
- I pulsanti Estendi/Duplica del palco mostrano il modo effettivo dello sfondo in onda. Se lo sfondo ha una scelta sua, sono disattivati e sotto compare "Scelto per questo sfondo nella Libreria"; altrimenti cambiano la scelta generale, come oggi.

**Impostazioni**
- I pulsanti "Con più monitor" mostrano `impostazioni.modoMonitor` (la scelta generale). La spiegazione aggiunge "Ogni sfondo può avere la sua scelta nella Libreria".
- La descrizione di "Ripristina impostazioni aziendali" diventa: "Intervallo, ordine, monitor, opzioni e personalizzazioni degli sfondi tornano alle scelte dell'IT. La libreria resta com'è."

### 4.8 Pagine di prova (`web/sviluppo`)

- `prova-player.html`: costruisce gli URL dalle opzioni del manifest 2 (predefiniti) con i colori della prima tavolozza.
- `prova-app.html` simula il programma: legge il manifest 2, tiene le personalizzazioni in memoria, risponde a `personalizza` e `ripristinaSfondo` con le stesse regole essenziali (tavolozze, limiti, valori uguali al predefinito tolti), costruisce gli URL come in 3.2 e manda `tavolozze`, `opzioni`, `personalizzazione`, `modoMonitor` e `riempimento` nello stato. Basta per provare la finestra; le regole complete le provano i test del Core.
- `prova-due-schermi.html`: invariata (passa i parametri all'animazione).

## 5. Animazioni da aggiornare

Tutte e sette (onda-di-luce, onde-di-energia, paesaggio-rinnovabile, circuito, aurora, segnale, trama): colori dai ruoli (3.3) in script e stili, opzioni come nel manifest (Trama legge `distanza`, Aurora usa `Sfondo.interruttore('veli', true)`). Per ognuna si controlla nel browser:
- con Energy Italy, accanto alla versione 1.1.2 (P10);
- con le altre quattro tavolozze;
- con una "Personalizzata" chiara (fondo molto luminoso): deve restare leggibile e il logo bianco deve vedersi; il velo dietro il logo usa `velo`.

## 6. Fuori dall'ambito

- Personalizzazioni decise dall'IT in `impostazioni-predefinite.json`.
- Colori o filtri per immagini, video e pagine HTML importate.
- Rinominare gli sfondi; esportare o condividere le personalizzazioni tra PC.
- Più varianti dello stesso sfondo (per esempio "Segnale notte" e "Segnale alba" insieme in playlist).

## 7. Verifica

- Test del Core:
  - manifest 2: opzioni valide e scartate, tavolozze valide e scartate, ripiego su Energy Italy;
  - `Normalizza` e `Applica`: limiti e passo, valori uguali al predefinito tolti, campi non ammessi per tipo di voce, cambio tavolozza che toglie i colori, passaggio a "personalizzata" senza colori, errori;
  - `ValoreOpzione.PerUrl` con cultura italiana impostata sul thread (punto decimale, `1`/`0`);
  - `personalizzazioni` in `impostazioni.json`: andata e ritorno, campi del tipo sbagliato, `Riconcilia` con sfondi sconosciuti e opzioni non più nel manifest;
  - `Url` con tavolozza e con "personalizzata" (solo fondo e accento);
  - coordinatore: disposizione per sfondo al cambio di sfondo (un solo `mostra`), `personalizza` e `ripristinaSfondo` sullo sfondo in onda (un `mostra` da 600 ms) e su uno non in onda (nessun `mostra`), cambio di sola modalità monitor, `Ripristina` che azzera tutto e aggiorna il desktop, eliminazione di uno sfondo importato, `LeggiComandoApp` per `personalizza`.
- Nel browser (server di sviluppo, `prova-app.html`): apertura e chiusura della barra, tutti i controlli, anteprima che segue il trascinamento, etichetta "PERSONALIZZATO", ripristino, pulsanti del palco con uno sfondo che ha una scelta sua.
- Le sette animazioni come in sezione 5, anche su due schermi con `prova-due-schermi.html`.
- Sul PC di riferimento: uno sfondo forzato su Duplica seguito da uno su Estendi, con il cambio di disposizione al passaggio.
