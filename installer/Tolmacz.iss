; Per-user installer: no administrator rights and no separate runtime needed.
[Setup]
AppId={{6E4B7C0A-52D1-4B7E-9F13-7A2C8D1E4F60}
AppName=Tolmacz
AppVersion=1.0
DefaultDirName={localappdata}\Programs\Tolmacz
DefaultGroupName=Tolmacz
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
MinVersion=10.0.17763
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=Tolmacz-Setup
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\Tolmacz.App.exe

[Files]
Source: "..\dist\windows\Tolmacz\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{autoprograms}\Tolmacz"; Filename: "{app}\Tolmacz.App.exe"
Name: "{autodesktop}\Tolmacz"; Filename: "{app}\Tolmacz.App.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Run]
Filename: "{app}\Tolmacz.App.exe"; Description: "Start Tolmacz"; Flags: nowait postinstall skipifsilent
