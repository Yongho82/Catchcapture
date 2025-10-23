; Inno Setup script for CatchCapture
; NOTE: Inno Setup uses .ico for icons. Convert icons\catcha.icns to .ico (e.g., icons\catcha.ico).

[Setup]
AppId={{A5A0FAF0-6D1F-4F4E-B2A6-9B8F0F5D2E31}}
AppName=CatchCapture
AppVersion=1.0.0
AppVerName=CatchCapture 1.0.0
AppPublisher=Yongho
DefaultDirName={localappdata}\CatchCapture
PrivilegesRequired=lowest
DefaultGroupName=CatchCapture
DisableDirPage=no
DisableProgramGroupPage=no
OutputBaseFilename=CatchCapture-Setup
OutputDir=dist
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
; Installer icon (must be .ico). Provide icons\\catcha.ico if available.
SetupIconFile=..\icons\catcha.ico

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "바탕화면 아이콘 만들기"; GroupDescription: "추가 작업:"; Flags: unchecked
Name: "startmenu"; Description: "시작 메뉴에 바로가기 만들기"; GroupDescription: "추가 작업:"; Flags: checkedonce

[Files]
; Prefer publish output
Source: "..\\bin\\Release\\net8.0-windows\\publish\\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
; Release build output (non-publish)
Source: "..\\bin\\Release\\net8.0-windows\\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "..\\bin\\Release\\net8.0-windows10.0.19041.0\\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
; Debug fallback (for quick local tests)
Source: "..\\bin\\Debug\\net8.0-windows\\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "..\\bin\\Debug\\net8.0-windows10.0.19041.0\\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
; Include icons if needed at runtime
Source: "..\\icons\\*"; DestDir: "{app}\\icons"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
; Ensure cons\\catcha.ico is present even if icons folder lacks it
Source: "..\\cons\\catcha.ico"; DestDir: "{app}\\icons"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
; Start Menu shortcut
Name: "{group}\\CatchCapture"; Filename: "{app}\\CatchCapture.exe"; WorkingDir: "{app}"; IconFilename: "{app}\\icons\\catcha.ico"; Tasks: startmenu
; Desktop shortcut (optional)
Name: "{userdesktop}\\CatchCapture"; Filename: "{app}\\CatchCapture.exe"; WorkingDir: "{app}"; IconFilename: "{app}\\icons\\catcha.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\\CatchCapture.exe"; Description: "설치가 끝나면 실행"; Flags: nowait postinstall skipifsilent
