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
CloseApplications=force
; Installer icon
SetupIconFile=..\icons\catcha.ico
; Uninstall icon in Control Panel
UninstallDisplayIcon={app}\icons\catcha.ico

; 언어 감지 및 대화상자 설정
; uilanguage: Windows UI 언어를 감지하여 일치하는 언어로 자동 선택 (일치하는게 없으면 첫 번째 언어 사용)
LanguageDetectionMethod=uilanguage
; yes: 언어 선택 대화상자 표시 - Inno Setup EXE 직접 실행 시 언어 선택 가능
; no: 대화상자 없이 자동 감지된 언어로 진행 - MSIX 패키징 시 시스템 언어로 자동 설치 (권장)
; auto: 감지된 언어와 일치하는 언어가 [Languages]에 없으면 표시
ShowLanguageDialog=no

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "schinese"; MessagesFile: "Languages\ChineseSimplified.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

; 시작 메뉴 바로가기 - 각 언어별 메시지
Name: "startmenu"; Description: "시작 메뉴에 바로가기 만들기"; GroupDescription: "추가 작업:"; Flags: checkedonce; Languages: korean
Name: "startmenu"; Description: "Create Start Menu shortcut"; GroupDescription: "Additional tasks:"; Flags: checkedonce; Languages: english
Name: "startmenu"; Description: "スタートメニューにショートカットを作成"; GroupDescription: "追加タスク:"; Flags: checkedonce; Languages: japanese
Name: "startmenu"; Description: "创建开始菜单快捷方式"; GroupDescription: "附加任务:"; Flags: checkedonce; Languages: schinese
Name: "startmenu"; Description: "Crear acceso directo en el menú Inicio"; GroupDescription: "Tareas adicionales:"; Flags: checkedonce; Languages: spanish
Name: "startmenu"; Description: "Startmenü-Verknüpfung erstellen"; GroupDescription: "Zusätzliche Aufgaben:"; Flags: checkedonce; Languages: german
Name: "startmenu"; Description: "Créer un raccourci dans le menu Démarrer"; GroupDescription: "Tâches supplémentaires:"; Flags: checkedonce; Languages: french

[Files]
; Publish output (Framework-dependent)
Source: "..\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
; Include icons if needed at runtime
Source: "..\icons\*"; DestDir: "{app}\icons"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Icons]
; Start Menu shortcuts - 각 언어별 적절한 이름
Name: "{group}\캐치캡처"; Filename: "{app}\CatchCapture.exe"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: korean; Tasks: startmenu
Name: "{group}\CatchCapture"; Filename: "{app}\CatchCapture.exe"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: english; Tasks: startmenu
Name: "{group}\キャッチキャプチャ"; Filename: "{app}\CatchCapture.exe"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: japanese; Tasks: startmenu
Name: "{group}\捕捉截图"; Filename: "{app}\CatchCapture.exe"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: schinese; Tasks: startmenu
Name: "{group}\CatchCapture"; Filename: "{app}\CatchCapture.exe"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: spanish; Tasks: startmenu
Name: "{group}\CatchCapture"; Filename: "{app}\CatchCapture.exe"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: german; Tasks: startmenu
Name: "{group}\CatchCapture"; Filename: "{app}\CatchCapture.exe"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: french; Tasks: startmenu

; Desktop shortcuts - 각 언어별 적절한 이름
Name: "{userdesktop}\캐치캡처"; Filename: "{app}\CatchCapture.exe"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: korean; Tasks: desktopicon
Name: "{userdesktop}\CatchCapture"; Filename: "{app}\CatchCapture.exe"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: english; Tasks: desktopicon
Name: "{userdesktop}\CatchCapture"; Filename: "{app}\CatchCapture.exe"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: japanese; Tasks: desktopicon
Name: "{userdesktop}\捕捉截图"; Filename: "{app}\CatchCapture.exe"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: schinese; Tasks: desktopicon
Name: "{userdesktop}\CatchCapture"; Filename: "{app}\CatchCapture.exe"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: spanish; Tasks: desktopicon
Name: "{userdesktop}\CatchCapture"; Filename: "{app}\CatchCapture.exe"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: german; Tasks: desktopicon
Name: "{userdesktop}\CatchCapture"; Filename: "{app}\CatchCapture.exe"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: french; Tasks: desktopicon

[Run]
; 설치 완료 후 실행 옵션 - 각 언어별 메시지
Filename: "{app}\CatchCapture.exe"; Description: "설치가 끝나면 실행"; Flags: nowait postinstall skipifsilent; Languages: korean
Filename: "{app}\CatchCapture.exe"; Description: "Launch CatchCapture"; Flags: nowait postinstall skipifsilent; Languages: english
Filename: "{app}\CatchCapture.exe"; Description: "インストール後に起動"; Flags: nowait postinstall skipifsilent; Languages: japanese
Filename: "{app}\CatchCapture.exe"; Description: "安装后启动"; Flags: nowait postinstall skipifsilent; Languages: schinese
Filename: "{app}\CatchCapture.exe"; Description: "Ejecutar después de la instalación"; Flags: nowait postinstall skipifsilent; Languages: spanish
Filename: "{app}\CatchCapture.exe"; Description: "Nach der Installation starten"; Flags: nowait postinstall skipifsilent; Languages: german
Filename: "{app}\CatchCapture.exe"; Description: "Lancer après l'installation"; Flags: nowait postinstall skipifsilent; Languages: french

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

