; Installer per utente di Energy Italy Sfondo Animato (PRD sezione 9). Senza diritti di amministratore.
; Compilazione: ISCC.exe installer\SfondoAnimato.iss (dopo dotnet publish in .\publish)
; Installazione silenziosa per la distribuzione: setup.exe /VERYSILENT /SUPPRESSMSGBOXES

#define NomeApp "Energy Italy Sfondo Animato"
#define Versione "1.0.0"
#define Eseguibile "SfondoAnimato.exe"

[Setup]
AppId={{6B2E7C1A-4F0D-4C55-9E7B-3A1D2F8C9E10}
AppName={#NomeApp}
AppVersion={#Versione}
AppPublisher=Energy Italy SpA
DefaultDirName={localappdata}\Programs\{#NomeApp}
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=Output
OutputBaseFilename=EnergyItaly-SfondoAnimato-{#Versione}-setup
SetupIconFile=..\src\EnergyItaly.SfondoAnimato\risorse\icona.ico
UninstallDisplayIcon={app}\{#Eseguibile}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "it"; MessagesFile: "compiler:Languages\Italian.isl"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{userprograms}\{#NomeApp}"; Filename: "{app}\{#Eseguibile}"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "EnergyItaly.SfondoAnimato"; ValueData: """{app}\{#Eseguibile}"" --avvio"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#Eseguibile}"; Description: "Avvia {#NomeApp}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\{#Eseguibile}"; Parameters: "--avvio"; Flags: nowait skipifnotsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/IM {#Eseguibile} /F"; Flags: runhidden; RunOnceId: "ChiudiProgramma"

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\EnergyItaly\SfondoAnimato"
Type: filesandordirs; Name: "{localappdata}\EnergyItaly\SfondoAnimato"

[Code]
const
  ChiaveWebView2 = 'Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';

function VersioneValida(const Valore: String): Boolean;
begin
  Result := (Valore <> '') and (Valore <> '0.0.0.0');
end;

function WebView2Presente(): Boolean;
var
  Versione: String;
begin
  Result :=
    (RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\' + ChiaveWebView2, 'pv', Versione) and VersioneValida(Versione)) or
    (RegQueryStringValue(HKLM, 'SOFTWARE\' + ChiaveWebView2, 'pv', Versione) and VersioneValida(Versione)) or
    (RegQueryStringValue(HKCU, 'Software\' + ChiaveWebView2, 'pv', Versione) and VersioneValida(Versione));
end;

// Su Windows 10 senza WebView2 scarica ed esegue il bootstrapper Evergreen di Microsoft.
procedure CurStepChanged(CurStep: TSetupStep);
var
  Codice: Integer;
begin
  if (CurStep = ssPostInstall) and not WebView2Presente() then
  begin
    try
      DownloadTemporaryFile('https://go.microsoft.com/fwlink/p/?LinkId=2124703', 'MicrosoftEdgeWebview2Setup.exe', '', nil);
      Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, Codice);
    except
      Log('WebView2 non installato: ' + GetExceptionMessage);
    end;
  end;
end;
