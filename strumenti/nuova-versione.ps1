# Crea una nuova versione: aggiorna <Version> in Directory.Build.props, fa il commit, crea il tag vX.Y.Z
# e invia tutto. GitHub Actions (.github/workflows/rilascio.yml) pubblica poi la release e i PC si aggiornano.
#
# Uso: powershell -File strumenti\nuova-versione.ps1 1.1.0
param(
    [Parameter(Mandatory = $true)][string]$Versione
)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

function Ferma($testo) { Write-Host $testo -ForegroundColor Red; exit 1 }

if ($Versione -notmatch '^\d+\.\d+\.\d+$') { Ferma "La versione deve essere nel formato X.Y.Z (ad esempio 1.1.0)" }
$ramo = (git rev-parse --abbrev-ref HEAD).Trim()
if ($ramo -ne 'main') { Ferma "Sei sul ramo '$ramo': le versioni si creano solo da main" }
if (git status --porcelain) { Ferma "Ci sono modifiche non salvate: fai prima il commit o mettile da parte" }
git fetch --quiet origin
if ((git rev-parse HEAD) -ne (git rev-parse origin/main)) { Ferma "main non è allineato con origin/main: fai prima pull o push" }
if (git tag --list "v$Versione") { Ferma "Il tag v$Versione esiste già" }

$props = Get-Content Directory.Build.props -Raw
if ($props -notmatch '<Version>([^<]+)</Version>') { Ferma "<Version> non trovato in Directory.Build.props" }
$attuale = [version]$Matches[1]
if ([version]$Versione -le $attuale) { Ferma "La nuova versione ($Versione) deve essere più alta di quella attuale ($attuale)" }

$props = $props -replace '<Version>[^<]+</Version>', "<Version>$Versione</Version>"
[IO.File]::WriteAllText((Join-Path (Get-Location) 'Directory.Build.props'), $props)
git add Directory.Build.props
git commit -m "Versione $Versione"
git tag -a "v$Versione" -m "Versione $Versione"
git push --atomic origin main "v$Versione"
if ($LASTEXITCODE -ne 0) { Ferma "Invio non riuscito: il commit e il tag sono solo in locale, riprova con: git push --atomic origin main v$Versione" }
Write-Host "Versione $Versione inviata: la release la pubblica GitHub Actions (scheda Actions del repository)." -ForegroundColor Green
