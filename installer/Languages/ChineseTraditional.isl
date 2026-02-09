; *** Inno Setup version 6.7.0+ Chinese Traditional messages ***
; Name: Anbang LI, anbangli@outlook.com
;
; Name: Enfong Tsao, nelson22768384@gmail.com
; Based on 5.5.3+ translations by Samuel Lee, Email: 751555749@qq.com
; Translation based on network resource
;
; Note: When translating this text, do not add periods (.) to the end of
; messages that didn't have them already, because on those messages Inno
; Setup adds the periods automatically (appending a period would result in
; two periods being displayed).
;
; Submit webpage: https://jrsoftware.org/files/istrans/

[LangOptions]
; The following three entries are very important. Be sure to read and 
; understand the '[LangOptions] section' topic in the help file.
LanguageName=繁體中文
; If Language Name display incorrect, uncomment next line
LanguageName=<7e41><9ad4><4e2d><6587>
; About LanguageID, to reference link:
; https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-lcid/a9eac961-e77d-41a6-90a5-ce1a8b0cdb9c
LanguageID=$0404
; About CodePage, to reference link:
; https://docs.microsoft.com/en-us/windows/win32/intl/code-page-identifiers
LanguageCodepage=950
; If the language you are translating to requires special font faces or
; sizes, uncomment any of the following entries and change them accordingly.
DialogFontName=Arial
DialogFontSize=10
;DialogFontBaseScaleWidth=7
;DialogFontBaseScaleHeight=15
WelcomeFontName=Segoe UI
WelcomeFontSize=14
;CopyrightFontName=Arial
;CopyrightFontSize=10

[Messages]

; *** Application titles
SetupAppTitle=安裝程式
SetupWindowTitle=%1 安裝程式
UninstallAppTitle=解除安裝
UninstallAppFullTitle=解除安裝 %1

; *** Misc. common
InformationTitle=訊息
ConfirmTitle=確認
ErrorTitle=錯誤

; *** SetupLdr messages
SetupLdrStartupMessage=這將會安裝 %1。您想要繼續嗎？
LdrCannotCreateTemp=無法建立暫存檔案。安裝程式將會結束。
LdrCannotExecTemp=無法執行暫存檔案。安裝程式將會結束。
HelpTextNote=

; *** Startup error messages
LastErrorMessage=%1%n%n錯誤 %2: %3
SetupFileMissing=安裝資料夾中遺失檔案 %1。請修正此問題或重新取得此軟體。
SetupFileCorrupt=安裝檔案已經損毀。請重新取得此軟體。
SetupFileCorruptOrWrongVer=安裝檔案已經損毀，或與安裝程式的版本不符。請重新取得此軟體。
InvalidParameter=某個無效的變量已被傳遞到了命令列:%n%n%1
SetupAlreadyRunning=安裝程式已經在執行。
WindowsVersionNotSupported=本安裝程式並不支援目前在電腦所運行的 Windows 版本。
WindowsServicePackRequired=本安裝程式需要 %1 Service Pack %2 或更新。
NotOnThisPlatform=這個程式無法在 %1 執行。
OnlyOnThisPlatform=這個程式必須在 %1 執行。
OnlyOnTheseArchitectures=這個程式只能在專門為以下處理器架構而設計的 Windows 上安裝:%n%n%1
WinVersionTooLowError=這個程式必須在 %1 版本 %2 或以上的系統執行。
WinVersionTooHighError=這個程式無法安裝在 %1 版本 %2 或以上的系統。
AdminPrivilegesRequired=您必須登入成系統管理員以安裝這個程式。
PowerUserPrivilegesRequired=您必須登入成具有系統管理員或 Power User 權限的使用者以安裝這個程式。
SetupAppRunningError=安裝程式偵測到 %1 正在執行。%n%n請關閉該程式後按 「確定」 繼續，或按 「取消」 離開。
UninstallAppRunningError=解除安裝程式偵測到 %1 正在執行。%n%n請關閉該程式後按 「確定」 繼續，或按 「取消」 離開。

; *** Startup questions
PrivilegesRequiredOverrideTitle=選擇安裝程式安裝模式
PrivilegesRequiredOverrideInstruction=選擇安裝模式
PrivilegesRequiredOverrideText1=可以為所有使用者安裝 %1 (需要系統管理權限)，或是僅為您安裝。
PrivilegesRequiredOverrideText2=可以僅為您安裝 %1，或是為所有使用者安裝 (需要系統管理權限)。
PrivilegesRequiredOverrideAllUsers=為所有使用者安裝 (&A)
PrivilegesRequiredOverrideAllUsersRecommended=為所有使用者安裝 (建議選項) (&A)
PrivilegesRequiredOverrideCurrentUser=僅為我安裝 (&M)
PrivilegesRequiredOverrideCurrentUserRecommended=僅為我安裝 (建議選項) (&M)

; *** Misc. errors
ErrorCreatingDir=安裝程式無法建立資料夾“%1”。
ErrorTooManyFilesInDir=無法在資料夾“%1”內建立檔案，因為資料夾內有太多的檔案。
