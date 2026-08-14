#ifndef AppVersion
  #define AppVersion "1.2.8.26"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

[Setup]
AppId={{8FA61ABA-1765-40E6-9F7D-56FF46D8D587}
AppName=iRacing Setup Manager
AppVersion={#AppVersion}
AppVerName=iRacing Setup Manager {#AppVersion}
AppPublisher=Benaudo
DefaultDirName={localappdata}\Programs\IracingSetupManager
DefaultGroupName=iRacing Setup Manager
UninstallDisplayName=iRacing Setup Manager
OutputDir={#OutputDir}
OutputBaseFilename=IracingSetupManager-{#AppVersion}-win-x64-setup
Compression=lzma2/max
SolidCompression=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=yes
UsePreviousAppDir=yes
UsePreviousGroup=yes
DisableProgramGroupPage=yes
WizardStyle=modern
SetupLogging=yes
VersionInfoVersion={#AppVersion}
VersionInfoProductVersion={#AppVersion}
VersionInfoProductName=iRacing Setup Manager
SetupIconFile={#PublishDir}\Assets\AppLogo.ico
UninstallDisplayIcon={app}\IracingSetupManager.App.exe

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\iRacing Setup Manager"; Filename: "{app}\IracingSetupManager.App.exe"
Name: "{autodesktop}\iRacing Setup Manager"; Filename: "{app}\IracingSetupManager.App.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Créer un raccourci sur le Bureau"; GroupDescription: "Raccourcis supplémentaires :"; Flags: unchecked

[Run]
Filename: "{app}\IracingSetupManager.App.exe"; Description: "Lancer iRacing Setup Manager"; Flags: nowait postinstall skipifsilent
Filename: "{app}\IracingSetupManager.App.exe"; Flags: nowait; Check: ShouldRelaunchApplication

[Code]
function ShouldRelaunchApplication(): Boolean;
begin
  Result := CompareText(ExpandConstant('{param:RELAUNCHAPP|0}'), '1') = 0;
end;

function InitializeSetup(): Boolean;
begin
  { Le même AppId et le même dossier permettent la mise à niveau. }
  { Aucune suppression ne cible LocalAppData\IracingSetupManager. }
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  CacheDirectory: String;
  CachedInstaller: String;
begin
  if CurStep = ssPostInstall then
  begin
    CacheDirectory := ExpandConstant('{localappdata}\IracingSetupManager\Updates\Installers');
    ForceDirectories(CacheDirectory);
    CachedInstaller := CacheDirectory + '\IracingSetupManager-{#AppVersion}-win-x64-setup.exe';
    if not FileExists(CachedInstaller) then
      CopyFile(ExpandConstant('{srcexe}'), CachedInstaller, False);
  end;
end;
