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
; Installer icon
SetupIconFile=..\icons\catcha.ico
; Uninstall icon in Control Panel
UninstallDisplayIcon={app}\icons\catcha.ico

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startmenu"; Description: "시작 메뉴에 바로가기 만들기"; GroupDescription: "추가 작업:"; Flags: checkedonce

[Files]
; Prefer publish output
Source: "..\bin\Release\net8.0-windows\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
; Release build output (non-publish)
Source: "..\bin\Release\net8.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "..\bin\Release\net8.0-windows10.0.19041.0\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
; Debug fallback (for quick local tests)
Source: "..\bin\Debug\net8.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "..\bin\Debug\net8.0-windows10.0.19041.0\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
; Include icons if needed at runtime
Source: "..\icons\*"; DestDir: "{app}\icons"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Icons]
; Start Menu shortcut - Korean
Name: "{group}\캐치캡처"; Filename: "{app}\CatchCapture.exe"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: korean; Tasks: startmenu
; Start Menu shortcut - English
Name: "{group}\CatchC"; Filename: "{app}\CatchCapture.exe"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: english; Tasks: startmenu

; Desktop shortcut - Korean
Name: "{userdesktop}\캐치캡처"; Filename: "{app}\CatchCapture.exe"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: korean; Tasks: desktopicon
; Desktop shortcut - English
Name: "{userdesktop}\CatchC"; Filename: "{app}\CatchCapture.exe"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: english; Tasks: desktopicon

[Run]
Filename: "{app}\CatchCapture.exe"; Description: "설치가 끝나면 실행"; Flags: nowait postinstall skipifsilent

[Code]
// 기존 버전 삭제 로직
function GetUninstallString(): String;
var
  sUnInstPath: String;
  sUnInstPathKey: String;
begin
  sUnInstPath := '';
  sUnInstPathKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{{A5A0FAF0-6D1F-4F4E-B2A6-9B8F0F5D2E31}}_is1';
  
  // 레지스트리에서 언인스톨러 경로 찾기 (Current User)
  if RegQueryStringValue(HKCU, sUnInstPathKey, 'UninstallString', sUnInstPath) then
    Result := sUnInstPath
  // 레지스트리에서 언인스톨러 경로 찾기 (Local Machine - 관리자 권한 설치 시)
  else if RegQueryStringValue(HKLM, sUnInstPathKey, 'UninstallString', sUnInstPath) then
    Result := sUnInstPath
  else
    Result := '';
end;

function InitializeSetup(): Boolean;
var
  sUnInstallString: String;
  iResultCode: Integer;
begin
  Result := True;
  sUnInstallString := GetUninstallString();
  
  if sUnInstallString <> '' then
  begin
    // 기존 버전 발견 시 삭제 진행
    // /SILENT: UI 최소화, /SUPPRESSMSGBOXES: 메시지 박스 숨김
    sUnInstallString := RemoveQuotes(sUnInstallString);
    if Exec(sUnInstallString, '/SILENT /NORESTART /SUPPRESSMSGBOXES', '', SW_HIDE, ewWaitUntilTerminated, iResultCode) then
    begin
      // 삭제 성공
    end
    else
    begin
      // 삭제 실패 시에도 설치는 계속 진행할지 여부 결정 (여기서는 계속 진행)
    end;
  end;
end;
[UninstallDelete]
Type: filesandordirs; Name: "{app}\*"
; LocalAppData 설정 파일 삭제
Type: filesandordirs; Name: "{localappdata}\CatchCapture"
; Roaming AppData 설정 파일 삭제 (레거시)
Type: filesandordirs; Name: "{userappdata}\CatchCapture"

