# Spec - Finestra principale: barra del titolo personalizzata e scheda "In onda" compatta

- Data: 2026-10-14
- Riguarda: `FinestraPrincipale` e la pagina `web/app/` (index.html, app.css, app.js)
- Modifica la spec `2026-10-13-sfondo-animato-design.md`: la decisione **D10** (barra del titolo nativa di Windows) è sostituita dalla D12 qui sotto.
- Bozze approvate (visual companion): barra variante **B** (barra sottile del titolo + intestazione sotto), impaginazione "In onda" variante **A** (due colonne compatte, playlist sotto).

## 1. Problema

1. La finestra usa la barra del titolo standard di Windows (scura, ma generica): l'utente vuole una barra con l'aspetto Energy Italy.
2. Nella scheda "In onda" l'anteprima occupa quasi tutta la larghezza e cresce con la finestra. Alla dimensione minima (960 x 640) la pagina è alta 1405 px su 601 visibili: pulsanti di riproduzione e playlist finiscono sotto il bordo e bisogna scorrere più di due schermate. Anche alla dimensione di apertura (1280 x 860) la playlist non si vede.
3. Tutta la pagina scorre, intestazione compresa: scorrendo per raggiungere i comandi si perdono le schede.

## 2. Decisioni

| # | Decisione | Motivo |
|---|---|---|
| D12 | Barra del titolo disegnata dal programma (C#, WinForms) in una striscia in cima alla finestra; la WebView parte sotto la striscia. La finestra resta una finestra Windows normale (bordo ridimensionabile, ombra DWM, angoli arrotondati); si toglie solo la barra standard. | Con la striscia nativa Windows riconosce pulsanti e area di trascinamento: Snap layouts, menu di sistema, Aero Snap e doppio clic funzionano senza ricostruirli. Una barra in HTML (WebView2 `app-region`) non dà il riquadro Snap sul pulsante "ingrandisci". |
| D13 | Il calcolo "che cosa c'è sotto il mouse" nella striscia (pulsanti, area di trascinamento, bordo superiore per ridimensionare) e la posizione dei pulsanti sono una funzione pura nel Core. | Testabile senza finestra, a scale DPI diverse e con finestra ingrandita o no. |
| D14 | Intestazione HTML (logo, schede, stato) fissa; scorre solo il contenuto sotto. | I comandi di navigazione restano sempre raggiungibili. |
| D15 | "In onda": anteprima a metà larghezza circa e alta al massimo ~40% della finestra; comandi accanto; playlist sotto, visibile senza scorrere alla dimensione di apertura. | Richiesta dell'utente: anteprima troppo grande, pulsanti non visibili. |

## 3. Barra del titolo

### 3.1 Aspetto

- Altezza: 32 DIP (32 px al 100%, 40 px al 125%, 48 px al 150%), cioè `32 * DeviceDpi / 96` arrotondato.
- Sfondo `#050d18`, un tono più scuro del fondo pagina `#07111f`. Nessuna linea tra striscia e intestazione HTML: il cambio di tono basta a separarle.
- A sinistra, a 12 DIP dal bordo: icona del programma 16 DIP (la stessa `Icon` della finestra, disegnata alla dimensione corretta per il DPI), poi a 8 DIP il titolo "Energy Italy Sfondo Animato" in Segoe UI 9 pt, colore `#a9bbcf`; con la finestra non attiva colore `#5d7593`.
- A destra tre pulsanti larghi 46 DIP, alti quanto la striscia: riduci a icona, ingrandisci/ripristina, chiudi. Glifi disegnati a linee (10 DIP, spessore 1 DIP) come quelli di Windows 11, colore `#c6d4e3` (`#5d7593` a finestra non attiva).
  - Passaggio del mouse: riduci/ingrandisci sfondo `#13243c`; chiudi sfondo `#c42b1c` con glifo bianco.
  - Pulsante premuto: riduci/ingrandisci `#1a2f4d`; chiudi `#a1281c`.
  - A finestra ingrandita il glifo di "ingrandisci" diventa "ripristina" (due quadrati sovrapposti).
- Windows 10: la cornice standard non ha più il bordo superiore una volta tolta la barra; la striscia disegna una linea di 1 px in alto, colore `#22395a`, solo su Windows 10 e solo a finestra non ingrandita.

### 3.2 Comportamento

| Azione | Risultato |
|---|---|
| Trascinare la striscia (fuori dai pulsanti) | Sposta la finestra; Aero Snap ai bordi dello schermo |
| Doppio clic sulla striscia | Ingrandisce / ripristina |
| Clic destro sulla striscia, Alt+Spazio | Menu di sistema di Windows |
| Mouse fermo su "ingrandisci" (Windows 11) | Riquadro Snap layouts |
| Clic su riduci / ingrandisci | Come i pulsanti di Windows |
| Clic su chiudi, Alt+F4 | Nasconde la finestra nell'area di notifica (come oggi, D11) |
| Bordo superiore (fascia di 5 DIP in cima alla striscia, finestra non ingrandita) | Ridimensiona dall'alto; angoli in alto ridimensionano in diagonale |
| Bordi sinistro, destro, inferiore | Invariati: li gestisce la cornice standard |

Un clic su un pulsante vale solo se il tasto viene premuto e rilasciato sullo stesso pulsante (come in Windows): premere su "chiudi", spostarsi e rilasciare altrove non chiude.

### 3.3 Funzionamento

`FinestraPrincipale` resta un `Form` con `FormBorderStyle.Sizable`. In `WndProc`:

- **`WM_NCCALCSIZE`** (wParam = TRUE): si lascia calcolare a Windows l'area client, poi si riporta il bordo superiore dell'area client al bordo superiore della finestra, togliendo così la barra standard ma conservando i bordi laterali e inferiore. A finestra ingrandita il bordo superiore si abbassa dello spessore della cornice (`GetSystemMetricsForDpi(SM_CYFRAME) + SM_CXPADDEDBORDER`), altrimenti la striscia finirebbe fuori dallo schermo.
- Dopo la creazione dell'handle si forza il ricalcolo della cornice (`SetWindowPos` con `SWP_FRAMECHANGED`), così la barra standard non compare nemmeno al primo disegno.
- **`WM_NCHITTEST`**: prima il risultato standard (per i bordi laterali e inferiore); se è `HTCLIENT` e il punto cade nella striscia, si restituisce la zona calcolata dal Core: `HTCAPTION`, `HTMINBUTTON`, `HTMAXBUTTON`, `HTCLOSE`, `HTTOP`, `HTTOPLEFT`, `HTTOPRIGHT`. Restituire `HTMAXBUTTON` è ciò che fa comparire il riquadro Snap su Windows 11.
- **Pulsanti**: per le zone pulsante Windows manda messaggi non-client (`WM_NCMOUSEMOVE`, `WM_NCLBUTTONDOWN`, `WM_NCLBUTTONUP`, `WM_NCMOUSELEAVE`). Il programma li gestisce da sé (stato di passaggio e pressione, ridisegno, azione al rilascio) e **non** li passa a `DefWndProc` per quelle zone, che altrimenti disegnerebbe i vecchi pulsanti classici sopra la striscia. Per avere `WM_NCMOUSELEAVE` si usa `TrackMouseEvent` con `TME_NONCLIENT | TME_LEAVE`.
- Azioni: riduci = `WindowState = Minimized`; ingrandisci = alterna `Maximized`/`Normal`; chiudi = `Close()` con `CloseReason.UserClosing`, cioè la stessa strada della X di oggi (`SendMessage(WM_SYSCOMMAND, SC_CLOSE)`), quindi la finestra si nasconde.
- **Disegno**: `OnPaint` del form disegna la striscia (sfondo, icona, titolo, pulsanti nello stato corrente). Ridisegno su: cambio stato attivo/non attivo (`OnActivated`/`OnDeactivate`), cambio di stato ingrandita/normale, cambio DPI (`WM_DPICHANGED`), passaggio/pressione sui pulsanti.
- **WebView**: `controller.Bounds` diventa il rettangolo client **sotto** la striscia (`y = altezzaStriscia`, altezza = client - striscia). `AggiornaWebView` lo ricalcola a ogni ridimensionamento e cambio DPI. La `MinimumSize` attuale (960 x 640 DIP, limitata all'area di lavoro) non cambia.
- Tema scuro DWM (`DWMWA_USE_IMMERSIVE_DARK_MODE`) resta impostato: colora la cornice e il menu di sistema.

### 3.4 Core: `BarraTitolo`

Nuovo file `src/EnergyItaly.SfondoAnimato.Core/BarraTitolo.cs`, senza dipendenze da WinForms:

```csharp
public enum ZonaBarra { Fuori, Trascina, Riduci, Ingrandisci, Chiudi, BordoAlto, AngoloAltoSinistra, AngoloAltoDestra }

public sealed record MisureBarra(int Altezza, int LarghezzaPulsante, int BordoRidimensiona, int Angolo);

public static class BarraTitolo
{
    /// <summary>Misure in pixel per una scala DPI (96 = 100%).</summary>
    public static MisureBarra Misure(int dpi);

    /// <summary>Rettangoli dei tre pulsanti (Riduci, Ingrandisci, Chiudi) per un'area client larga così.</summary>
    public static IReadOnlyList<(ZonaBarra Zona, Rettangolo Area)> Pulsanti(int larghezzaClient, MisureBarra m);

    /// <summary>Che cosa c'è nel punto (coordinate client). Fuori = sotto la striscia.</summary>
    public static ZonaBarra Zona(int x, int y, int larghezzaClient, MisureBarra m, bool ingrandita);
}
```

`Rettangolo` è il tipo già presente in `Geometria.cs` (`X, Y, L, A`). Regole di `Zona`:

1. `y < 0`, `y >= Altezza`, `x < 0` o `x >= larghezzaClient` → `Fuori`.
2. Finestra non ingrandita e `y < BordoRidimensiona`: `x < Angolo` → `AngoloAltoSinistra`; `x >= larghezzaClient - Angolo` → `AngoloAltoDestra`; altrimenti `BordoAlto`. Vale anche sopra i pulsanti (come in Windows 11).
3. Dentro un pulsante → quel pulsante.
4. Altrimenti → `Trascina`.

Misure in DIP: altezza 32, pulsante 46, bordo 5, angolo 16; convertite con `(int)Math.Round(dip * dpi / 96.0)`.

`FinestraPrincipale` traduce `ZonaBarra` nei codici `HT*` e usa `Pulsanti` per disegnare.

## 4. Pagina HTML

### 4.1 Struttura comune

- `html, body` alti quanto la finestra (`height: 100%`), `body` a colonna: `.testata` fissa in alto (non scorre), `.contenuto` con `overflow-y: auto` e `flex: 1`. Barra di scorrimento solo nel contenuto.
- La testata resta com'è (logo, schede, stato).
- Libreria e Impostazioni: nessun cambiamento di struttura; scorrono dentro `.contenuto`.

### 4.2 Scheda "In onda"

Riga superiore a due colonne (`.riga-onda`):

- **Colonna anteprima** (`.palco`): circa metà della larghezza disponibile (`flex: 1 1 0`, il pannello comandi altrettanto, con il pannello mai sotto 300 px). Si toglie il riquadro decorato attorno all'anteprima (bordo, sfondo a gradiente e spaziatura interna del `.palco` attuale): restano didascalia, monitor e nomi dei monitor, più compatti (didascalia su una riga: `IL TUO DESKTOP · 1 MONITOR · 2560x1440`).
  - L'area dei monitor (`.schermi`) mantiene le proporzioni del desktop ed entra in un riquadro largo quanto la colonna e alto al massimo `40vh`: se il desktop è molto largo (3 monitor estesi) si restringe in altezza, se la finestra è bassa si restringe in larghezza e si centra. Le miniature in scala (`inScala`) seguono già la larghezza del contenitore e non cambiano.
  - "Estendi / Duplica" resta accanto alla didascalia (solo con 2+ monitor).
- **Colonna comandi** (`.pannello-onda`), centrata in verticale rispetto all'anteprima:
  - etichetta di stato (IN ONDA / IN PAUSA), titolo (`h1` da 44 a 32 px), posizione nella playlist;
  - tempo (da 40 a 32 px) e barra di avanzamento;
  - pulsanti precedente / pausa / successivo invariati;
  - riquadro "Poi": sotto i pulsanti quando la colonna è larga almeno 360 px, altrimenti accanto ai pulsanti in forma ridotta (miniatura 64 px, una riga di testo). Decisione con container query sulla colonna, non con media query sulla finestra.
- **Playlist** sotto la riga:
  - testata su una riga: titolo "Playlist" e nota a sinistra, controlli (intervallo, ordine) a destra; se lo spazio non basta i controlli vanno a capo sotto il titolo, ma restano visibili.
  - L'interruttore "I video cambiano a fine riproduzione" si toglie dalla scheda "In onda" (resta in Impostazioni, `#sw-fine-video`). Si toglie anche il gestore JS dell'interruttore rimosso (`#sw-fine-video-playlist`).
  - Striscia delle schede orizzontale come oggi, schede larghe 168 px (da 200).
- Spaziature verticali: `.contenuto` padding 20 px (da 32/48), gap tra le sezioni 24 px (da 40).

### 4.3 Obiettivi misurabili

Misure dell'area della pagina (cioè la finestra meno la striscia del titolo):

| Finestra | Area pagina | Deve vedersi senza scorrere |
|---|---|---|
| 1280 x 860 (apertura) | circa 1264 x 789 | Tutto: anteprima, comandi, "Poi", testata playlist con controlli, schede playlist intere |
| 960 x 640 (minima) | circa 944 x 569 | Anteprima, comandi (pulsanti compresi), testata playlist con controlli; schede playlist almeno per metà |
| Ingrandita su 2560 x 1440 | circa 2560 x 1360 | Tutto; l'anteprima non supera il 40% dell'altezza, lo spazio in più va alla playlist (`.contenuto` resta limitato a 1440 px e centrato) |

## 5. Fuori dall'ambito

- Consumo della finestra principale quando è aperta (barre di avanzamento e miniature): rilevato nella sessione del 2026-10-13, rimandato dall'utente.
- Nuove funzioni nelle schede Libreria e Impostazioni.
- Barra del titolo per altre finestre (non ce ne sono).

## 6. Errori e casi limite

- Cambio DPI spostando la finestra su un monitor con scala diversa: `WM_DPICHANGED` → nuove misure, nuovi limiti della WebView, ridisegno della striscia.
- Finestra ingrandita su un monitor con barra delle applicazioni nascosta automaticamente: si usa il comportamento standard di `WM_NCCALCSIZE` per l'area ingrandita; nessun calcolo personalizzato dell'area di lavoro.
- Se la WebView viene ricreata dopo un errore (`RicreaWebView`), i nuovi limiti tengono già conto della striscia (passano da `AggiornaWebView`).
- Contrasto alto di Windows: la striscia usa i colori di sistema (`SystemColors.ActiveCaption`, `ActiveCaptionText`, `Highlight`) invece dei colori Energy Italy quando `SystemInformation.HighContrast` è attivo.

## 7. Verifica

**Test automatici (Core)** - `tests/.../BarraTitoloTests.cs`:
- misure a 96, 120, 144 DPI;
- posizione dei tre pulsanti allineati a destra, senza spazi tra loro;
- `Zona`: area di trascinamento, ciascun pulsante, bordo e angoli in alto a finestra normale, nessun bordo a finestra ingrandita, `Fuori` sotto la striscia e fuori dai limiti, ai margini esatti dei rettangoli.

**Pagina (browser, `strumenti/server_sviluppo.py` + `sviluppo/prova-app.html`)**: alle tre aree della tabella 4.3, con 1, 2 e 3 monitor (`?monitor=1|2|3`): controllare con screenshot e misure (`scrollHeight`, posizione dei pulsanti) gli obiettivi della 4.3; testata fissa durante lo scorrimento in Libreria e Impostazioni.

**Programma (a mano, sul PC)**: trascinamento, doppio clic, menu di sistema (clic destro e Alt+Spazio), Snap layouts su "ingrandisci", Aero Snap ai bordi, ridimensionamento da tutti i bordi e angoli, ingrandita senza parti fuori schermo, riduci/ripristina dalla barra delle applicazioni, chiudi e Alt+F4 che nascondono, stato non attivo, cambio di scala (100% / 125% / 150%) e spostamento tra monitor con scale diverse, contrasto alto.
