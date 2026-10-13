"""Server di sviluppo: serve la cartella web/ del programma e animazioni/ del repository sullo stesso host.

Uso: python strumenti/server_sviluppo.py [porta]   (predefinita 5599)
Pagine utili: /sviluppo/prova-player.html e /sviluppo/prova-app.html
"""
import http.server
import pathlib
import sys
import urllib.parse

RADICE = pathlib.Path(__file__).resolve().parent.parent
WEB = RADICE / 'src' / 'EnergyItaly.SfondoAnimato' / 'web'
ANIMAZIONI = RADICE / 'animazioni'


class Gestore(http.server.SimpleHTTPRequestHandler):
    def translate_path(self, path):
        percorso = urllib.parse.unquote(path.split('?', 1)[0].split('#', 1)[0])
        if percorso.startswith('/animazioni/'):
            base, resto = ANIMAZIONI, percorso[len('/animazioni/'):]
        else:
            base, resto = WEB, percorso.lstrip('/')
        destinazione = (base / resto).resolve()
        if destinazione != base and base not in destinazione.parents:
            return str(base / '__fuori_dalla_cartella__')
        return str(destinazione)

    def end_headers(self):
        self.send_header('Cache-Control', 'no-store')
        super().end_headers()


if __name__ == '__main__':
    porta = int(sys.argv[1]) if len(sys.argv) > 1 else 5599
    print(f'Server di sviluppo su http://127.0.0.1:{porta}/')
    http.server.ThreadingHTTPServer(('127.0.0.1', porta), Gestore).serve_forever()
