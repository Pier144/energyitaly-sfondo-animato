/*
 * Contratto comune delle animazioni Energy Italy (vedi docs/PRD.md, sezione "Contratto animazioni").
 *
 * Disposizione dei monitor: il player la passa nella query string
 *   ?monitor=x,y,w,h;x,y,w,h
 * in pixel CSS, relativi all'angolo in alto a sinistra della finestra.
 * Senza parametro la finestra intera vale come un solo monitor.
 *
 * Colori (spec personalizzazione 3.2-3.3): ?fondo=rrggbb&accento=rrggbb&secondario=rrggbb, esadecimali senza #.
 * Mancano tutti: tavolozza Energy Italy. Manca solo il secondario: si ricava dal fondo ("Personalizzata").
 * I ruoli si leggono con Sfondo.colori(), Sfondo.tono(), Sfondo.velo(), Sfondo.rgba() e nei CSS come
 * var(--accento-chiaro) o rgba(var(--accento-chiaro-rgb), 0.5).
 * Le opzioni dell'animazione arrivano anch'esse nella query string: numeri con il punto decimale, interruttori 1/0.
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

  // ---------- Colori (spec personalizzazione 3.3) ----------
  // Tre colori base dalla query string (esadecimali senza #); le sfumature si ricavano spostando luminosità e
  // saturazione HSL. Con la tavolozza Energy Italy i ruoli cadono sui colori usati fino alla versione 1.1.2.
  const ENERGY_ITALY = { fondo: '#0f1e35', accento: '#7ab52a', secondario: '#1e5b8e' };
  const RUOLI = {
    fondoScuro: ['fondo', -6, 0],
    fondoChiaro: ['fondo', 8, 0],
    accentoChiaro: ['accento', 21, 0],
    accentoPallido: ['accento', 36, 0],
    secondarioScuro: ['secondario', -5, 0],
    secondarioMedio: ['secondario', 11, -6],
    secondarioChiaro: ['secondario', 20, -12]
  };
  // Il velo dietro il logo bianco non supera questa luminosità, anche con un fondo chiaro.
  const LUCE_MASSIMA_VELO = 12;

  const limita = (x) => Math.max(0, Math.min(100, x));

  function inRgb(hex) {
    return [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16));
  }

  function inHsl(hex) {
    const [r, g, b] = inRgb(hex).map((x) => x / 255);
    const max = Math.max(r, g, b);
    const min = Math.min(r, g, b);
    const l = (max + min) / 2;
    if (max === min) return [0, 0, l * 100];
    const d = max - min;
    const s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
    const h = max === r ? (g - b) / d + (g < b ? 6 : 0) : max === g ? (b - r) / d + 2 : (r - g) / d + 4;
    return [h * 60, s * 100, l * 100];
  }

  function daHsl(h, s, l) {
    s /= 100;
    l /= 100;
    const k = (n) => (n + h / 30) % 12;
    const a = s * Math.min(l, 1 - l);
    const f = (n) => l - a * Math.max(-1, Math.min(k(n) - 3, 9 - k(n), 1));
    return '#' + [0, 8, 4].map((n) => Math.round(f(n) * 255).toString(16).padStart(2, '0')).join('');
  }

  /** Lo stesso colore con luminosità e saturazione spostate di qualche punto. */
  function sposta(hex, luce, saturazione) {
    const [h, s, l] = inHsl(hex);
    return daHsl(h, limita(s + (saturazione || 0)), limita(l + (luce || 0)));
  }

  /** Tinta e saturazione del fondo, luminosità spostata ma mai sopra LUCE_MASSIMA_VELO. */
  function velo(luce) {
    const [h, s, l] = inHsl(colori.fondo);
    return daHsl(h, s, limita(Math.min(l + (luce || 0), LUCE_MASSIMA_VELO)));
  }

  function leggiColore(nome) {
    const v = new URLSearchParams(location.search).get(nome);
    return v && /^[0-9a-fA-F]{6}$/.test(v) ? '#' + v.toLowerCase() : null;
  }

  const colori = {
    fondo: leggiColore('fondo') || ENERGY_ITALY.fondo,
    accento: leggiColore('accento') || ENERGY_ITALY.accento
  };
  // Con "Personalizzata" arrivano solo fondo e accento: il secondario si ricava dal fondo (spec P4),
  // più chiaro se il fondo è scuro e più scuro se il fondo è chiaro. Un fondo grigio resta grigio.
  function secondarioDalFondo(fondo) {
    const [, s, l] = inHsl(fondo);
    return sposta(fondo, l < 50 ? 22 : -30, s < 5 ? 0 : 20);
  }
  colori.secondario = leggiColore('secondario') ||
    (leggiColore('fondo') ? secondarioDalFondo(colori.fondo) : ENERGY_ITALY.secondario);
  for (const [ruolo, [base, luce, saturazione]] of Object.entries(RUOLI)) colori[ruolo] = sposta(colori[base], luce, saturazione);
  colori.velo = velo(0);

  const stileRadice = document.documentElement.style;
  for (const [ruolo, hex] of Object.entries(colori)) {
    const nome = '--' + ruolo.replace(/[A-Z]/g, (c) => '-' + c.toLowerCase());
    stileRadice.setProperty(nome, hex);
    stileRadice.setProperty(nome + '-rgb', inRgb(hex).join(', '));
  }

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

    /** I colori dei ruoli come "#rrggbb" (spec personalizzazione 3.3). */
    colori() {
      return Object.assign({}, colori);
    },

    /** Un ruolo con luminosità (e saturazione) spostate: per le sfumature che non hanno un ruolo proprio. */
    tono(ruolo, luce, saturazione) {
      return sposta(colori[ruolo], luce, saturazione);
    },

    /** Il velo scuro dietro il logo: il fondo spostato di qualche punto, mai più chiaro della luminosità 12. */
    velo(luce) {
      return velo(luce);
    },

    /** "rgba(...)" di un ruolo o di un colore "#rrggbb". */
    rgba(colore, alfa) {
      const hex = colore.startsWith('#') ? colore : colori[colore];
      return 'rgba(' + inRgb(hex).join(',') + ',' + alfa + ')';
    },

    /** Interruttore dalla query string: 1/0 (accetta anche true/false). */
    interruttore(nome, predefinito) {
      const v = new URLSearchParams(location.search).get(nome);
      if (v === '1' || v === 'true') return true;
      if (v === '0' || v === 'false') return false;
      return predefinito;
    },

    /** Componenti [r, g, b] di un colore "#rrggbb". */
    rgb(hex) {
      return inRgb(hex);
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
