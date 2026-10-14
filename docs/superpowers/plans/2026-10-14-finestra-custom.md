# Finestra personalizzata e scheda "In onda" compatta - Piano di implementazione

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** sostituire la barra del titolo di Windows con una striscia Energy Italy disegnata dal programma e rendere compatta la scheda "In onda" (variante C "stile lettore"), come da spec.

**Architecture:** la parte web (index.html, app.css, app.js) cambia impaginazione con una sola modifica verificata nel prototipo. Nel Core una funzione pura `BarraTitolo` dice cosa c'è sotto il mouse nella striscia (testata con xUnit). Nell'app una classe `BarraTitoloFinestra` gestisce i messaggi non-client di Windows e disegna la striscia; `FinestraPrincipale` la usa e sposta la WebView sotto la striscia.

**Tech Stack:** .NET 10 WinForms, WebView2, xUnit, HTML/CSS/JS senza dipendenze.

**Spec:** `docs/superpowers/specs/2026-10-14-finestra-custom-design.md` (prevale in caso di dubbio).

**Comandi:**
- Test Core: `& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" test tests/EnergyItaly.SfondoAnimato.Core.Tests`
- Build app: `& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" build src/EnergyItaly.SfondoAnimato -c Release`
- Publish: `& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" publish src/EnergyItaly.SfondoAnimato/EnergyItaly.SfondoAnimato.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish`
- Prova nel browser: server `sviluppo-web` di `.claude/launch.json` (porta 5610), pagina `/sviluppo/prova-app.html?monitor=1|2|3`.

## File

| File | Cosa |
|---|---|
| `src/EnergyItaly.SfondoAnimato/web/sviluppo/prova-app.html` | Modifica: lo stato parte anche se il "pronta" della pagina arriva prima dell'ascolto |
| `src/EnergyItaly.SfondoAnimato/web/app/index.html` | Modifica: contenitore `.scorrimento`, controlli della rotazione nella colonna comandi, testata playlist senza controlli |
| `src/EnergyItaly.SfondoAnimato/web/app/app.css` | Modifica: testata fissa, impaginazione compatta, stili variante C |
| `src/EnergyItaly.SfondoAnimato/web/app/app.js` | Modifica: stato solo in pausa, didascalie compatte, pulsante ordine casuale, rimozioni |
| `src/EnergyItaly.SfondoAnimato.Core/BarraTitolo.cs` | Crea: zone e misure della striscia (funzione pura) |
| `tests/EnergyItaly.SfondoAnimato.Core.Tests/BarraTitoloTests.cs` | Crea: test di `BarraTitolo` |
| `src/EnergyItaly.SfondoAnimato/Win32.cs` | Modifica: costanti, strutture e funzioni per la cornice personalizzata |
| `src/EnergyItaly.SfondoAnimato/BarraTitoloFinestra.cs` | Crea: messaggi non-client e disegno della striscia |
| `src/EnergyItaly.SfondoAnimato/FinestraPrincipale.cs` | Modifica: usa la striscia, WebView sotto la striscia |
| `docs/superpowers/specs/2026-10-13-sfondo-animato-design.md` | Modifica: D10 rimanda alla nuova spec |

## Chunk 1: Pagina web

### Task 1: corsa all'avvio di prova-app.html

**Files:** Modify `src/EnergyItaly.SfondoAnimato/web/sviluppo/prova-app.html`

`prova-app.html` si mette in ascolto dei messaggi solo dopo aver letto il manifest (`await fetch`). Se la pagina `app` manda `pronta` prima, lo stato non parte e la pagina resta su "AVVIO" (osservato nel prototipo).

- [ ] **Step 1: Riprodurre.** Avviare `sviluppo-web`, aprire `/sviluppo/prova-app.html?monitor=1` e ricaricare più volte: almeno una volta la pagina resta su "AVVIO".
- [ ] **Step 2: Correggere.** Subito dopo `window.addEventListener('message', ...)` (fine dello script, prima di `})();`) aggiungere:

```js
  // La pagina può aver mandato "pronta" prima che questo script fosse in ascolto: lo stato parte comunque.
  inviaStato();
```

- [ ] **Step 3: Verificare.** Ricaricare 5 volte: la pagina mostra sempre la scheda "In onda" con uno sfondo.
- [ ] **Step 4: Commit.**

```bash
git add src/EnergyItaly.SfondoAnimato/web/sviluppo/prova-app.html
git commit -m "La pagina di prova manda lo stato anche se la finestra era già pronta"
```

### Task 2: impaginazione compatta e variante C

**Files:** Modify `web/app/index.html`, `web/app/app.css`, `web/app/app.js` (sotto `src/EnergyItaly.SfondoAnimato/`)

Le tre modifiche sono quelle del prototipo verificato nell'anteprima (spec 4.1-4.3). Applicarle esattamente come nei diff qui sotto (salvati in un file: `git apply --ignore-whitespace <file>`, perché la cartella di lavoro ha fine riga CRLF; `core.autocrlf` li uniforma al commit).

- [ ] **Step 1: index.html**

```diff
--- a/src/EnergyItaly.SfondoAnimato/web/app/index.html
+++ b/src/EnergyItaly.SfondoAnimato/web/app/index.html
@@ -1,4 +1,4 @@
-<!doctype html>
+﻿<!doctype html>
 <html lang="it">
 <head>
 <meta charset="utf-8">
@@ -18,7 +18,7 @@
   <span class="stato-app"><span class="puntino" aria-hidden="true"></span><span id="testo-stato">AVVIO</span></span>
 </header>
 
-<main class="contenuto">
+<div class="scorrimento"><main class="contenuto">
   <section id="vista-onda" class="vista" aria-label="In onda">
     <div class="riga-onda">
       <div class="palco">
@@ -32,34 +32,16 @@
         <div id="monitor-riga"></div>
       </div>
       <div class="pannello-onda">
-        <span class="etichetta-onda"><span class="puntino" aria-hidden="true"></span><span id="etichetta-onda-testo">IN ONDA</span></span>
+        <span class="etichetta-onda"><span class="puntino" aria-hidden="true"></span>IN PAUSA</span>
         <div>
           <h1 id="titolo-corrente"></h1>
           <p class="tenue" id="posizione-corrente"></p>
         </div>
         <div class="conto">
-          <div class="conto-riga"><span class="tempo" id="tempo">--:--</span><span class="tenue" id="nota-tempo"></span></div>
-          <div class="barra"><div id="barra-avanzamento"></div></div>
-        </div>
-        <div class="comandi">
-          <button type="button" class="tondo" id="btn-precedente" aria-label="Sfondo precedente"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M18 5l-9 7 9 7z"/><path d="M6 5v14"/></svg></button>
-          <button type="button" class="tondo primario" id="btn-pausa" aria-label="Pausa"></button>
-          <button type="button" class="tondo" id="btn-successivo" aria-label="Sfondo successivo"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M6 5l9 7-9 7z"/><path d="M18 5v14"/></svg></button>
-        </div>
-        <div class="poi" id="poi"></div>
-      </div>
-    </div>
-
-    <section class="playlist" aria-labelledby="titolo-playlist">
-      <div class="playlist-testa">
-        <div>
-          <h2 id="titolo-playlist">Playlist</h2>
-          <p class="tenue" id="nota-playlist"></p>
-        </div>
-        <div class="controlli-playlist">
-          <div class="riga-controlli">
+          <div class="conto-riga">
+            <span class="tempo" id="tempo">--:--</span><span class="tenue" id="nota-tempo"></span>
             <div class="gruppo-intervallo" id="gruppo-intervallo" role="group" aria-label="Cambio sfondo">
-              <label for="intervallo-valore" class="mini">CAMBIA OGNI</label>
+              <label for="intervallo-valore" class="tenue">ogni</label>
               <input id="intervallo-valore" type="number" min="1" step="1" inputmode="numeric" aria-describedby="intervallo-errore">
               <label for="intervallo-unita" class="nascosto">Unità di tempo</label>
               <select id="intervallo-unita">
@@ -68,17 +50,24 @@
               </select>
               <button type="button" class="pillola" id="btn-mai" aria-pressed="false" title="Cambia sfondo solo a mano">Mai</button>
             </div>
-            <div class="segmentato" role="group" aria-label="Ordine">
-              <button type="button" class="pillola" data-ordine="sequenziale">In ordine</button>
-              <button type="button" class="pillola" data-ordine="casuale"><svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M16 3h5v5"/><path d="M4 20L21 3"/><path d="M21 16v5h-5"/><path d="M15 15l6 6"/><path d="M4 4l5 5"/></svg>Casuale</button>
-            </div>
           </div>
+          <div class="barra"><div id="barra-avanzamento"></div></div>
           <span id="intervallo-errore" class="errore" role="alert" hidden></span>
-          <div class="riga-interruttore">
-            <button type="button" role="switch" class="interruttore" id="sw-fine-video-playlist" aria-checked="true" aria-labelledby="etichetta-fine-video"><span></span></button>
-            <span id="etichetta-fine-video">I video cambiano a fine riproduzione</span>
-          </div>
         </div>
+        <div class="comandi">
+          <button type="button" class="tondo casuale" id="btn-casuale" aria-label="Ordine casuale" aria-pressed="false"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M16 3h5v5"/><path d="M4 20L21 3"/><path d="M21 16v5h-5"/><path d="M15 15l6 6"/><path d="M4 4l5 5"/></svg></button>
+          <button type="button" class="tondo" id="btn-precedente" aria-label="Sfondo precedente"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M18 5l-9 7 9 7z"/><path d="M6 5v14"/></svg></button>
+          <button type="button" class="tondo primario" id="btn-pausa" aria-label="Pausa"></button>
+          <button type="button" class="tondo" id="btn-successivo" aria-label="Sfondo successivo"><svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M6 5l9 7-9 7z"/><path d="M18 5v14"/></svg></button>
+          <div class="poi" id="poi"></div>
+        </div>
+      </div>
+    </div>
+
+    <section class="playlist" aria-labelledby="titolo-playlist">
+      <div class="playlist-testa">
+        <h2 id="titolo-playlist">Playlist</h2>
+        <p class="tenue" id="nota-playlist"></p>
       </div>
       <div class="striscia" id="striscia"></div>
     </section>
@@ -174,7 +163,7 @@
       </div>
     </div>
   </section>
-</main>
+</main></div>
 
 <div class="avviso" id="avviso" role="status" hidden></div>
 
```

- [ ] **Step 2: app.css**

```diff
--- a/src/EnergyItaly.SfondoAnimato/web/app/app.css
+++ b/src/EnergyItaly.SfondoAnimato/web/app/app.css
@@ -24,7 +24,7 @@
 }
 
 * { box-sizing: border-box; }
-html, body { margin: 0; min-height: 100%; background: var(--fondo); color: var(--testo); font-family: var(--sans); font-size: 14px; }
+html, body { margin: 0; height: 100%; overflow: hidden; background: var(--fondo); color: var(--testo); font-family: var(--sans); font-size: 14px; }
 body.in-attesa .contenuto, body.in-attesa .schede { visibility: hidden; }
 button, input, select { font: inherit; color: inherit; }
 button:focus-visible, input:focus-visible, select:focus-visible, [tabindex]:focus-visible { outline: 2px solid var(--verde-chiaro); outline-offset: 2px; }
@@ -32,11 +32,13 @@ button:focus-visible, input:focus-visible, select:focus-visible, [tabindex]:focu
 .nascosto { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
 .tenue { margin: 0; color: var(--tenue); }
 .mini { font-family: var(--mono); font-size: 11px; letter-spacing: .04em; color: var(--tenue); }
-h1 { margin: 0 0 6px; font-size: 44px; line-height: 1.05; font-weight: 800; letter-spacing: -.02em; overflow-wrap: anywhere; }
+h1 { margin: 0 0 6px; font-size: 32px; line-height: 1.05; font-weight: 800; letter-spacing: -.02em; overflow-wrap: anywhere; }
 h2 { margin: 0; font-size: 24px; font-weight: 800; letter-spacing: -.01em; }
 
 /* Testata */
-.testata { display: flex; flex-wrap: wrap; align-items: center; gap: 16px 32px; padding: 0 28px; min-height: 64px; border-bottom: 1px solid #142640; }
+body { display: flex; flex-direction: column; }
+.scorrimento { flex: 1; min-height: 0; overflow-y: auto; }
+.testata { flex: none; display: flex; flex-wrap: wrap; align-items: center; gap: 16px 32px; padding: 0 28px; min-height: 64px; border-bottom: 1px solid #142640; }
 .logo { width: 128px; height: auto; }
 .schede { display: flex; flex-wrap: wrap; gap: 4px; }
 .scheda { min-height: 40px; padding: 0 16px; border: 0; border-radius: 20px; background: transparent; color: var(--tenue); font-weight: 600; cursor: pointer; }
@@ -47,15 +49,15 @@ h2 { margin: 0; font-size: 24px; font-weight: 800; letter-spacing: -.01em; }
 .in-pausa .puntino { background: var(--arancio); animation: none; }
 @keyframes battito { 0%, 100% { opacity: 1; } 50% { opacity: .35; } }
 
-.contenuto { display: flex; flex-direction: column; gap: 40px; max-width: 1440px; margin: 0 auto; padding: 32px 28px 48px; }
-.vista { display: flex; flex-direction: column; gap: 40px; }
+.contenuto { display: flex; flex-direction: column; gap: 24px; max-width: 1440px; margin: 0 auto; padding: 20px; }
+.vista { display: flex; flex-direction: column; gap: 24px; }
 
 /* In onda */
-.riga-onda { display: flex; flex-wrap: wrap; gap: 32px; align-items: stretch; }
-.palco { flex: 999 1 620px; min-width: 0; display: flex; flex-direction: column; gap: 18px; padding: 24px 28px 28px; border-radius: 20px; border: 1px solid var(--bordo); background: radial-gradient(ellipse at 50% 0%, #11284a 0%, var(--palco) 70%); }
-.palco-testa { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 12px; min-height: 44px; }
-.didascalia { font-family: var(--mono); font-size: 12px; letter-spacing: .08em; color: var(--tenue); white-space: pre-wrap; }
-.palco-schermi { display: flex; flex-direction: column; gap: 10px; }
+.riga-onda { display: flex; gap: 28px; align-items: center; }
+.palco { flex: 1 1 0; min-width: 0; display: flex; flex-direction: column; gap: 10px; }
+.palco-testa { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 8px 12px; min-height: 36px; }
+.didascalia { font-family: var(--mono); font-size: 12px; letter-spacing: .06em; color: var(--tenue); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
+.palco-schermi { display: flex; flex-direction: column; gap: 8px; width: min(100%, calc(40vh * var(--rapporto, 1.7778))); margin: 0 auto; }
 .schermi { position: relative; width: 100%; background: var(--palco); }
 .scena { position: absolute; overflow: hidden; background: var(--scena); }
 .scena iframe { position: absolute; top: 0; left: 0; border: 0; transform-origin: 0 0; pointer-events: none; }
@@ -66,22 +68,24 @@ h2 { margin: 0; font-size: 24px; font-weight: 800; letter-spacing: -.01em; }
 .etichette-monitor { position: relative; height: 18px; }
 .etichette-monitor span { position: absolute; top: 0; overflow: hidden; text-align: center; text-overflow: ellipsis; white-space: pre; font-family: var(--mono); font-size: 12px; color: var(--tenue); }
 
-.pannello-onda { flex: 1 1 300px; display: flex; flex-direction: column; gap: 22px; padding: 8px 4px; min-width: 0; }
+.pannello-onda { flex: 1 1 0; display: flex; flex-direction: column; gap: 16px; padding: 4px 0; min-width: 0; }
 .etichetta-onda { display: flex; align-items: center; gap: 8px; font-family: var(--mono); font-size: 12px; font-weight: 600; letter-spacing: .1em; color: var(--verde-chiaro); }
 .in-pausa .etichetta-onda { color: var(--arancio); }
 .conto { display: flex; flex-direction: column; gap: 10px; }
 .conto-riga { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 10px; }
-.tempo { font-family: var(--mono); font-size: 40px; font-weight: 500; letter-spacing: -.02em; }
+.tempo { font-family: var(--mono); font-size: 32px; font-weight: 500; letter-spacing: -.02em; }
 .barra { height: 4px; overflow: hidden; border-radius: 2px; background: #1a2f4d; }
 .barra > div { width: 0; height: 100%; background: var(--verde); transition: width .9s linear; }
 .barra.sottile { height: 3px; }
-.comandi { display: flex; align-items: center; gap: 12px; }
+.comandi { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; }
+.tondo.casuale { width: 44px; height: 44px; border-color: transparent; color: var(--tenue); }
+.tondo.casuale[aria-pressed="true"] { background: rgba(122, 181, 42, .14); color: var(--verde-chiaro); }
 .tondo { display: flex; align-items: center; justify-content: center; width: 52px; height: 52px; border: 1px solid var(--bordo-forte); border-radius: 26px; background: transparent; color: var(--testo); cursor: pointer; }
 .tondo:hover { background: #10223c; }
 .tondo.primario { width: 68px; height: 68px; border: 0; border-radius: 34px; background: var(--verde); color: var(--fondo); }
 .tondo.primario:hover { background: #8cc63a; }
-.poi { display: flex; align-items: center; gap: 12px; min-height: 74px; padding: 10px; border: 1px solid var(--bordo); border-radius: 12px; background: var(--superficie); }
-.poi > .miniatura, .segnaposto-poi { flex: none; width: 96px; border-radius: 6px; }
+.poi { flex: 1 1 130px; min-width: 0; display: flex; align-items: center; gap: 10px; min-height: 56px; margin-left: 4px; padding: 8px; border: 1px solid var(--bordo); border-radius: 12px; background: var(--superficie); }
+.poi > .miniatura, .segnaposto-poi { flex: none; width: 56px; border-radius: 6px; }
 .segnaposto-poi { display: flex; align-items: center; justify-content: center; aspect-ratio: 16 / 9; background: #13243c; color: var(--verde-chiaro); }
 .poi-testi { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
 .poi-testi .mini { letter-spacing: .08em; }
@@ -89,15 +93,14 @@ h2 { margin: 0; font-size: 24px; font-weight: 800; letter-spacing: -.01em; }
 
 /* Playlist */
 .playlist { display: flex; flex-direction: column; gap: 18px; }
-.playlist-testa { display: flex; flex-wrap: wrap; align-items: flex-start; justify-content: space-between; gap: 16px; }
-.playlist-testa > div:first-child { display: flex; flex-direction: column; gap: 4px; }
-.controlli-playlist { display: flex; flex-direction: column; align-items: flex-end; gap: 10px; }
-.riga-controlli { display: flex; flex-wrap: wrap; align-items: center; justify-content: flex-end; gap: 12px; }
-.gruppo-intervallo { display: flex; align-items: center; gap: 6px; padding: 4px 4px 4px 12px; border: 1px solid var(--bordo); border-radius: 12px; background: var(--superficie); }
-.gruppo-intervallo.errato { border-color: #ff8a7a; }
-.gruppo-intervallo input { width: 76px; min-height: 36px; padding: 0 8px; border: 1px solid var(--bordo-forte); border-radius: 8px; background: var(--fondo); font-weight: 700; text-align: right; }
+.playlist-testa { display: flex; flex-direction: column; gap: 4px; min-width: 0; }
+#nota-playlist { overflow: hidden; white-space: nowrap; text-overflow: ellipsis; }
+/* Intervallo compatto sulla riga del conto alla rovescia: "ogni [30] [minuti] Mai" */
+.gruppo-intervallo { display: flex; align-self: center; align-items: center; gap: 4px; margin-left: auto; font-size: 13px; }
+.gruppo-intervallo input { width: 54px; min-height: 30px; padding: 0 8px; border: 1px solid var(--bordo-forte); border-radius: 8px; background: var(--fondo); font-weight: 700; text-align: right; }
 .gruppo-intervallo.errato input { border-color: #ff8a7a; }
-.gruppo-intervallo select { min-height: 36px; padding: 0 8px; border: 1px solid var(--bordo-forte); border-radius: 8px; background: var(--fondo); font-weight: 600; cursor: pointer; }
+.gruppo-intervallo select { min-height: 30px; padding: 0 8px; border: 1px solid var(--bordo-forte); border-radius: 8px; background: var(--fondo); font-weight: 600; cursor: pointer; }
+.gruppo-intervallo .pillola { min-height: 30px; }
 .gruppo-intervallo.spento input, .gruppo-intervallo.spento select { background: transparent; color: var(--spento); }
 .segmentato { display: flex; align-items: center; gap: 4px; padding: 4px; border: 1px solid var(--bordo); border-radius: 12px; background: var(--superficie); }
 .palco .segmentato, .riga-imp .segmentato { background: var(--fondo); }
@@ -105,14 +108,13 @@ h2 { margin: 0; font-size: 24px; font-weight: 800; letter-spacing: -.01em; }
 .pillola[aria-pressed="true"] { background: var(--blu); color: #fff; font-weight: 700; }
 .pillola:disabled { opacity: .45; cursor: default; }
 .errore { font-size: 13px; color: var(--errore); }
-.riga-interruttore { display: flex; align-items: center; gap: 10px; color: var(--tenue-chiaro); }
 .interruttore { position: relative; flex: none; width: 46px; height: 26px; padding: 0; border: 0; border-radius: 13px; background: #2a3f5c; cursor: pointer; }
 .interruttore span { position: absolute; top: 3px; left: 3px; width: 20px; height: 20px; border-radius: 10px; background: #fff; transition: left .2s; }
 .interruttore[aria-checked="true"] { background: var(--verde); }
 .interruttore[aria-checked="true"] span { left: 23px; }
 
 .striscia { display: flex; gap: 14px; overflow-x: auto; padding-bottom: 8px; }
-.voce-playlist { flex: none; display: flex; flex-direction: column; gap: 10px; width: 200px; padding: 10px; border: 1px solid var(--bordo); border-radius: 14px; background: var(--superficie); cursor: grab; transition: opacity .15s; }
+.voce-playlist { flex: none; display: flex; flex-direction: column; gap: 10px; width: 168px; padding: 10px; border: 1px solid var(--bordo); border-radius: 14px; background: var(--superficie); cursor: grab; transition: opacity .15s; }
 .voce-playlist.attiva { border-color: var(--verde); background: var(--superficie-alta); }
 .voce-playlist.trascinata { opacity: .4; }
 .voce-testa { display: flex; align-items: center; justify-content: space-between; min-height: 32px; }
@@ -134,7 +136,7 @@ h2 { margin: 0; font-size: 24px; font-weight: 800; letter-spacing: -.01em; }
 .voce-titolo { overflow: hidden; font-weight: 700; text-overflow: ellipsis; white-space: nowrap; }
 .voce-meta { font-size: 12px; color: var(--tenue); }
 .non-disponibile .voce-titolo, .non-disponibile .voce-meta { opacity: .6; }
-.aggiungi-playlist { flex: none; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 8px; width: 200px; min-height: 200px; border: 1.5px dashed #2a4568; border-radius: 14px; background: transparent; color: var(--testo); font-weight: 700; cursor: pointer; }
+.aggiungi-playlist { flex: none; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 8px; width: 168px; min-height: 160px; border: 1.5px dashed #2a4568; border-radius: 14px; background: transparent; color: var(--testo); font-weight: 700; cursor: pointer; }
 .aggiungi-playlist svg { color: var(--verde-chiaro); }
 
 /* Libreria */
```

- [ ] **Step 3: app.js**

```diff
--- a/src/EnergyItaly.SfondoAnimato/web/app/app.js
+++ b/src/EnergyItaly.SfondoAnimato/web/app/app.js
@@ -200,14 +200,10 @@
     aggiornaConto();
   }
 
-  function testoStato() {
-    if (stato.inPausa) return stato.motivoPausa ? 'IN PAUSA: ' + stato.motivoPausa : 'IN PAUSA';
-    const n = stato.monitor.length;
-    return n === 1 ? 'ATTIVO SU 1 MONITOR' : `ATTIVO SU ${n} MONITOR`;
-  }
-
   function renderTestata() {
-    $('#testo-stato').textContent = testoStato();
+    // Lo stato si mostra solo in pausa: "attivo" è la condizione normale e non serve dirlo.
+    $('#testo-stato').textContent = stato.motivoPausa ? 'IN PAUSA: ' + stato.motivoPausa : 'IN PAUSA';
+    $('.stato-app').hidden = !stato.inPausa;
     document.querySelectorAll('.scheda').forEach((b) => {
       if (b.dataset.scheda === scheda) b.setAttribute('aria-current', 'page');
       else b.removeAttribute('aria-current');
@@ -218,14 +214,15 @@
   function apriScheda(nome) {
     scheda = nome;
     render();
+    $('.scorrimento').scrollTop = 0;
   }
 
   function didascalia() {
     const n = stato.monitor.length;
     const f = stato.finestra;
-    if (n === 1) return `IL TUO DESKTOP ADESSO  /  1 MONITOR  /  ${f.w} x ${f.h} px`;
-    if (stato.modo === 'estendi') return `IL TUO DESKTOP ADESSO  /  ${n} MONITOR ESTESI  /  ${f.w} x ${f.h} px`;
-    return `IL TUO DESKTOP ADESSO  /  STESSA SCENA SU ${n} MONITOR`;
+    if (n === 1) return `IL TUO DESKTOP · 1 MONITOR · ${f.w}x${f.h}`;
+    if (stato.modo === 'estendi') return `IL TUO DESKTOP · ${n} MONITOR ESTESI · ${f.w}x${f.h}`;
+    return `IL TUO DESKTOP · STESSA SCENA SU ${n} MONITOR`;
   }
 
   function aggiornaModo() {
@@ -269,7 +266,7 @@
     const etichette = el('div', { class: 'etichette-monitor' });
     stato.monitor.forEach((m) => etichette.append(el('span', { style: `left: ${(m.x / W) * 100}%; width: ${(m.w / W) * 100}%` },
       `${m.nome}  ${m.w}x${m.h}${m.principale ? '  PRINCIPALE' : ''}`)));
-    return el('div', { class: 'palco-schermi' }, schermi, etichette);
+    return el('div', { class: 'palco-schermi', style: `--rapporto: ${W / H}` }, schermi, etichette);
   }
 
   function contenutoScena(v, s, box) {
@@ -314,7 +311,7 @@
 
   function renderPannello() {
     const v = voce(stato.corrente);
-    $('#etichetta-onda-testo').textContent = stato.inPausa ? 'IN PAUSA' : 'IN ONDA';
+    $('.etichetta-onda').hidden = !stato.inPausa;
     $('#titolo-corrente').textContent = v ? v.titolo : 'Nessuno sfondo';
     const pos = v ? stato.playlist.indexOf(v.id) : -1;
     $('#posizione-corrente').textContent = !v ? '' :
@@ -369,9 +366,8 @@
     unita.disabled = !imp.rotazioneAttiva;
     $('#gruppo-intervallo').classList.toggle('spento', !imp.rotazioneAttiva);
     $('#btn-mai').setAttribute('aria-pressed', String(!imp.rotazioneAttiva));
-    document.querySelectorAll('[data-ordine]').forEach((b) => b.setAttribute('aria-pressed', String(b.dataset.ordine === imp.ordine)));
-    $('#sw-fine-video-playlist').setAttribute('aria-checked', String(imp.fineVideo));
-    $('#nota-playlist').textContent = `Trascina per cambiare l'ordine, clicca un'anteprima per mostrarla subito. Dissolvenza di ${imp.dissolvenzaSecondi} s.`;
+    $('#nota-playlist').textContent = `Trascina per cambiare l'ordine, clicca un'anteprima per mostrarla subito.`;
+    $('#btn-casuale').setAttribute('aria-pressed', String(imp.ordine === 'casuale'));
   }
 
   function validaValore() {
@@ -717,11 +713,10 @@
     $('#btn-successivo').addEventListener('click', () => ponte.invia({ tipo: 'successivo' }));
     $('#btn-pausa').addEventListener('click', conStato(() => ponte.invia({ tipo: stato.pausaManuale ? 'riprendi' : 'pausa' })));
     document.querySelectorAll('[data-modo]').forEach((b) => b.addEventListener('click', () => modifica({ modoMonitor: b.dataset.modo })));
-    document.querySelectorAll('[data-ordine]').forEach((b) => b.addEventListener('click', () => modifica({ ordine: b.dataset.ordine })));
     document.querySelectorAll('[data-dissolvenza]').forEach((b) => b.addEventListener('click', () => modifica({ dissolvenzaSecondi: Number(b.dataset.dissolvenza) })));
+    $('#btn-casuale').addEventListener('click', conStato(() => modifica({ ordine: stato.impostazioni.ordine === 'casuale' ? 'sequenziale' : 'casuale' })));
     $('#btn-mai').addEventListener('click', conStato(() => modifica({ rotazioneAttiva: !stato.impostazioni.rotazioneAttiva })));
     const fineVideo = conStato(() => modifica({ fineVideo: !stato.impostazioni.fineVideo }));
-    $('#sw-fine-video-playlist').addEventListener('click', fineVideo);
     $('#sw-fine-video').addEventListener('click', fineVideo);
     $('#sw-batteria').addEventListener('click', conStato(() => modifica({ pausaABatteria: !stato.impostazioni.pausaABatteria })));
     $('#sw-avvio').addEventListener('click', conStato(() => modifica({ avvioConWindows: !stato.impostazioni.avvioConWindows })));
```

- [ ] **Step 4: Verificare nel browser** (`/sviluppo/prova-app.html?monitor=1`, `?monitor=2`, `?monitor=3`, ciascuna alle viewport 1264x820, 944x600 e 2560x1360; nella pagina interna `app/index.html` si misura con `document.querySelector('.scorrimento')`):
  - console senza errori;
  - 1264x820: `.scorrimento` non scorre (`scrollHeight == clientHeight`);
  - 944x600: pulsante successivo e `#gruppo-intervallo` dentro l'area visibile; prima scheda della playlist visibile almeno al 45%; nessuno scorrimento orizzontale (`scrollWidth == clientWidth`);
  - 2560x1360: `.scorrimento` non scorre; `.schermi` alto al massimo il 40% dell'altezza della viewport; `.contenuto` largo 1440 px e centrato; la barra di scorrimento (quando c'è, ad esempio in Impostazioni a 944x600) sta sul bordo destro della viewport, non su quello di `.contenuto`;
  - pulsante ordine casuale: un clic → `aria-pressed="true"` e "Poi" diventa "A sorpresa dalla playlist"; un altro clic → torna `false`;
  - "Mai" → campo e menu disabilitati; intervallo 9999 → messaggio d'errore sotto la barra;
  - pausa → compaiono "● IN PAUSA" nel pannello e "IN PAUSA" nella testata; ripresa → spariscono;
  - Libreria e Impostazioni: la testata resta ferma scorrendo, cambiando scheda lo scorrimento torna in cima.
- [ ] **Step 5: Commit.**

```bash
git add src/EnergyItaly.SfondoAnimato/web/app
git commit -m "Scheda In onda compatta: anteprima più piccola, comandi sempre visibili, controlli della rotazione in stile lettore"
```

## Chunk 2: Core

### Task 3: BarraTitolo

**Files:** Create `src/EnergyItaly.SfondoAnimato.Core/BarraTitolo.cs`, Test `tests/EnergyItaly.SfondoAnimato.Core.Tests/BarraTitoloTests.cs`

- [ ] **Step 1: Scrivere i test**

```csharp
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

public class BarraTitoloTests
{
    static readonly MisureBarra M100 = BarraTitolo.Misure(96);

    [Theory]
    [InlineData(96, 32, 46, 8, 16)]
    [InlineData(120, 40, 58, 10, 20)]
    [InlineData(144, 48, 69, 12, 24)]
    public void Misure_SeguonoLaScala(int dpi, int altezza, int pulsante, int bordo, int angolo)
    {
        Assert.Equal(new MisureBarra(altezza, pulsante, bordo, angolo), BarraTitolo.Misure(dpi));
    }

    [Fact]
    public void Pulsanti_AllineatiADestraSenzaSpazi()
    {
        var p = BarraTitolo.Pulsanti(1000, M100);
        Assert.Equal(
            [
                (ZonaBarra.Riduci, new Rettangolo(862, 0, 46, 32)),
                (ZonaBarra.Ingrandisci, new Rettangolo(908, 0, 46, 32)),
                (ZonaBarra.Chiudi, new Rettangolo(954, 0, 46, 32))
            ],
            p);
    }

    [Fact]
    public void Pulsanti_AreaStretta_SiTaglianoASinistra()
    {
        var p = BarraTitolo.Pulsanti(100, M100);
        Assert.Equal(new Rettangolo(-38, 0, 46, 32), p[0].Area);
        Assert.Equal(new Rettangolo(54, 0, 46, 32), p[2].Area);
    }

    [Theory]
    [InlineData(500, 20, ZonaBarra.Trascina)]
    [InlineData(862, 20, ZonaBarra.Riduci)]
    [InlineData(861, 20, ZonaBarra.Trascina)]
    [InlineData(907, 20, ZonaBarra.Riduci)]
    [InlineData(908, 20, ZonaBarra.Ingrandisci)]
    [InlineData(953, 20, ZonaBarra.Ingrandisci)]
    [InlineData(954, 20, ZonaBarra.Chiudi)]
    [InlineData(999, 31, ZonaBarra.Chiudi)]
    [InlineData(500, 32, ZonaBarra.Fuori)]
    [InlineData(500, -1, ZonaBarra.Fuori)]
    [InlineData(-1, 20, ZonaBarra.Fuori)]
    [InlineData(1000, 20, ZonaBarra.Fuori)]
    [InlineData(500, 0, ZonaBarra.BordoAlto)]
    [InlineData(500, 7, ZonaBarra.BordoAlto)]
    [InlineData(500, 8, ZonaBarra.Trascina)]
    [InlineData(960, 3, ZonaBarra.BordoAlto)]
    [InlineData(15, 3, ZonaBarra.AngoloAltoSinistra)]
    [InlineData(16, 3, ZonaBarra.BordoAlto)]
    [InlineData(984, 3, ZonaBarra.AngoloAltoDestra)]
    [InlineData(983, 3, ZonaBarra.BordoAlto)]
    public void Zona_FinestraNormale(int x, int y, ZonaBarra attesa)
    {
        Assert.Equal(attesa, BarraTitolo.Zona(x, y, 1000, M100, ingrandita: false));
    }

    [Theory]
    [InlineData(500, 3, ZonaBarra.Trascina)]
    [InlineData(5, 3, ZonaBarra.Trascina)]
    [InlineData(990, 3, ZonaBarra.Chiudi)]
    [InlineData(870, 0, ZonaBarra.Riduci)]
    public void Zona_FinestraIngrandita_NienteBordo(int x, int y, ZonaBarra attesa)
    {
        Assert.Equal(attesa, BarraTitolo.Zona(x, y, 1000, M100, ingrandita: true));
    }

    [Fact]
    public void Zona_A150PerCento_UsaLeMisureScalate()
    {
        var m = BarraTitolo.Misure(144);
        Assert.Equal(ZonaBarra.Chiudi, BarraTitolo.Zona(1500 - 69, 47, 1500, m, ingrandita: false));
        Assert.Equal(ZonaBarra.Ingrandisci, BarraTitolo.Zona(1500 - 70, 47, 1500, m, ingrandita: false));
        Assert.Equal(ZonaBarra.Fuori, BarraTitolo.Zona(700, 48, 1500, m, ingrandita: false));
        Assert.Equal(ZonaBarra.BordoAlto, BarraTitolo.Zona(700, 11, 1500, m, ingrandita: false));
    }
}
```

- [ ] **Step 2: Eseguirli e vederli fallire** (errore di compilazione: `BarraTitolo` non esiste).
- [ ] **Step 3: Implementazione**

```csharp
namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Che cosa c'è in un punto della striscia del titolo disegnata dal programma.</summary>
public enum ZonaBarra { Fuori, Trascina, Riduci, Ingrandisci, Chiudi, BordoAlto, AngoloAltoSinistra, AngoloAltoDestra }

/// <summary>Misure della striscia in pixel fisici.</summary>
public sealed record MisureBarra(int Altezza, int LarghezzaPulsante, int BordoRidimensiona, int Angolo);

/// <summary>
/// Geometria della striscia del titolo (spec 2026-10-14, 3.4): dove stanno i pulsanti e che cosa c'è
/// sotto il mouse. Senza dipendenze da WinForms, così si prova a ogni scala e con la finestra ingrandita.
/// </summary>
public static class BarraTitolo
{
    // In DIP: altezza 32, pulsante 46, fascia per ridimensionare dall'alto 8 (come la cornice a 100%), angolo 16.
    public static MisureBarra Misure(int dpi) => new(Px(32, dpi), Px(46, dpi), Px(8, dpi), Px(16, dpi));

    static int Px(int dip, int dpi) => (int)Math.Round(dip * dpi / 96.0);

    /// <summary>Riduci, Ingrandisci, Chiudi affiancati al bordo destro. Se l'area è stretta si tagliano a sinistra.</summary>
    public static IReadOnlyList<(ZonaBarra Zona, Rettangolo Area)> Pulsanti(int larghezzaClient, MisureBarra m)
    {
        int l = m.LarghezzaPulsante;
        return
        [
            (ZonaBarra.Riduci, new Rettangolo(larghezzaClient - 3 * l, 0, l, m.Altezza)),
            (ZonaBarra.Ingrandisci, new Rettangolo(larghezzaClient - 2 * l, 0, l, m.Altezza)),
            (ZonaBarra.Chiudi, new Rettangolo(larghezzaClient - l, 0, l, m.Altezza))
        ];
    }

    /// <summary>Zona del punto (coordinate client). Fuori = sotto la striscia o fuori dalla finestra.</summary>
    public static ZonaBarra Zona(int x, int y, int larghezzaClient, MisureBarra m, bool ingrandita)
    {
        if (y < 0 || y >= m.Altezza || x < 0 || x >= larghezzaClient) return ZonaBarra.Fuori;
        // Come in Windows 11 la fascia per ridimensionare vale anche sopra i pulsanti.
        if (!ingrandita && y < m.BordoRidimensiona)
        {
            if (x < m.Angolo) return ZonaBarra.AngoloAltoSinistra;
            if (x >= larghezzaClient - m.Angolo) return ZonaBarra.AngoloAltoDestra;
            return ZonaBarra.BordoAlto;
        }
        foreach (var (zona, area) in Pulsanti(larghezzaClient, m))
            if (x >= area.X && x < area.Destra) return zona;
        return ZonaBarra.Trascina;
    }
}
```

Nota: 46 * 120 / 96 = 57,5; `Math.Round` arrotonda al pari (58), come nel test.

- [ ] **Step 4: Eseguire i test**: tutti verdi, compresi quelli già esistenti.
- [ ] **Step 5: Commit.**

```bash
git add src/EnergyItaly.SfondoAnimato.Core/BarraTitolo.cs tests/EnergyItaly.SfondoAnimato.Core.Tests/BarraTitoloTests.cs
git commit -m "Il Core calcola zone e pulsanti della barra del titolo personalizzata"
```

## Chunk 3: Finestra

### Task 4: Win32

**Files:** Modify `src/EnergyItaly.SfondoAnimato/Win32.cs`

- [ ] **Step 1:** aggiungere dopo le costanti esistenti (prima di `GUID_CONSOLE_DISPLAY_STATE`):

```csharp
    // Barra del titolo personalizzata (FinestraPrincipale)
    public const int WM_NCCALCSIZE = 0x0083;
    public const int WM_NCHITTEST = 0x0084;
    public const int WM_NCMOUSEMOVE = 0x00A0;
    public const int WM_NCLBUTTONDOWN = 0x00A1;
    public const int WM_NCLBUTTONUP = 0x00A2;
    public const int WM_NCLBUTTONDBLCLK = 0x00A3;
    public const int WM_NCMOUSELEAVE = 0x02A2;
    public const int WM_SYSCOMMAND = 0x0112;
    public const int SC_MINIMIZE = 0xF020;
    public const int SC_MAXIMIZE = 0xF030;
    public const int SC_CLOSE = 0xF060;
    public const int SC_RESTORE = 0xF120;
    public const int HTCLIENT = 1;
    public const int HTCAPTION = 2;
    public const int HTMINBUTTON = 8;
    public const int HTMAXBUTTON = 9;
    public const int HTTOP = 12;
    public const int HTTOPLEFT = 13;
    public const int HTTOPRIGHT = 14;
    public const int HTCLOSE = 20;
    public const int SM_CYFRAME = 33;
    public const int SM_CXPADDEDBORDER = 92;
    public const uint SWP_FRAMECHANGED = 0x20;
    public const uint TME_LEAVE = 0x2;
    public const uint TME_NONCLIENT = 0x10;
    public const uint ABM_GETAUTOHIDEBAREX = 0xB;
    public const uint ABE_LEFT = 0;
    public const uint ABE_TOP = 1;
    public const uint ABE_RIGHT = 2;
    public const uint ABE_BOTTOM = 3;
```

- [ ] **Step 2:** aggiungere dopo `POWERBROADCAST_SETTING`:

```csharp
    [StructLayout(LayoutKind.Sequential)]
    public struct NCCALCSIZE_PARAMS { public RECT rgrc0; public RECT rgrc1; public RECT rgrc2; public IntPtr lppos; }

    [StructLayout(LayoutKind.Sequential)]
    public struct TRACKMOUSEEVENT { public uint cbSize; public uint dwFlags; public IntPtr hwndTrack; public uint dwHoverTime; }

    [StructLayout(LayoutKind.Sequential)]
    public struct APPBARDATA { public uint cbSize; public IntPtr hWnd; public uint uCallbackMessage; public uint uEdge; public RECT rc; public IntPtr lParam; }
```

- [ ] **Step 3:** aggiungere in fondo alle funzioni importate:

```csharp
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool IsZoomed(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int GetSystemMetricsForDpi(int indice, uint dpi);
    [DllImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT dati);
    [DllImport("user32.dll", EntryPoint = "PostMessageW")][return: MarshalAs(UnmanagedType.Bool)] public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("shell32.dll")] public static extern IntPtr SHAppBarMessage(uint messaggio, ref APPBARDATA dati);
```

(Se una di queste esiste già in `Win32.cs`, non duplicarla.)

- [ ] **Step 4:** build dell'app: nessun errore. Nessun commit separato (va con il Task 5).

### Task 5: BarraTitoloFinestra e FinestraPrincipale

**Files:** Create `src/EnergyItaly.SfondoAnimato/BarraTitoloFinestra.cs`; Modify `src/EnergyItaly.SfondoAnimato/FinestraPrincipale.cs`

- [ ] **Step 1: Creare `BarraTitoloFinestra.cs`**

```csharp
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato;

/// <summary>
/// Striscia del titolo disegnata dal programma al posto di quella di Windows (spec 2026-10-14, sezione 3).
/// La finestra resta una finestra normale: qui si toglie la barra standard (WM_NCCALCSIZE), si dice a
/// Windows dove stanno pulsanti e area di trascinamento (WM_NCHITTEST) e si disegnano i pulsanti.
/// Così Snap, menu di sistema e doppio clic restano quelli di Windows.
/// </summary>
sealed class BarraTitoloFinestra : IDisposable
{
    public delegate void ProceduraStandard(ref Message m);

    static readonly Color Sfondo = Color.FromArgb(0x05, 0x0d, 0x18);
    static readonly Color Titolo = Color.FromArgb(0xa9, 0xbb, 0xcf);
    static readonly Color Spento = Color.FromArgb(0x5d, 0x75, 0x93);
    static readonly Color Glifo = Color.FromArgb(0xc6, 0xd4, 0xe3);
    static readonly Color Passaggio = Color.FromArgb(0x13, 0x24, 0x3c);
    static readonly Color Premuto = Color.FromArgb(0x1a, 0x2f, 0x4d);
    static readonly Color ChiudiPassaggio = Color.FromArgb(0xc4, 0x2b, 0x1c);
    static readonly Color ChiudiPremuto = Color.FromArgb(0xa1, 0x28, 0x1c);
    static readonly Color BordoWindows10 = Color.FromArgb(0x22, 0x39, 0x5a);
    static readonly bool Windows10 = Environment.OSVersion.Version.Build < 22000;

    readonly Form finestra;
    readonly Icon icona;
    readonly string testo;
    int dpi = 96;
    MisureBarra misure = BarraTitolo.Misure(96);
    Font carattere = new("Segoe UI", 12f, GraphicsUnit.Pixel);
    Icon iconaPiccola;
    ZonaBarra passaggio = ZonaBarra.Fuori;
    ZonaBarra premuto = ZonaBarra.Fuori;
    bool uscitaTracciata;

    public BarraTitoloFinestra(Form finestra, Icon icona, string testo)
    {
        this.finestra = finestra;
        this.icona = icona;
        this.testo = testo;
        iconaPiccola = new Icon(icona, 16, 16);
    }

    public int Altezza => misure.Altezza;

    int Px(int dip) => (int)Math.Round(dip * dpi / 96.0);

    /// <summary>Nuove misure, carattere e icona per una scala diversa (creazione e cambio di monitor).</summary>
    public void ImpostaDpi(int nuovo)
    {
        dpi = nuovo;
        misure = BarraTitolo.Misure(nuovo);
        carattere.Dispose();
        // In pixel e non in punti: un carattere in punti seguirebbe i DPI di sistema, non quelli del monitor.
        carattere = new Font("Segoe UI", 12f * nuovo / 96f, GraphicsUnit.Pixel);
        iconaPiccola.Dispose();
        iconaPiccola = new Icon(icona, Px(16), Px(16));
        Ridisegna();
    }

    public void Ridisegna()
    {
        if (finestra.IsHandleCreated) finestra.Invalidate(new Rectangle(0, 0, finestra.ClientSize.Width, misure.Altezza));
    }

    /// <summary>true se il messaggio è stato gestito qui e non va passato alla procedura standard.</summary>
    public bool Gestisci(ref Message m, ProceduraStandard standard)
    {
        switch (m.Msg)
        {
            case Win32.WM_NCCALCSIZE when m.WParam != IntPtr.Zero:
                CalcolaArea(ref m, standard);
                return true;

            case Win32.WM_NCHITTEST:
            {
                // Prima la cornice standard (bordi laterali e inferiore), poi la striscia.
                standard(ref m);
                if (m.Result != (IntPtr)Win32.HTCLIENT) return true;
                var p = finestra.PointToClient(PuntoSchermo(m.LParam));
                var zona = BarraTitolo.Zona(p.X, p.Y, finestra.ClientSize.Width, misure, Win32.IsZoomed(m.HWnd));
                if (zona != ZonaBarra.Fuori) m.Result = (IntPtr)CodiceHT(zona);
                return true;
            }

            // Sulle zone pulsante niente procedura standard: disegnerebbe i pulsanti classici sopra la
            // striscia e avvierebbe il suo tracciamento modale. Il riquadro Snap dipende solo da HTMAXBUTTON.
            case Win32.WM_NCMOUSEMOVE:
            {
                TracciaUscita();
                var zona = ZonaDaCodice((int)m.WParam);
                CambiaStato(EPulsante(zona) ? zona : ZonaBarra.Fuori, premuto);
                if (!EPulsante(zona)) return false;
                m.Result = IntPtr.Zero;
                return true;
            }

            // Il secondo clic rapido arriva come doppio clic anche senza CS_DBLCLKS: vale come pressione.
            case Win32.WM_NCLBUTTONDOWN:
            case Win32.WM_NCLBUTTONDBLCLK:
            {
                var zona = ZonaDaCodice((int)m.WParam);
                if (!EPulsante(zona)) return false;
                CambiaStato(zona, zona);
                m.Result = IntPtr.Zero;
                return true;
            }

            case Win32.WM_NCLBUTTONUP:
            {
                var zona = ZonaDaCodice((int)m.WParam);
                var eraPremuto = premuto;
                // "Premuto" si azzera a ogni rilascio: senza cattura del mouse un rilascio altrove non arriva.
                CambiaStato(passaggio, ZonaBarra.Fuori);
                if (!EPulsante(zona)) return false;
                if (zona == eraPremuto) Esegui(zona);
                m.Result = IntPtr.Zero;
                return true;
            }

            case Win32.WM_NCMOUSELEAVE:
                uscitaTracciata = false;
                CambiaStato(ZonaBarra.Fuori, ZonaBarra.Fuori);
                return false;
        }
        return false;
    }

    // L'area client comincia dal bordo superiore della finestra: la barra standard sparisce, i bordi
    // laterali e inferiore (e il ridimensionamento da lì) restano quelli calcolati da Windows.
    void CalcolaArea(ref Message m, ProceduraStandard standard)
    {
        int alto = Marshal.PtrToStructure<Win32.NCCALCSIZE_PARAMS>(m.LParam).rgrc0.Top;
        standard(ref m);
        var p = Marshal.PtrToStructure<Win32.NCCALCSIZE_PARAMS>(m.LParam);
        p.rgrc0.Top = alto;
        if (Win32.IsZoomed(m.HWnd))
        {
            // Ingrandita, la finestra sborda dallo schermo dello spessore della cornice: senza questo
            // la striscia finirebbe in parte fuori.
            uint dpiFinestra = Win32.GetDpiForWindow(m.HWnd);
            p.rgrc0.Top += Win32.GetSystemMetricsForDpi(Win32.SM_CYFRAME, dpiFinestra)
                         + Win32.GetSystemMetricsForDpi(Win32.SM_CXPADDEDBORDER, dpiFinestra);
            LasciaSpazioBarraAScomparsa(ref p.rgrc0);
        }
        Marshal.StructureToPtr(p, m.LParam, false);
        m.Result = IntPtr.Zero;
    }

    // Se l'area client coprisse tutto il monitor Windows la tratterebbe come schermo intero e la barra
    // delle applicazioni a scomparsa non ricomparirebbe più (come Windows Terminal: 2 px liberi su quel lato).
    static void LasciaSpazioBarraAScomparsa(ref Win32.RECT area)
    {
        // Il monitor dell'area proposta, non quello attuale della finestra: con Win+Maiusc+freccia
        // la finestra si ingrandisce su un altro monitor prima di esserci arrivata.
        var monitor = Screen.FromRectangle(Rectangle.FromLTRB(area.Left, area.Top, area.Right, area.Bottom)).Bounds;
        bool aScomparsa(uint lato)
        {
            var dati = new Win32.APPBARDATA
            {
                cbSize = (uint)Marshal.SizeOf<Win32.APPBARDATA>(),
                uEdge = lato,
                rc = new Win32.RECT { Left = monitor.Left, Top = monitor.Top, Right = monitor.Right, Bottom = monitor.Bottom }
            };
            return Win32.SHAppBarMessage(Win32.ABM_GETAUTOHIDEBAREX, ref dati) != IntPtr.Zero;
        }
        if (aScomparsa(Win32.ABE_TOP)) area.Top += 2;
        if (aScomparsa(Win32.ABE_BOTTOM)) area.Bottom -= 2;
        if (aScomparsa(Win32.ABE_LEFT)) area.Left += 2;
        if (aScomparsa(Win32.ABE_RIGHT)) area.Right -= 2;
    }

    void TracciaUscita()
    {
        if (uscitaTracciata) return;
        var dati = new Win32.TRACKMOUSEEVENT
        {
            cbSize = (uint)Marshal.SizeOf<Win32.TRACKMOUSEEVENT>(),
            dwFlags = Win32.TME_LEAVE | Win32.TME_NONCLIENT,
            hwndTrack = finestra.Handle
        };
        uscitaTracciata = Win32.TrackMouseEvent(ref dati);
    }

    void CambiaStato(ZonaBarra nuovoPassaggio, ZonaBarra nuovoPremuto)
    {
        if (nuovoPassaggio == passaggio && nuovoPremuto == premuto) return;
        passaggio = nuovoPassaggio;
        premuto = nuovoPremuto;
        Ridisegna();
    }

    // Come i pulsanti veri: WM_SYSCOMMAND, così Windows applica animazioni e stato di Snap.
    // SC_CLOSE arriva a OnFormClosing come UserClosing: la finestra si nasconde (D11).
    void Esegui(ZonaBarra zona)
    {
        int comando = zona switch
        {
            ZonaBarra.Riduci => Win32.SC_MINIMIZE,
            ZonaBarra.Ingrandisci => Win32.IsZoomed(finestra.Handle) ? Win32.SC_RESTORE : Win32.SC_MAXIMIZE,
            _ => Win32.SC_CLOSE
        };
        Win32.PostMessage(finestra.Handle, Win32.WM_SYSCOMMAND, (IntPtr)comando, IntPtr.Zero);
    }

    static bool EPulsante(ZonaBarra z) => z is ZonaBarra.Riduci or ZonaBarra.Ingrandisci or ZonaBarra.Chiudi;

    static Point PuntoSchermo(IntPtr lParam)
    {
        // Lette come short: sui monitor a sinistra o sopra il principale le coordinate sono negative.
        long v = lParam.ToInt64();
        return new Point((short)(v & 0xFFFF), (short)((v >> 16) & 0xFFFF));
    }

    static int CodiceHT(ZonaBarra z) => z switch
    {
        ZonaBarra.Riduci => Win32.HTMINBUTTON,
        ZonaBarra.Ingrandisci => Win32.HTMAXBUTTON,
        ZonaBarra.Chiudi => Win32.HTCLOSE,
        ZonaBarra.BordoAlto => Win32.HTTOP,
        ZonaBarra.AngoloAltoSinistra => Win32.HTTOPLEFT,
        ZonaBarra.AngoloAltoDestra => Win32.HTTOPRIGHT,
        _ => Win32.HTCAPTION
    };

    static ZonaBarra ZonaDaCodice(int codice) => codice switch
    {
        Win32.HTMINBUTTON => ZonaBarra.Riduci,
        Win32.HTMAXBUTTON => ZonaBarra.Ingrandisci,
        Win32.HTCLOSE => ZonaBarra.Chiudi,
        _ => ZonaBarra.Fuori
    };

    public void Disegna(Graphics g)
    {
        int larghezza = finestra.ClientSize.Width;
        bool contrasto = SystemInformation.HighContrast;
        bool attiva = Form.ActiveForm == finestra;
        bool ingrandita = Win32.IsZoomed(finestra.Handle);

        using (var fondo = new SolidBrush(contrasto ? SystemColors.ActiveCaption : Sfondo))
            g.FillRectangle(fondo, 0, 0, larghezza, misure.Altezza);
        if (Windows10 && !ingrandita)
            using (var linea = new Pen(BordoWindows10))
                g.DrawLine(linea, 0, 0, larghezza, 0);

        int lato = Px(16);
        int x = Px(12);
        g.DrawIcon(iconaPiccola, new Rectangle(x, (misure.Altezza - lato) / 2, lato, lato));
        int inizioTesto = x + lato + Px(8);
        var areaTesto = new Rectangle(inizioTesto, 0, Math.Max(0, larghezza - 3 * misure.LarghezzaPulsante - inizioTesto - Px(8)), misure.Altezza);
        var coloreTesto = contrasto ? SystemColors.ActiveCaptionText : attiva ? Titolo : Spento;
        TextRenderer.DrawText(g, testo, carattere, areaTesto, coloreTesto,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        foreach (var (zona, area) in BarraTitolo.Pulsanti(larghezza, misure))
        {
            var r = new Rectangle(area.X, area.Y, area.L, area.A);
            bool chiudi = zona == ZonaBarra.Chiudi;
            Color? evidenza = passaggio != zona ? null
                : premuto == zona ? (chiudi ? ChiudiPremuto : Premuto)
                : (chiudi ? ChiudiPassaggio : Passaggio);
            if (contrasto && evidenza is not null) evidenza = SystemColors.Highlight;
            if (evidenza is Color c)
                using (var pennello = new SolidBrush(c))
                    g.FillRectangle(pennello, r);
            var colore = contrasto ? (evidenza is null ? SystemColors.ActiveCaptionText : SystemColors.HighlightText)
                : chiudi && evidenza is not null ? Color.White
                : attiva ? Glifo : Spento;
            DisegnaGlifo(g, zona, r, colore, ingrandita);
        }
    }

    // Glifi a linee come quelli di Windows 11: 10 DIP, spessore 1 DIP.
    void DisegnaGlifo(Graphics g, ZonaBarra zona, Rectangle r, Color colore, bool ingrandita)
    {
        float s = Px(10);
        float x = r.X + (r.Width - s) / 2f;
        float y = r.Y + (r.Height - s) / 2f;
        using var penna = new Pen(colore, Math.Max(1f, dpi / 96f));
        var prima = g.SmoothingMode;
        g.SmoothingMode = zona == ZonaBarra.Chiudi ? SmoothingMode.AntiAlias : SmoothingMode.None;
        switch (zona)
        {
            case ZonaBarra.Riduci:
                g.DrawLine(penna, x, y + s / 2f, x + s, y + s / 2f);
                break;
            case ZonaBarra.Ingrandisci when ingrandita:
                // Ripristina: due quadrati sovrapposti.
                float d = MathF.Round(s * 0.2f);
                g.DrawRectangle(penna, x, y + d, s - d, s - d);
                g.DrawLines(penna, [new PointF(x + d, y + d), new PointF(x + d, y), new PointF(x + s, y), new PointF(x + s, y + s - d), new PointF(x + s - d, y + s - d)]);
                break;
            case ZonaBarra.Ingrandisci:
                g.DrawRectangle(penna, x, y, s, s);
                break;
            default:
                g.DrawLine(penna, x, y, x + s, y + s);
                g.DrawLine(penna, x + s, y, x, y + s);
                break;
        }
        g.SmoothingMode = prima;
    }

    public void Dispose()
    {
        carattere.Dispose();
        iconaPiccola.Dispose();
    }
}
```

- [ ] **Step 2: Modificare `FinestraPrincipale.cs`**

```diff
--- a/src/EnergyItaly.SfondoAnimato/FinestraPrincipale.cs
+++ b/src/EnergyItaly.SfondoAnimato/FinestraPrincipale.cs
@@ -1,16 +1,21 @@
 using System.Drawing;
 using EnergyItaly.SfondoAnimato.Core;
 using Microsoft.Web.WebView2.Core;
+using Microsoft.Win32;
 
 namespace EnergyItaly.SfondoAnimato;
 
-/// <summary>Finestra galleria (design B). Chiuderla la nasconde: il programma resta nell'area di notifica (D11).</summary>
+/// <summary>
+/// Finestra galleria (design B). Chiuderla la nasconde: il programma resta nell'area di notifica (D11).
+/// In cima la striscia del titolo Energy Italy (D12); la WebView occupa il resto.
+/// </summary>
 sealed class FinestraPrincipale : Form
 {
     static readonly Color Fondo = Color.FromArgb(0x07, 0x11, 0x1f);
     static readonly TimeSpan IntervalloCrash = TimeSpan.FromMinutes(5);
 
     readonly ILog log;
+    readonly BarraTitoloFinestra barra;
     HostWebView web;
     readonly Action<string> daApp;
     readonly Action<IReadOnlyList<string>> importa;
@@ -28,14 +33,30 @@ sealed class FinestraPrincipale : Form
         Icon = icona;
         BackColor = Fondo;
         StartPosition = FormStartPosition.CenterScreen;
+        barra = new BarraTitoloFinestra(this, icona, Text);
+        DoubleBuffered = true;
+        SystemEvents.UserPreferenceChanged += PreferenzeCambiate;
     }
 
+    protected override void WndProc(ref Message m)
+    {
+        // barra è null solo se Windows manda messaggi prima della fine del costruttore.
+        if (barra is not null && barra.Gestisci(ref m, ProceduraStandard)) return;
+        base.WndProc(ref m);
+    }
+
+    void ProceduraStandard(ref Message m) => base.WndProc(ref m);
+
     protected override void OnHandleCreated(EventArgs e)
     {
         base.OnHandleCreated(e);
         int scuro = 1;
         if (Win32.DwmSetWindowAttribute(Handle, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE, ref scuro, sizeof(int)) != 0)
             Win32.DwmSetWindowAttribute(Handle, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE_PRIMA_20H1, ref scuro, sizeof(int));
+        barra.ImpostaDpi(DeviceDpi);
+        // Ricalcolo della cornice subito: la barra standard non compare nemmeno al primo disegno.
+        Win32.SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0,
+            Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED);
         float k = DeviceDpi / 96f;
         var lavoro = Screen.FromHandle(Handle).WorkingArea;
         MinimumSize = new Size(Math.Min((int)(960 * k), lavoro.Width), Math.Min((int)(640 * k), lavoro.Height));
@@ -84,7 +105,7 @@ sealed class FinestraPrincipale : Form
     {
         try
         {
-            await web.InizializzaAsync(Handle, ClientRectangle, scalaFissa: false, Fondo, $"https://{Catalogo.HostApp}/app/index.html");
+            await web.InizializzaAsync(Handle, AreaWebView(), scalaFissa: false, Fondo, $"https://{Catalogo.HostApp}/app/index.html");
             AggiornaWebView();
         }
         catch (Exception ex)
@@ -96,6 +117,8 @@ sealed class FinestraPrincipale : Form
     protected override void OnResize(EventArgs e)
     {
         base.OnResize(e);
+        // I pulsanti seguono il bordo destro: si ridisegna solo la striscia.
+        barra.Ridisegna();
         AggiornaWebView();
     }
 
@@ -105,12 +128,51 @@ sealed class FinestraPrincipale : Form
         AggiornaWebView();
     }
 
+    protected override void OnDpiChanged(DpiChangedEventArgs e)
+    {
+        base.OnDpiChanged(e);
+        barra.ImpostaDpi(e.DeviceDpiNew);
+        AggiornaWebView();
+    }
+
+    protected override void OnPaint(PaintEventArgs e)
+    {
+        base.OnPaint(e);
+        barra.Disegna(e.Graphics);
+    }
+
+    protected override void OnActivated(EventArgs e)
+    {
+        base.OnActivated(e);
+        barra.Ridisegna();
+    }
+
+    protected override void OnDeactivate(EventArgs e)
+    {
+        base.OnDeactivate(e);
+        barra.Ridisegna();
+    }
+
+    // Il contrasto alto può cambiare a programma aperto: la striscia passa ai colori di sistema.
+    void PreferenzeCambiate(object? mittente, UserPreferenceChangedEventArgs e)
+    {
+        if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(barra.Ridisegna));
+    }
+
+    /// <summary>L'area della WebView: tutto sotto la striscia del titolo.</summary>
+    Rectangle AreaWebView()
+    {
+        int alto = barra.Altezza;
+        return new Rectangle(0, alto, ClientSize.Width, Math.Max(0, ClientSize.Height - alto));
+    }
+
     void AggiornaWebView()
     {
         var controller = web.Controller;
         if (controller is null) return;
-        controller.Bounds = ClientRectangle;
         bool visibile = Visible && WindowState != FormWindowState.Minimized;
+        // Ridotta a icona l'area client è 0 x 0: i limiti restano quelli di prima.
+        if (visibile) web.ImpostaArea(AreaWebView());
         if (controller.IsVisible != visibile) controller.IsVisible = visibile;
     }
 
@@ -139,6 +201,14 @@ sealed class FinestraPrincipale : Form
         Close();
     }
 
+    protected override void Dispose(bool disposing)
+    {
+        if (disposing) SystemEvents.UserPreferenceChanged -= PreferenzeCambiate;
+        base.Dispose(disposing);
+        // Dopo: durante la distruzione della finestra WndProc riceve ancora messaggi.
+        if (disposing) barra.Dispose();
+    }
+
     public void Invia(string json)
     {
         try { web.Core?.PostWebMessageAsJson(json); }
```

- [ ] **Step 3: Build** dell'app senza avvisi nuovi.

> **Nota di esecuzione (2026-10-14):** nella prova sul PC il clic destro sulla striscia non apriva il menu di sistema (la procedura standard non riconosce la striscia come barra del titolo). Aggiunta in `BarraTitoloFinestra` la gestione di `WM_NCRBUTTONDOWN` e `WM_NCRBUTTONUP` con `ApriMenuDiSistema`, e in `Win32.cs` le costanti e funzioni relative (`SC_SIZE`, `SC_MOVE`, `TPM_*`, `MF_*`, `GetSystemMenu`, `EnableMenuItem`, `TrackPopupMenu`). Vedi spec 3.3.
- [ ] **Step 4: Commit.**

```bash
git add src/EnergyItaly.SfondoAnimato/Win32.cs src/EnergyItaly.SfondoAnimato/BarraTitoloFinestra.cs src/EnergyItaly.SfondoAnimato/FinestraPrincipale.cs
git commit -m "Finestra principale con barra del titolo Energy Italy disegnata dal programma"
```

## Chunk 4: Verifica e consegna

### Task 6: prova del programma, documenti, push

- [ ] **Step 1: Test Core** tutti verdi.
- [ ] **Step 2: Publish** in `publish/` (chiudere prima il programma in esecuzione: `Stop-Process -Name SfondoAnimato`).
- [ ] **Step 3: Prova a mano sul PC** (spec sezione 7, "Programma"). Avviare `publish\SfondoAnimato.exe` e controllare: aspetto della striscia (attiva e non attiva), trascinamento, doppio clic, clic destro e Alt+Spazio, Snap layouts su "ingrandisci", Aero Snap, ridimensionamento da tutti i bordi e dagli angoli in alto, ingrandita senza parti fuori schermo, riduci e ripristina, chiudi e Alt+F4 che nascondono, doppio clic rapido sui pulsanti, premere e rilasciare fuori, passaggio del mouse senza pulsanti classici, clic destro su un pulsante (nessun pulsante classico disegnato, al massimo il menu di sistema). Le prove non eseguibili da qui (cambio di scala, monitor con scale diverse, barra a scomparsa, contrasto alto) si elencano all'utente come da fare.
  - Se passando più volte tra monitor con scale diverse la finestra cresce o cala a ogni passaggio (spec 3.3, "Cambio DPI"), la correzione va in `FinestraPrincipale.WndProc`: gestire `WM_GETDPISCALEDSIZE` (0x02E4) restituendo in `lParam` (struttura `SIZE`) la dimensione attuale scalata di `nuovoDpi / DeviceDpi`, con `m.Result = 1`.
- [ ] **Step 4: Spec generale.** In `docs/superpowers/specs/2026-10-13-sfondo-animato-design.md`, riga D10, aggiungere in fondo alla cella del motivo: `Superata da D12 in 2026-10-14-finestra-custom-design.md.`
- [ ] **Step 5: Commit e push.** `.claude/launch.json` contiene una voce temporanea (`anteprima-finestra`, punta a una cartella temporanea della sessione): va annullata con `git checkout .claude/launch.json` e non va committata.

```bash
git checkout .claude/launch.json
git add docs/superpowers/specs/2026-10-13-sfondo-animato-design.md
git commit -m "La spec generale rimanda alla nuova barra del titolo"
git push origin main
```
