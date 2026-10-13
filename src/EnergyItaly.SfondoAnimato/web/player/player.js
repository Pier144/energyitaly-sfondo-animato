/* Player degli sfondi: mostra una voce alla volta, in una o più scene, con dissolvenza (spec 5.1). */
(function () {
  'use strict';

  const ATTESA_MASSIMA_MS = 10000;
  const palco = document.getElementById('palco');
  const ponte = creaPonte();
  let corrente = null; // { dati, strato, elementi }
  let visibile = null; // ultima voce mostrata davvero: il suo strato è ancora sul palco
  let inPausa = false;

  function creaPonte() {
    const webview = window.chrome && window.chrome.webview;
    if (webview) {
      return {
        invia: (m) => webview.postMessage(m),
        ascolta: (cb) => webview.addEventListener('message', (e) => cb(e.data))
      };
    }
    // Pagina di prova nel browser: comandi dalla finestra genitore.
    return {
      invia: (m) => { if (window.parent !== window) window.parent.postMessage({ dalPlayer: m }, '*'); },
      ascolta: (cb) => window.addEventListener('message', (e) => {
        if (e.source === window.parent && e.data && e.data.alPlayer) cb(e.data.alPlayer);
      })
    };
  }

  function conMonitor(url, monitor) {
    const u = new URL(url, location.href);
    u.searchParams.set('monitor', monitor.map((m) => [m.x, m.y, m.w, m.h].join(',')).join(';'));
    return u.toString();
  }

  function posiziona(nodo, area) {
    nodo.style.left = area.x + 'px';
    nodo.style.top = area.y + 'px';
    nodo.style.width = area.w + 'px';
    nodo.style.height = area.h + 'px';
  }

  function creaElemento(dati, scena, primo) {
    let nodo;
    let pronto;
    if (dati.tipoVoce === 'animazione' || dati.tipoVoce === 'html') {
      nodo = document.createElement('iframe');
      nodo.className = 'elemento';
      nodo.title = 'Sfondo';
      nodo.setAttribute('scrolling', 'no');
      nodo.setAttribute('tabindex', '-1');
      if (dati.tipoVoce === 'html') nodo.setAttribute('sandbox', 'allow-scripts');
      // L'evento load arriva anche per una pagina 404: lo stato lo dice solo una richiesta a parte.
      pronto = Promise.all([
        new Promise((ok) => nodo.addEventListener('load', ok, { once: true })),
        primo ? verificaPagina(dati.url) : null
      ]);
      nodo.src = dati.modo === 'estendi' ? conMonitor(dati.url, scena.monitor) : dati.url;
    } else if (dati.tipoVoce === 'immagine') {
      nodo = document.createElement('div');
      nodo.className = 'elemento';
      const img = document.createElement('img');
      img.alt = '';
      pronto = new Promise((ok, ko) => {
        img.onload = ok;
        img.onerror = () => ko(new Error('immagine non leggibile'));
      });
      img.src = dati.url;
      nodo.appendChild(img);
    } else {
      nodo = document.createElement('div');
      nodo.className = 'elemento';
      const video = document.createElement('video');
      video.muted = true;
      video.defaultMuted = true;
      video.playsInline = true;
      video.loop = !dati.fineVideo;
      video.preload = 'auto';
      pronto = new Promise((ok, ko) => {
        video.addEventListener('loadeddata', ok, { once: true });
        video.addEventListener('error', () => ko(new Error('video non riproducibile')), { once: true });
      });
      if (primo) {
        video.addEventListener('loadedmetadata', () => {
          if (isFinite(video.duration) && video.duration > 0) {
            ponte.invia({ tipo: 'durataVideo', id: dati.id, secondi: video.duration });
          }
        }, { once: true });
        video.addEventListener('ended', () => ponte.invia({ tipo: 'videoFinito', id: dati.id }));
      }
      video.src = dati.url;
      nodo.appendChild(video);
    }
    posiziona(nodo, scena.area);
    return { nodo, pronto };
  }

  function verificaPagina(url) {
    return fetch(url, { cache: 'no-store' }).then((r) => {
      if (!r.ok) throw new Error('pagina non trovata (' + r.status + ')');
    });
  }

  function conLimite(promessa) {
    return Promise.race([
      promessa,
      new Promise((_, ko) => setTimeout(() => ko(new Error('tempo scaduto')), ATTESA_MASSIMA_MS))
    ]);
  }

  function applicaPausa(voce, pausa) {
    voce.elementi.forEach((nodo) => {
      if (nodo.tagName === 'IFRAME') {
        try { nodo.contentWindow.postMessage({ tipo: pausa ? 'pausa' : 'riprendi' }, '*'); } catch (e) { /* pagina non raggiungibile */ }
        return;
      }
      const video = nodo.querySelector('video');
      if (!video) return;
      if (pausa) video.pause();
      else video.play().catch(() => { /* riproduzione automatica rifiutata: riprova al prossimo riprendi */ });
    });
  }

  // Toglie dal palco tutto tranne la voce indicata (libera iframe e video, come RF-12).
  function pulisci(voce) {
    if (corrente !== voce) return;
    Array.from(palco.children).forEach((figlio) => { if (figlio !== voce.strato) figlio.remove(); });
  }

  function mostra(dati) {
    const strato = document.createElement('div');
    strato.className = 'strato';
    const durata = Math.max(0, Number(dati.dissolvenzaMs) || 0);
    strato.style.transitionDuration = durata + 'ms';
    const creati = dati.scene.map((s, i) => creaElemento(dati, s, i === 0));
    creati.forEach((c) => strato.appendChild(c.nodo));
    palco.appendChild(strato);
    const voce = { dati, strato, elementi: creati.map((c) => c.nodo) };
    corrente = voce;

    conLimite(Promise.all(creati.map((c) => c.pronto))).then(() => {
      if (corrente !== voce) return;
      visibile = voce;
      applicaPausa(voce, inPausa);
      if (durata === 0) strato.classList.add('visibile');
      else requestAnimationFrame(() => requestAnimationFrame(() => strato.classList.add('visibile')));
      setTimeout(() => pulisci(voce), durata + 100);
      ponte.invia({ tipo: 'mostrato', id: dati.id });
    }).catch((errore) => {
      strato.remove();
      if (corrente !== voce) return;
      // Non "precedente": se anche quella è fallita, il suo strato non c'è più.
      corrente = visibile;
      ponte.invia({ tipo: 'errore', id: dati.id, messaggio: errore && errore.message ? errore.message : String(errore) });
    });
  }

  function layout(dati) {
    if (!corrente) return;
    const vecchi = corrente.dati;
    if (dati.modo !== vecchi.modo || dati.scene.length !== corrente.elementi.length) {
      mostra(Object.assign({}, vecchi, { modo: dati.modo, scene: dati.scene }));
      return;
    }
    corrente.dati = Object.assign({}, vecchi, { scene: dati.scene });
    corrente.elementi.forEach((nodo, i) => {
      posiziona(nodo, dati.scene[i].area);
      if (nodo.tagName !== 'IFRAME') return;
      try { nodo.contentWindow.postMessage({ tipo: 'layout', monitor: dati.scene[i].monitor }, '*'); } catch (e) { /* ignora */ }
    });
  }

  ponte.ascolta((m) => {
    if (!m || typeof m !== 'object') return;
    switch (m.tipo) {
      case 'mostra':
        mostra(m);
        break;
      case 'pausa':
        inPausa = true;
        if (corrente) applicaPausa(corrente, true);
        break;
      case 'riprendi':
        inPausa = false;
        if (corrente) applicaPausa(corrente, false);
        break;
      case 'layout':
        layout(m);
        break;
      default:
        break;
    }
  });

  ponte.invia({ tipo: 'pronto' });
})();
