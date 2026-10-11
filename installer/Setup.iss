#ifndef AppVersion
  #error AppVersion must be supplied
#endif
#ifndef PayloadDir
  #error PayloadDir must be supplied
#endif
#ifndef OutputDir
  #error OutputDir must be supplied
#endif
[Setup]
AppId={{942F2447-54AC-5C47-BC13-774845DFEC5C}
AppName=Checkup
AppVersion={#AppVersion}
AppPublisher=informent
AppPublisherURL=https://github.com/informent/Checkup
DefaultDirName={localappdata}\Programs\Checkup
DefaultGroupName=Checkup
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename=Checkup-v{#AppVersion}-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Checkup.exe
CloseApplications=yes
CloseApplicationsFilter=Checkup.exe
RestartApplications=no
SetupLogging=yes
[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked
[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{autoprograms}\Checkup"; Filename: "{app}\Checkup.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Checkup"; Filename: "{app}\Checkup.exe"; WorkingDir: "{app}"; Tasks: desktopicon
[Run]
Filename: "{app}\Checkup.exe"; Description: "Open Checkup"; Flags: nowait postinstall skipifsilent unchecked
