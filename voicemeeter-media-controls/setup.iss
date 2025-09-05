[Setup]
AppName=VoiceMeeter Media Controls
AppVersion=1.0.5
DefaultDirName={pf}\VMMC
DefaultGroupName=VoiceMeeter Media Controls
OutputDir=bin\x64\Release\inno\
OutputBaseFilename=VMMC-Setup
Compression=lzma
SolidCompression=yes

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked
Name: "startup"; Description: "Run at Windows &startup"; Flags: unchecked

[Files]
Source: "bin\x64\Release\net8.0-windows10.0.17763.0\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\VMMC"; Filename: "{app}\voicemeeter-media-controls.exe"
Name: "{commondesktop}\VMMC"; Filename: "{app}\voicemeeter-media-controls.exe"; Tasks: desktopicon
Name: "{userstartup}\VMMC"; Filename: "{app}\voicemeeter-media-controls.exe"; Tasks: startup

[Registry]
Root: HKCU; Subkey: "Software\\Microsoft\\Windows\\CurrentVersion\\Run"; ValueType: string; ValueName: "VMMC"; ValueData: """{app}\\voicemeeter-media-controls.exe"""; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\\voicemeeter-media-controls.exe"; Description: "Launch VMMC"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\\voicemeeter-media-controls.exe"; Parameters: "/uninstall"; Flags: runhidden skipifdoesntexist