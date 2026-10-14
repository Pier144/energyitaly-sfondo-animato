/* Barra laterale della Libreria: personalizzazione di uno sfondo (spec personalizzazione 4.7).
   Riceve da app.js gli strumenti comuni e manda i comandi "personalizza", "ripristinaSfondo" e "mostra". */
window.BarraSfondo = function (d) {
  'use strict';
  const { el, icona, invia, miniatura } = d;

  const SPIEGA_MODO = {
    estendi: "Un'unica scena su tutti i monitor.",
    duplica: 'Ogni monitor mostra la sua copia completa.'
  };
  const SPIEGA_RIEMPIMENTO = {
    riempi: 'Riempie lo schermo tagliando i bordi.',
    adatta: 'Mostra tutto, con bande nere ai lati.'
  };
  // Mentre si trascina, l'anteprima si ricarica al massimo ogni ATTESA_ANTEPRIMA_MS.
  const ATTESA_ANTEPRIMA_MS = 250;
  // Le conferme di un controllo arrivate entro ATTESA_CONFERMA_MS partono come un solo "personalizza".
  const ATTESA_CONFERMA_MS = 300;

  let id = null;
  let stato = null;
  let costruitaPer = null;
  let bozza = {};
  let attesaAnteprima = null;
  let chiaveAnteprima = null;
  let osservatore = null;
  const parti = {};

  // Struttura fissa: si ridisegnano testa, controlli e piede; il riquadro dell'anteprima non lascia mai la pagina,
  // altrimenti l'iframe si ricaricherebbe a ogni conferma.
  const testa = el('div', { class: 'pannello-testa' });
  const bloccoAnteprima = el('div');
  const controlli = el('div', { class: 'pannello-controlli' });
  const piede = el('div', { class: 'pannello-piede' });
  const nodo = el('aside', { class: 'pannello-sfondo', 'aria-labelledby': 'pannello-titolo', hidden: true },
    testa, el('div', { class: 'pannello-corpo' }, bloccoAnteprima, controlli), piede);
  nodo.addEventListener('keydown', (e) => { if (e.key === 'Escape') chiudi(); });

  function voce() {
    return (stato && stato.voci.find((v) => v.id === id)) || null;
  }

  function tipoTesto(v) {
    return { animazione: 'Animazione Energy Italy', immagine: 'Immagine', video: 'Video' }[v.tipoVoce] || 'Pagina HTML';
  }

  function personalizza(modifiche) {
    invia({ tipo: 'personalizza', id, modifiche });
  }

  function valoreOpzione(v, o) {
    if (o.id in bozza) return bozza[o.id];
    const p = v.personalizzazione;
    return p && p.opzioni && o.id in p.opzioni ? p.opzioni[o.id] : o.predefinito;
  }

  function tavolozzaScelta(v) {
    return (v.personalizzazione && v.personalizzazione.tavolozza) || stato.tavolozze[0].id;
  }

  function coloriPersonalizzati(v) {
    const p = v.personalizzazione || {};
    if (p.tavolozza === 'personalizzata') return { fondo: bozza.fondo || p.fondo, accento: bozza.accento || p.accento };
    const t = stato.tavolozze.find((x) => x.id === tavolozzaScelta(v)) || stato.tavolozze[0];
    return { fondo: bozza.fondo || t.fondo, accento: bozza.accento || t.accento };
  }

  function etichetta(o, valore) {
    const testo = String(valore).replace('.', ',');
    if (!o.unita) return testo;
    return o.unita === '×' ? testo + '×' : testo + ' ' + o.unita;
  }

  // Ciò che decide se la barra va ridisegnata: lo sfondo in onda non c'è, cambia solo il piede.
  function firma(v) {
    return JSON.stringify([v.id, v.titolo, v.disponibile, v.url, v.personalizzazione, v.riempimento,
      stato.impostazioni.modoMonitor, stato.monitor.length, stato.playlist.includes(v.id)]);
  }

  // ---------- Anteprima ----------
  // Mentre si trascina un cursore o si sceglie un colore l'URL si ricostruisce qui (spec 4.7, "Invii e anteprima").
  function urlConBozza(v) {
    if (v.tipoVoce !== 'animazione' || !Object.keys(bozza).length) return v.url;
    const u = new URL(v.url, location.href);
    if (bozza.fondo || bozza.accento) {
      const c = coloriPersonalizzati(v);
      u.searchParams.set('fondo', c.fondo.slice(1));
      u.searchParams.set('accento', c.accento.slice(1));
      u.searchParams.delete('secondario');
    }
    Object.entries(bozza).forEach(([chiave, valore]) => {
      if (chiave === 'fondo' || chiave === 'accento') return;
      u.searchParams.set(chiave, typeof valore === 'boolean' ? (valore ? '1' : '0') : String(valore));
    });
    return u.toString();
  }

  function mandaPausa(f) {
    try { f.contentWindow.postMessage({ tipo: stato && stato.inPausa ? 'pausa' : 'riprendi' }, '*'); } catch (e) { /* non ancora caricata */ }
  }

  function disegnaAnteprima() {
    attesaAnteprima = null;
    const v = voce();
    const palco = parti.palco;
    if (!v || !palco) return;
    if (!v.disponibile) {
      if (chiaveAnteprima !== 'mancante') palco.replaceChildren(el('div', { class: 'segnaposto' }, 'File non trovato'));
      chiaveAnteprima = 'mancante';
      return;
    }
    if (v.tipoVoce === 'animazione') {
      const url = urlConBozza(v);
      if (url === chiaveAnteprima) return;
      chiaveAnteprima = url;
      const f = el('iframe', { class: 'miniatura-iframe', title: 'Anteprima', tabindex: '-1', 'aria-hidden': 'true' });
      f.style.width = '1920px';
      f.style.height = '1080px';
      f.style.transform = `scale(${palco.clientWidth / 1920})`;
      f.addEventListener('load', () => mandaPausa(f));
      f.src = url;
      palco.replaceChildren(f);
    } else if (v.tipoVoce === 'immagine' || v.tipoVoce === 'video') {
      const chiave = v.url + '|' + v.riempimento;
      if (chiave === chiaveAnteprima) return;
      chiaveAnteprima = chiave;
      const media = v.tipoVoce === 'immagine'
        ? el('img', { src: v.url, alt: '' })
        : el('video', { src: v.url, autoplay: true, loop: true, muted: true, playsinline: true });
      if (media.tagName === 'VIDEO') media.muted = true;
      if (v.riempimento === 'adatta') media.className = 'adatta';
      palco.replaceChildren(media);
    } else if (chiaveAnteprima !== v.url) {
      chiaveAnteprima = v.url;
      palco.replaceChildren(miniatura('pannello', v));
    }
  }

  // subito: ridisegna adesso; altrimenti al massimo una volta ogni ATTESA_ANTEPRIMA_MS, senza rimandare all'infinito.
  function aggiornaAnteprima(subito) {
    if (subito) {
      clearTimeout(attesaAnteprima);
      disegnaAnteprima();
    } else if (attesaAnteprima === null) {
      attesaAnteprima = setTimeout(disegnaAnteprima, ATTESA_ANTEPRIMA_MS);
    }
  }

  // ---------- Controlli ----------
  function pillole(etichettaGruppo, valori, scelto, alClic, disattivo) {
    return el('div', { class: 'segmentato', role: 'group', 'aria-label': etichettaGruppo },
      valori.map(([valore, testo]) => el('button', {
        type: 'button', class: 'pillola', 'aria-pressed': String(valore === scelto), disabled: disattivo,
        onclick: () => alClic(valore)
      }, testo)));
  }

  function gruppo(titolo, ...contenuto) {
    return el('section', { class: 'gruppo-pannello' }, el('h4', null, titolo), ...contenuto);
  }

  // Un controllo che si trascina: la bozza guida l'anteprima, la conferma manda il comando,
  // e se si esce senza confermare (rilascio sul valore di partenza) la bozza si butta.
  // Le conferme ravvicinate (frecce della tastiera tenute premute) partono come un solo comando.
  function collegaTrascinabile(campo, chiave, alCambio, conferma) {
    let attesaConferma = null;
    campo.addEventListener('input', () => {
      bozza[chiave] = alCambio();
      aggiornaAnteprima(false);
    });
    campo.addEventListener('change', () => {
      aggiornaAnteprima(true);
      clearTimeout(attesaConferma);
      attesaConferma = setTimeout(() => {
        delete bozza[chiave];
        conferma();
      }, ATTESA_CONFERMA_MS);
    });
    campo.addEventListener('blur', () => {
      // Dopo il click che ha tolto il fuoco: ricostruire subito sostituirebbe il pulsante cliccato.
      // Si aspetta anche l'eventuale conferma in corso: se è partita, la bozza è già vuota.
      setTimeout(() => {
        if (!(chiave in bozza)) return;
        delete bozza[chiave];
        costruitaPer = null;
        aggiorna(stato);
      }, ATTESA_CONFERMA_MS + 50);
    });
  }

  function sezioneColori(v, spento) {
    const scelta = tavolozzaScelta(v);
    const tavole = stato.tavolozze.map((t) => el('button', {
      type: 'button', class: 'tavolozza', 'aria-pressed': String(scelta === t.id), disabled: spento,
      onclick: () => personalizza({ tavolozza: t.id })
    }, el('span', { class: 'campione', style: `background: linear-gradient(135deg, ${t.fondo} 50%, ${t.accento} 50%)` }), t.titolo));
    tavole.push(el('button', {
      type: 'button', class: 'tavolozza', 'aria-pressed': String(scelta === 'personalizzata'), disabled: spento,
      onclick: () => personalizza({ tavolozza: 'personalizzata' })
    }, el('span', { class: 'campione arcobaleno' }), 'Personalizzata'));
    const contenuto = [el('div', { class: 'tavolozze' }, tavole)];
    if (scelta === 'personalizzata') {
      const c = coloriPersonalizzati(v);
      contenuto.push(el('div', { class: 'colori-liberi' },
        ['fondo', 'accento'].map((chiave) => {
          const nome = chiave === 'fondo' ? 'Fondo' : 'Accento';
          const campo = el('input', { type: 'color', value: c[chiave], disabled: spento, 'aria-label': 'Colore: ' + nome });
          const codice = el('b', null, c[chiave].toUpperCase());
          collegaTrascinabile(campo, chiave, () => {
            codice.textContent = campo.value.toUpperCase();
            return campo.value;
          }, () => personalizza({ [chiave]: campo.value }));
          return el('label', { class: 'colore' }, campo, el('span', null, nome, codice));
        })));
    }
    return gruppo('COLORI', ...contenuto);
  }

  function controlloOpzione(v, o, spento) {
    const valore = valoreOpzione(v, o);
    if (o.tipo === 'numero') {
      const uscita = el('output', null, etichetta(o, valore));
      const cursore = el('input', { type: 'range', min: o.min, max: o.max, step: o.passo, value: valore, disabled: spento, 'aria-label': o.titolo });
      collegaTrascinabile(cursore, o.id, () => {
        uscita.textContent = etichetta(o, cursore.value);
        return Number(cursore.value);
      }, () => personalizza({ opzioni: { [o.id]: Number(cursore.value) } }));
      return el('div', { class: 'opzione' }, el('div', { class: 'opzione-testa' }, el('span', null, o.titolo), uscita), cursore);
    }
    if (o.tipo === 'scelta') {
      return el('div', { class: 'opzione' }, el('div', { class: 'opzione-testa' }, el('span', null, o.titolo)),
        pillole(o.titolo, o.valori.map((x) => [x.valore, x.titolo]), valore, (x) => personalizza({ opzioni: { [o.id]: x } }), spento));
    }
    return el('div', { class: 'opzione opzione-riga' }, el('span', null, o.titolo),
      el('button', {
        type: 'button', role: 'switch', class: 'interruttore', 'aria-checked': String(valore), 'aria-label': o.titolo, disabled: spento,
        onclick: () => personalizza({ opzioni: { [o.id]: !valore } })
      }, el('span')));
  }

  function sezioneModo(v, spento) {
    const p = v.personalizzazione || {};
    const scelto = p.modoMonitor || 'generale';
    const generale = stato.impostazioni.modoMonitor === 'duplica' ? 'Duplica' : 'Estendi';
    let testo = scelto === 'generale' ? `Segue la scelta generale (ora: ${generale}).` : SPIEGA_MODO[scelto];
    if (stato.monitor.length < 2) testo += ' Vale quando colleghi altri monitor.';
    return gruppo('CON PIÙ MONITOR',
      pillole('Con più monitor', [['generale', 'Come in Impostazioni'], ['estendi', 'Estendi'], ['duplica', 'Duplica']], scelto,
        (x) => personalizza({ modoMonitor: x }), spento),
      el('p', { class: 'spiega' }, testo));
  }

  function aggiornaPiede(v) {
    if (parti.ripristina) parti.ripristina.disabled = !v.personalizzazione;
    if (parti.mostra) parti.mostra.disabled = !v.disponibile || stato.corrente === v.id;
  }

  function costruisci(v) {
    costruitaPer = firma(v);
    const spento = !v.disponibile;
    // Il riquadro dell'anteprima resta lo stesso finché la barra è aperta su questo sfondo.
    if (!parti.palco) {
      parti.palco = el('div', { class: 'pannello-palco' });
      chiaveAnteprima = null;
      // Un solo osservatore per l'anteprima: l'iframe si riduce alla larghezza del riquadro.
      osservatore = new ResizeObserver(() => {
        const f = parti.palco && parti.palco.querySelector('iframe');
        if (f) f.style.transform = `scale(${parti.palco.clientWidth / 1920})`;
      });
      osservatore.observe(parti.palco);
      bloccoAnteprima.replaceChildren(parti.palco, el('p', { class: 'mini nota-anteprima' }, 'ANTEPRIMA · le modifiche si salvano da sole'));
    }

    const corpo = [];
    if (v.tipoVoce === 'animazione') {
      corpo.push(sezioneColori(v, spento));
      if (v.opzioni.length) corpo.push(gruppo('FUNZIONAMENTO', v.opzioni.map((o) => controlloOpzione(v, o, spento))));
    }
    if (v.tipoVoce === 'immagine' || v.tipoVoce === 'video') {
      corpo.push(gruppo('RIEMPIMENTO',
        pillole('Riempimento', [['riempi', 'Riempi'], ['adatta', 'Adatta']], v.riempimento, (x) => personalizza({ riempimento: x }), spento),
        el('p', { class: 'spiega' }, SPIEGA_RIEMPIMENTO[v.riempimento])));
    }
    corpo.push(sezioneModo(v, spento));

    parti.chiudi = el('button', { type: 'button', class: 'tondo-piccolo', 'aria-label': 'Chiudi', onclick: () => chiudi() }, icona('chiudi'));
    parti.ripristina = el('button', { type: 'button', class: 'bottone-contorno', onclick: () => invia({ tipo: 'ripristinaSfondo', id }) },
      'Ripristina questo sfondo');
    parti.mostra = el('button', { type: 'button', class: 'bottone-verde', onclick: () => invia({ tipo: 'mostra', id }) }, 'Mostra ora');
    const inPlaylist = stato.playlist.includes(v.id);
    testa.replaceChildren(
      el('div', null,
        el('h3', { id: 'pannello-titolo' }, v.titolo),
        el('p', { class: 'tenue' }, tipoTesto(v) + (inPlaylist ? ' · in playlist' : ''))),
      parti.chiudi);
    controlli.replaceChildren(...corpo);
    piede.replaceChildren(parti.ripristina, parti.mostra);
    aggiornaPiede(v);
    aggiornaAnteprima(true);
  }

  function chiaveFuoco(x) {
    return x.getAttribute('aria-label') || x.textContent;
  }

  // ---------- Interfaccia per app.js ----------
  function apri(nuovoId) {
    if (id !== nuovoId) liberaAnteprima();
    id = nuovoId;
    bozza = {};
    nodo.hidden = false;
    costruitaPer = null;
    aggiorna(stato);
    if (parti.chiudi) parti.chiudi.focus();
  }

  function chiudi() {
    if (id === null) return;
    const chiuso = id;
    id = null;
    nodo.hidden = true;
    [testa, bloccoAnteprima, controlli, piede].forEach((x) => x.replaceChildren());
    liberaAnteprima();
    if (d.alChiudere) d.alChiudere(chiuso);
  }

  function liberaAnteprima() {
    clearTimeout(attesaAnteprima);
    attesaAnteprima = null;
    if (osservatore) osservatore.disconnect();
    osservatore = null;
    parti.palco = null;
    chiaveAnteprima = null;
  }

  function aggiorna(nuovoStato) {
    stato = nuovoStato;
    if (id === null || !stato) return;
    const v = voce();
    if (!v) { chiudi(); return; }
    const anteprima = parti.palco && parti.palco.querySelector('iframe');
    if (anteprima) mandaPausa(anteprima);
    if (firma(v) === costruitaPer) {
      aggiornaPiede(v);
      return;
    }
    // Non ricostruire sotto il dito: mentre si trascina si aspetta la conferma (o il blur).
    const attivo = document.activeElement;
    const dentro = attivo && nodo.contains(attivo);
    if (dentro && attivo.tagName === 'INPUT' && Object.keys(bozza).length) return;
    bozza = {};
    const fuoco = dentro ? chiaveFuoco(attivo) : null;
    costruisci(v);
    if (fuoco !== null) {
      const torna = Array.from(nodo.querySelectorAll('button, input')).find((x) => !x.disabled && chiaveFuoco(x) === fuoco);
      (torna || parti.chiudi).focus();
    }
  }

  return { nodo, apri, chiudi, aggiorna, idAperto: () => id };
};
