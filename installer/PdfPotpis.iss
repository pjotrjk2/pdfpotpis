; PDFPotpis — simple two-step install wizard (folder + progress/finish)
; Compile with Inno Setup 6: ISCC.exe PdfPotpis.iss
; Or run: scripts\build-installer.ps1

#define MyAppName "PDFPotpis"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "PDFPotpis"
#define MyAppExeName "PdfPotpis.exe"
#define PublishDir "..\src\PdfPotpis\bin\Release\net9.0-windows\win-x64\publish"

[Setup]
AppId={{8F3C2A1B-9D4E-4F6A-B7C8-1E2D3A4B5C6D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableReadyPage=yes
DisableDirPage=no
AllowNoIcons=yes
OutputDir=output
OutputBaseFilename=PDFPotpis-Setup-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\assets\PdfPotpis.ico
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
InfoBeforeFile=
LicenseFile=
SetupLogging=yes

[Languages]
Name: "serbian"; MessagesFile: "compiler:Default.isl"

[Messages]
serbian.WelcomeLabel1=Dobrodošli u instalaciju PDFPotpis
serbian.WelcomeLabel2=Ovaj čarobnjak će instalirati PDFPotpis na vaš računar.%n%nAplikacija radi potpuno lokalno — ne šalje podatke na internet i ne čuva dokumente na udaljenim serverima.%n%nKliknite Dalje za nastavak.
serbian.SelectDirLabel3=Program će biti instaliran u sledeći folder.
serbian.SelectDirBrowseLabel=Izaberite folder instalacije, zatim kliknite Dalje.
serbian.InstallingLabel=Instalacija PDFPotpis je u toku…
serbian.FinishedLabel=PDFPotpis je uspešno instaliran na ovaj računar.
serbian.FinishedHeadingLabel=Instalacija završena
serbian.ClickFinish=Kliknite Završi da zatvorite čarobnjak.
serbian.ButtonNext=&Dalje
serbian.ButtonBack=&Nazad
serbian.ButtonCancel=Otkaži
serbian.ButtonInstall=&Instaliraj
serbian.ButtonFinish=&Završi

[Tasks]
; No optional tasks — folder choice only, per product requirements.

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Registry]
Root: HKCU; Subkey: "Software\Classes\PDFPotpis.Document"; ValueType: string; ValueName: ""; ValueData: "PDF dokument (PDFPotpis)"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\PDFPotpis.Document\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"",0"
Root: HKCU; Subkey: "Software\Classes\PDFPotpis.Document\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\.pdf\OpenWithProgids"; ValueType: string; ValueName: "PDFPotpis.Document"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "PDFPotpis"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
Root: HKCU; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".pdf"; ValueData: ""
Root: HKCU; Subkey: "Software\PDFPotpis\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "PDFPotpis"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\PDFPotpis\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "Lokalno potpisivanje PDF dokumenata"
Root: HKCU; Subkey: "Software\PDFPotpis\Capabilities\FileAssociations"; ValueType: string; ValueName: ".pdf"; ValueData: "PDFPotpis.Document"
Root: HKCU; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "PDFPotpis"; ValueData: "Software\PDFPotpis\Capabilities"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Pokreni PDFPotpis"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
end;
