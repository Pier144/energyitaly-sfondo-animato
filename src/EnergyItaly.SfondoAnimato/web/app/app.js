/* Finestra principale di Energy Italy Sfondo Animato.
   Disegna lo stato ricevuto dal programma e gli manda i comandi (spec sezioni 5.2 e 7). */
(function () {
  'use strict';

  const ICONE = {
    piu: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round"><path d="M12 5v14M5 12h14"/></svg>',
    spunta: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><path d="M5 12l5 5 9-10"/></svg>',
    chiudi: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><path d="M6 6l12 12M18 6L6 18"/></svg>',
    cestino: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M4 7h16"/><path d="M9 7V4h6v3"/><path d="M6 7l1 13h10l1-13"/></svg>',
    maniglia: '<svg viewBox="0 0 24 24" fill="currentColor"><circle cx="9" cy="6" r="1.6"/><circle cx="15" cy="6" r="1.6"/><circle cx="9" cy="12" r="1.6"/><circle cx="15" cy="12" r="1.6"/><circle cx="9" cy="18" r="1.6"/><circle cx="15" cy="18" r="1.6"/></svg>',
    casuale: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M16 3h5v5"/><path d="M4 20L21 3"/><path d="M21 16v5h-5"/><path d="M15 15l6 6"/><path d="M4 4l5 5"/></svg>',
    codice: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><path d="M8 8l-4 4 4 4"/><path d="M16 8l4 4-4 4"/><path d="M13.5 5l-3 14"/></svg>',
    pausa: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round"><path d="M9 5v14M15 5v14"/></svg>',
    play: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M8 5l12 7-12 7z"/></svg>',
    successivo: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M6 5l9 7-9 7z"/><path d="M18 5v14"/></svg>'
  };

  // ---------- Ponte con il programma ----------
  const webview = window.chrome && window.chrome.webview;
  const ponte = {
    invia(m) {
      if (webview) webview.postMessage(m);
      else if (window.parent !== window) window.parent.postMessage({ dallApp: m }, '*');
    },
    inviaFile(m, file) {
      if (webview && webview.postMessageWithAdditionalObjects) webview.postMessageWithAdditionalObjects(m, file);
      else if (window.parent !== window) window.parent.postMessage({ dallApp: Object.assign({}, m, { nomiFile: Array.from(file, (f) => f.name) }) }, '*');
    },
    ascolta(cb) {
      if (webview) webview.addEventListener('message', (e) => cb(e.data));
      else window.addEventListener('message', (e) => { if (e.source === window.parent && e.data && e.data.allApp) cb(e.data.allApp); });
    }
  };

  // ---------- Stato locale ----------
  let stato = null;
  let ricevutoAlle = 0;
  let scheda = 'onda';
  let filtro = 'tutti';
  let trascinato = null;
  let attesaValore = null;
  let attesaAvviso = null;
  let chiavePalco = '';
  let daEliminare = null;
  let fuocoPrimaDellaConferma = null;
  const cacheMiniature = new Map();
  const schedePlaylist = new Map();
  const schedeLibreria = new Map();

  const $ = (selettore) => document.querySelector(selettore);

  // ---------- Utilità ----------
  function el(tag, attributi, ...figli) {
    const nodo = document.createElement(tag);
    if (attributi) {
      for (const [chiave, valore] of Object.entries(attributi)) {
        if (valore === null || valore === undefined || valore === false) continue;
        if (chiave === 'class') nodo.className = valore;
        else if (chiave === 'style') nodo.style.cssText = valore;
        else if (chiave.startsWith('on')) nodo.addEventListener(chiave.slice(2), valore);
        else nodo.setAttribute(chiave, valore === true ? '' : String(valore));
      }
    }
    for (const figlio of figli.flat()) {
      if (figlio === null || figlio === undefined || figlio === false) continue;
      nodo.append(figlio instanceof Node ? figlio : document.createTextNode(String(figlio)));
    }
    return nodo;
  }

  function icona(nome, lato) {
    const t = document.createElement('template');
    const l = lato || 16;
    t.innerHTML = ICONE[nome].replace('<svg ', `<svg width="${l}" height="${l}" aria-hidden="true" focusable="false" `);
    return t.content.firstElementChild;
  }

  function orologio(ms) {
    const s = Math.max(0, Math.round(ms / 1000));
    const h = Math.floor(s / 3600);
    const m = Math.floor((s % 3600) / 60);
    const x = s % 60;
    const due = (n) => String(n).padStart(2, '0');
    return h > 0 ? `${h}:${due(m)}:${due(x)}` : `${due(m)}:${due(x)}`;
  }

  function voce(id) {
    return (stato && stato.voci.find((v) => v.id === id)) || null;
  }

  function descrizione(v) {
    switch (v.tipoVoce) {
      case 'animazione': return 'Animazione Energy Italy';
      case 'immagine': return 'Immagine importata';
      case 'video': return 'Video importato' + (v.durataSecondi ? ', ' + orologio(v.durataSecondi * 1000) : '');
      default: return 'Pagina web importata';
    }
  }

  function metaBreve(v) {
    switch (v.tipoVoce) {
      case 'animazione': return 'Energy Italy';
      case 'immagine': return 'Immagine';
      case 'video': return 'Video' + (v.durataSecondi ? ', ' + orologio(v.durataSecondi * 1000) : '');
      default: return 'Pagina web';
    }
  }

  function conMonitor(url, monitor) {
    const u = new URL(url, location.href);
    u.searchParams.set('monitor', monitor.map((m) => [m.x, m.y, m.w, m.h].join(',')).join(';'));
    return u.toString();
  }

  function invia(iframe, tipo) {
    try { iframe.contentWindow.postMessage({ tipo }, '*'); } catch (e) { /* non ancora caricato */ }
  }

  function percento(r, W, H) {
    return `left: ${(r.x / W) * 100}%; top: ${(r.y / H) * 100}%; width: ${(r.w / W) * 100}%; height: ${(r.h / H) * 100}%`;
  }

  // Un iframe disegnato a grandezza naturale e ridotto per stare nel contenitore.
  function inScala(contenitore, iframe, larghezza, altezza) {
    iframe.style.width = larghezza + 'px';
    iframe.style.height = altezza + 'px';
    const aggiorna = () => { iframe.style.transform = `scale(${contenitore.clientWidth / larghezza})`; };
    new ResizeObserver(aggiorna).observe(contenitore);
    aggiorna();
  }

  function modifica(m) {
    ponte.invia({ tipo: 'impostazioni', modifiche: m });
  }

  // Riordina i figli senza ricaricare gli iframe (moveBefore) quando il browser lo permette.
  function riconcilia(contenitore, nodi) {
    nodi.forEach((nodo, i) => {
      const attuale = contenitore.children[i] || null;
      if (attuale === nodo) return;
      if (nodo.parentNode === contenitore && typeof contenitore.moveBefore === 'function') contenitore.moveBefore(nodo, attuale);
      else contenitore.insertBefore(nodo, attuale);
    });
    while (contenitore.children.length > nodi.length) contenitore.lastElementChild.remove();
  }

  // ---------- Miniature ----------
  function miniatura(contesto, v) {
    const chiave = contesto + '|' + v.id;
    const firma = v.url + '|' + v.disponibile;
    const memo = cacheMiniature.get(chiave);
    if (memo && memo.firma === firma) return memo.nodo;

    const nodo = el('div', { class: 'miniatura' });
    if (!v.disponibile) {
      nodo.append(el('div', { class: 'segnaposto' }, 'File non trovato'));
    } else if (v.tipoVoce === 'animazione') {
      const f = el('iframe', { class: 'miniatura-iframe', title: '', tabindex: '-1', 'aria-hidden': 'true' });
      f.addEventListener('load', () => invia(f, 'pausa'));
      nodo.addEventListener('mouseenter', () => invia(f, 'riprendi'));
      nodo.addEventListener('mouseleave', () => invia(f, 'pausa'));
      f.src = v.url;
      nodo.append(f);
      inScala(nodo, f, 1920, 1080);
    } else if (v.tipoVoce === 'immagine') {
      nodo.append(el('img', { src: v.url, alt: '', loading: 'lazy' }));
    } else if (v.tipoVoce === 'video') {
      const video = el('video', { preload: 'metadata', playsinline: true, muted: true });
      video.muted = true;
      video.src = v.url + '#t=1';
      nodo.append(video);
    } else {
      nodo.append(el('div', { class: 'segnaposto codice' }, icona('codice', 36)));
    }
    cacheMiniature.set(chiave, { nodo, firma, id: v.id });
    return nodo;
  }

  function potaCache() {
    const ids = new Set(stato.voci.map((v) => v.id));
    for (const [chiave, memo] of cacheMiniature) if (!ids.has(memo.id)) cacheMiniature.delete(chiave);
    for (const id of schedeLibreria.keys()) if (!ids.has(id)) schedeLibreria.delete(id);
    for (const id of schedePlaylist.keys()) if (!stato.playlist.includes(id)) schedePlaylist.delete(id);
  }

  // ---------- Disegno ----------
  function render() {
    if (!stato) return;
    document.body.classList.remove('in-attesa');
    document.body.classList.toggle('in-pausa', stato.inPausa);
    renderTestata();
    renderPalco();
    renderPannello();
    renderControlli();
    renderStriscia();
    renderLibreria();
    renderImpostazioni();
    potaCache();
    aggiornaConto();
  }

  function renderTestata() {
    // Lo stato si mostra solo in pausa: "attivo" è la condizione normale e non serve dirlo.
    $('#testo-stato').textContent = stato.motivoPausa ? 'IN PAUSA: ' + stato.motivoPausa : 'IN PAUSA';
    $('.stato-app').hidden = !stato.inPausa;
    document.querySelectorAll('.scheda').forEach((b) => {
      if (b.dataset.scheda === scheda) b.setAttribute('aria-current', 'page');
      else b.removeAttribute('aria-current');
    });
    ['onda', 'libreria', 'impostazioni'].forEach((nome) => { $('#vista-' + nome).hidden = nome !== scheda; });
  }

  function apriScheda(nome) {
    scheda = nome;
    render();
    $('.scorrimento').scrollTop = 0;
  }

  function didascalia() {
    const n = stato.monitor.length;
    const f = stato.finestra;
    if (n === 1) return `IL TUO DESKTOP · 1 MONITOR · ${f.w}x${f.h}`;
    if (stato.modo === 'estendi') return `IL TUO DESKTOP · ${n} MONITOR ESTESI · ${f.w}x${f.h}`;
    return `IL TUO DESKTOP · STESSA SCENA SU ${n} MONITOR`;
  }

  function aggiornaModo() {
    const uno = stato.monitor.length < 2;
    document.querySelectorAll('[data-modo]').forEach((b) => {
      b.setAttribute('aria-pressed', String(b.dataset.modo === stato.impostazioni.modoMonitor));
      b.disabled = uno;
    });
  }

  function renderPalco() {
    $('#didascalia-scena').textContent = didascalia();
    $('#modo-palco').hidden = stato.monitor.length < 2;
    aggiornaModo();
    const v = voce(stato.corrente);
    const chiave = JSON.stringify([stato.corrente, v && v.url, v && v.disponibile, stato.modo, stato.finestra, stato.monitor, stato.scene]);
    const contenitore = $('#monitor-riga');
    if (chiave !== chiavePalco) {
      chiavePalco = chiave;
      contenitore.replaceChildren(costruisciPalco(v));
    }
    contenitore.querySelectorAll('iframe').forEach((f) => invia(f, stato.inPausa ? 'pausa' : 'riprendi'));
    contenitore.querySelectorAll('video').forEach((x) => {
      if (stato.inPausa) x.pause(); else x.play().catch(() => { /* ignora */ });
    });
  }

  function costruisciPalco(v) {
    const W = stato.finestra.w;
    const H = stato.finestra.h;
    const schermi = el('div', { class: 'schermi', style: `aspect-ratio: ${W} / ${H}` });
    if (v && v.disponibile) {
      stato.scene.forEach((s) => {
        const box = el('div', { class: 'scena', style: percento(s.area, W, H) });
        box.append(contenutoScena(v, s, box));
        schermi.append(box);
      });
    }
    mascherePalco(H).forEach((r) => schermi.append(el('div', { class: 'maschera', style: percento(r, W, H) })));
    stato.monitor.forEach((m) => schermi.append(el('div', { class: 'cornice' + (m.principale ? ' principale' : ''), style: percento(m, W, H) })));
    const etichette = el('div', { class: 'etichette-monitor' });
    stato.monitor.forEach((m) => etichette.append(el('span', { style: `left: ${(m.x / W) * 100}%; width: ${(m.w / W) * 100}%` },
      `${m.nome}  ${m.w}x${m.h}${m.principale ? '  PRINCIPALE' : ''}`)));
    return el('div', { class: 'palco-schermi', style: `--rapporto: ${W / H}` }, schermi, etichette);
  }

  function contenutoScena(v, s, box) {
    if (v.tipoVoce === 'immagine') return el('img', { src: v.url, alt: '' });
    if (v.tipoVoce === 'video') {
      const video = el('video', { autoplay: true, loop: true, playsinline: true, muted: true });
      video.muted = true;
      video.src = v.url;
      return video;
    }
    const f = el('iframe', { title: 'Anteprima dello sfondo in onda', tabindex: '-1', 'aria-hidden': 'true' });
    if (v.tipoVoce === 'html') f.setAttribute('sandbox', 'allow-scripts');
    f.addEventListener('load', () => invia(f, stato && stato.inPausa ? 'pausa' : 'riprendi'));
    f.src = stato.modo === 'estendi' ? conMonitor(v.url, s.monitor) : v.url;
    inScala(box, f, s.area.w, s.area.h);
    return f;
  }

  // Copre le parti dello schermo virtuale che non stanno su nessun monitor (es. lo sfalsamento di 7 px).
  function mascherePalco(H) {
    const m = stato.monitor;
    for (let i = 0; i < m.length; i++) {
      for (let j = i + 1; j < m.length; j++) {
        if (m[i].x < m[j].x + m[j].w && m[j].x < m[i].x + m[i].w) return [];
      }
    }
    const risultato = [];
    m.forEach((r) => {
      if (r.y > 0) risultato.push({ x: r.x, y: 0, w: r.w, h: r.y });
      const basso = r.y + r.h;
      if (basso < H) risultato.push({ x: r.x, y: basso, w: r.w, h: H - basso });
    });
    return risultato;
  }

  function prossimaVoce() {
    const pl = stato.playlist.map(voce).filter((v) => v && v.disponibile);
    if (pl.length === 0) return null;
    const i = pl.findIndex((v) => v.id === stato.corrente);
    return pl[(i + 1) % pl.length];
  }

  function renderPannello() {
    const v = voce(stato.corrente);
    $('.etichetta-onda').hidden = !stato.inPausa;
    $('#titolo-corrente').textContent = v ? v.titolo : 'Nessuno sfondo';
    const pos = v ? stato.playlist.indexOf(v.id) : -1;
    $('#posizione-corrente').textContent = !v ? '' :
      descrizione(v) + (pos >= 0 ? `, ${pos + 1} di ${stato.playlist.length} nella playlist` : ', fuori dalla playlist');
    const bottone = $('#btn-pausa');
    bottone.replaceChildren(icona(stato.pausaManuale ? 'play' : 'pausa', 24));
    bottone.setAttribute('aria-label', stato.pausaManuale ? 'Riprendi' : 'Pausa');
    renderPoi();
  }

  function renderPoi() {
    const imp = stato.impostazioni;
    const poi = $('#poi');
    let figura;
    let testo;
    if (!imp.rotazioneAttiva) {
      figura = el('div', { class: 'segnaposto-poi' }, icona('successivo', 22));
      testo = 'Solo quando premi Successivo';
    } else if (imp.ordine === 'casuale' && stato.playlist.filter((id) => { const v = voce(id); return v && v.disponibile; }).length > 1) {
      figura = el('div', { class: 'segnaposto-poi' }, icona('casuale', 22));
      testo = 'A sorpresa dalla playlist';
    } else {
      const p = prossimaVoce();
      figura = p ? miniatura('poi', p) : el('div', { class: 'segnaposto-poi' });
      testo = p ? p.titolo : 'Nessuno';
    }
    const testi = el('div', { class: 'poi-testi' }, el('span', { class: 'mini' }, 'POI'), el('strong', null, testo));
    if (poi.firstElementChild === figura) poi.replaceChild(testi, poi.lastElementChild);
    else poi.replaceChildren(figura, testi);
  }

  // ---------- Controlli della playlist ----------
  function massimo(unita) {
    return unita === 'ore' ? 24 : 1440;
  }

  function mostraErrore(testo) {
    const errore = $('#intervallo-errore');
    errore.textContent = testo;
    errore.hidden = !testo;
    $('#gruppo-intervallo').classList.toggle('errato', !!testo);
  }

  function renderControlli() {
    const imp = stato.impostazioni;
    const campo = $('#intervallo-valore');
    const unita = $('#intervallo-unita');
    if (document.activeElement !== campo && $('#intervallo-errore').hidden) campo.value = imp.intervalloValore;
    if (document.activeElement !== unita) unita.value = imp.intervalloUnita;
    campo.max = massimo(unita.value);
    campo.disabled = !imp.rotazioneAttiva;
    unita.disabled = !imp.rotazioneAttiva;
    $('#gruppo-intervallo').classList.toggle('spento', !imp.rotazioneAttiva);
    $('#btn-mai').setAttribute('aria-pressed', String(!imp.rotazioneAttiva));
    $('#nota-playlist').textContent = `Trascina per cambiare l'ordine, clicca un'anteprima per mostrarla subito.`;
    $('#btn-casuale').setAttribute('aria-pressed', String(imp.ordine === 'casuale'));
  }

  function validaValore() {
    const campo = $('#intervallo-valore');
    const unita = $('#intervallo-unita').value;
    const testo = campo.value.trim();
    const n = Number(testo);
    const ok = testo !== '' && Number.isInteger(n) && n >= 1 && n <= massimo(unita);
    mostraErrore(ok ? '' : `Scrivi un numero intero da 1 a ${massimo(unita)} ${unita}`);
    return ok ? n : null;
  }

  function collegaIntervallo() {
    const campo = $('#intervallo-valore');
    const unita = $('#intervallo-unita');
    campo.addEventListener('input', () => {
      if (!stato) return;
      clearTimeout(attesaValore);
      const n = validaValore();
      if (n !== null && n !== stato.impostazioni.intervalloValore) attesaValore = setTimeout(() => modifica({ intervalloValore: n }), 600);
    });
    campo.addEventListener('keydown', (e) => {
      if (e.key !== 'Enter' || !stato) return;
      const n = validaValore();
      if (n === null) return;
      clearTimeout(attesaValore);
      if (n !== stato.impostazioni.intervalloValore) modifica({ intervalloValore: n });
    });
    campo.addEventListener('blur', () => {
      if (!stato || validaValore() !== null) return;
      clearTimeout(attesaValore);
      campo.value = stato.impostazioni.intervalloValore;
      mostraErrore('');
    });
    unita.addEventListener('change', () => {
      if (!stato) return;
      clearTimeout(attesaValore);
      const n = Math.max(1, Math.min(Math.round(Number(campo.value)) || stato.impostazioni.intervalloValore, massimo(unita.value)));
      campo.value = n;
      mostraErrore('');
      modifica({ intervalloUnita: unita.value, intervalloValore: n });
    });
  }

  // ---------- Striscia della playlist ----------
  function inviaPlaylist(ids) {
    stato.playlist = ids;
    render();
    ponte.invia({ tipo: 'playlist', ids });
  }

  function togliDallaPlaylist(id) {
    if (stato.playlist.length <= 1) {
      mostraAvviso('Lascia almeno uno sfondo nella playlist');
      return;
    }
    const v = voce(id);
    inviaPlaylist(stato.playlist.filter((x) => x !== id));
    if (v) mostraAvviso('Tolto dalla playlist: ' + v.titolo);
  }

  function aggiungiAllaPlaylist(id) {
    if (stato.playlist.includes(id)) return;
    const v = voce(id);
    inviaPlaylist(stato.playlist.concat([id]));
    if (v) mostraAvviso('Aggiunto alla playlist: ' + v.titolo);
  }

  function sposta(da, a) {
    if (!da || da === a) return;
    const pl = stato.playlist.slice();
    const i = pl.indexOf(da);
    const j = pl.indexOf(a);
    if (i < 0 || j < 0) return;
    pl.splice(i, 1);
    pl.splice(pl.indexOf(a) + (i < j ? 1 : 0), 0, da);
    inviaPlaylist(pl);
  }

  function spostaDi(id, passo) {
    const pl = stato.playlist.slice();
    const i = pl.indexOf(id);
    const j = i + passo;
    if (i < 0 || j < 0 || j >= pl.length) return;
    [pl[i], pl[j]] = [pl[j], pl[i]];
    inviaPlaylist(pl);
    const memo = schedePlaylist.get(id);
    if (memo) memo.nodo.focus();
  }

  function creaSchedaPlaylist(id) {
    const numero = el('span');
    const segnale = el('span');
    const testa = el('div', { class: 'voce-testa' }, el('span', { class: 'numero' }, icona('maniglia', 14), numero), segnale);
    const anteprima = el('button', { type: 'button', class: 'anteprima-bottone', onclick: () => ponte.invia({ tipo: 'mostra', id }) });
    const titolo = el('span', { class: 'voce-titolo' });
    const meta = el('span', { class: 'voce-meta' });
    const barra = el('div', { class: 'barra sottile' }, el('div', { class: 'avanzamento-voce' }));
    const nodo = el('div', { class: 'voce-playlist', draggable: 'true', tabindex: '0', 'data-id': id },
      testa, anteprima, el('div', { class: 'voce-testi' }, titolo, meta), barra);

    nodo.addEventListener('dragstart', (e) => {
      trascinato = id;
      nodo.classList.add('trascinata');
      e.dataTransfer.effectAllowed = 'move';
      try { e.dataTransfer.setData('text/plain', id); } catch (err) { /* ignora */ }
    });
    nodo.addEventListener('dragend', () => { trascinato = null; nodo.classList.remove('trascinata'); });
    nodo.addEventListener('dragover', (e) => { if (trascinato) e.preventDefault(); });
    nodo.addEventListener('drop', (e) => {
      if (!trascinato) return;
      e.preventDefault();
      const da = trascinato;
      trascinato = null;
      sposta(da, id);
    });
    nodo.addEventListener('keydown', (e) => {
      if (e.target !== nodo || !e.ctrlKey) return;
      if (e.key === 'ArrowLeft') { e.preventDefault(); spostaDi(id, -1); }
      if (e.key === 'ArrowRight') { e.preventDefault(); spostaDi(id, 1); }
    });

    return {
      nodo,
      aggiorna(v, i) {
        const attiva = v.id === stato.corrente;
        nodo.className = 'voce-playlist' + (attiva ? ' attiva' : '') + (v.disponibile ? '' : ' non-disponibile');
        nodo.setAttribute('aria-label', `${i + 1}. ${v.titolo}. Ctrl più freccia sinistra o destra per spostarlo`);
        numero.textContent = String(i + 1).padStart(2, '0');
        segnale.replaceChildren(attiva
          ? el('span', { class: 'badge-onda' }, 'IN ONDA')
          : el('button', {
            type: 'button', class: 'togli', title: 'Togli dalla playlist', 'aria-label': `Togli ${v.titolo} dalla playlist`,
            onclick: (e) => { e.stopPropagation(); togliDallaPlaylist(v.id); }
          }, icona('chiudi', 14)));
        anteprima.setAttribute('aria-label', `Mostra ${v.titolo} ora sul desktop`);
        anteprima.disabled = !v.disponibile;
        const m = miniatura('striscia', v);
        if (anteprima.firstElementChild !== m) anteprima.replaceChildren(m);
        titolo.textContent = v.titolo;
        meta.textContent = v.disponibile ? metaBreve(v) : 'File non trovato';
        barra.hidden = !attiva;
      }
    };
  }

  let bottoneAggiungi = null;

  function renderStriscia() {
    const voci = stato.playlist.map(voce).filter(Boolean);
    const nodi = voci.map((v, i) => {
      let memo = schedePlaylist.get(v.id);
      if (!memo) {
        memo = creaSchedaPlaylist(v.id);
        schedePlaylist.set(v.id, memo);
      }
      memo.aggiorna(v, i);
      return memo.nodo;
    });
    if (!bottoneAggiungi) {
      bottoneAggiungi = el('button', { type: 'button', class: 'aggiungi-playlist', onclick: () => apriScheda('libreria') },
        icona('piu', 22), el('span', null, 'Aggiungi alla playlist'));
    }
    nodi.push(bottoneAggiungi);
    riconcilia($('#striscia'), nodi);
  }

  // ---------- Libreria ----------
  function chiediConferma(v) {
    daEliminare = v.id;
    fuocoPrimaDellaConferma = document.activeElement;
    $('#conferma-titolo').textContent = `Eliminare ${v.titolo} dalla libreria?`;
    $('#conferma').hidden = false;
    $('#conferma-no').focus();
  }

  function chiudiConferma() {
    $('#conferma').hidden = true;
    daEliminare = null;
    // Il fuoco torna dove era prima del dialogo.
    const torna = fuocoPrimaDellaConferma && fuocoPrimaDellaConferma.isConnected ? fuocoPrimaDellaConferma : $('#btn-importa');
    fuocoPrimaDellaConferma = null;
    if (torna) torna.focus();
  }

  function creaSchedaLibreria(id) {
    const badge = el('span', { class: 'badge-sovrapposto' }, el('span', { class: 'puntino-verde' }), 'IN ONDA');
    const anteprima = el('button', { type: 'button', class: 'anteprima-bottone grande', onclick: () => ponte.invia({ tipo: 'mostra', id }) });
    const titolo = el('span', { class: 'voce-titolo' });
    const meta = el('span', { class: 'voce-meta' });
    const azioni = el('div', { class: 'azioni-scheda' });
    const nodo = el('div', { class: 'scheda-libreria' }, anteprima,
      el('div', { class: 'scheda-piede' }, el('div', { class: 'voce-testi' }, titolo, meta), azioni));
    return {
      nodo,
      aggiorna(v) {
        const inPlaylist = stato.playlist.includes(v.id);
        const inOnda = v.id === stato.corrente;
        nodo.className = 'scheda-libreria' + (v.disponibile ? '' : ' non-disponibile');
        anteprima.className = 'anteprima-bottone grande' + (inOnda ? ' in-onda' : '');
        anteprima.setAttribute('aria-label', `Mostra ${v.titolo} ora sul desktop`);
        anteprima.disabled = !v.disponibile;
        const m = miniatura('libreria', v);
        if (anteprima.firstElementChild !== m) anteprima.replaceChildren(m, badge);
        badge.hidden = !inOnda;
        titolo.textContent = v.titolo;
        meta.textContent = v.disponibile ? descrizione(v) : 'File non trovato';
        const bottoni = [];
        if (!v.aziendale) {
          bottoni.push(el('button', {
            type: 'button', class: 'tondo-piccolo pericolo', title: 'Elimina dalla libreria', 'aria-label': `Elimina ${v.titolo} dalla libreria`,
            onclick: () => chiediConferma(v)
          }, icona('cestino')));
        }
        bottoni.push(el('button', {
          type: 'button', class: 'tondo-piccolo' + (inPlaylist ? ' attivo' : ''), 'aria-pressed': String(inPlaylist),
          title: inPlaylist ? 'Togli dalla playlist' : 'Aggiungi alla playlist',
          'aria-label': inPlaylist ? `Togli ${v.titolo} dalla playlist` : `Aggiungi ${v.titolo} alla playlist`,
          onclick: () => (inPlaylist ? togliDallaPlaylist(v.id) : aggiungiAllaPlaylist(v.id))
        }, icona(inPlaylist ? 'spunta' : 'piu')));
        azioni.replaceChildren(...bottoni);
      }
    };
  }

  let zonaImport = null;

  function creaZonaImport() {
    const zona = el('div', { class: 'zona-import' }, icona('piu', 24), el('strong', null, 'Trascina qui un file'),
      el('span', null, 'Immagini, video o pagine HTML'));
    return zona;
  }

  function renderLibreria() {
    const tutte = stato.voci;
    const conteggi = {
      tutti: tutte.length,
      energy: tutte.filter((v) => v.aziendale).length,
      miei: tutte.filter((v) => !v.aziendale).length
    };
    document.querySelectorAll('[data-filtro]').forEach((b) => {
      b.setAttribute('aria-pressed', String(b.dataset.filtro === filtro));
      b.querySelector('.conteggio').textContent = conteggi[b.dataset.filtro];
    });
    $('#nota-libreria').textContent = `${tutte.length} sfondi, ${stato.playlist.length} nella playlist. Clicca un'anteprima per mostrarla subito.`;
    const visibili = tutte.filter((v) => filtro === 'tutti' || (filtro === 'miei' ? !v.aziendale : v.aziendale));
    const nodi = visibili.map((v) => {
      let memo = schedeLibreria.get(v.id);
      if (!memo) {
        memo = creaSchedaLibreria(v.id);
        schedeLibreria.set(v.id, memo);
      }
      memo.aggiorna(v);
      return memo.nodo;
    });
    if (filtro !== 'energy') {
      if (!zonaImport) zonaImport = creaZonaImport();
      nodi.push(zonaImport);
    }
    riconcilia($('#griglia'), nodi);
  }

  function contieneFile(e) {
    return e.dataTransfer && Array.from(e.dataTransfer.types || []).includes('Files');
  }

  function collegaTrascinamentoFile() {
    const vista = $('#vista-libreria');
    vista.addEventListener('dragover', (e) => {
      if (!contieneFile(e)) return;
      e.preventDefault();
      e.dataTransfer.dropEffect = 'copy';
      if (zonaImport) zonaImport.classList.add('sopra');
    });
    vista.addEventListener('dragleave', (e) => {
      if (zonaImport && !vista.contains(e.relatedTarget)) zonaImport.classList.remove('sopra');
    });
    vista.addEventListener('drop', (e) => {
      if (!contieneFile(e)) return;
      e.preventDefault();
      if (zonaImport) zonaImport.classList.remove('sopra');
      if (e.dataTransfer.files.length > 0) ponte.inviaFile({ tipo: 'importaFile' }, e.dataTransfer.files);
    });
    // Fuori dalla libreria un file trascinato non deve aprire una pagina.
    window.addEventListener('dragover', (e) => { if (contieneFile(e)) e.preventDefault(); });
    window.addEventListener('drop', (e) => { if (contieneFile(e)) e.preventDefault(); });
  }

  // ---------- Impostazioni ----------
  function renderImpostazioni() {
    const imp = stato.impostazioni;
    const n = stato.monitor.length;
    $('#titolo-monitor').textContent = n === 1 ? 'Rilevato 1 monitor' : `Rilevati ${n} monitor`;
    $('#mini-monitor').replaceChildren(...stato.monitor.map((m, i) => el('div', {
      class: m.principale ? 'principale' : '', title: m.nome, style: `margin-top: ${Math.min(20, Math.round(m.y / 4))}px`
    }, String(i + 1))));
    $('#descrizione-modo').textContent = n === 1
      ? 'Con un solo monitor lo sfondo lo riempie e basta.'
      : imp.modoMonitor === 'estendi'
        ? "Estendi: un'unica scena attraversa tutti i monitor, logo solo su quello più a sinistra."
        : 'Duplica: la stessa scena intera su ogni monitor, ognuna con il suo logo.';
    document.querySelectorAll('[data-dissolvenza]').forEach((b) =>
      b.setAttribute('aria-pressed', String(Number(b.dataset.dissolvenza) === imp.dissolvenzaSecondi)));
    $('#sw-fine-video').setAttribute('aria-checked', String(imp.fineVideo));
    $('#sw-batteria').setAttribute('aria-checked', String(imp.pausaABatteria));
    $('#sw-avvio').setAttribute('aria-checked', String(imp.avvioConWindows));
  }

  // ---------- Conto alla rovescia ----------
  function aggiornaConto() {
    if (!stato) return;
    const imp = stato.impostazioni;
    let ms = stato.restanteMs;
    if (ms !== null && stato.contoAttivo) ms = Math.max(0, ms - (performance.now() - ricevutoAlle));
    $('#tempo').textContent = ms === null ? '--:--' : orologio(ms);
    let nota = 'al prossimo cambio';
    if (!imp.rotazioneAttiva) nota = 'cambio solo a mano';
    else if (stato.inPausa) nota = 'in pausa';
    else if (stato.attendeFineVideo) nota = 'alla fine del video';
    $('#nota-tempo').textContent = nota;
    const p = ms === null || !stato.totaleMs ? 0 : Math.min(100, Math.max(0, (1 - ms / stato.totaleMs) * 100));
    $('#barra-avanzamento').style.width = p + '%';
    document.querySelectorAll('.avanzamento-voce').forEach((b) => { b.style.width = p + '%'; });
  }

  // ---------- Avvisi ----------
  function mostraAvviso(testo) {
    const avviso = $('#avviso');
    avviso.textContent = testo;
    avviso.hidden = false;
    avviso.style.animation = 'none';
    void avviso.offsetWidth;
    avviso.style.animation = '';
    clearTimeout(attesaAvviso);
    attesaAvviso = setTimeout(() => { avviso.hidden = true; }, 2600);
  }

  // ---------- Collegamenti ----------
  function collega() {
    const conStato = (azione) => () => { if (stato) azione(); };
    document.querySelectorAll('.scheda').forEach((b) => b.addEventListener('click', () => apriScheda(b.dataset.scheda)));
    $('#btn-precedente').addEventListener('click', () => ponte.invia({ tipo: 'precedente' }));
    $('#btn-successivo').addEventListener('click', () => ponte.invia({ tipo: 'successivo' }));
    $('#btn-pausa').addEventListener('click', conStato(() => ponte.invia({ tipo: stato.pausaManuale ? 'riprendi' : 'pausa' })));
    document.querySelectorAll('[data-modo]').forEach((b) => b.addEventListener('click', () => modifica({ modoMonitor: b.dataset.modo })));
    document.querySelectorAll('[data-dissolvenza]').forEach((b) => b.addEventListener('click', () => modifica({ dissolvenzaSecondi: Number(b.dataset.dissolvenza) })));
    $('#btn-casuale').addEventListener('click', conStato(() => modifica({ ordine: stato.impostazioni.ordine === 'casuale' ? 'sequenziale' : 'casuale' })));
    $('#btn-mai').addEventListener('click', conStato(() => modifica({ rotazioneAttiva: !stato.impostazioni.rotazioneAttiva })));
    const fineVideo = conStato(() => modifica({ fineVideo: !stato.impostazioni.fineVideo }));
    $('#sw-fine-video').addEventListener('click', fineVideo);
    $('#sw-batteria').addEventListener('click', conStato(() => modifica({ pausaABatteria: !stato.impostazioni.pausaABatteria })));
    $('#sw-avvio').addEventListener('click', conStato(() => modifica({ avvioConWindows: !stato.impostazioni.avvioConWindows })));
    $('#btn-log').addEventListener('click', () => ponte.invia({ tipo: 'apriLog' }));
    $('#btn-ripristina').addEventListener('click', () => ponte.invia({ tipo: 'ripristina' }));
    $('#btn-importa').addEventListener('click', () => ponte.invia({ tipo: 'importaDialogo' }));
    document.querySelectorAll('[data-filtro]').forEach((b) => b.addEventListener('click', conStato(() => { filtro = b.dataset.filtro; render(); })));
    $('#conferma-no').addEventListener('click', chiudiConferma);
    $('#conferma-si').addEventListener('click', () => {
      if (daEliminare) ponte.invia({ tipo: 'elimina', id: daEliminare });
      // La scheda della voce sta per sparire: il fuoco va su "Importa file".
      fuocoPrimaDellaConferma = $('#btn-importa');
      chiudiConferma();
    });
    $('#conferma').addEventListener('keydown', (e) => {
      if (e.key === 'Escape') chiudiConferma();
      else if (e.key === 'Tab') {
        // Il fuoco resta nel dialogo: Tab e Maiusc+Tab passano da un pulsante all'altro.
        e.preventDefault();
        ($('#conferma-no') === document.activeElement ? $('#conferma-si') : $('#conferma-no')).focus();
      }
    });
    $('#conferma').addEventListener('click', (e) => { if (e.target === e.currentTarget) chiudiConferma(); });
    collegaIntervallo();
    collegaTrascinamentoFile();
  }

  ponte.ascolta((m) => {
    if (!m || typeof m !== 'object') return;
    if (m.tipo === 'stato') {
      stato = m;
      ricevutoAlle = performance.now();
      render();
    } else if (m.tipo === 'avviso') {
      mostraAvviso(m.testo);
    }
  });

  collega();
  setInterval(aggiornaConto, 500);
  ponte.invia({ tipo: 'pronta' });
})();
