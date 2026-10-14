# Spec - Aggiornamento automatico

- Data: 2026-10-14
- Riguarda: pubblicazione delle versioni (GitHub Actions), controllo e installazione degli aggiornamenti nel programma, righe "Aggiornamenti automatici" e "Versione" in Impostazioni.
- Scelte dell'utente: repository **reso pubblico**, release nello stesso repository; aggiornamento **solo quando si crea una versione** (tag); installazione **silenziosa**; approccio 1 (release GitHub + installer Inno Setup esistente); **opzione per spegnere** gli aggiornamenti automatici.
- Revisione: la prima stesura è stata rivista; questa versione corregge installazioni fallite, avvio manuale, copia di sviluppo, disconnessione, modello di fiducia e workflow.

## 1. Obiettivo

Quando si pubblica una nuova versione del programma, i PC su cui è installato la scaricano e la installano da soli, mentre l'utente non sta guardando il desktop, senza che nessuno debba intervenire. Chi preferisce decidere da sé spegne l'opzione e installa con un clic.

## 2. Decisioni

| # | Decisione | Motivo |
|---|---|---|
| A1 | Una versione nasce da un tag `vX.Y.Z` su un commit di `main`. GitHub Actions prova, compila, crea l'installer e pubblica la release con due file: l'installer e `versione.json`. | I commit di lavoro non arrivano ai PC. Il numero di versione resta quello di `Directory.Build.props`. |
| A2 | I PC leggono `https://github.com/Pier144/energyitaly-sfondo-animato/releases/latest/download/versione.json` (non l'API). | L'API anonima ammette 60 richieste l'ora per indirizzo IP, e in azienda molti PC escono dallo stesso indirizzo. Anche il sito ha limiti, ma molto più larghi; in più i controlli si distribuiscono nel tempo (A10). |
| A3 | L'installazione usa l'installer Inno Setup esistente in modo silenzioso. Il programma lo avvia e si chiude; l'installer lo fa ripartire alla fine, **anche se l'installazione fallisce** (sezione 4.5). | Nessun meccanismo nuovo per sostituire file in uso; il programma non resta mai spento per colpa di un aggiornamento. |
| A4 | Durante la sessione si installa solo quando l'utente non vede il desktop - sfondo in pausa per desktop coperto, app a schermo intero, sessione bloccata o monitor spenti - con la finestra principale non visibile (chiusa o ridotta a icona), e solo se la condizione dura da **60 secondi** e non è in corso una disconnessione o uno spegnimento. | Lo sfondo sparisce per pochi secondi mentre nessuno lo guarda. I 60 secondi evitano di partire nel mezzo di una disconnessione, che passa anch'essa per "sessione non attiva". |
| A5 | Un aggiornamento scaricato e non installato durante la sessione si installa al successivo avvio **automatico** del programma (`--avvio`, cioè all'accesso a Windows), prima di mostrare lo sfondo. Avviato a mano dal menu Start, il programma non installa: apre la finestra come sempre. | All'accesso lo sfondo non è ancora comparso; chi apre il programma a mano si aspetta la finestra. |
| A6 | Si installa solo una versione **più alta** di quella in uso, solo se l'impronta SHA-256 dell'installer coincide con quella di `versione.json`, e solo se il download parte da `https://github.com/Pier144/energyitaly-sfondo-animato/releases/` e finisce su un host GitHub previsto, sempre in HTTPS. | Nessun ritorno indietro, nessun file troncato o corrotto, nessun download da altri indirizzi. Cosa questo **non** protegge: vedi sezione 6. |
| A7 | Si aggiorna solo la **copia installata** (cartella indicata da `InstallLocation` nella chiave di disinstallazione dell'installer) e solo nella build Release. Una copia di sviluppo legge `versione.json` e dice se c'è una versione nuova, ma non scarica, non installa e non tocca i file di stato della copia installata. | Sullo stesso PC possono esserci le due copie. |
| A8 | La logica (versioni, `versione.json`, `stato.json`, quando controllare, quando installare) sta nel Core come funzioni pure; rete, file e processi nell'app. | Testabile senza rete, come il resto del Core. |
| A9 | Nuova impostazione **"Aggiornamenti automatici"** (predefinita: attiva; anche in `impostazioni-predefinite.json`, quindi l'IT sceglie il valore predefinito). Spenta: nessun controllo né installazione automatici; "Controlla ora" funziona sempre e, se c'è una versione nuova, la scarica e mostra "Installa ora", che installa subito e riapre la finestra dopo l'aggiornamento. | Richiesta dell'utente. |
| A10 | Primo controllo tra 2 e 30 minuti dopo l'avvio (ritardo casuale scelto all'avvio), poi ogni 6 ore. Il confronto con l'orologio si fa a ogni secondo nel timer che il programma ha già, non con un timer lungo. | Gli accessi del mattino non producono un picco di richieste dallo stesso indirizzo IP; dopo una sospensione del PC il controllo non slitta. |
| A11 | Se l'installazione della stessa versione non va a buon fine 2 volte (il programma riparte con la versione vecchia), non si riprova più da soli: la finestra mostra l'errore e resta "Installa ora". Una versione più nuova azzera il conto. | Niente tentativi all'infinito a ogni accesso. |
| A12 | La versione 1.1.0, la prima con l'aggiornamento automatico, va installata a mano una volta (o dall'IT): le copie 1.0.0 non sanno aggiornarsi. | Limite inevitabile della prima volta. |

## 3. Pubblicazione di una versione

### 3.1 Flusso

1. `powershell -File strumenti\nuova-versione.ps1 X.Y.Z`. Lo script:
   - controlla che il numero sia `X.Y.Z` e più alto di quello attuale, di essere su `main` allineato a `origin/main`, che non ci siano modifiche non salvate e che il tag non esista;
   - porta `<Version>` di `Directory.Build.props` a `X.Y.Z`, fa il commit "Versione X.Y.Z", crea il tag annotato `vX.Y.Z`;
   - invia `main` e il tag insieme (`git push --atomic origin main vX.Y.Z`).
2. Il workflow `.github/workflows/rilascio.yml` parte sul tag.

### 3.2 Workflow `rilascio.yml`

- Evento: `push` di tag `v*.*.*`. Permessi: `contents: write`. Macchina: `windows-latest`. Shell: `pwsh`.
- Passi:
  1. `actions/checkout@v4` con `fetch-depth: 0`; `actions/setup-dotnet@v4` con `global-json-file: global.json`;
  2. controlli, con messaggio chiaro se falliscono: il tag è `vX.Y.Z`; `X.Y.Z` è uguale a `<Version>` di `Directory.Build.props`; il commit del tag è su `origin/main` (`git merge-base --is-ancestor`);
  3. `dotnet test tests/EnergyItaly.SfondoAnimato.Core.Tests -c Release`;
  4. `dotnet publish` come nel README (Release, win-x64, self-contained, file singolo) in `publish`;
  5. Inno Setup: si usa `ISCC.exe` in `C:\Program Files (x86)\Inno Setup 6\` se c'è (non è nel PATH); altrimenti `choco install innosetup --version=6.4.3 -y --no-progress` (versione fissata) e si ricerca `ISCC.exe` nelle cartelle `Inno Setup*` di `Program Files (x86)`; poi `ISCC.exe /DVersione=X.Y.Z installer\SfondoAnimato.iss`;
  6. impronta SHA-256 dell'installer e scrittura di `versione.json` (UTF-8 senza BOM);
  7. `gh release create vX.Y.Z <installer> versione.json --title "Energy Italy Sfondo Animato X.Y.Z" --generate-notes`, con `env: GH_TOKEN: ${{ github.token }}` sul passo.
- `gh release create` con allegati pubblica la release solo dopo il caricamento dei file: non c'è un momento in cui "latest" esiste senza `versione.json`.

### 3.3 Installer (`installer/SfondoAnimato.iss`)

- `#define Versione "1.0.0"` diventa `#ifndef Versione` / `#define Versione "1.0.0"` / `#endif`: la versione arriva da `/DVersione=`.
- La riga `[Run]` silenziosa diventa `Parameters: "{code:ParametriRiavvio}"`: restituisce `--avvio`, oppure stringa vuota se l'installer è stato lanciato con `/APRI=1` (il programma riparte con la finestra aperta, usato da "Installa ora").
- `[Code]`: `CurStepChanged(ssPostInstall)` segna l'installazione come riuscita; `DeinitializeSetup`, solo in modalità silenziosa e solo se l'installazione **non** è riuscita, rilancia `{app}\SfondoAnimato.exe` con gli stessi argomenti di `ParametriRiavvio` più `--dopo-aggiornamento` (dentro `try/except`: se `{app}` non è ancora noto non fa nulla). Così un aggiornamento fallito lascia il programma acceso con la versione vecchia (con la finestra aperta se era "Installa ora").
- `--dopo-aggiornamento` dice al programma di non ritentare subito l'installazione (il tentativo successivo avviene al prossimo accesso o al prossimo momento buono) e di aspettare fino a 10 secondi il mutex dell'istanza unica, se la copia vecchia non è ancora uscita.

### 3.4 `versione.json`

```json
{ "versione": "1.1.0", "file": "EnergyItaly-SfondoAnimato-1.1.0-setup.exe", "sha256": "<64 cifre esadecimali>" }
```

Regole di validità (Core): `versione` è `X.Y.Z` con numeri di al massimo 5 cifre; `file` contiene solo lettere, cifre, `.`, `-`, `_`, comincia con una lettera o una cifra, finisce con `.exe` e non contiene percorsi; `sha256` è di 64 cifre esadecimali (si confronta in minuscolo). Un file non valido equivale a un controllo non riuscito.

L'installer si scarica da `https://github.com/Pier144/energyitaly-sfondo-animato/releases/download/v{versione}/{file}`.

## 4. Nel programma

### 4.1 Core: `Aggiornamenti.cs`

```csharp
public sealed record InfoVersione(Version Versione, string File, string Sha256);
public sealed record StatoAggiornamenti(DateTimeOffset? UltimoControllo, InfoVersione? Pronta, int Tentativi);

public static class Aggiornamenti
{
    public const string Repository = "Pier144/energyitaly-sfondo-animato";
    public const int TentativiMassimi = 2;
    public static readonly Uri IndirizzoInfo;
    public static Uri IndirizzoInstaller(InfoVersione info);
    public static InfoVersione? Leggi(string? json);                 // versione.json
    public static Version? VersioneDa(string? testo);                // "1.0.0", "v1.0.0", "1.0.0+commit"
    public static bool PiuNuova(InfoVersione? info, Version inUso);
    public static bool IndirizzoAffidabile(Uri indirizzo);           // A6
    public static TimeSpan RitardoPrimoControllo(Random caso);       // tra 2 e 30 minuti
    public static DateTimeOffset ProssimoControllo(DateTimeOffset primoPossibile, DateTimeOffset? ultimoControllo);
    public static bool MomentoBuono(SegnaliSistema segnali, bool finestraVisibile);
    public static StatoAggiornamenti LeggiStato(string? json);
    public static string ScriviStato(StatoAggiornamenti s);
    public static string ScriviInfo(InfoVersione info);
    public static string MessaggioVista(...);                        // sezione 4.6
}
```

- `IndirizzoAffidabile`: schema `https`; host `github.com` con percorso che comincia con `/Pier144/energyitaly-sfondo-animato/releases/` (maiuscole indifferenti), oppure host `objects.githubusercontent.com` o `release-assets.githubusercontent.com` (dove GitHub reindirizza i file delle release). Il primo indirizzo è sempre quello del repository: il secondo è affidabile perché ci si arriva da lì.
- `ProssimoControllo`: `ultimoControllo` null → `primoPossibile`; altrimenti il più tardi tra `primoPossibile` e `ultimoControllo + 6 h`. `primoPossibile` = avvio + `RitardoPrimoControllo`.
- `MomentoBuono`: `!finestraVisibile && (DesktopCoperto || SchermoIntero || SessioneNonAttiva || MonitorSpenti)`. Batteria e pausa manuale non contano. La durata di 60 secondi e la chiusura di sessione le gestisce l'app.

### 4.2 App: `Aggiornatore.cs`

- Stato su disco in `%LOCALAPPDATA%\EnergyItaly\SfondoAnimato\aggiornamenti\`: l'installer scaricato, `stato.json` (scritto in modo atomico: file temporaneo poi spostamento) e `installazione.log` (registro dell'installer). Solo la copia installata scrive qui (A7).
- `HttpClient` con il proxy di sistema e le credenziali dell'utente (`DefaultProxyCredentials = CredentialCache.DefaultCredentials`), `User-Agent: EnergyItaly-SfondoAnimato/<versione>`.
- **Avvio** (`AppSfondo`, prima di creare lo sfondo):
  1. pulizia (solo copia installata, anche con gli automatici spenti): file `*.part` rimasti; se `stato.json` ha un aggiornamento pronto di versione **minore o uguale** a quella in uso, si cancellano installer e voce (se uguale si registra `Aggiornamento: installata X.Y.Z`); se la versione pronta è più alta e `Tentativi > 0`, l'installazione precedente non è andata: si registra `Aggiornamento: installazione della X.Y.Z non riuscita (tentativo N)`;
  2. se l'avvio è automatico (`--avvio` senza `--dopo-aggiornamento`), gli automatici sono attivi, c'è un aggiornamento pronto più alto, `Tentativi < 2`, il file c'è e l'impronta coincide → installazione (4.4) e il programma esce senza mostrare sfondo né finestra.
- **Controllo**: ogni secondo `Tick()` confronta l'ora con `ProssimoControllo` (solo con gli automatici attivi); "Controlla ora" lo fa partire subito. Un solo controllo alla volta.
  1. `GET IndirizzoInfo` (30 s), indirizzo finale affidabile, `Leggi`;
  2. non più alta → "aggiornato" (copia installata: si dimentica un eventuale pronto superato);
  3. più alta e copia di sviluppo → si ricorda solo in memoria la versione disponibile;
  4. più alta e già scaricata (stessa `InfoVersione`, file presente) → nulla;
  5. altrimenti download in `.part` (10 min, massimo 500 MB, indirizzo finale affidabile), impronta, spostamento nel nome finale, `stato.json` con `Pronta` = nuova e `Tentativi` = 0; un installer pronto più vecchio si cancella;
  6. copia installata: `UltimoControllo` = adesso in ogni caso (anche se non riesce, così non si riprova a raffica).
- **Durante la sessione** (copia installata, automatici attivi, aggiornamento pronto, `Tentativi < 2`, nessun controllo in corso): a ogni cambio dei segnali o della finestra si ricalcola `MomentoBuono`; quando diventa vero parte un'attesa di 60 secondi, annullata se nel frattempo torna falso; allo scadere, se è ancora vero e non è in corso una chiusura di sessione (`SystemEvents.SessionEnding` o `WM_QUERYENDSESSION` già ricevuti), si installa. La chiusura di sessione resta segnata fino all'uscita del programma: se lo spegnimento viene annullato, durante quella sessione non si installa più (si installa al prossimo accesso).
- **"Installa ora"** (copia installata, aggiornamento pronto, nessun controllo in corso): installa subito con `/APRI=1`, anche con gli automatici spenti e anche se `Tentativi` ha raggiunto il massimo.

### 4.3 App: collegamenti

- `AppSfondo` crea l'`Aggiornatore` prima del `Coordinatore` (che all'avvio chiama `ISistema.ImpostaAggiornamentiAutomatici`).
- L'installazione all'avvio avviene nel costruttore di `AppSfondo` subito dopo `coord.Avvia(...)`: se parte, il costruttore ritorna senza creare sfondo, finestra e timer, e mette in coda (`SynchronizationContext.Post`) l'uscita, che verrà eseguita appena `Application.Run` avvia il ciclo dei messaggi. La procedura di uscita normale è sicura in quel caso (gli oggetti non creati sono `null`; `attesaApri` diventa annullabile).
- Durante la sessione, a installazione avviata, l'uscita passa da `Post` alla procedura normale.

### 4.4 Installazione

1. Si controlla di nuovo l'impronta del file; `Tentativi` + 1 e si salva `stato.json`.
2. `Process.Start(installer, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /LOG=\"<cartella>\\installazione.log\"" + (riapri ? " /APRI=1" : ""))`, `UseShellExecute = false`.
3. Si registra `Aggiornamento: avvio installazione X.Y.Z` e il programma esce. Da qui il programma non avvia altre installazioni (indicatore interno).
4. L'installer (`CloseApplications=force`) chiude il programma se non è ancora uscito, sostituisce i file e lo fa ripartire (3.3), anche se fallisce.
5. Se `Process.Start` fallisce: si registra l'errore, `Tentativi` resta aumentato, il programma resta acceso.

### 4.5 Impostazione "Aggiornamenti automatici" (A9)

Stesso schema di `AvvioConWindows`:
- `Impostazioni.AggiornamentiAutomatici` (bool, predefinito `true`), `ModificheImpostazioni.AggiornamentiAutomatici`, lettura e scrittura in `ArchivioImpostazioni` (chiave `aggiornamentiAutomatici`), nel messaggio `stato` e in `LeggiComandoApp`;
- `impostazioni-predefinite.json`: `"aggiornamentiAutomatici": true`. Il file sta nella cartella del programma ed è sostituito a ogni aggiornamento: i valori predefiniti aziendali si cambiano nel repository, non a mano sui PC;
- `ISistema.ImpostaAggiornamentiAutomatici(bool)`, chiamato dal `Coordinatore` all'avvio, a ogni cambio e dopo "Ripristina impostazioni aziendali"; `AppSfondo` lo passa all'`Aggiornatore`.

### 4.6 Finestra: righe in Impostazioni

Gruppo SISTEMA, prime due righe:

1. **Aggiornamenti automatici** - "Il programma si aggiorna da solo quando esce una nuova versione, mentre non stai guardando il desktop", interruttore `#sw-aggiornamenti`.
2. **Versione X.Y.Z** - sotto, lo stato:
   - "Aggiornato · ultimo controllo oggi alle 09:15" (o "ieri alle", o la data); "Nessun controllo ancora fatto" se non ce ne sono stati;
   - "Controllo in corso…";
   - "Versione X.Y.Z pronta: si installa appena il desktop è coperto" (automatici attivi) oppure "Versione X.Y.Z disponibile" (automatici spenti);
   - "Ultimo controllo non riuscito (motivo breve)";
   - "Installazione della X.Y.Z non riuscita: prova Installa ora o contatta l'IT" (A11);
   - "Copia di sviluppo: non si installa nulla", con " · disponibile la X.Y.Z" se un controllo l'ha trovata e " · ultimo controllo non riuscito (motivo)" se l'ultimo controllo è fallito;
   - priorità quando valgono più stati: controllo in corso, copia di sviluppo, installazione non riuscita, pronto, errore dell'ultimo controllo, aggiornato (chi chiede un'azione viene prima dell'errore). Un errore nell'avviare l'installer non è un errore di controllo: conta come tentativo e va nel registro;
   - a destra "Controlla ora" (disattivato durante un controllo) oppure, con un aggiornamento pronto nella copia installata e automatici spenti o installazione non riuscita, "Installa ora".

Messaggi:
- dalla pagina: `controllaAggiornamenti` e `installaAggiornamento`, intercettati in `FinestraPrincipale` come `importaDialogo`; `pronta` va al `Coordinatore` **e** all'`AppSfondo`, che risponde con il messaggio `aggiornamenti`;
- verso la pagina: `{ "tipo": "aggiornamenti", "versione": "1.1.0", "stato": "aggiornato" | "controllo" | "pronto" | "errore" | "fallito" | "sviluppo", "ultimoControllo": "<ISO 8601>" | null, "versioneNuova": "1.2.0" | null, "errore": "..." | null }`, mandato a ogni cambio e quando la pagina manda `pronta`.

## 5. Rendere pubblico il repository

Prima del cambio di visibilità si controlla che la storia non contenga dati da non pubblicare (chiavi, token, password, file di log). Il cambio lo fa l'utente dalle impostazioni di GitHub, o Claude con `gh repo edit` solo dopo una conferma esplicita. Senza repository pubblico i PC ricevono 404 e la finestra mostra "Ultimo controllo non riuscito (nessuna versione pubblicata)".

## 6. Modello di fiducia

- L'impronta SHA-256 viene dalla stessa release dell'installer: protegge da download troncati o corrotti, **non** da chi può pubblicare una release. Chi controlla l'account GitHub, può inviare un tag o altera il workflow (o un'azione o il pacchetto di Inno Setup che usa) esegue codice su tutti i PC con il programma installato. HTTPS e il controllo degli host proteggono dal traffico intercettato in rete.
- Senza firma digitale dell'installer non c'è un'altra difesa. Misure minime, da fare sul repository: autenticazione a due fattori sull'account; regola di protezione dei tag `v*` (solo il proprietario li crea); azioni del workflow fissate a una versione; versione di Inno Setup fissata.
- Una release difettosa che blocca il programma all'avvio arriva su tutti i PC e ne ferma anche gli aggiornamenti successivi: ogni versione si prova prima su un PC (copia installata) prima di pubblicarne un'altra sopra.
- Criteri aziendali (AppLocker, WDAC, antivirus) devono permettere di eseguire un installer non firmato da `%LOCALAPPDATA%\EnergyItaly\SfondoAnimato\aggiornamenti\`: da verificare con l'IT.

## 7. Fuori dall'ambito

- Firma digitale dell'installer (servirebbe un certificato di firma).
- Aggiornamenti differenziali, canali beta, ritorno a versioni precedenti.
- Blocco dell'impostazione da parte dell'IT (oggi l'IT ne sceglie il valore predefinito, ma l'utente può cambiarlo).

## 8. Verifica

- **Core (xUnit)**:
  - impostazione `AggiornamentiAutomatici` (predefinito, lettura e scrittura, valore aziendale, modifica dalla finestra, `ISistema` chiamato all'avvio, al cambio e al ripristino);
  - `VersioneDa` (con e senza `v`, con `+commit`, non valide);
  - `Leggi` (valido, campi mancanti, `file` con percorso o senza `.exe`, `sha256` corto o non esadecimale, JSON rotto);
  - `PiuNuova`, `IndirizzoAffidabile` (validi, `http`, altro host, altro repository, host simili);
  - `RitardoPrimoControllo` (sempre tra 2 e 30 minuti), `ProssimoControllo`, `MomentoBuono`;
  - `stato.json` scritto e riletto (con tentativi), illeggibile = vuoto.
- **Pagina** (browser, `prova-app.html` con messaggi simulati): tutti gli stati della riga "Versione", interruttore, pulsanti.
- **Workflow**: il primo tag reale `v1.1.0` deve finire verde, con la release che ha i due file e `versione.json` con la versione e l'impronta dell'installer pubblicato (controllo con `Get-FileHash` sul file scaricato). Prima, `nuova-versione.ps1` si prova sui casi di rifiuto.
- **Programma** (sul PC, copia installata 1.1.0 e una release 1.1.1 di prova): controllo, download, installazione dopo 60 secondi con desktop coperto, riavvio, registro; installazione al successivo accesso se non c'è stato un momento buono; avvio dal menu Start che non installa; "Installa ora" con automatici spenti che riapre la finestra; installazione fallita (ad esempio installer reso illeggibile) che lascia il programma acceso e conta i tentativi; copia di sviluppo che non scarica e non tocca `stato.json`.
