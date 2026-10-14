# Spec - Aggiornamento automatico

- Data: 2026-10-14
- Riguarda: pubblicazione delle versioni (GitHub Actions), controllo e installazione degli aggiornamenti nel programma, riga "Aggiornamenti" in Impostazioni.
- Scelte dell'utente: repository **reso pubblico**, release nello stesso repository; aggiornamento **solo quando si crea una versione** (tag); installazione **silenziosa**; approccio 1 (release GitHub + installer Inno Setup esistente).

## 1. Obiettivo

Quando si pubblica una nuova versione del programma, i PC su cui è installato la scaricano e la installano da soli, senza che nessuno debba intervenire e senza che l'utente veda lo sfondo sparire mentre lavora.

## 2. Decisioni

| # | Decisione | Motivo |
|---|---|---|
| A1 | Una versione nasce da un tag `vX.Y.Z` su `main`. GitHub Actions compila, prova, crea l'installer e pubblica la release con due file: l'installer e `versione.json`. | I commit di lavoro non arrivano ai PC. Il numero di versione resta quello di `Directory.Build.props`. |
| A2 | I PC leggono `https://github.com/Pier144/energyitaly-sfondo-animato/releases/latest/download/versione.json` (non l'API). | L'API anonima ammette 60 richieste l'ora per indirizzo IP, e in azienda molti PC escono dallo stesso indirizzo. I download delle release non hanno questo limite. |
| A3 | L'installazione usa l'installer Inno Setup già esistente in modo silenzioso. Il programma lo avvia e si chiude; l'installer, in modalità silenziosa, lo fa ripartire con `--avvio` (già così oggi). | Nessun meccanismo nuovo per sostituire file in uso. |
| A4 | Si installa solo quando l'utente non vede il desktop: sfondo in pausa per desktop coperto, app a schermo intero, sessione bloccata o monitor spenti, **e** finestra principale non visibile. Un aggiornamento scaricato e non installato durante la sessione si installa al successivo avvio del programma, prima di mostrare lo sfondo. | Nessuno vede lo sfondo sparire. All'avvio (accesso a Windows) lo sfondo non è ancora comparso. |
| A5 | Si installa solo una versione **più alta** di quella in uso, solo se l'impronta SHA-256 dell'installer coincide con quella di `versione.json`, solo da `https://github.com/Pier144/energyitaly-sfondo-animato/` (anche dopo i reindirizzamenti, che devono restare in HTTPS). | Nessun ritorno a versioni vecchie, nessun file corrotto o sostituito. |
| A6 | Aggiornamento attivo solo nella copia **installata** (cartella `%LOCALAPPDATA%\Programs\Energy Italy Sfondo Animato`) e solo nella build Release. Altrove (cartella `publish`, debug) il programma controlla e mostra se c'è una versione nuova, ma non installa. | Una copia di sviluppo non deve installare nulla né lanciare un'altra copia. |
| A7 | La logica (versioni, lettura di `versione.json`, quando controllare, quando installare) sta nel Core come funzioni pure; rete, file e processi nell'app. | Testabile senza rete, come il resto del Core. |
| A8 | La versione con l'aggiornamento automatico (1.1.0) va installata a mano una volta (o dall'IT): le copie 1.0.0 non sanno aggiornarsi. | Limite inevitabile della prima volta. |
| A9 | Nuova impostazione **"Aggiornamenti automatici"** (predefinita: attiva; anche in `impostazioni-predefinite.json`, quindi l'IT può spegnerla per tutti). Spenta: nessun controllo né installazione automatici, nemmeno all'avvio; "Controlla ora" funziona sempre e, se c'è una versione nuova, la scarica e mostra "Installa ora", che installa subito. | Richiesta dell'utente. Chi spegne gli aggiornamenti decide lui quando installare. |

## 3. Pubblicazione di una versione

### 3.1 Flusso

1. Si porta `<Version>` in `Directory.Build.props` al nuovo numero (es. `1.1.0`) e si fa il commit su `main`.
2. Si crea e si invia il tag `v1.1.0`. Lo script `strumenti/nuova-versione.ps1 1.1.0` fa i punti 1 e 2 (controlla che la cartella di lavoro sia pulita, che il numero sia più alto di quello attuale e che si sia su `main`).
3. Il workflow `.github/workflows/rilascio.yml` parte sul tag.

### 3.2 Workflow `rilascio.yml`

- Evento: `push` di tag `v*.*.*`. Permessi: `contents: write`. Macchina: `windows-latest`.
- Passi:
  1. checkout; `actions/setup-dotnet` con `global-json-file: global.json`;
  2. controllo: il tag (senza `v`) deve essere uguale a `<Version>` di `Directory.Build.props`, altrimenti il workflow fallisce con un messaggio chiaro;
  3. `dotnet test tests/EnergyItaly.SfondoAnimato.Core.Tests -c Release`;
  4. `dotnet publish` come nel README (Release, win-x64, self-contained, file singolo) in `publish`;
  5. Inno Setup: se `ISCC.exe` non c'è, `choco install innosetup -y --no-progress`; poi `ISCC.exe /DVersione=X.Y.Z installer\SfondoAnimato.iss`;
  6. impronta SHA-256 dell'installer e scrittura di `versione.json`;
  7. `gh release create vX.Y.Z <installer> versione.json --title "Energy Italy Sfondo Animato X.Y.Z" --generate-notes`.
- `installer/SfondoAnimato.iss`: `#define Versione` diventa `#ifndef Versione` / `#define Versione "1.0.0"` / `#endif`, così la versione arriva dalla riga di comando.

### 3.3 `versione.json`

```json
{ "versione": "1.1.0", "file": "EnergyItaly-SfondoAnimato-1.1.0-setup.exe", "sha256": "<64 cifre esadecimali>" }
```

Regole di validità (Core): `versione` è `X.Y.Z` con numeri interi; `file` contiene solo lettere, cifre, `.`, `-`, `_`, finisce con `.exe` e non contiene percorsi; `sha256` è di 64 cifre esadecimali. Un file non valido equivale a "nessun aggiornamento" e finisce nel registro.

L'installer si scarica da `https://github.com/Pier144/energyitaly-sfondo-animato/releases/download/v{versione}/{file}`.

## 4. Nel programma

### 4.1 Core: `Aggiornamenti.cs`

```csharp
public sealed record InfoVersione(Version Versione, string File, string Sha256);

public static class Aggiornamenti
{
    public const string Repository = "Pier144/energyitaly-sfondo-animato";
    public static readonly Uri IndirizzoInfo;        // .../releases/latest/download/versione.json
    public static Uri IndirizzoInstaller(InfoVersione info);

    /// <summary>Legge versione.json; null se non valido.</summary>
    public static InfoVersione? Leggi(string json);

    /// <summary>"1.0.0", "1.0.0+abc" (ProductVersion) o "v1.0.0" → Version a tre numeri; null se non valida.</summary>
    public static Version? VersioneDa(string testo);

    /// <summary>true se l'indirizzo finale di un download è accettabile (HTTPS; github.com del repository o i suoi host di download).</summary>
    public static bool IndirizzoAffidabile(Uri indirizzo);

    /// <summary>Quando fare il prossimo controllo: 2 minuti dopo l'avvio, poi ogni 6 ore.</summary>
    public static DateTimeOffset ProssimoControllo(DateTimeOffset avvio, DateTimeOffset? ultimoControllo);

    /// <summary>Si può installare adesso (A4).</summary>
    public static bool MomentoBuono(SegnaliSistema segnali, bool finestraVisibile);
}
```

- `IndirizzoAffidabile`: schema `https` e host `github.com` con percorso che comincia con `/Pier144/energyitaly-sfondo-animato/releases/`, oppure host `objects.githubusercontent.com` o `release-assets.githubusercontent.com` (dove GitHub reindirizza i download).
- `MomentoBuono`: `!finestraVisibile && (DesktopCoperto || SchermoIntero || SessioneNonAttiva || MonitorSpenti)`.
- `ProssimoControllo`: se `ultimoControllo` è null → `avvio + 2 min`; altrimenti `max(avvio + 2 min, ultimoControllo + 6 h)`.

### 4.2 App: `Aggiornatore.cs`

Stato su disco in `%LOCALAPPDATA%\EnergyItaly\SfondoAnimato\aggiornamenti\`: l'installer scaricato e `stato.json` (`ultimoControllo`, `versione`, `file`, `sha256` dell'aggiornamento scaricato e verificato).

- **Avvio** (`AppSfondo`, prima di creare lo sfondo, solo con "Aggiornamenti automatici" attivo): se `stato.json` indica un installer scaricato di versione più alta di quella in uso, il file c'è e l'impronta coincide, e l'aggiornamento è attivo (A6) → si installa subito (4.3) e il programma esce. Se il file manca o non coincide, si cancella e si va avanti.
- **Controllo** (timer da `ProssimoControllo`, e "Controlla ora"):
  1. `GET IndirizzoInfo` con timeout 30 s, intestazione `User-Agent: EnergyItaly-SfondoAnimato/<versione>`;
  2. si verifica che l'indirizzo finale sia affidabile; `Leggi`; se la versione non è più alta → "aggiornato";
  3. se più alta: download dell'installer in un file `.part` (timeout 10 min, indirizzo finale affidabile), impronta SHA-256, se coincide → rinomina nel nome finale e aggiorna `stato.json`; altrimenti cancella e registra l'errore;
  4. `ultimoControllo` = adesso in ogni caso (anche se fallisce, così non si riprova a raffica).
- **Installazione durante la sessione**: con un aggiornamento pronto, a ogni cambio dei segnali di pausa o di visibilità della finestra si valuta `MomentoBuono`; se vero → 4.3.
- Tutto in background (`async`), mai sul thread dell'interfaccia per rete e disco; un solo controllo alla volta.
- Ogni passaggio nel registro (`Aggiornamento: ...`).

### 4.3 Installazione

1. Si controlla di nuovo l'impronta del file.
2. `Process.Start(installer, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-")` con `UseShellExecute = false`, cartella di lavoro quella dell'installer.
3. Si registra `Aggiornamento: avvio installazione X.Y.Z` e il programma esce con la stessa procedura di "Esci" (lo sfondo di Windows torna com'era).
4. L'installer (`CloseApplications=force`) aspetta che il programma sia chiuso, sostituisce i file e lo fa ripartire con `--avvio`.
5. Al riavvio, se la versione in uso è uguale a quella di `stato.json`, si cancellano installer e `stato.json` e si registra `Aggiornamento: installata X.Y.Z`.

Se `Process.Start` fallisce, si registra l'errore e il programma resta acceso; si ritenta al controllo successivo.

### 4.4 Impostazione "Aggiornamenti automatici" (A9)

Stesso schema di `AvvioConWindows`:
- `Impostazioni.AggiornamentiAutomatici` (bool, predefinito `true`), `ModificheImpostazioni.AggiornamentiAutomatici`, lettura e scrittura in `ArchivioImpostazioni` (chiave `aggiornamentiAutomatici`, mancante = valore predefinito o aziendale), nel messaggio `stato` e in `LeggiComandoApp`;
- `impostazioni-predefinite.json`: `"aggiornamentiAutomatici": true`;
- `ISistema.ImpostaAggiornamentiAutomatici(bool)`, chiamato dal `Coordinatore` all'avvio, a ogni cambio e dopo "Ripristina impostazioni aziendali", come `ImpostaAvvioConWindows`; `AppSfondo` lo passa all'`Aggiornatore`.

Con l'impostazione spenta l'`Aggiornatore` ferma il timer, non installa all'avvio né durante la sessione; "Controlla ora" controlla e scarica; con un aggiornamento pronto la riga mostra "Installa ora", che installa subito (4.3) anche con la finestra aperta. Riaccendendola, il prossimo controllo si calcola da `ProssimoControllo` e un aggiornamento già pronto torna a installarsi da solo.

### 4.5 Finestra: righe in Impostazioni

Gruppo SISTEMA, prime due righe:

1. **Aggiornamenti automatici** - testo "Il programma si aggiorna da solo quando esce una nuova versione, mentre non stai guardando il desktop" e interruttore `#sw-aggiornamenti`.
2. **Versione X.Y.Z** - sotto, lo stato:
   - "Aggiornato · ultimo controllo oggi alle 09:15" (o "ieri alle", o la data);
   - "Controllo in corso…";
   - "Versione X.Y.Z pronta: si installa appena il desktop è coperto" (automatici attivi) oppure "Versione X.Y.Z disponibile" (automatici spenti);
   - "Ultimo controllo non riuscito (motivo breve)";
   - "Copia di sviluppo: non si installa nulla" (A6);
   - a destra il pulsante "Controlla ora" (disattivato durante un controllo) oppure, con un aggiornamento pronto e automatici spenti (e non copia di sviluppo), "Installa ora".

Messaggi:
- dalla pagina: `{ "tipo": "controllaAggiornamenti" }` e `{ "tipo": "installaAggiornamento" }`, intercettati in `FinestraPrincipale` come `importaDialogo`;
- verso la pagina: `{ "tipo": "aggiornamenti", "versione": "1.1.0", "stato": "aggiornato" | "controllo" | "pronto" | "errore" | "sviluppo", "ultimoControllo": "2026-10-14T09:15:00+02:00" | null, "versioneNuova": "1.2.0" | null, "errore": "..." | null }`, mandato a ogni cambio di stato e quando la pagina manda `pronta`. L'interruttore usa `stato.impostazioni.aggiornamentiAutomatici` come gli altri.

## 5. Rendere pubblico il repository

Prima del cambio di visibilità si controlla che la storia non contenga dati da non pubblicare (chiavi, token, password, indirizzi e-mail personali oltre a quelli dei commit, file di log). Il cambio lo fa l'utente dalle impostazioni di GitHub, o Claude con `gh repo edit --visibility public` solo dopo una conferma esplicita. Senza repository pubblico i PC ricevono 404 e il programma mostra "Ultimo controllo non riuscito".

## 6. Fuori dall'ambito

- Firma digitale dell'installer (servirebbe un certificato di firma).
- Aggiornamenti differenziali, canali beta, ritorno a versioni precedenti.
- Blocco dell'impostazione da parte dell'IT (oggi l'IT ne sceglie il valore predefinito, ma l'utente può cambiarlo).

## 7. Verifica

- **Core (xUnit)**: impostazione `AggiornamentiAutomatici` (predefinito, lettura e scrittura, valore aziendale, modifica dalla finestra, `ISistema` chiamato all'avvio e al cambio); `VersioneDa` (con e senza `v`, con `+commit`, non valide), `Leggi` (valido, campi mancanti, `file` con percorso o senza `.exe`, `sha256` corto o non esadecimale, JSON rotto), confronto versioni, `IndirizzoAffidabile` (casi validi, `http`, altro host, altro repository), `ProssimoControllo`, `MomentoBuono` (tutte le combinazioni rilevanti).
- **Workflow**: si verifica con il primo tag reale `v1.1.0`: il workflow deve finire verde, la release deve avere i due file e `versione.json` deve avere la versione e l'impronta dell'installer pubblicato (controllo con `Get-FileHash` sul file scaricato). Prima, lo script `nuova-versione.ps1` si prova sui casi di rifiuto (cartella non pulita, numero non più alto, ramo diverso da `main`).
- **Programma (sul PC)**: con la 1.1.0 installata e una release 1.1.1 di prova pubblicata: controllo, download, installazione con desktop coperto, riavvio, registro; installazione al successivo avvio se non c'è stato un momento buono; "Controlla ora"; copia di sviluppo che non installa.
