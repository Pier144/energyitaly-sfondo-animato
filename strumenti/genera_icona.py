"""Crea src/EnergyItaly.SfondoAnimato/risorse/icona.ico dal simbolo del logo Energy Italy.

Il simbolo (cerchio con la E) occupa il riquadro 0..205.47 del viewBox del logo (larghezza 764.29).
Edge in modalità headless lo ritaglia su un quadrato blu del marchio; Pillow crea le dimensioni standard.
Uso: python strumenti/genera_icona.py
"""
import pathlib
import subprocess
import sys
import tempfile

from PIL import Image

RADICE = pathlib.Path(__file__).resolve().parent.parent
LOGO = RADICE / 'animazioni' / 'img' / 'brand' / 'LogoEnergyItalyWhite.svg'
DESTINAZIONE = RADICE / 'src' / 'EnergyItaly.SfondoAnimato' / 'risorse' / 'icona.ico'
EDGE = [
    pathlib.Path(r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'),
    pathlib.Path(r'C:\Program Files\Microsoft\Edge\Application\msedge.exe'),
]
LATO = 256
LATO_SIMBOLO_SVG = 205.47
LARGHEZZA_SVG = 764.29
SIMBOLO_PX = 196


def main():
    edge = next((e for e in EDGE if e.exists()), None)
    if edge is None:
        sys.exit('Microsoft Edge non trovato')
    larghezza_logo = SIMBOLO_PX * LARGHEZZA_SVG / LATO_SIMBOLO_SVG
    margine = (LATO - SIMBOLO_PX) / 2
    with tempfile.TemporaryDirectory() as cartella_temp:
        cartella = pathlib.Path(cartella_temp)
        pagina = cartella / 'icona.html'
        pagina.write_text(f'''<!doctype html><html><head><meta charset="utf-8"><style>
html, body {{ margin: 0; width: {LATO}px; height: {LATO}px; background: transparent; overflow: hidden; }}
.fondo {{ position: absolute; left: 0; top: 0; width: {LATO}px; height: {LATO}px; border-radius: 56px; background: #1E5B8E; }}
.ritaglio {{ position: absolute; left: {margine}px; top: {margine}px; width: {SIMBOLO_PX}px; height: {SIMBOLO_PX}px; overflow: hidden; }}
img {{ position: absolute; left: 0; top: 0; width: {larghezza_logo}px; height: {SIMBOLO_PX}px; }}
</style></head><body><div class="fondo"></div><div class="ritaglio"><img src="{LOGO.as_uri()}" alt=""></div></body></html>''', encoding='utf-8')
        png = cartella / 'icona.png'
        subprocess.run([
            str(edge), '--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run',
            f'--user-data-dir={cartella / "profilo"}', '--default-background-color=00000000',
            f'--window-size={LATO},{LATO}', f'--screenshot={png}', pagina.as_uri()
        ], check=True, timeout=90)
        immagine = Image.open(png).convert('RGBA')
        DESTINAZIONE.parent.mkdir(parents=True, exist_ok=True)
        immagine.save(DESTINAZIONE, format='ICO', sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    print('Creata', DESTINAZIONE)


if __name__ == '__main__':
    main()
