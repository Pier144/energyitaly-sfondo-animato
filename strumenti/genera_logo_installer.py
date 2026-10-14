"""Crea i bitmap del logo per il disinstallatore (spec 2026-10-14-disinstallatore-design, U5).

Inno Setup 6.4 non legge SVG né PNG: Edge in modalità headless disegna il logo bianco già sul fondo
#07111f delle schermate, Pillow salva BMP a 24 bit (da 100 a 200%) in installer/disinstallazione.
Uso: python strumenti/genera_logo_installer.py
"""
import pathlib
import subprocess
import sys
import tempfile

from PIL import Image

RADICE = pathlib.Path(__file__).resolve().parent.parent
LOGO = RADICE / 'animazioni' / 'img' / 'brand' / 'LogoEnergyItalyWhite.svg'
DESTINAZIONE = RADICE / 'installer' / 'disinstallazione'
EDGE = [
    pathlib.Path(r'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'),
    pathlib.Path(r'C:\Program Files\Microsoft\Edge\Application\msedge.exe'),
]
FONDO = '#07111f'
LARGHEZZA_SVG = 764.29
ALTEZZA_SVG = 205.47
# Larghezza del logo a 100%, come nell'intestazione del programma (.logo in web/app/app.css).
LARGHEZZA_DIP = 128
SCALE = [100, 125, 150, 175, 200]


def disegna(edge, cartella, scala):
    larghezza = round(LARGHEZZA_DIP * scala / 100)
    altezza = round(larghezza * ALTEZZA_SVG / LARGHEZZA_SVG)
    pagina = cartella / f'logo-{scala}.html'
    pagina.write_text(f'''<!doctype html><html><head><meta charset="utf-8"><style>
html, body {{ margin: 0; width: {larghezza}px; height: {altezza}px; background: {FONDO}; overflow: hidden; }}
img {{ display: block; width: {larghezza}px; height: {altezza}px; }}
</style></head><body><img src="{LOGO.as_uri()}" alt=""></body></html>''', encoding='utf-8')
    png = cartella / f'logo-{scala}.png'
    subprocess.run([
        str(edge), '--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run',
        f'--user-data-dir={cartella / "profilo"}', '--force-device-scale-factor=1',
        f'--window-size={larghezza},{altezza}', f'--screenshot={png}', pagina.as_uri()
    ], check=True, timeout=90)
    immagine = Image.open(png).convert('RGB').crop((0, 0, larghezza, altezza))
    destinazione = DESTINAZIONE / f'logo-{scala}.bmp'
    immagine.save(destinazione, format='BMP')
    print('Creato', destinazione, f'{larghezza}x{altezza}')


def main():
    edge = next((e for e in EDGE if e.exists()), None)
    if edge is None:
        sys.exit('Microsoft Edge non trovato')
    DESTINAZIONE.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as cartella_temp:
        for scala in SCALE:
            disegna(edge, pathlib.Path(cartella_temp), scala)


if __name__ == '__main__':
    main()
