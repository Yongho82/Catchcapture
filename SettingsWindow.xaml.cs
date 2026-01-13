using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using CatchCapture.Models;
using CatchCapture.Utilities;
using System.Diagnostics;
using LocalizationManager = CatchCapture.Resources.LocalizationManager;

namespace CatchCapture
{
    public partial class SettingsWindow : Window
    {
        private Settings _settings = null!;
        private string _originalThemeMode = string.Empty;
        private string _originalThemeBg = string.Empty;
        private string _originalThemeFg = string.Empty;
        private string _currentPage = "Capture";
        private int[] _customColors = new int[16]; // 색상 대화상자의 사용자 지정 색상 저장
        private bool _isLoaded = false;
        private bool _suppressThemeUpdate = false;
        private System.Windows.Threading.DispatcherTimer? _applyThemeTimer;
        private System.Collections.Generic.HashSet<string> _loadedPages = new System.Collections.Generic.HashSet<string>();

        public SettingsWindow()
        {
            try
            {
                _suppressThemeUpdate = true;
                InitializeComponent();
                _settings = Settings.Load().Clone();
                UpdateUIText(); // 다국어 텍스트 적용
                // 언어 변경 이벤트 구독 (닫힐 때 해제)
                CatchCapture.Models.LocalizationManager.LanguageChanged += OnLanguageChanged;
                
                // Lazy Load the first page
                _loadedPages.Add("Capture");
                LoadCapturePage();
                
                // Store original theme for cancel revert
                _originalThemeMode = _settings.ThemeMode ?? "General";
                _originalThemeBg = _settings.ThemeBackgroundColor ?? "#FFFFFF";
                _originalThemeFg = _settings.ThemeTextColor ?? "#333333";

                // Ensure custom theme colors are initialized
                if (string.IsNullOrEmpty(_settings.CustomThemeBackgroundColor))
                    _settings.CustomThemeBackgroundColor = (_settings.ThemeMode == "Custom" ? _settings.ThemeBackgroundColor : "#FFFFFF") ?? "#FFFFFF";
                if (string.IsNullOrEmpty(_settings.CustomThemeTextColor))
                    _settings.CustomThemeTextColor = (_settings.ThemeMode == "Custom" ? _settings.ThemeTextColor : "#333333") ?? "#333333";
                
                _suppressThemeUpdate = false;
                
                HighlightNav(NavCapture, "Capture");
                _isLoaded = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsWindow Constructor Error: {ex.Message}");
                // 생성자에서 오류 발생 시 최소한 창은 열리거나 오류 메시지를 보여줘야 함
                CatchCapture.CustomMessageBox.Show("설정 창을 로드하는 중 오류가 발생했습니다: " + ex.Message, "오류", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        public void ShowNoteSettings()
        {
            HighlightNav(NavNote, "Note");
        }

        public void ShowHistorySettings()
        {
            HighlightNav(NavHistory, "History");
        }

        public void SelectPage(string page)
        {
            switch (page)
            {
                case "Capture": if (NavCapture != null) HighlightNav(NavCapture, "Capture"); break;
                case "Theme": if (NavTheme != null) HighlightNav(NavTheme, "Theme"); break;
                case "MenuEdit": if (NavMenuEdit != null) HighlightNav(NavMenuEdit, "MenuEdit"); break;
                case "Recording": if (NavRecording != null) HighlightNav(NavRecording, "Recording"); break;
                case "Note": if (NavNote != null) HighlightNav(NavNote, "Note"); break;
                case "System": if (NavSystem != null) HighlightNav(NavSystem, "System"); break;
                case "Hotkey": if (NavHotkey != null) HighlightNav(NavHotkey, "Hotkey"); break;
                case "History": if (NavHistory != null) HighlightNav(NavHistory, "History"); break;
            }
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            // 모든 텍스트 갱신
            UpdateUIText();
            // 메뉴 편집 항목 표시 이름도 갱신
            try
            {
                if (_menuItems != null)
                {
                    foreach (var item in _menuItems)
                    {
                        item.DisplayName = GetMenuItemDisplayName(item.Key);
                    }
                    MenuItemsListBox.Items.Refresh();
                }
                UpdateAvailableMenus();
            }
            catch { }
        }

private void UpdateUIText()
        {
            try
            {
                // 창 제목
                this.Title = LocalizationManager.GetString("Settings");
                
                // 사이드바
                if (SidebarGeneralText != null) SidebarGeneralText.Text = LocalizationManager.GetString("General");
                if (NavCapture != null) NavCapture.Content = LocalizationManager.GetString("CaptureSettings");
                if (NavMenuEdit != null) NavMenuEdit.Content = LocalizationManager.GetString("MenuEdit");
                if (NavRecording != null) NavRecording.Content = LocalizationManager.GetString("RecordingSettings");
                if (NavSystem != null) NavSystem.Content = LocalizationManager.GetString("SystemSettings");
                if (NavHotkey != null) NavHotkey.Content = LocalizationManager.GetString("HotkeySettings");
                
                // 메뉴 편집 페이지
                if (MenuEditSectionTitle != null) MenuEditSectionTitle.Text = LocalizationManager.GetString("MenuEdit");
                if (MenuEditGuideText != null) MenuEditGuideText.Text = LocalizationManager.GetString("MenuEditGuide");
                if (AddMenuButton != null) AddMenuButton.Content = "+ " + LocalizationManager.GetString("BtnAdd"); 
                
                // 앱 이름과 하단 정보
                if (SidebarAppNameText != null) SidebarAppNameText.Text = LocalizationManager.GetString("AppTitle");
                if (VersionText != null) VersionText.Text = $"{LocalizationManager.GetString("Version")} 1.0.0";
                if (WebsiteIcon != null) WebsiteIcon.ToolTip = LocalizationManager.GetString("VisitHomepage");
                if (RestoreDefaultsText != null) RestoreDefaultsText.Text = LocalizationManager.GetString("RestoreDefaults");
                if (PrivacyPolicyText != null) PrivacyPolicyText.Text = LocalizationManager.GetString("PrivacyPolicy");
                
                // 캡처 페이지
                if (CaptureSectionTitle != null) CaptureSectionTitle.Text = LocalizationManager.GetString("CaptureSettings");
                if (SaveSettingsGroup != null) SaveSettingsGroup.Header = LocalizationManager.GetString("SaveSettings");
                if (SavePathText != null) SavePathText.Text = LocalizationManager.GetString("SavePath");
                if (BtnBrowse != null) BtnBrowse.Content = LocalizationManager.GetString("Change");
                if (FileFormatText != null) FileFormatText.Text = LocalizationManager.GetString("FileFormat");
                if (QualityText != null) QualityText.Text = LocalizationManager.GetString("Quality");
                if (OptionsGroup != null) OptionsGroup.Header = LocalizationManager.GetString("Options");
                if (ChkAutoSave != null) ChkAutoSave.Content = LocalizationManager.GetString("SettingsAutoSaveDesc");
                if (ChkAutoCopy != null) ChkAutoCopy.Content = LocalizationManager.GetString("SettingsAutoCopyDesc");
                if (ChkShowPreview != null) ChkShowPreview.Content = LocalizationManager.GetString("SettingsShowPreviewDesc");
                if (ChkShowMagnifier != null) ChkShowMagnifier.Content = LocalizationManager.GetString("SettingsShowMagnifierDesc");
                if (ChkShowColorPalette != null) ChkShowColorPalette.Content = LocalizationManager.GetString("SettingsShowColorPaletteDesc");
                
                // 캡처 방식
                if (CaptureModeGroup != null) CaptureModeGroup.Header = LocalizationManager.GetString("CaptureModeTitle");
                if (RbCaptureOverlay != null) RbCaptureOverlay.Content = LocalizationManager.GetString("CaptureModeOverlay");
                if (RbCaptureStatic != null) RbCaptureStatic.Content = LocalizationManager.GetString("CaptureModeStatic");
                if (TxtCaptureModeDesc != null) TxtCaptureModeDesc.Text = LocalizationManager.GetString("CaptureModeDesc");
                
                // 엣지 캡처 설정
                if (EdgeCaptureSettingsGroup != null) EdgeCaptureSettingsGroup.Header = LocalizationManager.GetString("EdgeCaptureSettings");
                if (EdgePresetLabel != null) EdgePresetLabel.Text = LocalizationManager.GetString("Preset");
                if (CboEdgePreset != null)
                {
                    foreach (ComboBoxItem item in CboEdgePreset.Items)
                    {
                        string tag = item.Tag?.ToString() ?? "";
                        item.Content = tag switch
                        {
                            "1" => LocalizationManager.GetString("EdgeLevel1"),
                            "2" => LocalizationManager.GetString("EdgeLevel2"),
                            "3" => LocalizationManager.GetString("EdgeLevel3"),
                            "4" => LocalizationManager.GetString("EdgeLevel4"),
                            "5" => LocalizationManager.GetString("EdgeLevel5"),
                            _ => item.Content
                        };
                    }
                }

                // 파일명 설정 & 폴더 분류 설정 UI
                if (FileNameSettingsGroup != null) FileNameSettingsGroup.Header = LocalizationManager.GetString("FileNameSettings");
                if (PreviewLabel != null) PreviewLabel.Text = LocalizationManager.GetString("FileNamePreview") + " : ";
                if (FolderGroupingGroup != null) FolderGroupingGroup.Header = LocalizationManager.GetString("FolderGrouping");
                
                if (RbGroupNone != null) RbGroupNone.Content = LocalizationManager.GetString("GroupingNone");
                if (RbGroupMonthly != null) RbGroupMonthly.Content = LocalizationManager.GetString("GroupingMonthly");
                if (RbGroupQuarterly != null) RbGroupQuarterly.Content = LocalizationManager.GetString("GroupingQuarterly");
                if (RbGroupYearly != null) RbGroupYearly.Content = LocalizationManager.GetString("GroupingYearly");
                
                // 콤보박스 아이템 (동적 생성)
                if (CboFileNamePresets != null)
                {
                     string currentTag = "Default";
                     if (CboFileNamePresets.SelectedItem is ComboBoxItem sell && sell.Tag is string t) currentTag = t;
                     
                     CboFileNamePresets.Items.Clear();

                     void AddItem(string content, string tag)
                     {
                         var item = new ComboBoxItem { Content = content, Tag = tag };
                         CboFileNamePresets.Items.Add(item);
                         if (tag == currentTag) CboFileNamePresets.SelectedItem = item;
                     }

                     AddItem(LocalizationManager.GetString("FileNamePreset_Default"), "Default");
                     AddItem(LocalizationManager.GetString("FileNamePreset_Simple"), "Simple");
                     AddItem(LocalizationManager.GetString("FileNamePreset_Timestamp"), "Timestamp");
                     AddItem(LocalizationManager.GetString("FileNamePreset_App"), "AppDate");
                     AddItem(LocalizationManager.GetString("FileNamePreset_Title"), "TitleDate");
                     
                     if (CboFileNamePresets.SelectedIndex == -1 && CboFileNamePresets.Items.Count > 0)
                          CboFileNamePresets.SelectedIndex = 0;
                }
                
                UpdateFileNamePreview();
                
                // 단축키 페이지
                if (HotkeySectionTitle != null) HotkeySectionTitle.Text = LocalizationManager.GetString("HotkeySettings");
                if (PrintScreenGroup != null) PrintScreenGroup.Header = LocalizationManager.GetString("UsePrintScreen");
                if (ChkUsePrintScreen != null) ChkUsePrintScreen.Content = LocalizationManager.GetString("UsePrintScreen");
                
                // Print Screen 액션 항목
                if (CboPrintScreenAction != null)
                {
                    string? currentActionTag = null;
                    if (CboPrintScreenAction.SelectedItem is ComboBoxItem selectedAction)
                    {
                        currentActionTag = selectedAction.Tag as string ?? null;
                    }
                    var items = new[]
                    {
                        (key: "AreaCapture", tag: "영역 캡처"),
                        (key: "DelayCapture", tag: "지연 캡처"),
                        (key: "RealTimeCapture", tag: "실시간 캡처"),
                        (key: "MultiCapture", tag: "다중 캡처"),
                        (key: "FullScreen", tag: "전체화면"),
                        (key: "DesignatedCapture", tag: "지정 캡처"),
                        (key: "WindowCapture", tag: "창 캡처"),
                        (key: "ElementCapture", tag: "단위 캡처"),
                        (key: "ScrollCapture", tag: "스크롤 캡처"),
                        (key: "OcrCapture", tag: "OCR 캡처"),
                        (key: "ScreenRecording", tag: "동영상 녹화"),
                        (key: "EdgeCapture", tag: "엣지 캡처"),
                    };
                    
                    CboPrintScreenAction.Items.Clear();
                    foreach (var (key, tag) in items)
                    {
                        var item = new ComboBoxItem
                        {
                            Content = LocalizationManager.GetString(key),
                            Tag = tag
                        };
                        CboPrintScreenAction.Items.Add(item);
                        
                        if (tag == currentActionTag)
                        {
                            CboPrintScreenAction.SelectedItem = item;
                        }
                    }
                    if (CboPrintScreenAction.SelectedItem == null && CboPrintScreenAction.Items.Count > 0)
                    {
                         CboPrintScreenAction.SelectedIndex = 0;
                    }
                }
                
                // 단축키 체크박스 라벨
                if (HkRegionEnabled != null) HkRegionEnabled.Content = LocalizationManager.GetString("AreaCapture");
                if (HkDelayEnabled != null) HkDelayEnabled.Content = LocalizationManager.GetString("DelayCapture");
                if (HkRealTimeEnabled != null) HkRealTimeEnabled.Content = LocalizationManager.GetString("RealTimeCapture");
                if (HkMultiEnabled != null) HkMultiEnabled.Content = LocalizationManager.GetString("MultiCapture");
                if (HkFullEnabled != null) HkFullEnabled.Content = LocalizationManager.GetString("FullScreen");
                if (HkDesignatedEnabled != null) HkDesignatedEnabled.Content = LocalizationManager.GetString("DesignatedCapture");
                if (HkWindowCaptureEnabled != null) HkWindowCaptureEnabled.Content = LocalizationManager.GetString("WindowCapture");
                if (HkElementCaptureEnabled != null) HkElementCaptureEnabled.Content = LocalizationManager.GetString("ElementCapture");
                if (HkScrollCaptureEnabled != null) HkScrollCaptureEnabled.Content = LocalizationManager.GetString("ScrollCapture");
                if (HkOcrCaptureEnabled != null) HkOcrCaptureEnabled.Content = LocalizationManager.GetString("OcrCapture");
                if (HkScreenRecordEnabled != null) HkScreenRecordEnabled.Content = LocalizationManager.GetString("ScreenRecording");
                if (HkSimpleModeEnabled != null) HkSimpleModeEnabled.Content = LocalizationManager.GetString("SimpleMode");
                if (HkTrayModeEnabled != null) HkTrayModeEnabled.Content = LocalizationManager.GetString("TrayMode");
                if (HkSaveAllEnabled != null) HkSaveAllEnabled.Content = LocalizationManager.GetString("SaveAll");
                if (HkDeleteAllEnabled != null) HkDeleteAllEnabled.Content = LocalizationManager.GetString("DeleteAll");
                if (HkOpenSettingsEnabled != null) HkOpenSettingsEnabled.Content = LocalizationManager.GetString("OpenSettings");
                if (HkOpenEditorEnabled != null) HkOpenEditorEnabled.Content = LocalizationManager.GetString("OpenEditor");
                if (HkOpenNoteEnabled != null) HkOpenNoteEnabled.Content = LocalizationManager.GetString("OpenMyNote");
                if (HkEdgeEnabled != null) HkEdgeEnabled.Content = LocalizationManager.GetString("EdgeCapture");
                
                // 시스템 페이지
                if (SystemSectionTitle != null) SystemSectionTitle.Text = LocalizationManager.GetString("SystemSettings");
                if (StartupGroup != null) StartupGroup.Header = LocalizationManager.GetString("StartupMode");
                if (StartWithWindowsCheckBox != null) StartWithWindowsCheckBox.Content = LocalizationManager.GetString("StartWithWindows");
                if (RunAsAdminCheckBox != null) RunAsAdminCheckBox.Content = LocalizationManager.GetString("StartAsAdmin");

                if (StartupModeText != null) StartupModeText.Text = LocalizationManager.GetString("StartupMode");
                if (StartupModeTrayRadio != null) StartupModeTrayRadio.Content = LocalizationManager.GetString("StartInTray");
                if (StartupModeNormalRadio != null) StartupModeNormalRadio.Content = LocalizationManager.GetString("StartInNormal");
                if (StartupModeSimpleRadio != null) StartupModeSimpleRadio.Content = LocalizationManager.GetString("StartInSimple");

                
                // 언어 페이지
                if (LanguageGroup != null) LanguageGroup.Header = LocalizationManager.GetString("LanguageSettings");
                if (LanguageLabelText != null) LanguageLabelText.Text = LocalizationManager.GetString("LanguageLabel");
                if (LanguageRestartNotice != null) LanguageRestartNotice.Text = LocalizationManager.GetString("RestartRequired");

                // 테마 페이지
                if (NavTheme != null) NavTheme.Content = LocalizationManager.GetString("ThemeSettings");
                if (ThemeSectionTitle != null) ThemeSectionTitle.Text = LocalizationManager.GetString("ThemeSettings");
                if (ThemePresetGroup != null) ThemePresetGroup.Header = LocalizationManager.GetString("ThemePreset");
                if (ThemeGeneral != null) ThemeGeneral.Content = LocalizationManager.GetString("ThemeGeneral");
                if (ThemeDark != null) ThemeDark.Content = LocalizationManager.GetString("ThemeDark");
                if (ThemeLight != null) ThemeLight.Content = LocalizationManager.GetString("ThemeLight");
                if (ThemeBlue != null) ThemeBlue.Content = LocalizationManager.GetString("ThemeBlue");
                if (ThemeCustom != null) ThemeCustom.Content = LocalizationManager.GetString("ThemeCustom");
                if (ThemeBgLabel != null) ThemeBgLabel.Text = LocalizationManager.GetString("ThemeBgColor");
                if (ThemeTextLabel != null) ThemeTextLabel.Text = LocalizationManager.GetString("ThemeTextColor");

                // 캡처 라인 설정 (Theme 페이지 하단)
                if (CaptureLineSettingsGroup != null) CaptureLineSettingsGroup.Header = LocalizationManager.GetString("CaptureLineSettings");
                if (OverlayBgLabel != null) OverlayBgLabel.Text = LocalizationManager.GetString("OverlayBgColor");
                if (LineStyleLabel != null) LineStyleLabel.Text = LocalizationManager.GetString("LineStyle");
                if (LineThicknessLabel != null) LineThicknessLabel.Text = LocalizationManager.GetString("LineThickness");
                if (LineColorLabel != null) LineColorLabel.Text = LocalizationManager.GetString("LineColor");
                if (CaptureSettingsGuide != null) CaptureSettingsGuide.Text = LocalizationManager.GetString("CaptureSettingsGuide");

                // 라인 스타일 개별 텍스트
                if (TxtSolid != null) TxtSolid.Text = LocalizationManager.GetString("SolidLine");
                if (TxtDash != null) TxtDash.Text = LocalizationManager.GetString("DashLine");
                if (TxtDot != null) TxtDot.Text = LocalizationManager.GetString("DotLine");
                if (TxtDashDot != null) TxtDashDot.Text = LocalizationManager.GetString("DashDotLine");

                // 녹화 페이지
                if (RecordingSectionTitle != null) RecordingSectionTitle.Text = LocalizationManager.GetString("RecordingSettings");
                if (RecBasicGroup != null) RecBasicGroup.Header = LocalizationManager.GetString("BasicSettings");
                if (RecFormatLabel != null) RecFormatLabel.Text = LocalizationManager.GetString("FileFormat");
                if (RecQualityLabel != null) RecQualityLabel.Text = LocalizationManager.GetString("Quality") + "(MP4)";
                if (RecFpsLabel != null) RecFpsLabel.Text = LocalizationManager.GetString("FrameRate") + "(MP4)";
                if (ChkRecMouse != null) ChkRecMouse.Content = LocalizationManager.GetString("ShowMouseCursor");
                if (RecHotkeyGroup != null) RecHotkeyGroup.Header = LocalizationManager.GetString("RecordingStartStopHotkey");
                if (HkRecStartStopEnabled != null) HkRecStartStopEnabled.Content = LocalizationManager.GetString("RecordStartStop");
                
                // 노트 페이지
                if (NavNote != null) NavNote.Content = LocalizationManager.GetString("NoteSettings");
                if (NoteSectionTitle != null) NoteSectionTitle.Text = LocalizationManager.GetString("NoteSettings");
                if (NoteStorageGroup != null) NoteStorageGroup.Header = LocalizationManager.GetString("NoteStorage");
                if (NotePathLabel != null) NotePathLabel.Text = LocalizationManager.GetString("NotePath");
                if (NotePathDesc != null) NotePathDesc.Text = LocalizationManager.GetString("NotePathDesc");
                if (BtnBrowseNoteFolder != null) 
                {
                    BtnBrowseNoteFolder.Content = LocalizationManager.GetString("Change");
                    BtnBrowseNoteFolder.ToolTip = LocalizationManager.GetString("Tip4");
                }
                if (BtnOpenNoteFolder != null) BtnOpenNoteFolder.Content = LocalizationManager.GetString("OpenFolder");
                if (BtnCloudTip != null) BtnCloudTip.Content = LocalizationManager.GetString("RemoteStorage");
                if (TxtRemoteStorageWarning != null) TxtRemoteStorageWarning.Text = LocalizationManager.GetString("RemoteStorageWarning");
                if (TxtResetNote != null) TxtResetNote.Text = LocalizationManager.GetString("InitializeReset");

                
                if (NoteBackupGroup != null) NoteBackupGroup.Header = LocalizationManager.GetString("BackupRestore");
                if (BtnExportBackup != null) BtnExportBackup.Content = LocalizationManager.GetString("ExportBackup");
                if (BtnImportBackup != null) BtnImportBackup.Content = LocalizationManager.GetString("ImportBackup");

                // 노트 파일명 설정 로컬라이징 추가
                if (NoteFileNameSettingsGroup != null) NoteFileNameSettingsGroup.Header = LocalizationManager.GetString("FileNameSettings");
                if (NotePreviewLabel != null) NotePreviewLabel.Text = LocalizationManager.GetString("FileNamePreview") + " : ";
                
                if (CboNoteFileNamePresets != null)
                {
                    string currentTag = "Default";
                    if (CboNoteFileNamePresets.SelectedItem is ComboBoxItem sell && sell.Tag is string t) currentTag = t;
                    
                    CboNoteFileNamePresets.Items.Clear();
                    void AddItemNote(string content, string tag)
                    {
                        var item = new ComboBoxItem { Content = content, Tag = tag };
                        CboNoteFileNamePresets.Items.Add(item);
                        if (tag == currentTag) CboNoteFileNamePresets.SelectedItem = item;
                    }

                    AddItemNote(LocalizationManager.GetString("FileNamePreset_Default"), "Default");
                    AddItemNote(LocalizationManager.GetString("FileNamePreset_Simple"), "Simple");
                    AddItemNote(LocalizationManager.GetString("FileNamePreset_Timestamp"), "Timestamp");

                    if (CboNoteFileNamePresets.SelectedIndex == -1 && CboNoteFileNamePresets.Items.Count > 0)
                        CboNoteFileNamePresets.SelectedIndex = 0;
                }

                // 노트 폴더 분류 설정 로컬라이징 추가
                if (NoteFolderGroupingGroup != null) NoteFolderGroupingGroup.Header = LocalizationManager.GetString("FolderGrouping");
                if (RbNoteGroupNone != null) RbNoteGroupNone.Content = LocalizationManager.GetString("GroupingNone");
                if (RbNoteGroupMonthly != null) RbNoteGroupMonthly.Content = LocalizationManager.GetString("GroupingMonthly");
                if (RbNoteGroupQuarterly != null) RbNoteGroupQuarterly.Content = LocalizationManager.GetString("GroupingQuarterly");
                if (RbNoteGroupYearly != null) RbNoteGroupYearly.Content = LocalizationManager.GetString("GroupingYearly");

                if (NoteSecurityGroup != null) NoteSecurityGroup.Header = LocalizationManager.GetString("NoteSecurity");
                if (ChkEnableNotePassword != null) ChkEnableNotePassword.Content = LocalizationManager.GetString("EnableNotePassword");
                if (BtnSetNotePassword != null) BtnSetNotePassword.Content = LocalizationManager.GetString("SetChangePassword");
                if (PasswordWarningMsg != null) PasswordWarningMsg.Text = LocalizationManager.GetString("PasswordWarning");

                if (NoteOptimizeGroup != null) NoteOptimizeGroup.Header = LocalizationManager.GetString("SaveSettings");
                if (NoteFileFormatText != null) NoteFileFormatText.Text = LocalizationManager.GetString("FileFormat");
                if (NoteQualityLabel != null) NoteQualityLabel.Text = LocalizationManager.GetString("Quality");

                if (NoteTrashGroup != null) NoteTrashGroup.Header = LocalizationManager.GetString("TrashSettings");
                if (TrashRetentionLabel != null) TrashRetentionLabel.Text = LocalizationManager.GetString("TrashRetentionPeriod");
                if (TrashRetentionNotice != null) TrashRetentionNotice.Text = LocalizationManager.GetString("TrashRetentionNotice");

                if (CboTrashRetention != null)
                {
                    foreach (ComboBoxItem item in CboTrashRetention.Items)
                    {
                        string? tag = item.Tag as string;
                        if (tag == "0") item.Content = LocalizationManager.GetString("RetentionPermanent");
                        else if (tag == "1") item.Content = "1" + LocalizationManager.GetString("Days");
                        else if (tag == "3") item.Content = "3" + LocalizationManager.GetString("Days");
                        else if (tag == "7") item.Content = "7" + LocalizationManager.GetString("Days");
                        else if (tag == "15") item.Content = "15" + LocalizationManager.GetString("Days");
                        else if (tag == "30") item.Content = "30" + LocalizationManager.GetString("Days");
                        else if (tag == "60") item.Content = "60" + LocalizationManager.GetString("Days");
                        else if (tag == "90") item.Content = "90" + LocalizationManager.GetString("Days");
                    }
                }

                // 녹화 품질 콤보박스 아이템 로컬라이징
                if (CboRecQuality != null)
                {
                    foreach (ComboBoxItem item in CboRecQuality.Items)
                    {
                        string? tag = item.Tag as string;
                        if (tag == "High") item.Content = LocalizationManager.GetString("QualityHigh");
                        else if (tag == "Medium") item.Content = LocalizationManager.GetString("QualityMedium");
                        else if (tag == "Low") item.Content = LocalizationManager.GetString("QualityLow");
                    }
                }

                // 하단 버튼
                if (CancelButton != null) CancelButton.Content = LocalizationManager.GetString("BtnCancel");
                if (ApplyButton != null) ApplyButton.Content = LocalizationManager.GetString("Apply");
                if (SaveButton != null) SaveButton.Content = LocalizationManager.GetString("Save");
                if (PageDefaultButton != null) PageDefaultButton.Content = LocalizationManager.GetString("Default");
                
                // Sidebar Bottom Links
                if (RestoreDefaultsText != null) RestoreDefaultsText.Text = LocalizationManager.GetString("RestoreDefaults");
                if (PrivacyPolicyText != null) PrivacyPolicyText.Text = LocalizationManager.GetString("PrivacyPolicy");
                if (WebsiteIcon != null) WebsiteIcon.ToolTip = LocalizationManager.GetString("VisitHomepage");

                // History Page
                if (HistorySectionTitle != null) HistorySectionTitle.Text = LocalizationManager.GetString("HistorySettings");
                if (HistorySaveSettingsGroup != null) HistorySaveSettingsGroup.Header = LocalizationManager.GetString("SaveSettings");
                if (HistoryBackupGroup != null) HistoryBackupGroup.Header = LocalizationManager.GetString("BackupRestore");
                if (HistoryAutoGroup != null) HistoryAutoGroup.Header = LocalizationManager.GetString("AutoManagement");
                if (HistoryTrashGroup != null) HistoryTrashGroup.Header = LocalizationManager.GetString("TrashSettings");
                
                if (BtnHistoryExport != null) BtnHistoryExport.Content = LocalizationManager.GetString("ExportBackup");
                if (BtnHistoryImport != null) BtnHistoryImport.Content = LocalizationManager.GetString("ImportBackup");
                if (BtnDbOptimize != null) BtnDbOptimize.Content = LocalizationManager.GetString("DbOptimize");
                
                if (CboHistoryRetention != null)
                {
                    foreach (ComboBoxItem item in CboHistoryRetention.Items)
                    {
                        string? tag = item.Tag as string;
                        if (tag == "0") item.Content = LocalizationManager.GetString("RetentionPermanent");
                        else if (tag == "30") item.Content = "30" + LocalizationManager.GetString("Days");
                        else if (tag == "90") item.Content = "90" + LocalizationManager.GetString("Days");
                    }
                }

                if (CboHistoryTrashRetention != null)
                {
                    foreach (ComboBoxItem item in CboHistoryTrashRetention.Items)
                    {
                        string? tag = item.Tag as string;
                        if (tag == "0") item.Content = LocalizationManager.GetString("RetentionPermanent");
                        else if (tag != null && int.TryParse(tag, out int days)) item.Content = days.ToString() + LocalizationManager.GetString("Days");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateUIText error: {ex.Message}");
                // 에러가 발생해도 프로그램이 크래시되지 않도록 합니다
            }
        }

        private void InitKeyComboBoxes()
        {
            var keys = new System.Collections.Generic.List<string>();
            // A-Z
            for (char c = 'A'; c <= 'Z'; c++) keys.Add(c.ToString());
            // 0-9
            for (char c = '0'; c <= '9'; c++) keys.Add(c.ToString());
            // F1-F12
            for (int i = 1; i <= 12; i++) keys.Add($"F{i}");

            // 여기에 HkRealTimeKey와 HkMultiKey를 추가했습니다
            var boxes = new[] { 
                HkRegionKey, HkDelayKey, HkRealTimeKey, HkMultiKey, HkFullKey, HkDesignatedKey,
                HkWindowCaptureKey, HkElementCaptureKey, HkScrollCaptureKey,
                HkOcrCaptureKey, HkScreenRecordKey,
                HkSimpleModeKey, HkTrayModeKey,
                HkSaveAllKey, HkDeleteAllKey, HkOpenSettingsKey, HkOpenEditorKey,
                HkOpenNoteKey, HkRecStartStopKey, HkEdgeKey
            };

            foreach (var box in boxes)
            {
                box.ItemsSource = keys;
            }
        }

private void InitLanguageComboBox()
        {
            if (LanguageComboBox == null) return;
            LanguageComboBox.Items.Clear();
            foreach (var (code, name) in LocalizationManager.SupportedLanguages)
            {
                LanguageComboBox.Items.Add(new ComboBoxItem { Content = name, Tag = code });
            }
            // SelectionChanged 핸들러 연결 (중복 연결 방지)
            LanguageComboBox.SelectionChanged -= LanguageComboBox_SelectionChanged;
            LanguageComboBox.SelectionChanged += LanguageComboBox_SelectionChanged;
        }

        private void HighlightNav(Button btn, string tag)
        {
            _currentPage = tag;

            // Lazy load the incoming page
            EnsurePageLoaded(tag);

            NavCapture.FontWeight = tag == "Capture" ? FontWeights.Bold : FontWeights.Normal;
            NavTheme.FontWeight = tag == "Theme" ? FontWeights.Bold : FontWeights.Normal;
            NavMenuEdit.FontWeight = tag == "MenuEdit" ? FontWeights.Bold : FontWeights.Normal;
            NavRecording.FontWeight = tag == "Recording" ? FontWeights.Bold : FontWeights.Normal;
            NavNote.FontWeight = tag == "Note" ? FontWeights.Bold : FontWeights.Normal;
            NavSystem.FontWeight = tag == "System" ? FontWeights.Bold : FontWeights.Normal;
            NavHotkey.FontWeight = tag == "Hotkey" ? FontWeights.Bold : FontWeights.Normal;
            NavHistory.FontWeight = tag == "History" ? FontWeights.Bold : FontWeights.Normal;
            
            PageCapture.Visibility = tag == "Capture" ? Visibility.Visible : Visibility.Collapsed;
            PageTheme.Visibility = tag == "Theme" ? Visibility.Visible : Visibility.Collapsed;
            PageMenuEdit.Visibility = tag == "MenuEdit" ? Visibility.Visible : Visibility.Collapsed;
            PageRecording.Visibility = tag == "Recording" ? Visibility.Visible : Visibility.Collapsed;
            PageNote.Visibility = tag == "Note" ? Visibility.Visible : Visibility.Collapsed;
            PageSystem.Visibility = tag == "System" ? Visibility.Visible : Visibility.Collapsed;
            PageHotkey.Visibility = tag == "Hotkey" ? Visibility.Visible : Visibility.Collapsed;
            PageHistory.Visibility = tag == "History" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void EnsurePageLoaded(string tag)
        {
            if (_loadedPages.Contains(tag)) return;
            _loadedPages.Add(tag);

            bool prevSuppress = _suppressThemeUpdate;
            _suppressThemeUpdate = true;
            try
            {
                switch (tag)
                {
                    case "Capture": LoadCapturePage(); break;
                    case "Theme": LoadThemePage(); break;
                    case "MenuEdit": LoadMenuEditPage(); break;
                    case "Recording": LoadRecordingPage(); break;
                    case "Note": LoadNotePage(); break;
                    case "System": 
                        InitLanguageComboBox();
                        LoadSystemPage(); 
                        break;
                    case "Hotkey": 
                        InitKeyComboBoxes();
                        LoadHotkeysPage(); 
                        break;
                    case "History": LoadHistoryPage(); break;
                }
            }
            finally
            {
                _suppressThemeUpdate = prevSuppress;
            }
        }

        private void LoadThemePage()
        {
            if (ThemeDark != null && _settings.ThemeMode == "Dark") ThemeDark.IsChecked = true;
            else if (ThemeLight != null && _settings.ThemeMode == "Light") ThemeLight.IsChecked = true;
            else if (ThemeBlue != null && _settings.ThemeMode == "Blue") ThemeBlue.IsChecked = true;
            else if (ThemeCustom != null && _settings.ThemeMode == "Custom") ThemeCustom.IsChecked = true;
            else if (ThemeGeneral != null) ThemeGeneral.IsChecked = true;

            // Load Capture Line Settings
            if (CboLineStyle != null)
            {
                foreach (ComboBoxItem item in CboLineStyle.Items)
                {
                    if (item.Tag?.ToString() == _settings.CaptureLineStyle)
                    {
                        CboLineStyle.SelectedItem = item;
                        break;
                    }
                }
                if (CboLineStyle.SelectedItem == null) CboLineStyle.SelectedIndex = 1; // Default: Dash
            }

            if (CboLineThickness != null)
            {
                foreach (ComboBoxItem item in CboLineThickness.Items)
                {
                    if (item.Tag?.ToString() == _settings.CaptureLineThickness.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
                    {
                        CboLineThickness.SelectedItem = item;
                        break;
                    }
                }
                if (CboLineThickness.SelectedItem == null) CboLineThickness.SelectedIndex = 1; // Default: 1.0
            }

            // Load Overlay Opacity
            if (SldOverlayOpacity != null)
            {
                try
                {
                    string colorStr = _settings.OverlayBackgroundColor ?? "#8C000000";
                    if (colorStr.Length == 9)
                    {
                        byte alpha = Convert.ToByte(colorStr.Substring(1, 2), 16);
                        SldOverlayOpacity.Value = alpha;
                    }
                    else
                    {
                        SldOverlayOpacity.Value = 140;
                    }
                }
                catch { SldOverlayOpacity.Value = 140; }
            }

            UpdateColorPreviews();
        }

        private void UpdateColorPreviews()
        {
            try
            {
                if (PreviewThemeBgColor != null)
                {
                    string bgColor = string.IsNullOrEmpty(_settings.ThemeBackgroundColor) ? "#FFFFFF" : _settings.ThemeBackgroundColor;
                    PreviewThemeBgColor.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgColor));
                }
            }
            catch { if (PreviewThemeBgColor != null) PreviewThemeBgColor.Background = Brushes.White; }

            try
            {
                if (PreviewThemeTextColor != null)
                {
                    string textColor = string.IsNullOrEmpty(_settings.ThemeTextColor) ? "#333333" : _settings.ThemeTextColor;
                    PreviewThemeTextColor.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(textColor));
                }
            }
            catch { if (PreviewThemeTextColor != null) PreviewThemeTextColor.Background = Brushes.Black; }

            try
            {
                if (PreviewOverlayBgColor != null)
                {
                    string color = string.IsNullOrEmpty(_settings.OverlayBackgroundColor) ? "#8C000000" : _settings.OverlayBackgroundColor;
                    PreviewOverlayBgColor.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
                }
            }
            catch { if (PreviewOverlayBgColor != null) PreviewOverlayBgColor.Background = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)); }

            try
            {
                if (PreviewLineColor != null)
                {
                    string color = string.IsNullOrEmpty(_settings.CaptureLineColor) ? "#FF0000" : _settings.CaptureLineColor;
                    PreviewLineColor.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
                }
            }
            catch { if (PreviewLineColor != null) PreviewLineColor.Background = Brushes.Red; }
            
            // 사용자 지정 모드일 때만 색상 선택 패널 활성화
            if (CustomColorPanel != null && ThemeCustom != null)
            {
                CustomColorPanel.Visibility = ThemeCustom.IsChecked == true ? Visibility.Visible : Visibility.Hidden;
            }

            // Apply real-time preview to the whole app with debouncing
            if (!_suppressThemeUpdate)
            {
                if (_applyThemeTimer == null)
                {
                    _applyThemeTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
                    _applyThemeTimer.Tick += (s, ev) => 
                    {
                        _applyThemeTimer.Stop();
                        App.ApplyTheme(_settings);
                    };
                }
                _applyThemeTimer.Stop();
                _applyThemeTimer.Start();
            }
        }

        private void Theme_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.IsChecked == true && rb.Tag is string info)
            {
                _settings.ThemeMode = info;
                
                // Only update default colors if explicitly checked by user (not during initialization)
                if (_isLoaded)
                {
                    if (info == "General")
                    {
                        _settings.ThemeBackgroundColor = "#FFFFFF";
                        _settings.ThemeTextColor = "#333333";
                    }
                    else if (info == "Dark")
                    {
                        _settings.ThemeBackgroundColor = "#1E1E1E"; // More intense dark
                        _settings.ThemeTextColor = "#CCCCCC";
                    }
                    else if (info == "Light")
                    {
                        _settings.ThemeBackgroundColor = "#F5F5F7";
                        _settings.ThemeTextColor = "#333333";
                    }
                    else if (info == "Blue")
                    {
                        _settings.ThemeBackgroundColor = "#E3F2FD";
                        _settings.ThemeTextColor = "#0d47a1";
                    }
                    else if (info == "Custom")
                    {
                        // Restore saved custom colors
                        _settings.ThemeBackgroundColor = _settings.CustomThemeBackgroundColor ?? "#FFFFFF";
                        _settings.ThemeTextColor = _settings.CustomThemeTextColor ?? "#333333";
                    }
                }
                
                UpdateColorPreviews();
            }
        }

        private void SldOverlayOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_settings == null || SldOverlayOpacity == null) return;
            
            string currentHex = _settings.OverlayBackgroundColor ?? "#8C000000";
            if (currentHex.Length == 9)
            {
                byte alpha = (byte)e.NewValue;
                _settings.OverlayBackgroundColor = $"#{alpha:X2}{currentHex.Substring(3)}";
                UpdateColorPreviews();
            }
            else if (currentHex.Length == 7)
            {
                byte alpha = (byte)e.NewValue;
                _settings.OverlayBackgroundColor = $"#{alpha:X2}{currentHex.Substring(1)}";
                UpdateColorPreviews();
            }
        }

        private void ThemeColor_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateColorPreviews();
        }

        private void PreviewColor_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string type)
            {
                var dialog = new System.Windows.Forms.ColorDialog();
                dialog.FullOpen = true;
                
                // 팔레트 상태 복원 (깊은 복사)
                if (_customColors != null) dialog.CustomColors = (int[])_customColors.Clone();
                
                string currentHex = "#FFFFFF";
                if (type == "Bg") currentHex = _settings.ThemeBackgroundColor ?? "#FFFFFF";
                else if (type == "Text") currentHex = _settings.ThemeTextColor ?? "#333333";
                else if (type == "OverlayBg") currentHex = _settings.OverlayBackgroundColor ?? "#8C000000";
                else if (type == "LineColor") currentHex = _settings.CaptureLineColor ?? "#FF0000";

                try
                {
                    // If it has alpha channel (e.g. #8C000000), ColorConverter handles it. 
                    // But ColorDialog only handles RGB.
                    var color = (Color)ColorConverter.ConvertFromString(currentHex);
                    dialog.Color = System.Drawing.Color.FromArgb(color.R, color.G, color.B);
                }
                catch { }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    // 팔레트 상태 저장 (깊은 복사)
                    _customColors = (int[])dialog.CustomColors.Clone();
                    
                    if (type == "OverlayBg")
                    {
                        // Overlay background usually needs transparency. 
                        byte alpha = 140; 
                        if (SldOverlayOpacity != null) alpha = (byte)SldOverlayOpacity.Value;
                        _settings.OverlayBackgroundColor = $"#{alpha:X2}{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
                    }
                    else
                    {
                        string hex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
                        
                        if (type == "Bg") 
                        {
                            _settings.ThemeBackgroundColor = hex;
                            _settings.CustomThemeBackgroundColor = hex;
                        }
                        else if (type == "Text")
                        {
                            _settings.ThemeTextColor = hex;
                            _settings.CustomThemeTextColor = hex;
                        }
                        else if (type == "LineColor")
                        {
                            _settings.CaptureLineColor = hex;
                        }
                        
                        // 사용자 지정 테마 색상을 바꾼 경우에만 모드 전환
                        if (type == "Bg" || type == "Text")
                        {
                            if (ThemeCustom != null) 
                            {
                                ThemeCustom.IsChecked = true;
                                _settings.ThemeMode = "Custom";
                            }
                        }
                    }
                    
                    UpdateColorPreviews();
                }
            }
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                HighlightNav(btn, tag);
            }
        }

        private void LoadRecordingPage()
        {
            var rec = _settings.Recording;
            
            // Format
            if (CboRecFormat != null)
            {
                string fmt = rec.Format.ToString();
                foreach (ComboBoxItem item in CboRecFormat.Items)
                {
                    if (item.Content.ToString() == fmt)
                    {
                        CboRecFormat.SelectedItem = item;
                        break;
                    }
                }
            }

            // Quality
            if (CboRecQuality != null)
            {
                string qual = rec.Quality.ToString();
                foreach (ComboBoxItem item in CboRecQuality.Items)
                {
                    if (item.Tag?.ToString() == qual)
                    {
                        CboRecQuality.SelectedItem = item;
                        break;
                    }
                }
            }

            // FPS
            if (CboRecFps != null)
            {
                string fps = rec.FrameRate.ToString();
                foreach (ComboBoxItem item in CboRecFps.Items)
                {
                    if (item.Content.ToString() == fps)
                    {
                        CboRecFps.SelectedItem = item;
                        break;
                    }
                }
            }

            if (ChkRecMouse != null) ChkRecMouse.IsChecked = rec.ShowMouseEffects;

            // Hotkey
            BindHotkey(_settings.Hotkeys.RecordingStartStop, HkRecStartStopEnabled, HkRecStartStopCtrl, HkRecStartStopShift, HkRecStartStopAlt, HkRecStartStopWin, HkRecStartStopKey);
        }

        private void LoadCapturePage()
        {
            if (CboFormat != null)
            {
                string fmt = _settings.FileSaveFormat ?? "PNG";
                foreach (ComboBoxItem item in CboFormat.Items)
                {
                    if (item != null && item.Content?.ToString()?.Equals(fmt, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        CboFormat.SelectedItem = item;
                        break;
                    }
                }
                if (CboFormat.SelectedItem == null && CboFormat.Items.Count > 0) CboFormat.SelectedIndex = 0;
            }

            if (CboQuality != null)
            {
                foreach (ComboBoxItem item in CboQuality.Items)
                {
                    if (item != null && item.Tag?.ToString() == _settings.ImageQuality.ToString())
                    {
                        CboQuality.SelectedItem = item;
                        break;
                    }
                }
                if (CboQuality.SelectedItem == null && CboQuality.Items.Count > 0) CboQuality.SelectedIndex = 0;
            }

            var defaultInstallFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatchCapture");
            if (TxtFolder != null) TxtFolder.Text = string.IsNullOrWhiteSpace(_settings.DefaultSaveFolder)
                ? defaultInstallFolder
                : _settings.DefaultSaveFolder;
            if (ChkAutoSave != null) ChkAutoSave.IsChecked = _settings.AutoSaveCapture;
            if (ChkAutoCopy != null) ChkAutoCopy.IsChecked = _settings.AutoCopyToClipboard;
            if (ChkShowPreview != null) ChkShowPreview.IsChecked = _settings.ShowPreviewAfterCapture;
            if (ChkShowMagnifier != null) ChkShowMagnifier.IsChecked = _settings.ShowMagnifier;
            if (ChkShowColorPalette != null) ChkShowColorPalette.IsChecked = _settings.ShowColorPalette;

            // 캡처 모드 설정 로드
            if (RbCaptureOverlay != null && RbCaptureStatic != null)
            {
                if (_settings.UseOverlayCaptureMode)
                    RbCaptureOverlay.IsChecked = true;
                else
                    RbCaptureStatic.IsChecked = true;
            }

            // [추가] 엣지 캡처 프리셋 로드
            if (CboEdgePreset != null)
            {
                string currentLevel = _settings.EdgeCapturePresetLevel.ToString();
                foreach (ComboBoxItem item in CboEdgePreset.Items)
                {
                    if (item.Tag?.ToString() == currentLevel)
                    {
                        CboEdgePreset.SelectedItem = item;
                        break;
                    }
                }
                if (CboEdgePreset.SelectedItem == null) CboEdgePreset.SelectedIndex = 2; // Default 3
            }
            
            // 파일명 & 폴더 분류 설정 로드
            if (TxtFileNameTemplate != null) TxtFileNameTemplate.Text = _settings.FileNameTemplate ?? "Catch_$yyyy-MM-dd_HH-mm-ss$";
            
            if (RbGroupNone != null && RbGroupMonthly != null && RbGroupQuarterly != null && RbGroupYearly != null)
            {
                string mode = _settings.FolderGroupingMode ?? "Monthly";
                if (mode == "None") RbGroupNone.IsChecked = true;
                else if (mode == "Monthly") RbGroupMonthly.IsChecked = true;
                else if (mode == "Quarterly") RbGroupQuarterly.IsChecked = true;
                else if (mode == "Yearly") RbGroupYearly.IsChecked = true;
                else RbGroupMonthly.IsChecked = true; // Default is Monthly
            }

            
            // Print Screen key
            ChkUsePrintScreen.IsChecked = _settings.UsePrintScreenKey;
            foreach (ComboBoxItem item in CboPrintScreenAction.Items)
            {
                if (item.Tag?.ToString() == _settings.PrintScreenAction)
                {
                    CboPrintScreenAction.SelectedItem = item;
                    break;
                }
            }
            if (CboPrintScreenAction.SelectedItem == null)
                CboPrintScreenAction.SelectedIndex = 0;
        }

        private void LoadHistoryPage()
        {
            if (TxtHistoryFolder != null) TxtHistoryFolder.Text = _settings.DefaultSaveFolder;
            if (TxtHistoryFormat != null) TxtHistoryFormat.Text = _settings.FileSaveFormat ?? "PNG";

            if (CboHistoryRetention != null)
            {
                foreach (ComboBoxItem item in CboHistoryRetention.Items)
                {
                    if (item.Tag?.ToString() == _settings.HistoryRetentionDays.ToString())
                    {
                        CboHistoryRetention.SelectedItem = item;
                        break;
                    }
                }
                if (CboHistoryRetention.SelectedItem == null && CboHistoryRetention.Items.Count > 0) CboHistoryRetention.SelectedIndex = 2; // Default: Permanent (0)
            }

            if (CboHistoryTrashRetention != null)
            {
                foreach (ComboBoxItem item in CboHistoryTrashRetention.Items)
                {
                    if (item.Tag?.ToString() == _settings.HistoryTrashRetentionDays.ToString())
                    {
                        CboHistoryTrashRetention.SelectedItem = item;
                        break;
                    }
                }
                if (CboHistoryTrashRetention.SelectedItem == null && CboHistoryTrashRetention.Items.Count > 0) CboHistoryTrashRetention.SelectedIndex = 4; // Default: 30 days
            }
        }

        private void CaptureMode_Checked(object sender, RoutedEventArgs e)
        {
            if (_settings == null || RbCaptureOverlay == null) return;
            _settings.UseOverlayCaptureMode = RbCaptureOverlay.IsChecked == true;
        }

        private void LoadSystemPage()
        {
            if (StartWithWindowsCheckBox != null) StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
            if (RunAsAdminCheckBox != null) RunAsAdminCheckBox.IsChecked = _settings.RunAsAdmin;
            
            if (StartupModeTrayRadio != null && _settings.StartupMode == "Tray") StartupModeTrayRadio.IsChecked = true;
            else if (StartupModeSimpleRadio != null && _settings.StartupMode == "Simple") StartupModeSimpleRadio.IsChecked = true;
            else if (StartupModeNormalRadio != null) StartupModeNormalRadio.IsChecked = true;

            // 언어 설정
            string currentLang = _settings.Language ?? "ko";
            if (LanguageComboBox != null)
            {
                foreach (ComboBoxItem item in LanguageComboBox.Items)
                {
                    if (item.Tag?.ToString() == currentLang)
                    {
                        LanguageComboBox.SelectedItem = item;
                        break;
                    }
                }
                if (LanguageComboBox.SelectedItem == null)
                    LanguageComboBox.SelectedIndex = 0; // 기본값: 한국어
            }
        }

        private static void EnsureDefaultKey(ToggleHotkey hk, string defaultKey)
        {
            if (string.IsNullOrWhiteSpace(hk.Key))
                hk.Key = defaultKey;
        }

        private void LoadHotkeysPage()
        {
            var hk = _settings.Hotkeys;

            // Fill defaults if empty
            EnsureDefaultKey(hk.RegionCapture, "F1");
            EnsureDefaultKey(hk.DelayCapture, "D");
            EnsureDefaultKey(hk.RealTimeCapture, "R");
            EnsureDefaultKey(hk.MultiCapture, "M");
            EnsureDefaultKey(hk.FullScreen, "F");
            EnsureDefaultKey(hk.DesignatedCapture, "W");
            EnsureDefaultKey(hk.WindowCapture, "C");
            EnsureDefaultKey(hk.ElementCapture, "U");
            EnsureDefaultKey(hk.ScrollCapture, "S");
            EnsureDefaultKey(hk.OcrCapture, "O");
            EnsureDefaultKey(hk.ScreenRecord, "V");
            EnsureDefaultKey(hk.SimpleMode, "Q");
            EnsureDefaultKey(hk.TrayMode, "T");
            EnsureDefaultKey(hk.SaveAll, "A");
            EnsureDefaultKey(hk.DeleteAll, "D");
            EnsureDefaultKey(hk.OpenSettings, "O");
            EnsureDefaultKey(hk.OpenEditor, "E");
            EnsureDefaultKey(hk.OpenNote, "N");
            EnsureDefaultKey(hk.EdgeCapture, "E");
            EnsureDefaultKey(hk.RecordingStartStop, "F2");

            // Bind to UI
            BindHotkey(hk.RegionCapture, HkRegionEnabled, HkRegionCtrl, HkRegionShift, HkRegionAlt, HkRegionWin, HkRegionKey);
            BindHotkey(hk.DelayCapture, HkDelayEnabled, HkDelayCtrl, HkDelayShift, HkDelayAlt, HkDelayWin, HkDelayKey);
            BindHotkey(hk.RealTimeCapture, HkRealTimeEnabled, HkRealTimeCtrl, HkRealTimeShift, HkRealTimeAlt, HkRealTimeWin, HkRealTimeKey);
            BindHotkey(hk.MultiCapture, HkMultiEnabled, HkMultiCtrl, HkMultiShift, HkMultiAlt, HkMultiWin, HkMultiKey);
            BindHotkey(hk.FullScreen, HkFullEnabled, HkFullCtrl, HkFullShift, HkFullAlt, HkFullWin, HkFullKey);
            BindHotkey(hk.DesignatedCapture, HkDesignatedEnabled, HkDesignatedCtrl, HkDesignatedShift, HkDesignatedAlt, HkDesignatedWin, HkDesignatedKey);
            BindHotkey(hk.WindowCapture, HkWindowCaptureEnabled, HkWindowCaptureCtrl, HkWindowCaptureShift, HkWindowCaptureAlt, HkWindowCaptureWin, HkWindowCaptureKey);
            BindHotkey(hk.ElementCapture, HkElementCaptureEnabled, HkElementCaptureCtrl, HkElementCaptureShift, HkElementCaptureAlt, HkElementCaptureWin, HkElementCaptureKey);
            BindHotkey(hk.ScrollCapture, HkScrollCaptureEnabled, HkScrollCaptureCtrl, HkScrollCaptureShift, HkScrollCaptureAlt, HkScrollCaptureWin, HkScrollCaptureKey);
            BindHotkey(hk.OcrCapture, HkOcrCaptureEnabled, HkOcrCaptureCtrl, HkOcrCaptureShift, HkOcrCaptureAlt, HkOcrCaptureWin, HkOcrCaptureKey);
            BindHotkey(hk.ScreenRecord, HkScreenRecordEnabled, HkScreenRecordCtrl, HkScreenRecordShift, HkScreenRecordAlt, HkScreenRecordWin, HkScreenRecordKey);
            BindHotkey(hk.SimpleMode, HkSimpleModeEnabled, HkSimpleModeCtrl, HkSimpleModeShift, HkSimpleModeAlt, HkSimpleModeWin, HkSimpleModeKey);
            BindHotkey(hk.TrayMode, HkTrayModeEnabled, HkTrayModeCtrl, HkTrayModeShift, HkTrayModeAlt, HkTrayModeWin, HkTrayModeKey);
            BindHotkey(hk.SaveAll, HkSaveAllEnabled, HkSaveAllCtrl, HkSaveAllShift, HkSaveAllAlt, HkSaveAllWin, HkSaveAllKey);
            BindHotkey(hk.DeleteAll, HkDeleteAllEnabled, HkDeleteAllCtrl, HkDeleteAllShift, HkDeleteAllAlt, HkDeleteAllWin, HkDeleteAllKey);
            BindHotkey(hk.OpenSettings, HkOpenSettingsEnabled, HkOpenSettingsCtrl, HkOpenSettingsShift, HkOpenSettingsAlt, HkOpenSettingsWin, HkOpenSettingsKey);
            BindHotkey(hk.OpenEditor, HkOpenEditorEnabled, HkOpenEditorCtrl, HkOpenEditorShift, HkOpenEditorAlt, HkOpenEditorWin, HkOpenEditorKey);
            BindHotkey(hk.OpenNote, HkOpenNoteEnabled, HkOpenNoteCtrl, HkOpenNoteShift, HkOpenNoteAlt, HkOpenNoteWin, HkOpenNoteKey);
            BindHotkey(hk.EdgeCapture, HkEdgeEnabled, HkEdgeCtrl, HkEdgeShift, HkEdgeAlt, HkEdgeWin, HkEdgeKey);
            BindHotkey(hk.RecordingStartStop, HkRecStartStopEnabled, HkRecStartStopCtrl, HkRecStartStopShift, HkRecStartStopAlt, HkRecStartStopWin, HkRecStartStopKey);
        }

        private static void BindHotkey(ToggleHotkey src, CheckBox? en, CheckBox? ctrl, CheckBox? shift, CheckBox? alt, CheckBox? win, ComboBox? key)
        {
            if (src == null) return;
            if (en != null) en.IsChecked = src.Enabled;
            if (ctrl != null) ctrl.IsChecked = src.Ctrl;
            if (shift != null) shift.IsChecked = src.Shift;
            if (alt != null) alt.IsChecked = src.Alt;
            if (win != null) win.IsChecked = src.Win;
            if (key != null)
            {
                string val = (src.Key ?? string.Empty).Trim().ToUpperInvariant();
                foreach (var item in key.Items)
                {
                    if (item != null && item.ToString() == val)
                    {
                        key.SelectedItem = item;
                        break;
                    }
                }
                if (key.SelectedItem == null && !string.IsNullOrEmpty(val))
                {
                    key.Text = val;
                }
            }
        }

        private static void ReadHotkey(ToggleHotkey dst, CheckBox en, CheckBox ctrl, CheckBox shift, CheckBox alt, CheckBox win, ComboBox key)
        {
            dst.Enabled = en.IsChecked == true;
            dst.Ctrl = ctrl.IsChecked == true;
            dst.Shift = shift.IsChecked == true;
            dst.Alt = alt.IsChecked == true;
            dst.Win = win.IsChecked == true;
            dst.Key = (key.SelectedItem as string ?? string.Empty).Trim().ToUpperInvariant();
        }

        private void LoadNotePage()
        {
            TxtNoteFolder.Text = _settings.NoteStoragePath;

            if (CboNoteFileNamePresets != null)
            {
                // Note: Population is now handled in UpdateUIText for dynamic localization.
                // We just need to ensure the correct item is selected if it's not already.
                if (CboNoteFileNamePresets.SelectedIndex == -1 && CboNoteFileNamePresets.Items.Count > 0)
                    CboNoteFileNamePresets.SelectedIndex = 0;
            }
            if (TxtNoteFileNameTemplate != null) TxtNoteFileNameTemplate.Text = _settings.NoteFileNameTemplate ?? "Catch_$yyyy-MM-dd_HH-mm-ss$";

            string nGrp = _settings.NoteFolderGroupingMode ?? "None";
            if (RbNoteGroupNone != null && nGrp == "None") RbNoteGroupNone.IsChecked = true;
            else if (RbNoteGroupMonthly != null && nGrp == "Monthly") RbNoteGroupMonthly.IsChecked = true;
            else if (RbNoteGroupQuarterly != null && nGrp == "Quarterly") RbNoteGroupQuarterly.IsChecked = true;
            else if (RbNoteGroupYearly != null && nGrp == "Yearly") RbNoteGroupYearly.IsChecked = true;
            
            ChkEnableNotePassword.IsChecked = _settings.IsNoteLockEnabled;
            BtnSetNotePassword.IsEnabled = ChkEnableNotePassword.IsChecked == true;
            
            if (CboNoteFormat != null)
            {
                string fmt = _settings.NoteSaveFormat ?? "PNG";
                foreach (ComboBoxItem item in CboNoteFormat.Items)
                {
                    if (item.Content?.ToString()?.Equals(fmt, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        CboNoteFormat.SelectedItem = item;
                        break;
                    }
                }
                if (CboNoteFormat.SelectedItem == null && CboNoteFormat.Items.Count > 0) CboNoteFormat.SelectedIndex = 0; // Default PNG
            }

            if (CboNoteQuality != null)
            {
                foreach (ComboBoxItem item in CboNoteQuality.Items)
                {
                    if (item.Tag?.ToString() == _settings.NoteImageQuality.ToString())
                    {
                        CboNoteQuality.SelectedItem = item;
                        break;
                    }
                }
                if (CboNoteQuality.SelectedItem == null && CboNoteQuality.Items.Count > 0) CboNoteQuality.SelectedIndex = 0; // Default 100%
            }

            if (CboTrashRetention != null)
            {
                foreach (ComboBoxItem item in CboTrashRetention.Items)
                {
                    if (item.Tag?.ToString() == _settings.TrashRetentionDays.ToString())
                    {
                        CboTrashRetention.SelectedItem = item;
                        break;
                    }
                }
                if (CboTrashRetention.SelectedItem == null && CboTrashRetention.Items.Count > 0) CboTrashRetention.SelectedIndex = 4; // Default 30 days
            }
        }

        private void BtnBrowseNoteFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            dlg.SelectedPath = TxtNoteFolder.Text;
            var result = dlg.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                // Resolve the path immediately to show the user where the data will actually go
                TxtNoteFolder.Text = CatchCapture.Utilities.DatabaseManager.ResolveStoragePath(dlg.SelectedPath);
            }
        }

        private void BtnOpenNoteFolder_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start("explorer.exe", TxtNoteFolder.Text); } catch { }
        }

        private void BtnCloudTip_Click(object sender, RoutedEventArgs e)
        {
            if (CloudTipPopup != null)
            {
                CloudTipPopup.IsOpen = !CloudTipPopup.IsOpen;
            }
        }

        private void BtnResetNote_Click(object sender, RoutedEventArgs e)
        {
            // 1. First Warning
            if (CatchCapture.CustomMessageBox.Show(LocalizationManager.GetString("NoteResetConfirm1"), LocalizationManager.GetString("Confirm"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            // 2. Second Warning (Destructive)
            if (CatchCapture.CustomMessageBox.Show(LocalizationManager.GetString("NoteResetConfirm2"), LocalizationManager.GetString("DataLossWarning"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }


            // 3. Password Check (if enabled)
            if (!string.IsNullOrEmpty(_settings.NotePassword))
            {
                var lockWin = new NoteLockCheckWindow(_settings.NotePassword, _settings.NotePasswordHint);
                lockWin.Owner = this;
                if (lockWin.ShowDialog() != true)
                {
                    return;
                }
            }

            // 4. Execution
            try
            {
                string notePath = TxtNoteFolder.Text;
                string dbPath = System.IO.Path.Combine(notePath, "notedb", "catch_notes.db");
                string imgPath = System.IO.Path.Combine(notePath, "img");
                string attachPath = System.IO.Path.Combine(notePath, "attachments");

                // Release DB locks
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();

                if (System.IO.File.Exists(dbPath))
                {
                    System.IO.File.Delete(dbPath);
                }

                if (System.IO.Directory.Exists(imgPath))
                {
                    DeleteDirectoryContents(imgPath);
                }
                
                if (System.IO.Directory.Exists(attachPath))
                {
                    DeleteDirectoryContents(attachPath);
                }

                // Re-initialize DB
                CatchCapture.Utilities.DatabaseManager.Instance.Reload();

                CatchCapture.CustomMessageBox.Show(LocalizationManager.GetString("NoteResetComplete"), LocalizationManager.GetString("Success"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show(string.Format(LocalizationManager.GetString("NoteResetError"), ex.Message), LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

        private void DeleteDirectoryContents(string path)
        {
            try
            {
                System.IO.DirectoryInfo di = new System.IO.DirectoryInfo(path);
                foreach (System.IO.FileInfo file in di.GetFiles())
                {
                    try { file.Delete(); } catch { }
                }
                foreach (System.IO.DirectoryInfo dir in di.GetDirectories())
                {
                    try { dir.Delete(true); } catch { }
                }
            }
            catch { }
        }

        private void ChkEnableNotePassword_Click(object sender, RoutedEventArgs e)
        {
            if (ChkEnableNotePassword.IsChecked == true)
            {
                // If turning ON, check if password already exists
                if (string.IsNullOrEmpty(_settings.NotePassword))
                {
                    // No password set yet, open setup window
                    var pwdWin = new NotePasswordWindow(null, null);
                    pwdWin.Owner = this;
                    if (pwdWin.ShowDialog() == true)
                    {
                        _settings.NotePassword = pwdWin.Password;
                        _settings.NotePasswordHint = pwdWin.Hint;
                        _settings.IsNoteLockEnabled = true;
                        App.IsNoteAuthenticated = true;
                    }
                    else
                    {
                        // User cancelled, revert checkbox
                        ChkEnableNotePassword.IsChecked = false;
                        _settings.IsNoteLockEnabled = false;
                    }
                }
                else
                {
                    // Existing password found, just enable lock
                    _settings.IsNoteLockEnabled = true;
                }
            }
            else
            {
                // Turning OFF, ALWAYS verify current password first for security
                if (!string.IsNullOrEmpty(_settings.NotePassword))
                {
                    var lockWin = new NoteLockCheckWindow(_settings.NotePassword, _settings.NotePasswordHint);
                    lockWin.Owner = this;
                    if (lockWin.ShowDialog() != true)
                    {
                        // Verification failed or cancelled
                        ChkEnableNotePassword.IsChecked = true;
                        return;
                    }
                }

                // Confirm disable
                if (CatchCapture.CustomMessageBox.Show(LocalizationManager.GetString("ConfirmDisableNotePassword"), LocalizationManager.GetString("Confirm"), MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    ChkEnableNotePassword.IsChecked = true;
                }
                else
                {
                    _settings.IsNoteLockEnabled = false;
                    App.IsNoteAuthenticated = false; // Reset session auth
                }
            }
            BtnSetNotePassword.IsEnabled = ChkEnableNotePassword.IsChecked == true;
        }

        private void BtnSetNotePassword_Click(object sender, RoutedEventArgs e)
        {
            // ALWAYS verify current password before allowing change
            if (!string.IsNullOrEmpty(_settings.NotePassword))
            {
                var lockWin = new NoteLockCheckWindow(_settings.NotePassword, _settings.NotePasswordHint);
                lockWin.Owner = this;
                if (lockWin.ShowDialog() != true)
                {
                    return;
                }
            }

            var pwdWin = new NotePasswordWindow(_settings.NotePassword, _settings.NotePasswordHint);
            pwdWin.Owner = this;
            if (pwdWin.ShowDialog() == true)
            {
                _settings.NotePassword = pwdWin.Password;
                _settings.NotePasswordHint = pwdWin.Hint;
                App.IsNoteAuthenticated = true; // Stay authenticated with new password
            }
        }

        private void BtnExportBackup_Click(object sender, RoutedEventArgs e)
        {
            string? tempPath = null;
            try
            {
                var sfd = new Microsoft.Win32.SaveFileDialog();
                sfd.Filter = "Zip Files (*.zip)|*.zip";
                sfd.FileName = $"CatchCapture_Note_Backup_{DateTime.Now:yyyyMMdd}.zip";
                if (sfd.ShowDialog() == true)
                {
                    string sourceDir = TxtNoteFolder.Text;
                    if (!Directory.Exists(sourceDir))
                    {
                        CatchCapture.CustomMessageBox.Show(LocalizationManager.GetString("ErrorNoFolder"), "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 1. Destination already exists fix
                    if (File.Exists(sfd.FileName))
                    {
                        try { File.Delete(sfd.FileName); } catch { }
                    }

                    // 2. Temp copy logic
                    tempPath = Path.Combine(Path.GetTempPath(), "CatchCapture_Backup_Temp_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(tempPath);

                    // A. Copy folders (img, attachments, etc.) - Skip notedb as we use VACUUM for it
                    CopyDirectory(sourceDir, tempPath, "notedb");

                    // B. Safely backup the database using VACUUM INTO
                    string tempDbPath = Path.Combine(tempPath, "notedb", "catch_notes.db");
                    CatchCapture.Utilities.DatabaseManager.Instance.BackupDatabase(tempDbPath);

                    // 3. Zip from temp
                    System.IO.Compression.ZipFile.CreateFromDirectory(tempPath, sfd.FileName);
                    
                    CatchCapture.CustomMessageBox.Show(LocalizationManager.GetString("BackupSuccess"), "성공", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show($"{LocalizationManager.GetString("ErrorBackup")}: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (tempPath != null && Directory.Exists(tempPath))
                {
                    try { Directory.Delete(tempPath, true); } catch { }
                }
            }
        }

        private void CopyDirectory(string sourceDir, string targetDir, params string[] excludeDirNames)
        {
            Directory.CreateDirectory(targetDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string dest = Path.Combine(targetDir, Path.GetFileName(file));
                try
                {
                    // Use FileStream with FileShare.ReadWrite to copy even if the file is locked
                    using (var sourceStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var destStream = new FileStream(dest, FileMode.Create, FileAccess.Write))
                    {
                        sourceStream.CopyTo(destStream);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to copy {file}: {ex.Message}");
                    try { File.Copy(file, dest, true); } catch { }
                }
            }
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(subDir);
                if (excludeDirNames != null && excludeDirNames.Any(e => string.Equals(dirName, e, StringComparison.OrdinalIgnoreCase)))
                    continue;

                string dest = Path.Combine(targetDir, dirName);
                CopyDirectory(subDir, dest, Array.Empty<string>()); // Use empty array to avoid CS8625 warning
            }
        }

        private void BtnImportBackup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ofd = new Microsoft.Win32.OpenFileDialog();
                ofd.Filter = "Zip Files (*.zip)|*.zip";
                if (ofd.ShowDialog() == true)
                {
                    if (CatchCapture.CustomMessageBox.Show(LocalizationManager.GetString("ImportConfirmMsg"), LocalizationManager.GetString("Confirm"), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        string targetDir = TxtNoteFolder.Text;
                        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                        // 1. Aggressively release all SQLite file handles
                        CatchCapture.Utilities.DatabaseManager.Instance.CloseConnection();
                        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        System.Threading.Thread.Sleep(200); // Give Windows time to release handles

                        // 2. Extract and overwrite
                        System.IO.Compression.ZipFile.ExtractToDirectory(ofd.FileName, targetDir, true);
                        
                        // 3. Re-initialize DB Manager with the new data
                        CatchCapture.Utilities.DatabaseManager.Instance.Reinitialize();
                        
                        CatchCapture.CustomMessageBox.Show(LocalizationManager.GetString("ImportSuccessMsg"), LocalizationManager.GetString("Success"), MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show($"{LocalizationManager.GetString("ErrorImport")}: {ex.Message}", LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnMergeBackup_Click(object sender, RoutedEventArgs e)
        {
            string? tempDir = null;
            try
            {
                var ofd = new Microsoft.Win32.OpenFileDialog();
                ofd.Filter = "Zip Files (*.zip)|*.zip";
                if (ofd.ShowDialog() != true) return;

                if (CatchCapture.CustomMessageBox.Show(LocalizationManager.GetString("MergeConfirmMsg"), LocalizationManager.GetString("Confirm"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    return;
                }

                // Show Loading Overlay
                LoadingOverlay.Visibility = Visibility.Visible;
                TxtWorkStatus.Text = LocalizationManager.GetString("MergingMsg");

                string zipPath = ofd.FileName;
                tempDir = Path.Combine(Path.GetTempPath(), "CatchCapture_Merge_Temp_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);

                await Task.Run(() =>
                {
                    // 1. Extract ZIP
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tempDir);

                    string sourceDbPath = Path.Combine(tempDir, "notedb", "catch_notes.db");
                    string sourceImgDir = Path.Combine(tempDir, "img");
                    string sourceAttachDir = Path.Combine(tempDir, "attachments");

                    if (!File.Exists(sourceDbPath))
                        throw new FileNotFoundException("Invalid backup file: catch_notes.db not found.");

                    // 2. Perform Merge
                    CatchCapture.Utilities.DatabaseManager.Instance.MergeNotesFromBackup(sourceDbPath, sourceImgDir, sourceAttachDir, (msg) => {
                        Dispatcher.Invoke(() => TxtWorkStatus.Text = msg);
                    });
                });

                CatchCapture.CustomMessageBox.Show(LocalizationManager.GetString("MergeSuccessMsg"), LocalizationManager.GetString("Success"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show($"{LocalizationManager.GetString("ErrorMerge")}: {ex.Message}", LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                if (tempDir != null && Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        private bool ValidateNoteSettings()
        {
            if (ChkEnableNotePassword.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(_settings.NotePassword))
                {
                    CatchCapture.CustomMessageBox.Show("비밀번호가 설정되지 않았습니다. 비밀번호를 설정해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                    var pwdWin = new NotePasswordWindow(null, null);
                    pwdWin.Owner = this;
                    if (pwdWin.ShowDialog() == true)
                    {
                        _settings.NotePassword = pwdWin.Password;
                        _settings.NotePasswordHint = pwdWin.Hint;
                    }
                    else
                    {
                        HighlightNav(NavNote, "Note");
                        return false;
                    }
                }
                if (string.IsNullOrWhiteSpace(_settings.NotePasswordHint))
                {
                    CatchCapture.CustomMessageBox.Show("비밀번호 힌트 설정이 필요합니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                    HighlightNav(NavNote, "Note");
                    return false;
                }
            }
            return true;
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (!string.IsNullOrEmpty(TxtFolder.Text)) dlg.SelectedPath = TxtFolder.Text;
            var result = dlg.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                TxtFolder.Text = CatchCapture.Utilities.DatabaseManager.EnsureCatchCaptureSubFolder(dlg.SelectedPath);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateNoteSettings()) return;
            if (!ValidateHotkeys()) return;
            HarvestSettings();
            Settings.Save(_settings);
            
            // Re-store original theme so cancel doesn't revert to old values if we clicked Apply before
            _originalThemeMode = _settings.ThemeMode;
            _originalThemeBg = _settings.ThemeBackgroundColor;
            _originalThemeFg = _settings.ThemeTextColor;

            try { DialogResult = true; } catch { }
            Close();
        }

        private void PageDefault_Click(object sender, RoutedEventArgs e)
        {
            if (_settings == null) return;
            var defaults = new Settings();

            if (_currentPage == "Theme")
            {
                // 테마 관련 설정만 초기화
                _settings.ThemeMode = defaults.ThemeMode;
                _settings.ThemeBackgroundColor = defaults.ThemeBackgroundColor;
                _settings.ThemeTextColor = defaults.ThemeTextColor;
                _settings.CustomThemeBackgroundColor = defaults.CustomThemeBackgroundColor;
                _settings.CustomThemeTextColor = defaults.CustomThemeTextColor;
                _settings.OverlayBackgroundColor = defaults.OverlayBackgroundColor;
                _settings.CaptureLineColor = defaults.CaptureLineColor;
                _settings.CaptureLineThickness = defaults.CaptureLineThickness;
                _settings.CaptureLineStyle = defaults.CaptureLineStyle;
                
                LoadThemePage();
            }
            else if (_currentPage == "Capture")
            {
                _settings.FileSaveFormat = defaults.FileSaveFormat;
                _settings.ImageQuality = defaults.ImageQuality;
                // Preserve DefaultSaveFolder
                _settings.AutoSaveCapture = defaults.AutoSaveCapture;
                _settings.AutoCopyToClipboard = defaults.AutoCopyToClipboard;
                _settings.ShowPreviewAfterCapture = defaults.ShowPreviewAfterCapture;
                _settings.ShowMagnifier = defaults.ShowMagnifier;
                _settings.ShowColorPalette = defaults.ShowColorPalette;
                _settings.FileNameTemplate = defaults.FileNameTemplate;
                _settings.FolderGroupingMode = defaults.FolderGroupingMode;
                _settings.UsePrintScreenKey = defaults.UsePrintScreenKey;
                _settings.PrintScreenAction = defaults.PrintScreenAction;
                
                LoadCapturePage();
            }
            else if (_currentPage == "Recording")
            {
                _settings.Recording = defaults.Recording;
                LoadRecordingPage();
            }
            else if (_currentPage == "Note")
            {
                // Preserve NoteStoragePath
                _settings.NoteSaveFormat = defaults.NoteSaveFormat;
                _settings.NoteImageQuality = defaults.NoteImageQuality;
                _settings.OptimizeNoteImages = defaults.OptimizeNoteImages; // Also reset this for completeness, though deprecated
                _settings.TrashRetentionDays = defaults.TrashRetentionDays;
                LoadNotePage();
            }
            else if (_currentPage == "System")
            {
                _settings.StartWithWindows = defaults.StartWithWindows;
                _settings.StartupMode = defaults.StartupMode;
                // 언어는 유지함이 일반적이나 필요시 포함 가능
                LoadSystemPage();
            }
            else if (_currentPage == "Hotkey")
            {
                _settings.Hotkeys = defaults.Hotkeys;
                LoadHotkeysPage();
            }
            else if (_currentPage == "MenuEdit")
            {
                _settings.MainMenuItems = defaults.MainMenuItems;
                LoadMenuEditPage();
            }
            else if (_currentPage == "History")
            {
                _settings.HistoryRetentionDays = defaults.HistoryRetentionDays;
                _settings.HistoryTrashRetentionDays = defaults.HistoryTrashRetentionDays;
                LoadHistoryPage();
            }

            // 테마 변경사항 즉시 반영
            App.ApplyTheme(_settings);
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateNoteSettings()) return;
            if (!ValidateHotkeys()) return;
            HarvestSettings();
            Settings.Save(_settings);

            // Re-store original theme so cancel doesn't revert to old values
            _originalThemeMode = _settings.ThemeMode;
            _originalThemeBg = _settings.ThemeBackgroundColor;
            _originalThemeFg = _settings.ThemeTextColor;

            // Apply theme real-time (already happening but let's be safe)
            App.ApplyTheme(_settings);
            
            // Feedback for Apply
            // CatchCapture.CustomMessageBox.Show(LocalizationManager.GetString("SettingsSaved"), LocalizationManager.GetString("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private bool ValidateHotkeys()
        {
            var hotkeys = new System.Collections.Generic.List<(string Name, string Shortcut)>();

            void AddIfEnabled(System.Windows.Controls.CheckBox en, System.Windows.Controls.CheckBox ctrl, System.Windows.Controls.CheckBox shift, System.Windows.Controls.CheckBox alt, System.Windows.Controls.CheckBox win, System.Windows.Controls.ComboBox key, string nameKey)
            {
                if (en != null && en.IsChecked == true)
                {
                    string k = (key.SelectedItem as string ?? key.Text ?? "").Trim().ToUpperInvariant();
                    if (string.IsNullOrEmpty(k)) return;

                    string shortcut = (ctrl.IsChecked == true ? "Ctrl+" : "") +
                                     (shift.IsChecked == true ? "Shift+" : "") +
                                     (alt.IsChecked == true ? "Alt+" : "") +
                                     (win.IsChecked == true ? "Win+" : "") +
                                     k;
                    hotkeys.Add((LocalizationManager.GetString(nameKey), shortcut));
                }
            }

            AddIfEnabled(HkRegionEnabled, HkRegionCtrl, HkRegionShift, HkRegionAlt, HkRegionWin, HkRegionKey, "AreaCapture");
            AddIfEnabled(HkDelayEnabled, HkDelayCtrl, HkDelayShift, HkDelayAlt, HkDelayWin, HkDelayKey, "DelayCapture");
            AddIfEnabled(HkRealTimeEnabled, HkRealTimeCtrl, HkRealTimeShift, HkRealTimeAlt, HkRealTimeWin, HkRealTimeKey, "RealTimeCapture");
            AddIfEnabled(HkMultiEnabled, HkMultiCtrl, HkMultiShift, HkMultiAlt, HkMultiWin, HkMultiKey, "MultiCapture");
            AddIfEnabled(HkFullEnabled, HkFullCtrl, HkFullShift, HkFullAlt, HkFullWin, HkFullKey, "FullScreen");
            AddIfEnabled(HkDesignatedEnabled, HkDesignatedCtrl, HkDesignatedShift, HkDesignatedAlt, HkDesignatedWin, HkDesignatedKey, "DesignatedCapture");
            AddIfEnabled(HkWindowCaptureEnabled, HkWindowCaptureCtrl, HkWindowCaptureShift, HkWindowCaptureAlt, HkWindowCaptureWin, HkWindowCaptureKey, "WindowCapture");
            AddIfEnabled(HkElementCaptureEnabled, HkElementCaptureCtrl, HkElementCaptureShift, HkElementCaptureAlt, HkElementCaptureWin, HkElementCaptureKey, "ElementCapture");
            AddIfEnabled(HkScrollCaptureEnabled, HkScrollCaptureCtrl, HkScrollCaptureShift, HkScrollCaptureAlt, HkScrollCaptureWin, HkScrollCaptureKey, "ScrollCapture");
            AddIfEnabled(HkOcrCaptureEnabled, HkOcrCaptureCtrl, HkOcrCaptureShift, HkOcrCaptureAlt, HkOcrCaptureWin, HkOcrCaptureKey, "OcrCapture");
            AddIfEnabled(HkScreenRecordEnabled, HkScreenRecordCtrl, HkScreenRecordShift, HkScreenRecordAlt, HkScreenRecordWin, HkScreenRecordKey, "ScreenRecording");
            AddIfEnabled(HkSimpleModeEnabled, HkSimpleModeCtrl, HkSimpleModeShift, HkSimpleModeAlt, HkSimpleModeWin, HkSimpleModeKey, "SimpleMode");
            AddIfEnabled(HkTrayModeEnabled, HkTrayModeCtrl, HkTrayModeShift, HkTrayModeAlt, HkTrayModeWin, HkTrayModeKey, "TrayMode");
            AddIfEnabled(HkSaveAllEnabled, HkSaveAllCtrl, HkSaveAllShift, HkSaveAllAlt, HkSaveAllWin, HkSaveAllKey, "SaveAll");
            AddIfEnabled(HkDeleteAllEnabled, HkDeleteAllCtrl, HkDeleteAllShift, HkDeleteAllAlt, HkDeleteAllWin, HkDeleteAllKey, "DeleteAll");
            AddIfEnabled(HkOpenSettingsEnabled, HkOpenSettingsCtrl, HkOpenSettingsShift, HkOpenSettingsAlt, HkOpenSettingsWin, HkOpenSettingsKey, "OpenSettings");
            AddIfEnabled(HkOpenEditorEnabled, HkOpenEditorCtrl, HkOpenEditorShift, HkOpenEditorAlt, HkOpenEditorWin, HkOpenEditorKey, "OpenEditor");
            AddIfEnabled(HkOpenNoteEnabled, HkOpenNoteCtrl, HkOpenNoteShift, HkOpenNoteAlt, HkOpenNoteWin, HkOpenNoteKey, "OpenMyNote");
            AddIfEnabled(HkRecStartStopEnabled, HkRecStartStopCtrl, HkRecStartStopShift, HkRecStartStopAlt, HkRecStartStopWin, HkRecStartStopKey, "RecordingStartStop");

            var duplicates = hotkeys.GroupBy(h => h.Shortcut)
                                    .Where(g => g.Count() > 1)
                                    .ToList();

            if (duplicates.Any())
            {
                var dup = duplicates[0];
                string names = string.Join(", ", dup.Select(h => h.Name));
                string msg = string.Format(LocalizationManager.GetString("HotkeyConflictMsg"), dup.Key, names);
                CustomMessageBox.Show(msg, LocalizationManager.GetString("HotkeyConflictTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private void HarvestSettings()
        {
            // Capture options
            if (_loadedPages.Contains("Capture"))
            {
                if (CboFormat.SelectedItem is ComboBoxItem item)
                {
                    _settings.FileSaveFormat = item.Content?.ToString() ?? "PNG";
                }
                
                if (CboQuality.SelectedItem is ComboBoxItem qualityItem)
                {
                    if (int.TryParse(qualityItem.Tag?.ToString(), out int quality))
                    {
                        _settings.ImageQuality = quality;
                    }
                    else
                    {
                        _settings.ImageQuality = 100;
                    }
                }
                else
                {
                    _settings.ImageQuality = 100;
                }

                var desiredFolder = (TxtFolder.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(desiredFolder))
                {
                    desiredFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatchCapture");
                }
                _settings.DefaultSaveFolder = CatchCapture.Utilities.DatabaseManager.EnsureCatchCaptureSubFolder(desiredFolder);
                
                // 파일명 & 폴더 분류 설정 저장
                if (TxtFileNameTemplate != null) _settings.FileNameTemplate = TxtFileNameTemplate.Text;
                
                if (RbGroupNone != null && RbGroupNone.IsChecked == true) _settings.FolderGroupingMode = "None";
                else if (RbGroupMonthly != null && RbGroupMonthly.IsChecked == true) _settings.FolderGroupingMode = "Monthly";
                else if (RbGroupQuarterly != null && RbGroupQuarterly.IsChecked == true) _settings.FolderGroupingMode = "Quarterly";
                else if (RbGroupYearly != null && RbGroupYearly.IsChecked == true) _settings.FolderGroupingMode = "Yearly";
                _settings.AutoSaveCapture = ChkAutoSave.IsChecked == true;
                _settings.AutoCopyToClipboard = ChkAutoCopy.IsChecked == true;
                _settings.ShowPreviewAfterCapture = ChkShowPreview.IsChecked == true;
                _settings.ShowMagnifier = ChkShowMagnifier.IsChecked == true;
                _settings.ShowColorPalette = ChkShowColorPalette.IsChecked == true;
                
                _settings.UsePrintScreenKey = ChkUsePrintScreen.IsChecked == true;
                if (CboPrintScreenAction.SelectedItem is ComboBoxItem actionItem)
                {
                    _settings.PrintScreenAction = actionItem.Tag?.ToString() ?? "영역 캡처";
                }

                if (_settings.AutoSaveCapture)
                {
                    try { if (!System.IO.Directory.Exists(_settings.DefaultSaveFolder)) System.IO.Directory.CreateDirectory(_settings.DefaultSaveFolder); }
                    catch { }
                }

                // [추가] 엣지 캡처 프리셋 저장
                if (CboEdgePreset != null && CboEdgePreset.SelectedItem is ComboBoxItem edgeItem)
                {
                    if (int.TryParse(edgeItem.Tag?.ToString(), out int level))
                    {
                        _settings.EdgeCapturePresetLevel = level;
                    }
                }
            }

            // Hotkeys
            if (_loadedPages.Contains("Hotkey"))
            {
                ReadHotkey(_settings.Hotkeys.RegionCapture, HkRegionEnabled, HkRegionCtrl, HkRegionShift, HkRegionAlt, HkRegionWin, HkRegionKey);
                ReadHotkey(_settings.Hotkeys.DelayCapture, HkDelayEnabled, HkDelayCtrl, HkDelayShift, HkDelayAlt, HkDelayWin, HkDelayKey);
                ReadHotkey(_settings.Hotkeys.RealTimeCapture, HkRealTimeEnabled, HkRealTimeCtrl, HkRealTimeShift, HkRealTimeAlt, HkRealTimeWin, HkRealTimeKey); 
                ReadHotkey(_settings.Hotkeys.MultiCapture, HkMultiEnabled, HkMultiCtrl, HkMultiShift, HkMultiAlt, HkMultiWin, HkMultiKey); 
                ReadHotkey(_settings.Hotkeys.FullScreen, HkFullEnabled, HkFullCtrl, HkFullShift, HkFullAlt, HkFullWin, HkFullKey);
                ReadHotkey(_settings.Hotkeys.DesignatedCapture, HkDesignatedEnabled, HkDesignatedCtrl, HkDesignatedShift, HkDesignatedAlt, HkDesignatedWin, HkDesignatedKey);
                ReadHotkey(_settings.Hotkeys.WindowCapture, HkWindowCaptureEnabled, HkWindowCaptureCtrl, HkWindowCaptureShift, HkWindowCaptureAlt, HkWindowCaptureWin, HkWindowCaptureKey);
                ReadHotkey(_settings.Hotkeys.ElementCapture, HkElementCaptureEnabled, HkElementCaptureCtrl, HkElementCaptureShift, HkElementCaptureAlt, HkElementCaptureWin, HkElementCaptureKey);
                ReadHotkey(_settings.Hotkeys.ScrollCapture, HkScrollCaptureEnabled, HkScrollCaptureCtrl, HkScrollCaptureShift, HkScrollCaptureAlt, HkScrollCaptureWin, HkScrollCaptureKey);
                ReadHotkey(_settings.Hotkeys.OcrCapture, HkOcrCaptureEnabled, HkOcrCaptureCtrl, HkOcrCaptureShift, HkOcrCaptureAlt, HkOcrCaptureWin, HkOcrCaptureKey);
                ReadHotkey(_settings.Hotkeys.ScreenRecord, HkScreenRecordEnabled, HkScreenRecordCtrl, HkScreenRecordShift, HkScreenRecordAlt, HkScreenRecordWin, HkScreenRecordKey);
                ReadHotkey(_settings.Hotkeys.SimpleMode, HkSimpleModeEnabled, HkSimpleModeCtrl, HkSimpleModeShift, HkSimpleModeAlt, HkSimpleModeWin, HkSimpleModeKey);
                ReadHotkey(_settings.Hotkeys.TrayMode, HkTrayModeEnabled, HkTrayModeCtrl, HkTrayModeShift, HkTrayModeAlt, HkTrayModeWin, HkTrayModeKey);
                ReadHotkey(_settings.Hotkeys.SaveAll, HkSaveAllEnabled, HkSaveAllCtrl, HkSaveAllShift, HkSaveAllAlt, HkSaveAllWin, HkSaveAllKey);
                ReadHotkey(_settings.Hotkeys.DeleteAll, HkDeleteAllEnabled, HkDeleteAllCtrl, HkDeleteAllShift, HkDeleteAllAlt, HkDeleteAllWin, HkDeleteAllKey);
                ReadHotkey(_settings.Hotkeys.OpenSettings, HkOpenSettingsEnabled, HkOpenSettingsCtrl, HkOpenSettingsShift, HkOpenSettingsAlt, HkOpenSettingsWin, HkOpenSettingsKey);
                ReadHotkey(_settings.Hotkeys.OpenEditor, HkOpenEditorEnabled, HkOpenEditorCtrl, HkOpenEditorShift, HkOpenEditorAlt, HkOpenEditorWin, HkOpenEditorKey);
                ReadHotkey(_settings.Hotkeys.OpenNote, HkOpenNoteEnabled, HkOpenNoteCtrl, HkOpenNoteShift, HkOpenNoteAlt, HkOpenNoteWin, HkOpenNoteKey);
                ReadHotkey(_settings.Hotkeys.EdgeCapture, HkEdgeEnabled, HkEdgeCtrl, HkEdgeShift, HkEdgeAlt, HkEdgeWin, HkEdgeKey);
                ReadHotkey(_settings.Hotkeys.RecordingStartStop, HkRecStartStopEnabled, HkRecStartStopCtrl, HkRecStartStopShift, HkRecStartStopAlt, HkRecStartStopWin, HkRecStartStopKey);
            }

            // Note settings
            if (_loadedPages.Contains("Note"))
            {
                _settings.NoteStoragePath = CatchCapture.Utilities.DatabaseManager.ResolveStoragePath(TxtNoteFolder.Text);

                 if (TxtNoteFileNameTemplate != null) _settings.NoteFileNameTemplate = TxtNoteFileNameTemplate.Text;
                 
                 if (RbNoteGroupNone != null && RbNoteGroupNone.IsChecked == true) _settings.NoteFolderGroupingMode = "None";
                 else if (RbNoteGroupMonthly != null && RbNoteGroupMonthly.IsChecked == true) _settings.NoteFolderGroupingMode = "Monthly";
                 else if (RbNoteGroupQuarterly != null && RbNoteGroupQuarterly.IsChecked == true) _settings.NoteFolderGroupingMode = "Quarterly";
                 else if (RbNoteGroupYearly != null && RbNoteGroupYearly.IsChecked == true) _settings.NoteFolderGroupingMode = "Yearly";

                _settings.IsNoteLockEnabled = ChkEnableNotePassword.IsChecked == true;
                
                if (CboNoteFormat.SelectedItem is ComboBoxItem noteFmtItem)
                {
                    _settings.NoteSaveFormat = noteFmtItem.Content?.ToString() ?? "JPG";
                }
                
                if (CboNoteQuality.SelectedItem is ComboBoxItem noteQualItem)
                {
                    if (int.TryParse(noteQualItem.Tag?.ToString(), out int q))
                        _settings.NoteImageQuality = q;
                }

                if (CboTrashRetention != null && CboTrashRetention.SelectedItem is ComboBoxItem trashItem)
                {
                    if (int.TryParse(trashItem.Tag?.ToString(), out int d))
                        _settings.TrashRetentionDays = d;
                }
            }

            EnsureDefaultKey(_settings.Hotkeys.RegionCapture, "F1");
            EnsureDefaultKey(_settings.Hotkeys.DelayCapture, "D");
            EnsureDefaultKey(_settings.Hotkeys.RealTimeCapture, "R");
            EnsureDefaultKey(_settings.Hotkeys.MultiCapture, "M");   
            EnsureDefaultKey(_settings.Hotkeys.FullScreen, "F");
            EnsureDefaultKey(_settings.Hotkeys.DesignatedCapture, "W");
            EnsureDefaultKey(_settings.Hotkeys.WindowCapture, "C");
            EnsureDefaultKey(_settings.Hotkeys.ElementCapture, "U");
            EnsureDefaultKey(_settings.Hotkeys.ScrollCapture, "S");
            EnsureDefaultKey(_settings.Hotkeys.OcrCapture, "O");
            EnsureDefaultKey(_settings.Hotkeys.ScreenRecord, "V");
            EnsureDefaultKey(_settings.Hotkeys.SimpleMode, "Q");
            EnsureDefaultKey(_settings.Hotkeys.TrayMode, "T");
            EnsureDefaultKey(_settings.Hotkeys.SaveAll, "A");
            EnsureDefaultKey(_settings.Hotkeys.DeleteAll, "D");
            EnsureDefaultKey(_settings.Hotkeys.OpenSettings, "O");
            EnsureDefaultKey(_settings.Hotkeys.OpenEditor, "E");
            EnsureDefaultKey(_settings.Hotkeys.OpenNote, "N");
            EnsureDefaultKey(_settings.Hotkeys.EdgeCapture, "E");
            EnsureDefaultKey(_settings.Hotkeys.RecordingStartStop, "F2");

            if (_loadedPages.Contains("Recording"))
            {
                if (CboRecFormat.SelectedItem is ComboBoxItem recFmtItem)
                {
                    if (Enum.TryParse<RecordingFormat>(recFmtItem.Content.ToString(), out var format))
                        _settings.Recording.Format = format;
                }

                if (CboRecQuality.SelectedItem is ComboBoxItem recQualItem)
                {
                    if (Enum.TryParse<RecordingQuality>(recQualItem.Tag?.ToString(), out var quality))
                        _settings.Recording.Quality = quality;
                }

                if (CboRecFps.SelectedItem is ComboBoxItem recFpsItem)
                {
                    if (int.TryParse(recFpsItem.Content.ToString(), out int fps))
                        _settings.Recording.FrameRate = fps;
                }
                _settings.Recording.ShowMouseEffects = ChkRecMouse.IsChecked == true;
            }

            // Theme Settings (Capture Line)
            if (_loadedPages.Contains("Theme"))
            {
                if (CboLineStyle.SelectedItem is ComboBoxItem lineStyleItem)
                {
                    _settings.CaptureLineStyle = lineStyleItem.Tag?.ToString() ?? "Dash";
                }
                if (CboLineThickness.SelectedItem is ComboBoxItem lineThickItem)
                {
                    if (double.TryParse(lineThickItem.Tag?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double thick))
                        _settings.CaptureLineThickness = thick;
                }
            }

            // System settings
            if (_loadedPages.Contains("System"))
            {
                _settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
                _settings.RunAsAdmin = RunAsAdminCheckBox.IsChecked == true;
                if (StartupModeTrayRadio.IsChecked == true) { _settings.StartupMode = "Tray"; _settings.LastActiveMode = "Tray"; }
                else if (StartupModeNormalRadio.IsChecked == true) { _settings.StartupMode = "Normal"; _settings.LastActiveMode = "Normal"; }
                else if (StartupModeSimpleRadio.IsChecked == true) { _settings.StartupMode = "Simple"; _settings.LastActiveMode = "Simple"; }
                
                // Language
                if (LanguageComboBox.SelectedItem is ComboBoxItem langItem)
                {
                    var lang = langItem.Tag?.ToString() ?? "ko";
                    _settings.Language = lang;
                    CatchCapture.Resources.LocalizationManager.SetLanguage(lang);
                }
                
                SetStartup(_settings.StartWithWindows);
            }

            // Menu order
            if (_loadedPages.Contains("MenuEdit"))
            {
                _settings.MainMenuItems = _menuItems.Select(m => m.Key).ToList();
            }

            // History Settings Harvest
            if (_loadedPages.Contains("History"))
            {
                if (CboHistoryRetention != null && CboHistoryRetention.SelectedItem is ComboBoxItem hrItem)
                {
                    if (int.TryParse(hrItem.Tag?.ToString(), out int days))
                        _settings.HistoryRetentionDays = days;
                }
                if (CboHistoryTrashRetention != null && CboHistoryTrashRetention.SelectedItem is ComboBoxItem thItem)
                {
                    if (int.TryParse(thItem.Tag?.ToString(), out int days))
                        _settings.HistoryTrashRetentionDays = days;
                }
            }
            // Theme Setting
            if (_loadedPages.Contains("Theme"))
            {
                if (ThemeDark != null && ThemeDark.IsChecked == true) _settings.ThemeMode = "Dark";
                else if (ThemeLight != null && ThemeLight.IsChecked == true) _settings.ThemeMode = "Light";
                else if (ThemeBlue != null && ThemeBlue.IsChecked == true) _settings.ThemeMode = "Blue";
                else if (ThemeCustom != null && ThemeCustom.IsChecked == true) _settings.ThemeMode = "Custom";
                else _settings.ThemeMode = "General";
            }
        }

        private void AdminInfoIcon_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (AdminInfoPopup != null)
            {
                AdminInfoPopup.IsOpen = !AdminInfoPopup.IsOpen;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Revert theme to original if changed without reloading from disk
            if (_settings.ThemeMode != _originalThemeMode || 
                _settings.ThemeBackgroundColor != _originalThemeBg || 
                _settings.ThemeTextColor != _originalThemeFg)
            {
                _settings.ThemeMode = _originalThemeMode;
                _settings.ThemeBackgroundColor = _originalThemeBg;
                _settings.ThemeTextColor = _originalThemeFg;
                App.ApplyTheme(_settings);
            }
            
            try { DialogResult = false; } catch { }
            Close();
        }

        // Added: Sidebar bottom links
        private void RestoreDefaults_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 현재 중요 설정 보존 (언어, 저장 경로 등)
            string currentLanguage = _settings.Language;
            string currentSaveFolder = _settings.DefaultSaveFolder;
            string currentNotePath = _settings.NoteStoragePath;

            _settings = new Settings();
            
            // 보존된 설정 복원
            _settings.Language = currentLanguage;
            _settings.DefaultSaveFolder = currentSaveFolder;
            _settings.NoteStoragePath = currentNotePath;

            Settings.Save(_settings);
            LoadCapturePage();
            LoadMenuEditPage();  // 메뉴 편집도 기본값으로 복원
            LoadSystemPage();
            LoadHotkeysPage();
            LoadThemePage();     // 테마 페이지 UI 갱신 (캡처 라인 설정 포함)
            
            // Ensure Color Picker Preview is reset visually
            if (PreviewLineColor != null) PreviewLineColor.Background = Brushes.Red; // Default
            _settings.CaptureLineColor = "#FF0000"; // Ensure setting is default

            LoadRecordingPage(); // 녹화 페이지 UI 갱신
            LoadNotePage();      // 노트 설정 UI 갱신

            // 테마 즉시 적용
            App.ApplyTheme(_settings);

            CatchCapture.CustomMessageBox.Show(LocalizationManager.GetString("SettingsSaved"), LocalizationManager.GetString("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Website_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://ezupsoft.com",
                    UseShellExecute = true
                });
            }
            catch
            {
                CatchCapture.CustomMessageBox.Show("웹 브라우저를 열 수 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SetStartup(bool enable)
        {
            try
            {
                string appName = "CatchCapture";
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe");
                
                using (var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    if (enable)
                    {
                        key?.SetValue(appName, $"\"{exePath}\" /autostart");
                    }
                    else
                    {
                        key?.DeleteValue(appName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show($"시작 프로그램 설정 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Contact_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://ezupsoft.com",
                    UseShellExecute = true
                });
            }
            catch
            {
                CatchCapture.CustomMessageBox.Show("기본 메일 클라이언트를 열 수 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void PrivacyPolicy_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var win = new PrivacyPolicyWindow
            {
                Owner = this
            };
            win.ShowDialog();
        }

        private void AddSection(StackPanel parent, string title, string content)
        {
            var sectionTitle = new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 20, 0, 10),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 51, 51))
            };
            parent.Children.Add(sectionTitle);

            var sectionContent = new TextBlock
            {
                Text = content,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 24,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(85, 85, 85))
            };
            parent.Children.Add(sectionContent);
        }

        private void CboFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 모든 포맷에서 품질 조정 가능
            // 별도 처리 없음
        }



        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string selectedLang = selectedItem.Tag?.ToString() ?? "ko";
                // 즉시 적용: 미리보기 텍스트 갱신을 위해 런타임 변경
                LocalizationManager.SetLanguage(selectedLang);
                CatchCapture.Models.LocalizationManager.SetLanguage(selectedLang);
                if (LanguageRestartNotice != null) 
                    LanguageRestartNotice.Visibility = Visibility.Collapsed;
            }
        }

        // Menu Edit Page
        private System.Collections.ObjectModel.ObservableCollection<MenuItemViewModel> _menuItems = 
            new System.Collections.ObjectModel.ObservableCollection<MenuItemViewModel>();

        private static void FillKeyCombo(ComboBox? combo, List<string> keys)
        {
            if (combo == null) return;
            combo.Items.Clear();
            foreach (var k in keys) combo.Items.Add(k);
        }
        private void LoadMenuEditPage()
        {
            _menuItems.Clear();
            
            // Load menu items from settings
            var menuKeys = _settings.MainMenuItems ?? new System.Collections.Generic.List<string>
            {
                "AreaCapture", "EdgeCapture", "DelayCapture", "RealTimeCapture", "MultiCapture",
                "FullScreen", "DesignatedCapture", "WindowCapture", "ElementCapture", "ScrollCapture", "OcrCapture", "ScreenRecord"
            };

            foreach (var key in menuKeys)
            {
                _menuItems.Add(new MenuItemViewModel
                {
                    Key = key,
                    DisplayName = GetMenuItemDisplayName(key)
                });
            }

            MenuItemsListBox.ItemsSource = _menuItems;
            UpdateAvailableMenus();
        }

        private void UpdateAvailableMenus()
        {
            // All possible menu items
            var allMenuKeys = new[]
            {
                "AreaCapture", "EdgeCapture", "DelayCapture", "RealTimeCapture", "MultiCapture",
                "FullScreen", "DesignatedCapture", "WindowCapture", "ElementCapture", "ScrollCapture", "OcrCapture", "ScreenRecord"
            };

            // Get currently used keys
            var usedKeys = _menuItems.Select(m => m.Key).ToHashSet();

            // Find available (not currently used) items
            var availableItems = allMenuKeys
                .Where(key => !usedKeys.Contains(key))
                .Select(key => new MenuItemViewModel
                {
                    Key = key,
                    DisplayName = GetMenuItemDisplayName(key)
                })
                .ToList();

            AvailableMenuComboBox.ItemsSource = availableItems;
            AvailableMenuComboBox.DisplayMemberPath = "DisplayName";
            
            if (availableItems.Any())
            {
                AvailableMenuComboBox.SelectedIndex = 0;
            }
        }

        private string GetMenuItemDisplayName(string key)
        {
            return key switch
            {
                "AreaCapture"       => LocalizationManager.GetString("AreaCapture"),
                "DelayCapture"      => LocalizationManager.GetString("DelayCapture"),
                "RealTimeCapture"   => LocalizationManager.GetString("RealTimeCapture"),
                "MultiCapture"      => LocalizationManager.GetString("MultiCapture"),
                "FullScreen"        => LocalizationManager.GetString("FullScreen"),
                "DesignatedCapture" => LocalizationManager.GetString("DesignatedCapture"),
                "WindowCapture"     => LocalizationManager.GetString("WindowCapture"),
                "ElementCapture"    => LocalizationManager.GetString("ElementCapture"),
                "ScrollCapture"     => LocalizationManager.GetString("ScrollCapture"),
                "OcrCapture"        => LocalizationManager.GetString("OcrCapture"),
                "ScreenRecord"      => LocalizationManager.GetString("ScreenRecording"),
                "EdgeCapture"       => LocalizationManager.GetString("EdgeCapture"),
                "SimpleMode"        => LocalizationManager.GetString("SimpleMode"),
                "TrayMode"          => LocalizationManager.GetString("TrayMode"),
                _ => key
            };
        }

        // Drag and Drop support
        private Point _dragStartPoint;
        private MenuItemViewModel? _draggedItem;

        private void MenuItemsListBox_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void MenuItemsListBox_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(null);
                Vector diff = _dragStartPoint - currentPosition;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    // Get the dragged ListBoxItem
                    var listBox = sender as System.Windows.Controls.ListBox;
                    var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);

                    if (listBoxItem != null && listBox != null)
                    {
                        _draggedItem = listBoxItem.Content as MenuItemViewModel;
                        if (_draggedItem != null)
                        {
                            DragDrop.DoDragDrop(listBoxItem, _draggedItem, DragDropEffects.Move);
                        }
                    }
                }
            }
        }

        private ListBoxItem? _lastHighlightedItem = null;

        private void MenuItemsListBox_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;

            // Find the item under the mouse
            var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            
            // Clear previous highlight
            if (_lastHighlightedItem != null && _lastHighlightedItem != targetItem)
            {
                ClearDropHighlight(_lastHighlightedItem);
            }

            // Highlight current target
            if (targetItem != null && targetItem.Content != _draggedItem)
            {
                SetDropHighlight(targetItem);
                _lastHighlightedItem = targetItem;
            }
        }

        private void SetDropHighlight(ListBoxItem item)
        {
            // Find the Border inside the ListBoxItem and add a top border
            var border = FindVisualChild<Border>(item);
            if (border != null)
            {
                border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007AFF"));
                border.BorderThickness = new Thickness(2, 3, 2, 1);
            }
        }

        private void ClearDropHighlight(ListBoxItem item)
        {
            var border = FindVisualChild<Border>(item);
            if (border != null)
            {
                border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E5E5"));
                border.BorderThickness = new Thickness(1);
            }
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T t)
                    return t;
                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private void MenuItemsListBox_Drop(object sender, System.Windows.DragEventArgs e)
        {
            // Clear highlight
            if (_lastHighlightedItem != null)
            {
                ClearDropHighlight(_lastHighlightedItem);
                _lastHighlightedItem = null;
            }

            if (_draggedItem == null) return;

            var listBox = sender as System.Windows.Controls.ListBox;
            if (listBox == null) return;

            // Find drop target
            var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (targetItem == null) return;

            var target = targetItem.Content as MenuItemViewModel;
            if (target == null || target == _draggedItem) return;

            int oldIndex = _menuItems.IndexOf(_draggedItem);
            int newIndex = _menuItems.IndexOf(target);

            if (oldIndex != newIndex && oldIndex >= 0 && newIndex >= 0)
            {
                _menuItems.Move(oldIndex, newIndex);
            }

            _draggedItem = null;
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T t)
                    return t;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MenuItemViewModel item)
            {
                if (_menuItems.Count <= 1)
                {
                    CatchCapture.CustomMessageBox.Show("최소 1개 이상의 메뉴 항목이 필요합니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = CatchCapture.CustomMessageBox.Show(
                    $"'{item.DisplayName}' 메뉴를 삭제하시겠습니까?",
                    "확인",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _menuItems.Remove(item);
                    UpdateAvailableMenus();
                }
            }
        }

        private void AddMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (AvailableMenuComboBox.SelectedItem is MenuItemViewModel selectedItem)
            {
                _menuItems.Add(new MenuItemViewModel
                {
                    Key = selectedItem.Key,
                    DisplayName = selectedItem.DisplayName
                });
                UpdateAvailableMenus();
            }
            else
            {
                CatchCapture.CustomMessageBox.Show("추가할 메뉴를 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try { CatchCapture.Models.LocalizationManager.LanguageChanged -= OnLanguageChanged; } catch { }
            base.OnClosed(e);
        }

        // FileName & Folder Grouping Handlers
        private void TxtFileNameTemplate_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateFileNamePreview();
        }

        private void CboFileNamePresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CboFileNamePresets.SelectedItem is ComboBoxItem item && TxtFileNameTemplate != null)
            {
                if (item.Tag is string tag)
                {
                    if (tag == "Default") TxtFileNameTemplate.Text = "Catch_$yyyy-MM-dd_HH-mm-ss$";
                    else if (tag == "Simple") TxtFileNameTemplate.Text = "Image_$yyyy-MM-dd$";
                    else if (tag == "Timestamp") TxtFileNameTemplate.Text = "$yyyyMMdd_HHmmss$";
                    else if (tag == "AppDate") TxtFileNameTemplate.Text = "$App$_$yyyy-MM-dd_HH-mm-ss$";
                    else if (tag == "TitleDate") TxtFileNameTemplate.Text = "$Title$_$yyyy-MM-dd_HH-mm-ss$";
                }
            }
        }

        private void FolderGrouping_Checked(object sender, RoutedEventArgs e)
        {
            UpdateFileNamePreview();
        }

        private void UpdateFileNamePreview()
        {
            if (TxtFileNamePreview == null || TxtFileNameTemplate == null) return;

            string template = TxtFileNameTemplate.Text;
            string preview = template;
            DateTime now = DateTime.Now;

            try 
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(template, @"\$(.*?)\$");
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    string format = match.Groups[1].Value;
                    
                    if (format.Equals("App", StringComparison.OrdinalIgnoreCase)) 
                    {
                        preview = preview.Replace(match.Value, "MyBrowser");
                        continue;
                    }
                    if (format.Equals("Title", StringComparison.OrdinalIgnoreCase)) 
                    {
                        preview = preview.Replace(match.Value, "WebPageTitle");
                        continue;
                    }

                    try 
                    {
                        preview = preview.Replace(match.Value, now.ToString(format));
                    }
                    catch { }
                }
            } 
            catch { }

            string ext = ".png"; // Default
            if (CboFormat != null && CboFormat.SelectedItem is ComboBoxItem item)
            {
                ext = "." + (item.Content?.ToString() ?? "png").ToLower();
            }

            // Folder Preview
            string folderPart = "";
            if (RbGroupMonthly != null && RbGroupMonthly.IsChecked == true) folderPart = now.ToString("yyyy-MM") + "\\";
            else if (RbGroupQuarterly != null && RbGroupQuarterly.IsChecked == true) 
            {
                int q = (now.Month + 2) / 3;
                folderPart = $"{now.Year}_{q}Q\\";
            }
            else if (RbGroupYearly != null && RbGroupYearly.IsChecked == true) folderPart = now.ToString("yyyy") + "\\";

            TxtFileNamePreview.Text = preview + ext;
        }
        private void BtnFormatInfo_Click(object sender, RoutedEventArgs e)
        {
            if (FormatInfoPopup != null) FormatInfoPopup.IsOpen = !FormatInfoPopup.IsOpen;
        }

        private void BtnNoteFormatInfo_Click(object sender, RoutedEventArgs e)
        {
            if (NoteFormatInfoPopup != null) NoteFormatInfoPopup.IsOpen = !NoteFormatInfoPopup.IsOpen;
        }

        private void TxtNoteFileNameTemplate_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateNoteFileNamePreview();
        }

        private void CboNoteFileNamePresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CboNoteFileNamePresets.SelectedItem is ComboBoxItem item && TxtNoteFileNameTemplate != null)
            {
                if (item.Tag is string tag)
                {
                    if (tag == "Default") TxtNoteFileNameTemplate.Text = "Catch_$yyyy-MM-dd_HH-mm-ss$";
                    else if (tag == "Simple") TxtNoteFileNameTemplate.Text = "Image_$yyyy-MM-dd$";
                    else if (tag == "Timestamp") TxtNoteFileNameTemplate.Text = "$yyyyMMdd_HHmmss$";
                }
            }
        }

        private void NoteFolderGrouping_Checked(object sender, RoutedEventArgs e)
        {
            UpdateNoteFileNamePreview();
        }

        private void UpdateNoteFileNamePreview()
        {
            if (TxtNoteFileNamePreview == null || TxtNoteFileNameTemplate == null) return;

            string template = TxtNoteFileNameTemplate.Text;
            string preview = template;
            DateTime now = DateTime.Now;

            try 
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(template, @"\$(.*?)\$");
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    string format = match.Groups[1].Value;
                    try 
                    {
                        preview = preview.Replace(match.Value, now.ToString(format));
                    }
                    catch { }
                }
            } 
            catch { }

            string ext = ".webp"; // Default
            if (CboNoteFormat != null && CboNoteFormat.SelectedItem is ComboBoxItem item)
            {
                ext = "." + (item.Content?.ToString() ?? "webp").ToLower();
            }

            // Folder Preview
            string folderPart = "";
            if (RbNoteGroupMonthly != null && RbNoteGroupMonthly.IsChecked == true) folderPart = now.ToString("yyyy-MM") + "\\";
            else if (RbNoteGroupQuarterly != null && RbNoteGroupQuarterly.IsChecked == true) 
            {
                int q = (now.Month + 2) / 3;
                folderPart = $"{now.Year}_{q}Q\\";
            }
            else if (RbNoteGroupYearly != null && RbNoteGroupYearly.IsChecked == true) folderPart = now.ToString("yyyy") + "\\";

            TxtNoteFileNamePreview.Text = preview + ext;
        }

        private void BtnHistoryExport_Click(object sender, RoutedEventArgs e)
        {
            string? tempPath = null;
            try
            {
                var sfd = new Microsoft.Win32.SaveFileDialog();
                sfd.Filter = "Zip Files (*.zip)|*.zip";
                sfd.FileName = $"CatchCapture_History_Backup_{DateTime.Now:yyyyMMdd}.zip";
                if (sfd.ShowDialog() == true)
                {
                    string sourceDir = _settings.DefaultSaveFolder;
                    if (!Directory.Exists(sourceDir))
                    {
                        CatchCapture.CustomMessageBox.Show(LocalizationManager.GetString("ErrorNoFolder"), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (File.Exists(sfd.FileName))
                    {
                        try { File.Delete(sfd.FileName); } catch { }
                    }

                    tempPath = Path.Combine(Path.GetTempPath(), "CatchCapture_History_Backup_Temp_" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(tempPath);

                    // 1. 모든 캡처 이미지 및 폴더 복사 (notedata와 history 폴더는 제외)
                    CopyDirectory(sourceDir, tempPath, "notedata", "history");
                    
                    // 2. history.db는 사용 중일 수 있으므로 안전하게 백업 API 사용
                    string tempHistoryDir = Path.Combine(tempPath, "history");
                    Directory.CreateDirectory(tempHistoryDir);
                    string tempDbPath = Path.Combine(tempHistoryDir, "history.db");
                    DatabaseManager.Instance.BackupHistoryDatabase(tempDbPath);

                    System.IO.Compression.ZipFile.CreateFromDirectory(tempPath, sfd.FileName);
                    CatchCapture.CustomMessageBox.Show(LocalizationManager.GetString("BackupSuccess"), "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show($"{LocalizationManager.GetString("ErrorBackup")}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (tempPath != null && Directory.Exists(tempPath))
                {
                    try { Directory.Delete(tempPath, true); } catch { }
                }
            }
        }

        private void BtnHistoryImport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ofd = new Microsoft.Win32.OpenFileDialog();
                ofd.Filter = "Zip Files (*.zip)|*.zip";
                if (ofd.ShowDialog() == true)
                {
                    if (CatchCapture.CustomMessageBox.Show(LocalizationManager.GetString("ImportConfirmMsg"), LocalizationManager.GetString("Confirm"), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        string targetDir = _settings.DefaultSaveFolder;
                        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                        DatabaseManager.Instance.CloseConnection(); // This should close both if implemented correctly
                        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        System.Threading.Thread.Sleep(200);

                        System.IO.Compression.ZipFile.ExtractToDirectory(ofd.FileName, targetDir, true);
                        DatabaseManager.Instance.Reinitialize();
                        CatchCapture.CustomMessageBox.Show(LocalizationManager.GetString("ImportSuccessMsg"), LocalizationManager.GetString("Success"), MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show($"{LocalizationManager.GetString("ErrorImport")}: {ex.Message}", LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDbOptimize_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DatabaseManager.Instance.VacuumHistory();
                CatchCapture.CustomMessageBox.Show(LocalizationManager.GetString("Success"), LocalizationManager.GetString("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show(ex.Message, LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
    }

    // Helper class for menu item editing
    public class MenuItemViewModel
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}
