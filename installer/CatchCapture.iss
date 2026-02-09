#define MyAppName "CatchCapture"
#define MyAppVersion "1.0.1"
#define MyAppPublisher "Ezupsoft"
#define MyAppExeName "CatchCapture.exe"
#define IsBeta "false"

; Inno Setup script for CatchCapture
; NOTE: Inno Setup uses .ico for icons. Convert icons\catcha.icns to .ico (e.g., icons\catcha.ico).

[Setup]
AppId={{A5A0FAF0-6D1F-4F4E-B2A6-9B8F0F5D2E31}}
#if IsBeta == "true"
  AppName={#MyAppName} Beta
  AppVersion={#MyAppVersion}-beta
  AppVerName={#MyAppName} {#MyAppVersion} Beta
#else
  AppName={#MyAppName}
  AppVersion={#MyAppVersion}
  AppVerName={#MyAppName} {#MyAppVersion}
#endif
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\{#MyAppName}
PrivilegesRequired=admin
#if IsBeta == "true"
  DefaultGroupName={#MyAppName} Beta
#else
  DefaultGroupName={#MyAppName}
#endif
DisableDirPage=no
DisableProgramGroupPage=no
#if IsBeta == "true"
  OutputBaseFilename=CatchCapture-Beta-Setup
#else
  OutputBaseFilename=CatchCapture-Setup
#endif
OutputDir=dist
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
CloseApplications=force
; Installer icon
SetupIconFile=..\icons\catcha.ico
; Uninstall icon in Control Panel
UninstallDisplayIcon={app}\{#MyAppExeName}

; 언어 감지 및 대화상자 설정
LanguageDetectionMethod=uilanguage
ShowLanguageDialog=yes

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "schinese"; MessagesFile: "Languages\ChineseSimplified.isl"
Name: "tchinese"; MessagesFile: "Languages\ChineseTraditional.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "indonesian"; MessagesFile: "Languages\Indonesian.isl"
Name: "thai"; MessagesFile: "Languages\Thai.isl"
Name: "vietnamese"; MessagesFile: "Languages\Vietnamese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startmenu"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; dotnet build/publish output - Use the relative path from the script
Source: "..\publish_folder\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
; Include icons if needed at runtime
Source: "..\icons\*"; DestDir: "{app}\icons"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

; .NET 8.0 Desktop Runtime 설치 파일 (Resources 폴더에서 가져오기)
Source: "Resources\windowsdesktop-runtime-8.0.11-win-x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

; FFmpeg (동영상 녹화/변환용)
Source: "Resources\ffmpeg.exe"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
; Start Menu shortcuts - 각 언어별 적절한 이름
#if IsBeta == "true"
  Name: "{group}\캐치캡처 Beta"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: korean; Tasks: startmenu
  Name: "{group}\{#MyAppName} Beta"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: english; Tasks: startmenu
  Name: "{group}\キャッチキャプチャ Beta"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: japanese; Tasks: startmenu
  Name: "{group}\捕捉截图 Beta"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: schinese; Tasks: startmenu
#else
  Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Tasks: startmenu
  Name: "{group}\キャッチキャプチャ"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: japanese; Tasks: startmenu
  Name: "{group}\捕捉截图"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: schinese; Tasks: startmenu
  Name: "{group}\捕捉截圖"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: tchinese; Tasks: startmenu
  Name: "{group}\캐치캡처"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: korean; Tasks: startmenu
#endif

; Desktop shortcuts - 각 언어별 적절한 이름
#if IsBeta == "true"
  Name: "{userdesktop}\캐치캡처 Beta"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: korean; Tasks: desktopicon
  Name: "{userdesktop}\{#MyAppName} Beta"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: english; Tasks: desktopicon
  Name: "{userdesktop}\キャッチキャプチャ Beta"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: japanese; Tasks: desktopicon
  Name: "{userdesktop}\捕捉截图 Beta"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: schinese; Tasks: desktopicon
#else
  Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Tasks: desktopicon
  Name: "{userdesktop}\キャッチキャプチャ"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: japanese; Tasks: desktopicon
  Name: "{userdesktop}\捕捉截图"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: schinese; Tasks: desktopicon
  Name: "{userdesktop}\捕捉截圖"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: tchinese; Tasks: desktopicon
  Name: "{userdesktop}\캐치캡처"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\icons\catcha.ico"; Languages: korean; Tasks: desktopicon
#endif

[Run]
; .NET 런타임 설치 (조용히 설치)
; /install /passive /norestart 옵션으로 사용자 개입 없이 설치
Filename: "{tmp}\windowsdesktop-runtime-8.0.11-win-x64.exe"; Parameters: "/install /passive /norestart"; StatusMsg: ".NET Desktop Runtime 8.0.11 설치 중..."; 

; 설치 완료 후 실행 옵션
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

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
