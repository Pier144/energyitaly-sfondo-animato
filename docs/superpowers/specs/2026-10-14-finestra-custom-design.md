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
- A sinistra, a 12 DIP dal bordo: icona del programma 16 DIP (la stessa `Icon` della finestra, disegnata alla dimensione corretta per il DPI), poi a 8 DIP il titolo "Energy Italy Sfondo Animato" in Segoe UI 12 px reali (`Font` creato con `GraphicsUnit.Pixel`, `12 * dpi / 96`, ricreato a ogni cambio DPI: un font in punti seguirebbe i DPI di sistema e non quelli del monitor), colore `#a9bbcf`; con la finestra non attiva colore `#5d7593`.
- A destra tre pulsanti larghi 46 DIP, alti quanto la striscia: riduci a icona, ingrandisci/ripristina, chiudi. Glifi disegnati a linee (10 DIP, spessore 1 DIP) come quelli di Windows 11, colore `#c6d4e3` (`#5d7593` a finestra non attiva).
  - Passaggio del mouse: riduci/ingrandisci sfondo `#13243c`; chiudi sfondo `#c42b1c` con glifo bianco.
  - Pulsante premuto: riduci/ingrandisci `#1a2f4d`; chiudi `#a1281c`.
  - A finestra ingrandita il glifo di "ingrandisci" diventa "ripristina" (due quadrati sovrapposti).
- Windows 10 (`Environment.OSVersion.Version.Build < 22000`): la cornice standard non ha più il bordo superiore una volta tolta la barra; la striscia disegna una linea di 1 px in alto, colore `#22395a`, solo su Windows 10 e solo a finestra non ingrandita.
- I pulsanti disegnati non ricevono il fuoco da tastiera né sono letti dall'Assistente vocale: scelta accettata, perché da tastiera restano Alt+Spazio (menu di sistema con Riduci/Ingrandisci/Chiudi) e Alt+F4, come in molte barre personalizzate.

### 3.2 Comportamento

| Azione | Risultato |
|---|---|
| Trascinare la striscia (fuori dai pulsanti) | Sposta la finestra; Aero Snap ai bordi dello schermo |
| Doppio clic sulla striscia | Ingrandisce / ripristina |
| Clic destro sulla striscia, Alt+Spazio | Menu di sistema di Windows |
| Mouse fermo su "ingrandisci" (Windows 11) | Riquadro Snap layouts |
| Clic su riduci / ingrandisci | Come i pulsanti di Windows |
| Clic su chiudi, Alt+F4 | Nasconde la finestra nell'area di notifica (come oggi, D11) |
| Bordo superiore (fascia di 8 DIP in cima alla striscia, finestra non ingrandita: quanto la cornice standard a 100%) | Ridimensiona dall'alto; angoli in alto ridimensionano in diagonale |
| Bordi sinistro, destro, inferiore | Invariati: li gestisce la cornice standard |

Un clic su un pulsante vale solo se il tasto viene premuto e rilasciato sullo stesso pulsante (come in Windows): premere su "chiudi", spostarsi e rilasciare altrove non chiude.

### 3.3 Funzionamento

`FinestraPrincipale` resta un `Form` con `FormBorderStyle.Sizable`. In `WndProc`:

- **`WM_NCCALCSIZE`** (wParam = TRUE): si lascia calcolare a Windows l'area client, poi si riporta il bordo superiore dell'area client al bordo superiore della finestra, togliendo così la barra standard ma conservando i bordi laterali e inferiore. A finestra ingrandita il bordo superiore si abbassa dello spessore della cornice (`GetSystemMetricsForDpi(SM_CYFRAME, dpi) + GetSystemMetricsForDpi(SM_CXPADDEDBORDER, dpi)`), altrimenti la striscia finirebbe fuori dallo schermo.
- **Barra delle applicazioni nascosta automaticamente**: a finestra ingrandita l'area client coprirebbe tutto il monitor e Windows la tratterebbe come schermo intero, così la barra nascosta non ricomparirebbe più. Come Windows Terminal: se sul monitor della finestra c'è una barra a scomparsa (`SHAppBarMessage(ABM_GETAUTOHIDEBAREX)` per ciascun lato), in `WM_NCCALCSIZE` a finestra ingrandita si toglie 2 px all'area client su quel lato.
- Dopo la creazione dell'handle si forza il ricalcolo della cornice (`SetWindowPos` con `SWP_FRAMECHANGED`), così la barra standard non compare nemmeno al primo disegno.
- **`WM_NCHITTEST`**: prima il risultato standard (per i bordi laterali e inferiore); se è `HTCLIENT` e il punto cade nella striscia (coordinate da `lParam` lette come `short`, perché sui monitor a sinistra o sopra il principale sono negative, poi `PointToClient`), si restituisce la zona calcolata dal Core: `HTCAPTION`, `HTMINBUTTON`, `HTMAXBUTTON`, `HTCLOSE`, `HTTOP`, `HTTOPLEFT`, `HTTOPRIGHT`. Restituire `HTMAXBUTTON` è ciò che fa comparire il riquadro Snap su Windows 11.
- **Pulsanti**: per le zone pulsante Windows manda messaggi non-client (`WM_NCMOUSEMOVE`, `WM_NCLBUTTONDOWN`, `WM_NCLBUTTONDBLCLK`, `WM_NCLBUTTONUP`, `WM_NCMOUSELEAVE`). Il programma li gestisce da sé e, quando la zona è un pulsante, **non** passa a `DefWndProc` né `WM_NCMOUSEMOVE` né `WM_NCLBUTTONDOWN` né `WM_NCLBUTTONDBLCLK` né `WM_NCLBUTTONUP` (restituisce 0, come Windows Terminal; il riquadro Snap compare lo stesso, perché dipende da `HTMAXBUTTON` in `WM_NCHITTEST`): `DefWndProc` disegnerebbe i vecchi pulsanti classici sopra la striscia e avvierebbe il proprio tracciamento modale. `WM_NCLBUTTONDBLCLK` (il secondo clic rapido, che arriva sempre per le zone non-client) vale come una pressione.
  - Stato: `passaggio` (zona pulsante sotto il mouse) e `premuto` (zona su cui è iniziata la pressione). Su `WM_NCLBUTTONUP` l'azione parte solo se la zona del rilascio è uguale a `premuto`; `premuto` si azzera a **ogni** `WM_NCLBUTTONUP` (anche su zone non pulsante) e a `WM_NCMOUSELEAVE`, perché senza cattura del mouse un rilascio fuori dalla finestra non arriva.
  - Per avere `WM_NCMOUSELEAVE` si usa `TrackMouseEvent` con `TME_NONCLIENT | TME_LEAVE`, riarmato al primo `WM_NCMOUSEMOVE` dopo ogni uscita.
- Azioni, tutte con `WM_SYSCOMMAND` come i pulsanti veri (così Windows applica animazioni e stato Snap): riduci = `SC_MINIMIZE`; ingrandisci = `SC_MAXIMIZE` o `SC_RESTORE` se già ingrandita; chiudi = `SC_CLOSE`, che arriva a `OnFormClosing` con `CloseReason.UserClosing` e quindi nasconde la finestra, come oggi.
- **Disegno**: `OnPaint` del form disegna la striscia (sfondo, icona, titolo, pulsanti nello stato corrente) con doppio buffer; ogni ridisegno invalida solo il rettangolo della striscia. Ridisegno su: cambio stato attivo/non attivo (`OnActivated`/`OnDeactivate`), cambio di stato ingrandita/normale, cambio DPI, passaggio/pressione sui pulsanti, `SystemEvents.UserPreferenceChanged` (contrasto alto acceso a programma aperto).
- **Cambio DPI**: si usa `OnDpiChanged` (o si passa sempre `WM_DPICHANGED` a `base.WndProc`) per nuove misure, nuovo font, nuovi limiti della WebView. WinForms calcola la nuova dimensione supponendo la barra standard: va verificato a mano che passando più volte tra monitor con scale diverse la finestra non cresca o cali a ogni passaggio; se succede, si corregge in `WM_GETDPISCALEDSIZE` restituendo la dimensione scalata direttamente.
- **WebView**: un solo metodo `AreaWebView()` = rettangolo client **sotto** la striscia: `(0, h, larghezzaClient, max(0, altezzaClient - h))` con `h` = altezza della striscia. Lo usano sia `InizializzaWeb` (oggi passa `ClientRectangle`) sia `AggiornaWebView`, così la WebView non copre la striscia nemmeno per un istante. Con la finestra ridotta a icona `AggiornaWebView` non tocca i limiti (l'area client è 0 x 0) e si limita a `IsVisible = false`, come oggi. La `MinimumSize` attuale (960 x 640 DIP, limitata all'area di lavoro) non cambia.
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

Misure in DIP: altezza 32, pulsante 46, bordo 8, angolo 16; convertite con `(int)Math.Round(dip * dpi / 96.0)`.

`Pulsanti` restituisce sempre i tre rettangoli, allineati al bordo destro e affiancati senza spazi (Chiudi più a destra). Se l'area è più stretta di tre pulsanti (non succede con la `MinimumSize`, ma il test lo fissa) i rettangoli possono avere `X` negativa: si tagliano a sinistra, non si riducono.

`FinestraPrincipale` traduce `ZonaBarra` nei codici `HT*` e usa `Pulsanti` per disegnare.

## 4. Pagina HTML

### 4.1 Struttura comune

- `html, body` alti quanto la finestra (`height: 100%`, `overflow: hidden`), `body` a colonna:
  - `.testata` in alto, non scorre;
  - nuovo contenitore `.scorrimento` (`flex: 1; min-height: 0; overflow-y: auto`), largo quanto la finestra: la barra di scorrimento sta sul bordo destro della finestra e la rotella funziona anche sui margini laterali;
  - dentro, `.contenuto` com'è oggi: `max-width: 1440px`, centrato.
- La testata resta com'è (logo, schede, stato), ma lo stato (`.stato-app`) si mostra **solo in pausa** ("IN PAUSA: DESKTOP COPERTO" e simili): "ATTIVO SU N MONITOR" non compare più, perché è la condizione normale (richiesta dell'utente dopo l'anteprima).
- Libreria e Impostazioni: nessun cambiamento di struttura; scorrono dentro `.scorrimento`. Cambiando scheda lo scorrimento torna in cima.
- `.avviso` e `.velo` sono `position: fixed`: restano invariati.

### 4.2 Scheda "In onda"

Riga superiore a due colonne (`.riga-onda`), sempre affiancate (niente `flex-wrap`: con la larghezza minima della pagina, circa 944 px, ciascuna colonna ha circa 440 px):

- **Colonna anteprima** (`.palco`): metà della larghezza disponibile (`flex: 1 1 0; min-width: 0`). Si toglie il riquadro decorato attorno all'anteprima (bordo, sfondo a gradiente e spaziatura interna del `.palco` attuale): restano didascalia, monitor e nomi dei monitor, più compatti.
  - Didascalia su una riga (`didascalia()` in `app.js`): `IL TUO DESKTOP · 1 MONITOR · 2560x1440`, `IL TUO DESKTOP · 3 MONITOR ESTESI · 5760x1087`, `IL TUO DESKTOP · STESSA SCENA SU 3 MONITOR`.
  - Il blocco dei monitor (`.palco-schermi`, cioè `.schermi` **più** `.etichette-monitor`, così i nomi restano allineati ai monitor) mantiene le proporzioni del desktop ed entra in un riquadro largo quanto la colonna; i soli monitor (`.schermi`) sono alti al massimo `40vh`, le etichette sotto aggiungono circa 26 px: se il desktop è molto largo (3 monitor estesi) si abbassa, se la finestra è bassa si restringe in larghezza e si centra. In CSS: `.palco-schermi { width: min(100%, calc(40vh * W / H)); margin: 0 auto }` con `W / H` del desktop impostati da `costruisciPalco` come variabile CSS. Le miniature in scala (`inScala`) seguono già la larghezza del contenitore e non cambiano.
  - "Estendi / Duplica" resta accanto alla didascalia (solo con 2+ monitor).
- **Colonna comandi** (`.pannello-onda`, `flex: 1 1 0; min-width: 0`), centrata in verticale rispetto all'anteprima:
  - etichetta "● IN PAUSA" solo quando lo sfondo è in pausa (la scritta "IN ONDA" non compare più, richiesta dell'utente), titolo (`h1` da 44 a 32 px), posizione nella playlist;
  - tempo (da 40 a 32 px) e, sulla stessa riga allineato a destra, l'intervallo in forma compatta: "ogni [30] [minuti ▾] Mai" (stesso `#gruppo-intervallo`, senza riquadro e senza l'etichetta "CAMBIA OGNI"); sotto, la barra di avanzamento. Il messaggio d'errore dell'intervallo (`#intervallo-errore`) compare sotto la barra.
  - una riga con, nell'ordine: pulsante **ordine casuale** (icona frecce incrociate, 44 px, senza bordo; `aria-pressed` e colore verde chiaro su fondo verde tenue quando l'ordine è casuale; un clic alterna `casuale` / `sequenziale`; sostituisce il gruppo "In ordine / Casuale", che si toglie), pulsanti precedente / pausa / successivo (invariati) e, accanto, il riquadro "Poi" in forma ridotta (miniatura 64 px, "POI" e titolo su due righe, titolo con i puntini se lungo). La riga va a capo (`flex-wrap`) solo se lo spazio non basta: in pratica "Poi" sta sempre accanto ai pulsanti, e la colonna resta abbastanza bassa da stare accanto all'anteprima.
- **Playlist** sotto la riga:
  - testata: solo titolo "Playlist" e nota (la nota si accorcia con i puntini se lo spazio non basta). I controlli di intervallo e ordine non stanno più qui (variante **C** "stile lettore", scelta dall'utente tra tre provate nell'anteprima): si tolgono `.controlli-playlist` e `.riga-controlli`.
  - Nota: "Trascina per cambiare l'ordine, clicca un'anteprima per mostrarla subito." (senza "Dissolvenza di N s", richiesta dell'utente).
  - L'interruttore "I video cambiano a fine riproduzione" si toglie dalla scheda "In onda" (resta in Impostazioni, `#sw-fine-video`). In `app.js` vanno tolti **entrambi** gli usi di `#sw-fine-video-playlist`: l'aggiornamento in `renderControlli` (`setAttribute('aria-checked', ...)`, che altrimenti lancerebbe un `TypeError` su `null` e fermerebbe il disegno) e il gestore del clic. In CSS si toglie `.riga-interruttore`.
  - Striscia delle schede orizzontale come oggi, schede larghe 168 px (da 200); anche `.aggiungi-playlist` larga 168 px, con `min-height` adattata all'altezza delle schede.
- Spaziature: `.contenuto` padding 20 px su tutti i lati (oggi 32/28/48); gap 24 px sia in `.contenuto` sia in `.vista` (oggi 40 entrambi).

### 4.3 Obiettivi misurabili

Area della pagina = finestra meno la cornice standard (circa 8 px a sinistra, a destra e in basso a 100%; in alto non resta nulla) meno la striscia del titolo (32 px):

| Finestra | Area pagina (100%) | Deve vedersi senza scorrere |
|---|---|---|
| 1280 x 860 (apertura) | circa 1264 x 820 | Tutto: anteprima, comandi con intervallo e ordine casuale, "Poi", testata playlist, schede playlist intere |
| 960 x 640 (minima) | circa 944 x 600 | Anteprima, comandi con intervallo e ordine casuale, testata playlist; schede playlist circa per metà (nel prototipo 48%) |
| Ingrandita su 2560 x 1440 (barra delle applicazioni 48 px) | circa 2560 x 1360 | Tutto; il blocco dei monitor non supera il 40% dell'altezza, lo spazio in più va alla playlist (`.contenuto` resta limitato a 1440 px e centrato) |

## 5. Fuori dall'ambito

- Consumo della finestra principale quando è aperta (barre di avanzamento e miniature): rilevato nella sessione del 2026-10-13, rimandato dall'utente.
- Nuove funzioni nelle schede Libreria e Impostazioni.
- Barra del titolo per altre finestre (non ce ne sono).

## 6. Errori e casi limite

- Cambio DPI spostando la finestra su un monitor con scala diversa: nuove misure, nuovo font, nuovi limiti della WebView, ridisegno della striscia (vedi 3.3, "Cambio DPI").
- Finestra ingrandita con barra delle applicazioni a scomparsa: 2 px liberi sul lato della barra (vedi 3.3).
- Finestra ridotta a icona: limiti della WebView non toccati (vedi 3.3, "WebView").
- Se la WebView viene ricreata dopo un errore (`RicreaWebView`), i nuovi limiti tengono già conto della striscia (passano da `AreaWebView()`).
- Contrasto alto di Windows: la striscia usa i colori di sistema (`SystemColors.ActiveCaption`, `ActiveCaptionText`, `Highlight`) invece dei colori Energy Italy quando `SystemInformation.HighContrast` è attivo.

## 7. Verifica

**Test automatici (Core)** - `tests/.../BarraTitoloTests.cs`:
- misure a 96, 120, 144 DPI;
- posizione dei tre pulsanti allineati a destra, senza spazi tra loro; area più stretta di tre pulsanti;
- `Zona`: area di trascinamento, ciascun pulsante, bordo e angoli in alto a finestra normale, nessun bordo a finestra ingrandita, `Fuori` sotto la striscia e fuori dai limiti, ai margini esatti dei rettangoli.

**Pagina (browser, `strumenti/server_sviluppo.py` + `sviluppo/prova-app.html`)**: alle tre aree della tabella 4.3, con 1, 2 e 3 monitor (`?monitor=1|2|3`): controllare con screenshot e misure (`scrollHeight`, posizione dei pulsanti) gli obiettivi della 4.3; testata fissa e barra di scorrimento sul bordo della finestra durante lo scorrimento in Libreria e Impostazioni; cambiando scheda lo scorrimento torna in cima; "IN PAUSA" visibile solo in pausa (testata e pannello); nessun errore nella console.

**Programma (a mano, sul PC)**: trascinamento, doppio clic sulla striscia, passaggio del mouse e doppio clic rapido sui pulsanti (nessun pulsante classico disegnato sopra la striscia), fascia di ridimensionamento sopra i pulsanti (8 DIP: verificare che non intralci il clic, altrimenti ridurla solo lì), premere su un pulsante e rilasciare altrove (nessuna azione), menu di sistema (clic destro e Alt+Spazio), Snap layouts su "ingrandisci", Aero Snap ai bordi, ridimensionamento da tutti i bordi e angoli, ingrandita senza parti fuori schermo, barra delle applicazioni a scomparsa che ricompare con la finestra ingrandita, riduci/ripristina dalla barra delle applicazioni, chiudi e Alt+F4 che nascondono, stato non attivo, cambio di scala (100% / 125% / 150%) e spostamento ripetuto tra monitor con scale diverse (la dimensione non deve "scivolare"), contrasto alto acceso a programma aperto.
