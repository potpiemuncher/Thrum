; Thrum installer (Inno Setup 6.3 or later).
;
; Built from the packaged Thrum folder that utils/post-build.py produces, by
; utils/build-installer.ps1 in the CI and release workflows. By hand:
;   ISCC.exe /DAppVersion=0.9.0-beta.3 /DPackageDir=C:\src\Thrum\bin\x64\Release\Thrum installer\Thrum.iss
;
; Installs for all users in Program Files by default: one UAC prompt, during
; setup only. The first page also offers an installation for the current user
; only, in %LOCALAPPDATA%\Programs, which needs no administrator rights.
; Thrum itself is never started elevated from here: "Launch Thrum" on the last
; page starts it as the signed-in user (runasoriginaluser).

#ifndef AppVersion
  #error Define AppVersion, for example /DAppVersion=0.9.0-beta.3
#endif
#ifndef PackageDir
  #error Define PackageDir: the packaged Thrum folder (bin\x64\Release\Thrum)
#endif
#ifndef BinaryVersion
  #define BinaryVersion "0.0.0.0"
#endif
#ifndef OutputDir
  #define OutputDir "..\bin\installer"
#endif
#ifndef OutputBaseFilename
  #define OutputBaseFilename "Thrum_" + AppVersion + "_x64_setup"
#endif

#define AppName "Thrum"
#define AppExeName "Thrum.exe"
; ProductInfo.InstallerAppMutexName. Thrum holds it while it runs, so setup
; and the uninstaller ask for it to be closed first. A test keeps them equal.
#define AppMutexName "Thrum_AppRunning"
; ProductInfo.StartupShortcutName, in the user's Startup folder
; (Settings > Run at startup).
#define StartupShortcutName "Thrum.lnk"

[Setup]
; Never change AppId: Windows and later installers find this installation by it.
AppId={{9EE6AEB0-A634-4E16-A7E0-E6358E6E271F}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Thrum contributors
AppPublisherURL=https://github.com/potpiemuncher/Thrum
AppSupportURL=https://github.com/potpiemuncher/Thrum/issues
AppUpdatesURL=https://github.com/potpiemuncher/Thrum/releases
AppMutex={#AppMutexName}
VersionInfoVersion={#BinaryVersion}
VersionInfoProductTextVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 10 version 2004, as in the README.
MinVersion=10.0.19041
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
SetupIconFile=..\Thrum\Resources\Thrum.ico
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
; {userstartup} and {userappdata} below are the signed-in user's folders in
; both modes, as long as UAC elevates that same account.
UsedUserAreasWarning=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PackageDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallDelete]
Type: files; Name: "{userstartup}\{#StartupShortcutName}"

[Code]
// Settings live outside the program folder, so uninstalling keeps them unless
// the user says otherwise. Drivers and the VIIPER backend have their own
// uninstallers; see the README.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  SettingsDir: String;
begin
  if (CurUninstallStep = usPostUninstall) and not UninstallSilent then
  begin
    SettingsDir := ExpandConstant('{userappdata}\{#AppName}');
    if DirExists(SettingsDir) then
    begin
      if MsgBox('Also delete your Thrum settings, profiles and logs?' + #13#10#13#10 +
        SettingsDir + #13#10#13#10 +
        'Choose No to keep them for a later reinstall.',
        mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      begin
        DelTree(SettingsDir, True, True, True);
      end;
    end;
  end;
end;
