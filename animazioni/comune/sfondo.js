/*
 * Contratto comune delle animazioni Energy Italy (vedi docs/PRD.md, sezione "Contratto animazioni").
 *
 * Disposizione dei monitor: il player la passa nella query string
 *   ?monitor=x,y,w,h;x,y,w,h
 * in pixel CSS, relativi all'angolo in alto a sinistra della finestra.
 * Senza parametro la finestra intera vale come un solo monitor.
 *
 * Messaggi accettati via postMessage (solo dalla finestra genitore):
 *   { tipo: 'pausa' }      congela tutte le animazioni CSS
 *   { tipo: 'riprendi' }   le fa ripartire
 *   { tipo: 'layout', monitor: [{ x, y, w, h }, ...] }   nuova disposizione, la scena viene ricostruita
 */
(function () {
  'use strict';

  function leggiMonitorDaUrl() {
    const valore = new URLSearchParams(location.search).get('monitor');
    if (!valore) return null;
    const lista = valore
      .split(';')
      .map((parte) => parte.split(',').map(Number))
      .filter((n) => n.length === 4 && n.every(Number.isFinite) && n[2] > 0 && n[3] > 0)
      .map((n) => ({ x: n[0], y: n[1], w: n[2], h: n[3] }));
    return lista.length ? lista : null;
  }

  function monitorValidi(lista) {
    return Array.isArray(lista) && lista.length > 0 && lista.every((m) =>
      m && [m.x, m.y, m.w, m.h].every(Number.isFinite) && m.w > 0 && m.h > 0);
  }

  let monitorPassati = leggiMonitorDaUrl();
  const ascoltatori = [];

  function stato() {
    const larghezza = window.innerWidth;
    const altezza = window.innerHeight;
    const monitor = monitorPassati || [{ x: 0, y: 0, w: larghezza, h: altezza }];
    // Il logo va sul monitor più a sinistra; a parità, su quello che arriva più in basso.
    const monitorLogo = monitor.slice().sort((a, b) => a.x - b.x || (b.y + b.h) - (a.y + a.h))[0];
    return { larghezza, altezza, monitor, monitorLogo };
  }

  // Una finestra grande zero (iframe nascosto, ad esempio una miniatura in una scheda non visibile)
  // non ha una scena da costruire: si aspetta il primo ridimensionamento reale.
  function haDimensioni(s) {
    return s.larghezza > 0 && s.altezza > 0;
  }

  function notifica() {
    const s = stato();
    if (!haDimensioni(s)) return;
    ascoltatori.forEach((cb) => cb(s));
  }

  let attesaResize = null;
  window.addEventListener('resize', () => {
    clearTimeout(attesaResize);
    attesaResize = setTimeout(notifica, 250);
  });

  window.addEventListener('message', (evento) => {
    if (evento.source !== window.parent) return;
    const msg = evento.data;
    if (!msg || typeof msg !== 'object') return;
    if (msg.tipo === 'pausa') {
      document.documentElement.classList.add('in-pausa');
    } else if (msg.tipo === 'riprendi') {
      document.documentElement.classList.remove('in-pausa');
    } else if (msg.tipo === 'layout' && monitorValidi(msg.monitor)) {
      monitorPassati = msg.monitor;
      notifica();
    }
  });

  const stilePausa = document.createElement('style');
  stilePausa.textContent =
    '.in-pausa *,.in-pausa *::before,.in-pausa *::after{animation-play-state:paused!important}';
  document.head.appendChild(stilePausa);

  window.Sfondo = {
    /** Registra la funzione che costruisce la scena: viene chiamata subito e a ogni cambio di layout. */
    avvia(costruisci) {
      ascoltatori.push(costruisci);
      const vai = () => {
        const s = stato();
        if (haDimensioni(s)) costruisci(s);
      };
      if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', vai);
      else vai();
    },

    /** Mette il logo in basso a sinistra del monitor più a sinistra, in scala con la sua altezza. */
    posizionaLogo(img, s, distanzaDalBasso) {
      const m = s.monitorLogo;
      const k = m.h / 1080;
      const larghezza = 420 * k;
      const altezza = larghezza * 205.47 / 764.29;
      img.style.left = (m.x + 64 * k) + 'px';
      img.style.top = (m.y + m.h - (distanzaDalBasso ?? 112) * k - altezza) + 'px';
      img.style.width = larghezza + 'px';
    },

    /** Generatore pseudo-casuale ripetibile (stesso seme, stessa scena). */
    casuale(seme) {
      let x = seme % 2147483647;
      if (x <= 0) x += 2147483646;
      return () => {
        x = (x * 16807) % 2147483647;
        return (x - 1) / 2147483646;
      };
    },

    /** Legge un parametro numerico o testuale dalla query string, con valore predefinito. */
    parametro(nome, predefinito) {
      const v = new URLSearchParams(location.search).get(nome);
      if (v === null || v === '') return predefinito;
      if (typeof predefinito === 'number') {
        const n = Number(v);
        return Number.isFinite(n) ? n : predefinito;
      }
      return v;
    }
  };
})();
