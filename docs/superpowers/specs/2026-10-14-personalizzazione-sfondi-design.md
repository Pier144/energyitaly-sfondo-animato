# Spec - Personalizzazione degli sfondi

- Data: 2026-10-14
- Riguarda: barra laterale nella pagina "Libreria", colori e opzioni di ogni animazione, Estendi/Duplica per singolo sfondo, riempimento di immagini e video, contratto delle animazioni (`sfondo.js`, `manifest.json`).
- Scelte dell'utente: barra per **tutti** gli sfondi, con opzioni diverse per tipo; colori con **tavolozze pronte più "Personalizzata"** (fondo e accento); **anteprima nella barra**, il desktop cambia solo quando lo sfondo è in onda; "Ripristina impostazioni aziendali" **cancella anche** le personalizzazioni; approccio A (opzioni dichiarate nel manifest, colori comuni a tutte le animazioni).
- Bozza approvata: `.superpowers/brainstorm/1148-1791968172/barra-laterale.html`.

## 1. Obiettivo

Dalla Libreria si sceglie uno sfondo e lo si adatta: colori, funzionamento (velocità e opzioni proprie di ogni animazione) e comportamento con più monitor. Le scelte valgono solo su quel PC, si salvano da sole e si annullano con un clic.

## 2. Decisioni

| # | Decisione | Motivo |
|---|---|---|
| P1 | Clic su una scheda della Libreria: si apre la barra laterale di quello sfondo (non va più subito in onda). Per mandarlo in onda c'è "Mostra ora" nella barra. | Il clic serve a scegliere cosa personalizzare; "Mostra ora" resta a un clic di distanza. |
| P2 | Le opzioni di ogni animazione sono dichiarate in `manifest.json` (tipo, limiti, valore predefinito, titolo). La barra si costruisce da lì. | Un'animazione nuova ottiene la barra senza toccare il codice del programma. |
| P3 | I colori sono comuni a tutte le animazioni: tre colori base (fondo, accento, secondario) arrivano nella query string; `sfondo.js` ne ricava le sfumature e le offre alla pagina. Le tavolozze pronte stanno in `manifest.json`. | Una sola regola per tutte le animazioni; l'app mostra le stesse tavolozze che le animazioni ricevono. |
| P4 | "Personalizzata" fa scegliere fondo e accento; il secondario si ricava dal fondo. | Due selettori bastano e non si può creare un trio stonato. |
| P5 | Con più monitor ogni sfondo può seguire la scelta generale (Impostazioni) oppure forzare Estendi o Duplica. La disposizione dipende dallo sfondo in onda. | Richiesta dell'utente: Segnale, per esempio, rende meglio esteso; un'immagine può rendere meglio duplicata. |
| P6 | Immagini e video importati: Estendi/Duplica e riempimento ("Riempi" taglia i bordi, "Adatta" lascia bande nere). Pagine HTML importate: solo Estendi/Duplica. | Colori e opzioni non hanno senso per file che il programma non controlla. |
| P7 | Le personalizzazioni stanno in `impostazioni.json` (campo `personalizzazioni`), solo come differenze dai valori predefiniti. Uno sfondo senza differenze non ha voce. | Il file resta piccolo e leggibile; "personalizzato" significa "ha una voce". |
| P8 | Ogni modifica si salva subito. Se lo sfondo è in onda, il desktop si aggiorna con una dissolvenza breve (600 ms); altrimenti il desktop non cambia. | Nessun pulsante "Salva"; niente salti di sfondo mentre si prova (scelta dell'utente). |
| P9 | "Ripristina questo sfondo" toglie la sua voce. "Ripristina impostazioni aziendali" toglie tutte le voci. | Scelta dell'utente. |
| P10 | Con la tavolozza Energy Italy e le opzioni predefinite ogni animazione resta come oggi (sono ammesse differenze minime di tonalità dovute al calcolo delle sfumature). | La personalizzazione non deve cambiare l'aspetto aziendale di chi non tocca niente. |
| P11 | La logica (lettura del manifest, validazione, unione con i predefiniti, URL, disposizione per sfondo) sta nel Core come funzioni pure e si prova con i test, come il resto. | Stessa architettura delle altre funzioni. |

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
  - `numero`: `min`, `max`, `passo`, `predefinito`, `unita` facoltativa. Nella barra: cursore con il valore accanto.
  - `scelta`: `valori` come elenco di `{ "valore": "lieve", "titolo": "Lieve" }`, `predefinito`. Nella barra: pulsanti a pillola (fino a 4 valori) o menu.
  - `interruttore`: `predefinito` true/false. Nella barra: interruttore come quelli di Impostazioni.
- `parametri` (versione 1) sparisce: ogni valore che oggi sta in `parametri` diventa un'opzione con quel predefinito. Opzioni proposte:

| Animazione | Opzioni |
|---|---|
| Onda di luce | Ampiezza (scelta: lieve, media, forte), Durata dell'onda (numero, 8-30 s), Intensità (numero, 0.4-1) |
| Onde di energia | Durata (numero, 40-160 s), Linee (numero, 12-40) |
| Paesaggio rinnovabile | Velocità |
| Circuito | Tracce (numero, 12-40), Velocità |
| Aurora | Velocità, Veli di luce (interruttore) |
| Segnale | Velocità, Anelli per impulso |
| Trama | Velocità, Distanza tra i marchi (numero, 90-200) |

- Validazione nel Core (tollerante, come `impostazioni.json`): un'opzione con tipo sconosciuto, limiti incoerenti o predefinito fuori limiti viene scartata con un avviso nel log; l'animazione resta disponibile. Una tavolozza con colori non validi viene scartata; se non ne resta nessuna vale Energy Italy scritta nel codice.

### 3.2 Query string dell'animazione

`segnale.html?fondo=0f1e35&accento=7ab52a&secondario=1e5b8e&velocita=1.5&onde=2&monitor=...`

- I colori sono esadecimali senza `#`. Si passano sempre tutti e tre, anche per la tavolozza predefinita.
- Si passano sempre tutte le opzioni con il valore effettivo (predefinito o personalizzato).

### 3.3 `sfondo.js`

Nuova funzione `Sfondo.colori()`, che restituisce un oggetto con:

- `fondo`, `accento`, `secondario`: i tre colori ricevuti (Energy Italy se mancano o non sono validi);
- sfumature ricavate in HSL: `fondoScuro` (luminosità -6), `fondoChiaro` (+8), `accentoChiaro` (+20), `secondarioChiaro` (+20);
- `rgba(nome, alfa)`: la stringa `rgba(...)` di uno dei colori sopra, per gradienti e trasparenze.

Ogni animazione sostituisce i colori scritti nel codice con questi ruoli: i blu notte con `fondo`/`fondoScuro`/`fondoChiaro`, i verdi con `accento`/`accentoChiaro`, i blu con `secondario`/`secondarioChiaro`. Il logo resta bianco.

Il secondario della tavolozza "Personalizzata" si ricava dal fondo: stessa tinta, saturazione +20, luminosità +22 (limitate a 0-100). Il calcolo sta in `sfondo.js` (per le animazioni) e nel Core (per l'URL), con gli stessi numeri; un test confronta i due risultati su alcuni colori.

### 3.4 PRD

La sezione 6 "Contratto animazioni" di `docs/PRD.md` si aggiorna con i punti 3.1-3.3.

## 4. Nel programma

### 4.1 Core: catalogo

- `Catalogo` legge il manifest versione 2: tavolozze e, per ogni animazione, l'elenco `Opzioni` (record `Opzione`: id, titolo, tipo, min, max, passo, unita, valori, predefinito). `Voce.Parametri` sparisce.
- Una voce importata non ha opzioni.

### 4.2 Core: `Personalizzazione`

Nuovo file `Personalizzazione.cs`:

```csharp
public sealed record Personalizzazione
{
    public string? Tavolozza { get; init; }                         // id di tavolozza o "personalizzata"; null = predefinita
    public string? Fondo { get; init; }                             // "#rrggbb", solo con "personalizzata"
    public string? Accento { get; init; }
    public IReadOnlyDictionary<string, string> Opzioni { get; init; } // solo quelle diverse dal predefinito
    public ModoMonitor? ModoMonitor { get; init; }                  // null = come in Impostazioni
    public Riempimento? Riempimento { get; init; }                  // solo immagini e video; null = Riempi
    public bool Vuota { get; }
}
```

Funzioni pure:

- `Applica(Personalizzazione, ModifichePersonalizzazione, Voce, tavolozze)`: valida e restituisce la nuova personalizzazione o l'errore da mostrare. Un numero fuori limiti si porta al limite e si arrotonda al passo; una scelta sconosciuta, un colore non valido o un campo che non vale per quel tipo di voce è un errore. Un valore uguale al predefinito si toglie (P7).
- `ColoriEffettivi(Personalizzazione?, tavolozze)`: i tre colori da passare all'animazione.
- `ModoEffettivo(Personalizzazione?, Impostazioni)`.

`Catalogo.UrlBase(v)` diventa `Catalogo.Url(v, personalizzazione, tavolozze)`: per le animazioni aggiunge colori e opzioni (3.2); per gli altri tipi l'URL non cambia.

### 4.3 Core: `impostazioni.json`

```json
"personalizzazioni": {
  "segnale": { "tavolozza": "notte", "opzioni": { "velocita": "1.5" }, "modoMonitor": "estendi" },
  "u-i-ab12cd": { "modoMonitor": "duplica", "riempimento": "adatta" }
}
```

- Si legge in modo tollerante: una voce di sfondo sconosciuto si toglie con un avviso (come la playlist); un campo non valido si toglie con un avviso e il resto della voce resta. Le opzioni si validano contro il manifest al caricamento.
- `impostazioni-predefinite.json` non legge questo campo (sezione 6).
- Il formato resta `versione: 2`: il campo è nuovo e facoltativo.

### 4.4 Core: coordinatore

- Tiene `imp.Personalizzazioni` e le tavolozze del manifest.
- La disposizione si calcola con `ModoEffettivo` dello sfondo in onda. A ogni cambio di sfondo, se il modo effettivo cambia, si ricalcola prima di mandare `mostra` (il player sa già cambiare scene a ogni `mostra`). La pagina "In onda" mostra la disposizione dello sfondo in onda.
- Nuovi comandi dalla finestra:
  - `{ "tipo": "personalizza", "id": "segnale", "modifiche": { "tavolozza": "personalizzata", "fondo": "#102030", "accento": "#e0a040", "opzioni": { "velocita": 1.5 }, "modoMonitor": "generale" | "estendi" | "duplica", "riempimento": "riempi" | "adatta" } }`: tutti i campi facoltativi; si applica, si salva e, se lo sfondo è in onda, si rimanda `mostra` con dissolvenza di 600 ms (o si ricalcola la disposizione se cambia solo il modo).
  - `{ "tipo": "ripristinaSfondo", "id": "segnale" }`: toglie la voce, poi come sopra.
- `Ripristina()` (impostazioni aziendali) azzera anche `Personalizzazioni`.
- Eliminando uno sfondo importato si toglie anche la sua voce.
- Errori di validazione: avviso nella finestra, come per le impostazioni.

### 4.5 Messaggi

- `mostra`: l'URL contiene già colori e opzioni; nuovo campo `riempimento` (`"riempi"` o `"adatta"`) per immagini e video.
- `stato`, per ogni voce: `url` con la personalizzazione applicata (le miniature della Libreria e di "In onda" mostrano lo sfondo personalizzato), `opzioni` (lo schema), `personalizzazione` (oggetto o null), `modoMonitor` effettivo. In più, una volta per messaggio, `tavolozze`.

### 4.6 Player

- Immagini e video: `object-fit: cover` per "riempi", `contain` su fondo nero per "adatta".
- Nient'altro cambia: colori e opzioni arrivano nell'URL.

### 4.7 Finestra: Libreria e barra laterale

Come nella bozza approvata.

- **Griglia**: clic su una scheda apre la barra (la scheda ha il contorno verde); clic sulla stessa scheda, ✕ o Esc la chiudono e il fuoco torna sulla scheda. Una scheda con personalizzazione ha l'etichetta "PERSONALIZZATO". Il testo sotto il titolo diventa "Clicca uno sfondo per personalizzarlo". I pulsanti della scheda (playlist, elimina) restano e non aprono la barra.
- **Barra** (400 px a destra; sotto i 900 px di larghezza della finestra si apre sopra la griglia):
  1. titolo, tipo, "in playlist" o no;
  2. anteprima: l'animazione con l'URL personalizzato su un monitor solo; per immagini e video il file con il riempimento scelto; per le pagine HTML la miniatura attuale. Nota "Le modifiche si salvano da sole";
  3. **Colori** (solo animazioni): le tavolozze del manifest più "Personalizzata", che apre due `<input type="color">` (Fondo e Accento) partendo dai colori della tavolozza scelta prima;
  4. **Funzionamento** (solo animazioni con opzioni): un controllo per opzione (3.1);
  5. **Riempimento** (immagini e video): Riempi / Adatta;
  6. **Con più monitor**: Come in Impostazioni / Estendi / Duplica, con una riga di spiegazione; con un monitor solo la riga dice che vale quando se ne collegano altri;
  7. piede: "Ripristina questo sfondo" (disattivato se non c'è nulla da ripristinare) e "Mostra ora" (disattivato se lo sfondo è già in onda o non è disponibile).
- I cursori aggiornano il valore mentre si trascinano e mandano `personalizza` quando si rilasciano; i selettori di colore lo mandano al massimo ogni 300 ms mentre si sceglie. L'anteprima si aggiorna con lo `stato` successivo.
- Uno sfondo non disponibile (file importato sparito) apre la barra con i controlli disattivati e il messaggio "File non trovato".
- **Impostazioni**: sotto "Con più monitor" la spiegazione aggiunge "Ogni sfondo può avere la sua scelta nella Libreria". La descrizione di "Ripristina impostazioni aziendali" aggiunge le personalizzazioni.

## 5. Animazioni da aggiornare

Tutte e sette (onda-di-luce, onde-di-energia, paesaggio-rinnovabile, circuito, aurora, segnale, trama): colori da `Sfondo.colori()`, parametri come opzioni nel manifest. Per ognuna si controlla nel browser l'aspetto con le cinque tavolozze e con una "Personalizzata" chiara (fondo molto luminoso), per verificare che resti leggibile e che il logo bianco si veda; dove serve, il velo scuro dietro il logo usa `fondoScuro`.

## 6. Fuori dall'ambito

- Personalizzazioni decise dall'IT in `impostazioni-predefinite.json`.
- Colori o filtri per immagini, video e pagine HTML importate.
- Rinominare gli sfondi; esportare o condividere le personalizzazioni tra PC.
- Più varianti dello stesso sfondo (per esempio "Segnale notte" e "Segnale alba" insieme in playlist).

## 7. Verifica

- Test del Core: lettura del manifest 2 (opzioni valide e scartate, tavolozze), `Applica` (limiti, passo, valori uguali al predefinito, campi non ammessi per tipo), lettura e scrittura di `personalizzazioni` (andata e ritorno, voci e campi non validi), URL con colori e opzioni, secondario ricavato, modo effettivo e disposizione per sfondo nel coordinatore, `personalizza` e `ripristinaSfondo` sullo sfondo in onda e su uno non in onda, `Ripristina` che azzera tutto, eliminazione di uno sfondo importato.
- Nel browser (server di sviluppo e `prova-app.html`): apertura e chiusura della barra, tutti i controlli, anteprima che segue le modifiche, etichetta "PERSONALIZZATO", ripristino.
- Le sette animazioni con ogni tavolozza (sezione 5) e con la tavolozza Energy Italy confrontata con l'aspetto attuale (P10), anche su due schermi con `prova-due-schermi.html`.
- Sul PC di riferimento: uno sfondo forzato su Duplica e il successivo su Estendi, con il cambio di disposizione al passaggio.
