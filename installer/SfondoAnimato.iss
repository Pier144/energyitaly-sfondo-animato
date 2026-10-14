; Installer per utente di Energy Italy Sfondo Animato (PRD sezione 9). Senza diritti di amministratore.
; Compilazione: ISCC.exe installer\SfondoAnimato.iss (dopo dotnet publish in .\publish)
; Installazione silenziosa per la distribuzione: setup.exe /VERYSILENT /SUPPRESSMSGBOXES

#define NomeApp "Energy Italy Sfondo Animato"
; La versione arriva da ISCC /DVersione=X.Y.Z (workflow di rilascio); a mano resta questa.
#ifndef Versione
  #define Versione "1.0.0"
#endif
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
CloseApplications=force
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "it"; MessagesFile: "compiler:Languages\Italian.isl"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion
; Logo per le schermate del disinstallatore (strumenti\genera_logo_installer.py): il disinstallatore non
; può estrarre file dal setup, li legge da qui prima di cancellare la cartella.
Source: "disinstallazione\*.bmp"; DestDir: "{app}\disinstallazione"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\{#NomeApp}"; Filename: "{app}\{#Eseguibile}"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "EnergyItaly.SfondoAnimato"; ValueData: """{app}\{#Eseguibile}"" --avvio"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#Eseguibile}"; Description: "Avvia {#NomeApp}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\{#Eseguibile}"; Parameters: "{code:ParametriRiavvio}"; Flags: nowait skipifnotsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/IM {#Eseguibile} /F"; Flags: runhidden; RunOnceId: "ChiudiProgramma"

; Impostazioni e sfondi importati ({userappdata}) si cancellano nel [Code], perché si possono conservare.
[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\EnergyItaly\SfondoAnimato"

[Code]
const
  ChiaveWebView2 = 'Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  ChiaveDisinstallazione = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{6B2E7C1A-4F0D-4C55-9E7B-3A1D2F8C9E10}_is1';
  // Colori della finestra del programma (web/app/app.css), in ordine BGR come vuole TColor.
  ColoreFondo = $1F1107;      // #07111f
  ColoreTesto = $F6EEE8;      // #e8eef6
  ColoreTenue = $CFBBA9;      // #a9bbcf
  ColoreSpento = $93755D;     // #5d7593
  ColoreVerde = $2AB57A;      // #7AB52A
  ColoreBordoForte = $5A3922; // #22395a
  DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
  DWMWA_USE_IMMERSIVE_DARK_MODE_PRIMA_20H1 = 19;
  TitoloDisinstallazione = 'Disinstalla {#NomeApp}';

var
  InstallazioneRiuscita: Boolean;
  // Disinstallazione (spec 2026-10-14-disinstallatore-design).
  Grafica, Conserva: Boolean;
  Dpi: Integer;
  Logo: TBitmap;
  FinestraAttiva: TSetupForm;
  Scelta: Boolean;
  CasellaConserva: TNewCheckBox;

function DwmSetWindowAttribute(Finestra: HWND; Attributo: Cardinal; var Valore: Integer; Dimensione: Cardinal): Integer;
  external 'DwmSetWindowAttribute@dwmapi.dll stdcall delayload';
function GetDpiForSystem(): Cardinal;
  external 'GetDpiForSystem@user32.dll stdcall delayload';
function CreateRoundRectRgn(X1, Y1, X2, Y2, LarghezzaArco, AltezzaArco: Integer): Longword;
  external 'CreateRoundRectRgn@gdi32.dll stdcall';
function SetWindowRgn(Finestra: HWND; Regione: Longword; Ridisegna: Integer): Integer;
  external 'SetWindowRgn@user32.dll stdcall';

// "Installa ora" del programma passa /APRI=1: dopo l'aggiornamento riparte con la finestra aperta.
function ParametriRiavvio(Param: String): String;
begin
  if ExpandConstant('{param:APRI|0}') = '1' then
    Result := ''
  else
    Result := '--avvio';
end;

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

// Impostazioni > App lancia il disinstallatore con le schermate Energy Italy. Con /SILENT Inno non mostra
// le sue finestre di conferma e di fine (si possono togliere solo così); /GRAFICA=1 distingue questo caso
// da /SILENT lanciato dall'IT. Inno scrive la chiave prima di ssPostInstall, anche negli aggiornamenti.
procedure UsaDisinstallatoreGrafico();
var
  Comando: String;
begin
  if RegQueryStringValue(HKCU, ChiaveDisinstallazione, 'UninstallString', Comando) and (Pos('/GRAFICA=1', Comando) = 0) then
    RegWriteStringValue(HKCU, ChiaveDisinstallazione, 'UninstallString', Comando + ' /SILENT /GRAFICA=1');
end;

// Su Windows 10 senza WebView2 scarica ed esegue il bootstrapper Evergreen di Microsoft.
procedure CurStepChanged(CurStep: TSetupStep);
var
  Codice: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    InstallazioneRiuscita := True;
    UsaDisinstallatoreGrafico();
  end;
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

// Aggiornamento silenzioso non riuscito: il programma (uscito per farsi aggiornare) riparte con la
// versione di prima, invece di restare spento fino al prossimo accesso. --dopo-aggiornamento gli dice di
// non ritentare subito e di aspettare che la copia vecchia sia uscita; con /APRI=1 riapre la finestra.
procedure DeinitializeSetup();
var
  Codice: Integer;
begin
  if WizardSilent() and not InstallazioneRiuscita then
  begin
    try
      if FileExists(ExpandConstant('{app}\{#Eseguibile}')) then
        Exec(ExpandConstant('{app}\{#Eseguibile}'), Trim(ParametriRiavvio('') + ' --dopo-aggiornamento'), '', SW_SHOWNORMAL, ewNoWait, Codice);
    except
      Log('Programma non rilanciato: ' + GetExceptionMessage);
    end;
  end;
end;

// ---------------------------------------------------------------------------------------------------
// Disinstallazione con le schermate Energy Italy (spec 2026-10-14-disinstallatore-design): conferma con
// "Conserva impostazioni e sfondi importati", avanzamento, fine. Solo con /GRAFICA=1 (Impostazioni > App);
// /SILENT e /VERYSILENT dell'IT restano senza finestre, /CONSERVA=1 tiene i dati.

function ParametroPresente(const Nome: String): Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if CompareText(ParamStr(I), Nome) = 0 then
      Result := True;
end;

// Da DIP a pixel: le misure delle schermate seguono la scala di Windows.
function D(Valore: Integer): Integer;
begin
  Result := (Valore * Dpi + 48) div 96;
end;

procedure LeggiDpi();
begin
  Dpi := 96;
  try
    Dpi := GetDpiForSystem();
  except
    Log('GetDpiForSystem non disponibile: ' + GetExceptionMessage);
  end;
  if Dpi < 96 then
    Dpi := 96;
end;

// Il bitmap più vicino alla scala (100, 125, 150, 175, 200%), usato senza ridimensionarlo.
procedure CaricaLogo();
var
  Scala: Integer;
  Percorso: String;
begin
  Scala := ((Dpi * 100 div 96) + 12) div 25 * 25;
  if Scala < 100 then
    Scala := 100;
  if Scala > 200 then
    Scala := 200;
  Percorso := ExpandConstant('{app}\disinstallazione\logo-') + IntToStr(Scala) + '.bmp';
  if not FileExists(Percorso) then
  begin
    Log('Logo non trovato: ' + Percorso);
    Exit;
  end;
  try
    Logo := TBitmap.Create();
    Logo.LoadFromFile(Percorso);
  except
    Log('Logo non caricato: ' + GetExceptionMessage);
    Logo := nil;
  end;
end;

// Barra del titolo scura come la finestra del programma.
procedure BarraScura(Finestra: TForm);
var
  Valore: Integer;
begin
  Valore := 1;
  try
    if DwmSetWindowAttribute(Finestra.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, Valore, 4) <> 0 then
      DwmSetWindowAttribute(Finestra.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_PRIMA_20H1, Valore, 4);
  except
    Log('Barra del titolo scura non impostata: ' + GetExceptionMessage);
  end;
end;

procedure Arrotonda(Controllo: TWinControl);
begin
  SetWindowRgn(Controllo.Handle, CreateRoundRectRgn(0, 0, Controllo.Width + 1, Controllo.Height + 1, Controllo.Height, Controllo.Height), 1);
end;

function NuovaFinestra(): TSetupForm;
begin
  // Altezza provvisoria: la fissa chi costruisce la schermata, dopo aver messo i controlli.
  Result := CreateCustomForm(D(480), D(300), True, True);
  Result.Caption := TitoloDisinstallazione;
  Result.Color := ColoreFondo;
  Result.CenterOnShow := True;
  Result.KeyPreview := True;
end;

// Restituisce il bordo inferiore del logo (o Alto se il logo manca).
function AggiungiLogo(Genitore: TWinControl; Sinistra, Alto: Integer): Integer;
var
  Immagine: TBitmapImage;
begin
  Result := Alto;
  if Logo = nil then
    Exit;
  Immagine := TBitmapImage.Create(Genitore);
  Immagine.Parent := Genitore;
  Immagine.BackColor := ColoreFondo;
  Immagine.Bitmap := Logo;
  Immagine.SetBounds(Sinistra, Alto, Logo.Width, Logo.Height);
  Result := Alto + Logo.Height;
end;

function NuovaEtichetta(Genitore: TWinControl; const Testo: String; Sinistra, Alto, Larghezza: Integer;
  Colore: TColor; Punti: Integer; Grassetto: Boolean): TLabel;
begin
  Result := TLabel.Create(Genitore);
  Result.Parent := Genitore;
  Result.Color := ColoreFondo;
  Result.Font.Name := 'Segoe UI';
  Result.Font.Size := Punti;
  Result.Font.Color := Colore;
  if Grassetto then
    Result.Font.Style := [fsBold];
  // AutoSize (attivo di default) ridurrebbe la larghezza al testo ancora vuoto: si riattiva solo dopo,
  // quando con WordWrap adatta l'altezza mantenendo la larghezza.
  Result.AutoSize := False;
  Result.WordWrap := True;
  Result.SetBounds(Sinistra, Alto, Larghezza, D(20));
  Result.Caption := Testo;
  Result.AutoSize := True;
end;

// Pulsante a pillola come nel programma: principale verde pieno, secondario a contorno (.bottone-contorno).
function NuovoPulsante(Finestra: TSetupForm; const Testo: String; Sinistra, Alto, Larghezza: Integer;
  Principale: Boolean; Clic: TNotifyEvent): TPanel;
var
  Interno: TPanel;
begin
  Result := TPanel.Create(Finestra);
  Result.Parent := Finestra;
  Result.SetBounds(Sinistra, Alto, Larghezza, D(40));
  Result.BevelOuter := bvNone;
  Result.ParentBackground := False;
  Result.Cursor := crHand;
  Result.Font.Name := 'Segoe UI Semibold';
  Result.Font.Size := 10;
  Result.OnClick := Clic;
  if Principale then
  begin
    Result.Color := ColoreVerde;
    Result.Font.Color := ColoreFondo;
    Result.Caption := Testo;
  end
  else
  begin
    Result.Color := ColoreBordoForte;
    Interno := TPanel.Create(Finestra);
    Interno.Parent := Result;
    Interno.SetBounds(1, 1, Larghezza - 2, Result.Height - 2);
    Interno.BevelOuter := bvNone;
    Interno.ParentBackground := False;
    Interno.Color := ColoreFondo;
    Interno.Cursor := crHand;
    Interno.Font.Name := 'Segoe UI Semibold';
    Interno.Font.Size := 10;
    Interno.Font.Color := ColoreTesto;
    Interno.Caption := Testo;
    Interno.OnClick := Clic;
    Arrotonda(Interno);
  end;
  Arrotonda(Result);
end;

procedure ChiudiConScelta(Valore: Boolean);
begin
  Scelta := Valore;
  FinestraAttiva.Close();
end;

procedure ClicDisinstalla(Sender: TObject);
begin
  ChiudiConScelta(True);
end;

procedure ClicAnnulla(Sender: TObject);
begin
  ChiudiConScelta(False);
end;

procedure ClicEtichettaConserva(Sender: TObject);
begin
  CasellaConserva.Checked := not CasellaConserva.Checked;
end;

// Invio = Disinstalla, Esc = Annulla (chiudere la finestra vale Annulla).
procedure TastoConferma(Sender: TObject; var Key: Word; Shift: TShiftState);
begin
  if Key = 13 then
    ChiudiConScelta(True)
  else if Key = 27 then
    ChiudiConScelta(False);
end;

procedure TastoFine(Sender: TObject; var Key: Word; Shift: TShiftState);
begin
  if (Key = 13) or (Key = 27) then
    ChiudiConScelta(True);
end;

function MostraConferma(): Boolean;
var
  Finestra: TSetupForm;
  Etichetta: TLabel;
  Sinistra, Larghezza, Alto, LarghezzaPulsante, Rientro: Integer;
begin
  Finestra := NuovaFinestra();
  try
    Sinistra := D(32);
    Larghezza := Finestra.ClientWidth - 2 * Sinistra;
    Alto := AggiungiLogo(Finestra, Sinistra, D(28)) + D(24);
    Etichetta := NuovaEtichetta(Finestra, 'Disinstallare Sfondo Animato?', Sinistra, Alto, Larghezza, ColoreTesto, 15, True);
    Alto := Etichetta.Top + Etichetta.Height + D(8);
    Etichetta := NuovaEtichetta(Finestra, 'Lo sfondo animato verrà fermato e il programma rimosso dal PC.',
      Sinistra, Alto, Larghezza, ColoreTenue, 10, False);
    Alto := Etichetta.Top + Etichetta.Height + D(24);

    Rientro := D(26);
    Etichetta := NuovaEtichetta(Finestra, 'Conserva impostazioni e sfondi importati', Sinistra + Rientro, Alto,
      Larghezza - Rientro, ColoreTesto, 10, False);
    Etichetta.Cursor := crHand;
    Etichetta.OnClick := @ClicEtichettaConserva;
    CasellaConserva := TNewCheckBox.Create(Finestra);
    CasellaConserva.Parent := Finestra;
    CasellaConserva.Color := ColoreFondo;
    CasellaConserva.Caption := '';
    CasellaConserva.Checked := Conserva;
    CasellaConserva.SetBounds(Sinistra, Alto + (Etichetta.Height - D(18)) div 2, D(18), D(18));
    Etichetta := NuovaEtichetta(Finestra, 'Utile se pensi di reinstallarlo.', Sinistra + Rientro,
      Etichetta.Top + Etichetta.Height + D(2), Larghezza - Rientro, ColoreSpento, 9, False);
    Alto := Etichetta.Top + Etichetta.Height + D(32);

    LarghezzaPulsante := D(132);
    NuovoPulsante(Finestra, 'Disinstalla', Finestra.ClientWidth - Sinistra - LarghezzaPulsante, Alto, LarghezzaPulsante, True, @ClicDisinstalla);
    NuovoPulsante(Finestra, 'Annulla', Finestra.ClientWidth - Sinistra - 2 * LarghezzaPulsante - D(8), Alto, LarghezzaPulsante, False, @ClicAnnulla);
    Finestra.ClientHeight := Alto + D(40) + D(28);

    Finestra.OnKeyDown := @TastoConferma;
    Finestra.ActiveControl := CasellaConserva;
    BarraScura(Finestra);
    Scelta := False;
    FinestraAttiva := Finestra;
    Finestra.ShowModal();
    Result := Scelta;
    Conserva := CasellaConserva.Checked;
  finally
    FinestraAttiva := nil;
    CasellaConserva := nil;
    Finestra.Free();
  end;
end;

procedure MostraFine();
var
  Finestra: TSetupForm;
  Etichetta: TLabel;
  Sinistra, Larghezza, Alto, LarghezzaPulsante: Integer;
  Testo: String;
begin
  Finestra := NuovaFinestra();
  try
    Sinistra := D(32);
    Larghezza := Finestra.ClientWidth - 2 * Sinistra;
    Alto := AggiungiLogo(Finestra, Sinistra, D(28)) + D(24);
    Etichetta := NuovaEtichetta(Finestra, 'Sfondo Animato è stato disinstallato', Sinistra, Alto, Larghezza, ColoreTesto, 15, True);
    Alto := Etichetta.Top + Etichetta.Height + D(8);
    if Conserva then
      Testo := 'Impostazioni e sfondi importati sono stati conservati: li ritrovi se lo reinstalli.'
    else
      Testo := 'Sono stati cancellati anche le impostazioni e gli sfondi importati.';
    Etichetta := NuovaEtichetta(Finestra, Testo, Sinistra, Alto, Larghezza, ColoreTenue, 10, False);
    Alto := Etichetta.Top + Etichetta.Height;
    if DirExists(ExpandConstant('{app}')) then
    begin
      Etichetta := NuovaEtichetta(Finestra, 'Alcuni file non sono stati rimossi. Puoi cancellare a mano la cartella:',
        Sinistra, Alto + D(16), Larghezza, ColoreTenue, 10, False);
      Etichetta := NuovaEtichetta(Finestra, ExpandConstant('{app}'), Sinistra, Etichetta.Top + Etichetta.Height + D(4),
        Larghezza, ColoreTesto, 9, False);
      Alto := Etichetta.Top + Etichetta.Height;
    end;
    Alto := Alto + D(32);

    LarghezzaPulsante := D(132);
    NuovoPulsante(Finestra, 'Chiudi', Finestra.ClientWidth - Sinistra - LarghezzaPulsante, Alto, LarghezzaPulsante, True, @ClicDisinstalla);
    Finestra.ClientHeight := Alto + D(40) + D(28);

    Finestra.OnKeyDown := @TastoFine;
    BarraScura(Finestra);
    FinestraAttiva := Finestra;
    Finestra.ShowModal();
  finally
    FinestraAttiva := nil;
    Finestra.Free();
  end;
end;

function InitializeUninstall(): Boolean;
begin
  Result := True;
  Conserva := ParametroPresente('/CONSERVA=1');
  Grafica := UninstallSilent() and ParametroPresente('/GRAFICA=1');
  Log(Format('Disinstallazione: silenziosa=%d grafica=%d conserva=%d parametri=%d', [Ord(UninstallSilent()), Ord(Grafica), Ord(Conserva), ParamCount]));
  if not Grafica then
    Exit;
  LeggiDpi();
  // Il logo sta in {app}: va letto ora, prima che la disinstallazione cancelli la cartella.
  CaricaLogo();
  Result := MostraConferma();
  if not Result then
    Log('Disinstallazione annullata dall''utente');
end;

// La finestra di avanzamento di Inno con i colori del programma: un pannello copre l'intestazione bianca,
// le linee e il pulsante Annulla (che Inno tiene comunque disattivato); la barra di avanzamento ci finisce sopra.
procedure InitializeUninstallProgressForm();
var
  Finestra: TUninstallProgressForm;
  Pannello: TPanel;
  Etichetta: TLabel;
  Sinistra, Larghezza, Alto: Integer;
begin
  if not Grafica then
    Exit;
  Finestra := UninstallProgressForm;
  Finestra.Caption := TitoloDisinstallazione;
  Finestra.Color := ColoreFondo;
  Finestra.ClientWidth := D(480);

  Pannello := TPanel.Create(Finestra);
  Pannello.Parent := Finestra;
  Pannello.BevelOuter := bvNone;
  Pannello.ParentBackground := False;
  Pannello.Color := ColoreFondo;
  Pannello.Align := alClient;
  Pannello.BringToFront();

  Sinistra := D(32);
  Larghezza := Finestra.ClientWidth - 2 * Sinistra;
  Alto := AggiungiLogo(Pannello, Sinistra, D(28)) + D(24);
  Etichetta := NuovaEtichetta(Pannello, 'Disinstallazione in corso...', Sinistra, Alto, Larghezza, ColoreTesto, 15, True);
  Alto := Etichetta.Top + Etichetta.Height + D(20);
  Finestra.ProgressBar.Parent := Pannello;
  Finestra.ProgressBar.SetBounds(Sinistra, Alto, Larghezza, D(8));
  Finestra.ClientHeight := Alto + D(8) + D(36);
  BarraScura(Finestra);
end;

// Le cartelle EnergyItaly si tolgono solo se sono rimaste vuote. Una cartella può restare per un po' in uso
// anche vuota (i processi WebView2 del programma chiuso escono poco dopo): qualche tentativo.
procedure RimuoviSeVuota(const Cartella: String);
var
  Tentativo: Integer;
begin
  for Tentativo := 1 to 10 do
  begin
    if not DirExists(Cartella) or RemoveDir(Cartella) then
      Exit;
    Sleep(200);
  end;
  Log('Cartella non rimossa: ' + Cartella);
end;

function CartellaDati(): String;
begin
  Result := ExpandConstant('{userappdata}\EnergyItaly\SfondoAnimato');
end;

// Le versioni fino alla 1.2.0 cancellavano i dati con [UninstallDelete], e Inno aggiunge le istruzioni di
// ogni aggiornamento a quelle vecchie: sui PC aggiornati la cancellazione c'è ancora. Per conservare i dati
// li si sposta da parte prima che Inno cancelli e li si rimette al loro posto dopo.
procedure MettiDatiDaParte();
var
  Codice: Integer;
begin
  if not DirExists(CartellaDati()) then
    Exit;
  // Il programma aperto potrebbe tenere un file aperto e impedire lo spostamento: si chiude adesso
  // invece che in [UninstallRun], che viene dopo.
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM {#Eseguibile} /F', '', SW_HIDE, ewWaitUntilTerminated, Codice);
  Sleep(500);
  DelTree(CartellaDati() + '.conserva', True, True, True);
  if not RenameFile(CartellaDati(), CartellaDati() + '.conserva') then
    Log('Dati non spostati da parte: ' + CartellaDati());
end;

procedure RimettiDati();
begin
  if not DirExists(CartellaDati() + '.conserva') then
    Exit;
  DelTree(CartellaDati(), True, True, True);
  if not RenameFile(CartellaDati() + '.conserva', CartellaDati()) then
    Log('Dati non rimessi al loro posto: ' + CartellaDati() + '.conserva');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    if Conserva then
      MettiDatiDaParte();
  end
  else if CurUninstallStep = usPostUninstall then
  begin
    if Conserva then
      RimettiDati()
    else
      DelTree(CartellaDati(), True, True, True);
    if Grafica then
      MostraFine();
  end;
end;

procedure DeinitializeUninstall();
begin
  if Logo <> nil then
    Logo.Free();
  // Dopo la schermata finale: nel frattempo i processi WebView2 del programma sono usciti.
  RimuoviSeVuota(ExpandConstant('{userappdata}\EnergyItaly'));
  RimuoviSeVuota(ExpandConstant('{localappdata}\EnergyItaly'));
end;
