; RobloxGuard Inno Setup script (per-user install)
#define MyAppName "RobloxGuard"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Your Company"
#define MyAppExeName "RobloxGuard.exe"

[Setup]
AppId={{7B1B0D7F-1234-46BA-9A3D-ABCDEFGH1234}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\{#MyAppName}
DisableDirPage=yes
DisableProgramGroupPage=yes
OutputDir=.\Output
OutputBaseFilename=RobloxGuardInstaller
Compression=lzma
SolidCompression=yes
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64

[Files]
Source: "..\..\out\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\{#MyAppName}\Settings"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--ui"

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--install-first-run"; Flags: postinstall skipifsilent nowait

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
// Register per-user protocol handler and create scheduled task on install-first-run.
// You can also perform registry writes here if you prefer Inno over app self-setup.
