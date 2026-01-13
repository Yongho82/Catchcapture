using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CatchCapture.Models;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Windows.Interop;
using WpfImage = System.Windows.Controls.Image;
using DrawingIcon = System.Drawing.Icon;

namespace CatchCapture
{
    public partial class TrayModeWindow : Window
    {
        private MainWindow mainWindow;
        private Settings? settings;
        private bool isFolded = false;

        public TrayModeWindow(MainWindow owner)
        {
            InitializeComponent();
            mainWindow = owner;
            
            // 설정 로드 및 초기화
            LoadSettings();
            
            // 창 위치 설정
            PositionWindow();
            
            // 고정 아이콘 초기 상태 설정
            UpdateTopmostIcon();
            // 즉시편집 스위치 초기 상태 설정
            UpdateInstantEditToggleUI();
            
            // 다국어 UI 텍스트 적용 및 언어 변경 시 갱신
            try { UpdateUIText(); } catch { }
            try { LocalizationManager.LanguageChanged += OnLanguageChanged; } catch { }
        }

        // ★ 이벤트 핸들러 추가 (생성자 다음에)
        private void OnSettingsChanged(object? sender, EventArgs e)
        {
            // 설정이 변경되면 자동으로 다시 로드
            Dispatcher.Invoke(() =>
            {
                settings = Settings.Load();
                BuildIconButtons();
            });
        }

        private void LoadSettings()
        {
            settings = Settings.Load();
            BuildIconButtons();
        }

        public void ReloadSettings()
        {
            LoadSettings();
        }

        private void PositionWindow()
        {
            // 주 모니터의 작업 영역 가져오기
            var workArea = SystemParameters.WorkArea;
            
            // 트레이 모드는 기존처럼 우측 하단 (트레이 아이콘 근처)에 배치
            this.Left = workArea.Right - this.Width - 5;
            this.Top = workArea.Bottom - this.Height - 5;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            // 항상 위가 비활성화되어 있을 때만 창 밖 클릭 시 숨기기
            if (!this.Topmost)
            {
                this.Hide();
            }
        }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // 이벤트 구독 해제
            Settings.SettingsChanged -= OnSettingsChanged;
            try { LocalizationManager.LanguageChanged -= OnLanguageChanged; } catch { }
            
            // 설정 저장 제거 - 불필요하며 설정 창 닫힐 때 문제 발생
            base.OnClosing(e);
        }
        private void SwitchToNormalModeButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            mainWindow.SwitchToNormalMode();
        }

        private void TopmostButton_Click(object sender, RoutedEventArgs e)
        {
            // 항상 위 토글
            this.Topmost = !this.Topmost;
            UpdateTopmostIcon();
        }

        private void UpdateTopmostIcon()
        {
            // 이모지 색상 제어 시도 (이모지는 시스템 폰트이므로 색상 변경이 제한적일 수 있음)
            if (this.Topmost)
            {
                // 활성화: 진하게 + 빨간색 시도
                TopmostIcon.Opacity = 1.0;
                TopmostIcon.Foreground = new SolidColorBrush(Color.FromRgb(255, 80, 80)); // 빨간색
            }
            else
            {
                // 비활성화: 연하게 + 기본 색상
                TopmostIcon.Opacity = 0.3;
                TopmostIcon.Foreground = Brushes.Black; // 기본 검정색
            }
        }

        public void UpdateCaptureCount(int count)
        {
            CaptureCountText.Text = count.ToString();
            
            // 간단한 스케일 애니메이션
            if (count > 0)
            {
                var scaleTransform = new System.Windows.Media.ScaleTransform(1.0, 1.0);
                CaptureCounterButton.RenderTransform = scaleTransform;
                CaptureCounterButton.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                
                var animation = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = 1.3,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(200)
                };
                
                scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, animation);
                scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, animation);
            }
        }

        private void CaptureCounterButton_Click(object sender, RoutedEventArgs e)
        {
            // 일반 모드로 전환하여 캡처 목록 확인
            this.Hide();
            mainWindow.SwitchToNormalMode();
        }

        // 캡처 버튼 이벤트 핸들러들
        private void AreaCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            mainWindow.TriggerAreaCapture();
        }

        private void DelayCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            // [수정] 메인 윈도우 설정된 지연 시간으로 바로 실행
            this.Hide();
            mainWindow.TriggerDelayCapture(-1); // -1 이면 settings.DelayCaptureSeconds 사용
        }

        private void DelayMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem mi && mi.Tag is string tagStr && int.TryParse(tagStr, out int seconds))
            {
                this.Hide();
                mainWindow.TriggerDelayCapture(seconds);
            }
        }

        private void FullScreenButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            mainWindow.TriggerFullScreenCapture();
        }

        private void DesignatedCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            mainWindow.TriggerDesignatedCapture();
        }

        private void WindowCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            mainWindow.TriggerWindowCapture();
        }

        private void UnitCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            mainWindow.TriggerUnitCapture();
        }

        private void ScrollCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            mainWindow.TriggerScrollCapture();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.TriggerCopySelected();
        }

        private void CopyAllButton_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.TriggerCopyAll();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.TriggerSaveSelected();
        }

        private void SaveAllButton_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.TriggerSaveAll();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.TriggerDeleteSelected();
        }

        private void DeleteAllButton_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.TriggerDeleteAll();
        }

        private void SimpleModeButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            mainWindow.TriggerSimpleMode();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            mainWindow.TriggerSettings();
            this.Hide();
            mainWindow.TriggerSettings();
        }

        private void FoldButton_Click(object sender, RoutedEventArgs e)
        {
            isFolded = !isFolded;
            
            if (isFolded)
            {
                // 접기: 아이콘 변경 및 UI 숨김
                FoldIcon.Text = "▲";
                FoldButton.ToolTip = LocalizationManager.Get("Unfold");
                
                // 버튼 패널의 자식들 중 숨길 것들 숨기기
                ToggleButtonsVisibility(Visibility.Collapsed);
                
                // 창 높이 줄이기 (상단 컨트롤 + 접기버튼 + 여백)
                // 20(상단) + 30(접기 28+2) + 10(여백) = 60
                double foldedHeight = 60;
                
                // 하단 기준 높이 조절
                double heightDiff = foldedHeight - this.Height;
                this.Top -= heightDiff;
                this.Height = foldedHeight;
            }
            else
            {
                // 펼치기: 아이콘 변경 및 UI 보이기
                FoldIcon.Text = "▼";
                FoldButton.ToolTip = LocalizationManager.Get("Fold");
                
                // 버튼 패널의 자식들 보이기
                ToggleButtonsVisibility(Visibility.Visible);
                
                // 창 높이 자동 조절 (원래대로)
                AdjustWindowHeight();
            }
        }

        private void ToggleButtonsVisibility(Visibility visibility)
        {
            var buttonsPanel = FindButtonsPanel();
            if (buttonsPanel == null) return;
            
            for (int i = 0; i < buttonsPanel.Children.Count; i++)
            {
                var child = buttonsPanel.Children[i];

                // 1. FoldButton은 항상 보임
                if (child is Button btn && btn.Name == "FoldButton") continue;

                // 2. 첫 번째 요소(상단 컨트롤 패널)는 항상 보임
                if (i == 0) continue;

                // 나머지는 숨김/보임 처리 (핀, 카운트, 구분선, 캡처버튼 등)
                child.Visibility = visibility;
            }
        }
        private void BuildIconButtons()
        {
            var buttonsPanel = FindButtonsPanel();
            if (buttonsPanel == null) return;
            
            // 기존 캡처 버튼들 제거 (구분선 이후)
            ClearCaptureButtons(buttonsPanel);
            
            // 숨겨진 아이콘/앱 계산
            var hiddenIcons = GetHiddenIcons();
            var hiddenApps = GetHiddenApps();
            
            // 설정에 있는 아이콘들만 추가 (숨겨진 것 제외)
            if (settings != null)
            {
                foreach (var iconName in settings.SimpleModeIcons)
                {
                    // Settings는 항상 표시, 나머지는 숨겨진 것 제외
                    if (iconName == "Settings" || !hiddenIcons.Contains(iconName))
                    {
                        AddIconButton(buttonsPanel, iconName);
                    }
                }
                
                // 외부 앱 바로가기 추가 (숨겨진 것 제외)
                foreach (var app in settings.SimpleModeApps)
                {
                    if (!hiddenApps.Contains(app))
                    {
                        AddAppButton(buttonsPanel, app);
                    }
                }
            }
            
            // 빈 슬롯 추가 (⇄ + 버튼)
            AddEmptySlot(buttonsPanel);
            
            // HideToTrayButton을 + 버튼 위로 이동 (순서 보장)
            if (HideToTrayButton != null)
            {
                buttonsPanel.Children.Remove(HideToTrayButton);
                buttonsPanel.Children.Add(HideToTrayButton);
            }
            
            // FoldButton을 맨 아래로 이동 (순서 보장)
            if (FoldButton != null)
            {
                buttonsPanel.Children.Remove(FoldButton);
                buttonsPanel.Children.Add(FoldButton);
            }
            
            // 창 높이 자동 조절
            AdjustWindowHeight();
        }

        private void AdjustWindowHeight()
        {
            if (isFolded || settings == null) return;

            // 숨겨진 아이콘 계산
            var hiddenIcons = GetHiddenIcons();
            var hiddenApps = GetHiddenApps();
            
            // 실제로 표시되는 아이콘 개수 계산
            int visibleIconCount = settings.SimpleModeIcons.Count - hiddenIcons.Count;
            int visibleAppCount = settings.SimpleModeApps.Count - hiddenApps.Count;
            int totalVisibleIcons = visibleIconCount + visibleAppCount;
            
            // 기본 높이 계산 (아이콘 제외)
            // 상단 컨트롤: ~20px
            // InstantEditToggle: 26px
            // TopmostButton: 30px
            // CaptureCounter: 30px
            // 첫 번째 Separator: 21px
            // + 버튼: 30px
            // 나가기 버튼: 30px
            // FoldButton: 30px
            // 여백: 10px
            
            int baseHeight = 20 + 26 + 30 + 30 + 21 + 30 + 30 + 30 + 10; // = 227px
            
            // 숨겨진 아이콘이 있으면 ⇄ 버튼 높이 추가
            if (hiddenIcons.Count > 0 || hiddenApps.Count > 0)
            {
                baseHeight += 30;
            }
            
            // 실제 표시되는 아이콘 높이 (Settings 포함)
            int iconsHeight = totalVisibleIcons * 52;
            
            int newHeight = baseHeight + iconsHeight;
            
            // 최소/최대 높이 제한 (화면 높이의 90%까지)
            var workArea = SystemParameters.WorkArea;
            int maxHeight = (int)(workArea.Height * 0.9);
            newHeight = Math.Max(200, Math.Min(newHeight, maxHeight));
            
            // 현재 높이와 새 높이의 차이 계산
            double heightDiff = newHeight - this.Height;
            
            // 하단 기준으로 높이 조절 (Top 위치를 위로 이동)
            this.Top -= heightDiff;
            this.Height = newHeight;
        }

        private void AddIconButton(StackPanel panel, string iconName)
        {
            // Grid로 감싸서 버튼 + 호버 시 - 버튼 추가
            var grid = new Grid();
            
            // 메인 버튼
            var button = CreateIconButton(iconName);
            grid.Children.Add(button);
            
            // - 버튼 (우측 상단, 기본 숨김)
            var removeButton = CreateRemoveButton(iconName);
            removeButton.Visibility = Visibility.Collapsed;
            grid.Children.Add(removeButton);
            
            // 호버 이벤트
            grid.MouseEnter += (s, e) => removeButton.Visibility = Visibility.Visible;
            grid.MouseLeave += (s, e) => removeButton.Visibility = Visibility.Collapsed;
            
            panel.Children.Add(grid);
        }

        private void AddEmptySlot(StackPanel panel)
        {
            // 숨겨진 아이콘이 있을 때만 확장/축소 토글 버튼 표시
            var hiddenIcons = GetHiddenIcons();
            var hiddenApps = GetHiddenApps();
            
            if (hiddenIcons.Count > 0 || hiddenApps.Count > 0)
            {
                var expandButton = new Button
                {
                    Content = "⇄",
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Style = this.FindResource("TinyButtonStyle") as Style,
                    ToolTip = LocalizationManager.Get("ShowHiddenIcons")
                };
                expandButton.SetResourceReference(Button.ForegroundProperty, "ThemeForeground");
                expandButton.Click += ExpandToggleButton_Click;
                panel.Children.Add(expandButton);
            }
            
            // + 버튼 (호버 시만 표시)
            var grid = new Grid 
            { 
                Height = 30,
                Background = Brushes.Transparent
            };
            
            var addButton = new Button
            {
                Content = "+",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Style = this.FindResource("TinyButtonStyle") as Style,
                ToolTip = LocalizationManager.Get("AddIcon"),
                Opacity = 0.3
            };
            addButton.SetResourceReference(Button.ForegroundProperty, "ThemeForeground");
            
            addButton.Click += ShowAddIconMenu;
            grid.Children.Add(addButton);
            
            grid.MouseEnter += (s, e) => addButton.Opacity = 1.0;
            grid.MouseLeave += (s, e) => addButton.Opacity = 0.3;
            
            panel.Children.Add(grid);
        }

        private StackPanel? FindButtonsPanel()
        {
            // XAML에서 ButtonsPanel 찾기
            return this.FindName("ButtonsPanel") as StackPanel;
        }

        private void ClearCaptureButtons(StackPanel panel)
        {
            // 구분선 이후의 모든 버튼 제거
            // 상단 컨트롤(일반/간편모드), TopmostButton, CaptureCounterButton, 첫 번째 Separator는 유지
            // 그 이후의 모든 요소 제거
            
            var itemsToRemove = new List<UIElement>();
            bool foundFirstSeparator = false;
            
            foreach (UIElement child in panel.Children)
            {
                if (child is Separator)
                {
                    if (!foundFirstSeparator)
                    {
                        foundFirstSeparator = true;
                        continue;
                    }
                }
                
                if (foundFirstSeparator)
                {
                    // FoldButton은 지우지 않음
                    if (child is Button btn && btn.Name == "FoldButton") continue;

                    itemsToRemove.Add(child);
                }
            }
            
            foreach (var item in itemsToRemove)
            {
                panel.Children.Remove(item);
            }
        }

        private Button CreateIconButton(string iconName)
        {
            var button = new Button
            {
                Style = this.FindResource("IconButtonStyle") as Style,
                ToolTip = GetIconDisplayName(iconName)
            };
            
            // 아이콘과 텍스트를 담을 StackPanel 생성
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            Image? iconImage = null;
            string labelText = "";
            
            // 아이콘에 따라 클릭 이벤트 및 이미지 설정
            switch (iconName)
            {
                case "AreaCapture":
                    button.Click += AreaCaptureButton_Click;
                    iconImage = CreateImage("/icons/area_capture.png");
                    labelText = LocalizationManager.Get("AreaCapture");
                    break;
                case "DelayCapture":
                    button.Click += DelayCaptureButton_Click;
                    iconImage = CreateImage("/icons/clock.png");
                    labelText = LocalizationManager.Get("DelayCapture");
                    // ContextMenu 추가
                    button.ContextMenu = CreateDelayContextMenu();
                    break;
                case "RealTimeCapture":
                    button.Click += RealTimeCaptureButton_Click;
                    iconImage = CreateImage("/icons/real-time.png");
                    labelText = LocalizationManager.Get("RealTimeCapture");
                    break;
                case "FullScreen":
                    button.Click += FullScreenButton_Click;
                    iconImage = CreateImage("/icons/full_screen.png");
                    labelText = LocalizationManager.Get("FullScreen");
                    break;
                case "DesignatedCapture":
                    button.Click += DesignatedCaptureButton_Click;
                    iconImage = CreateImage("/icons/designated.png");
                    labelText = LocalizationManager.Get("DesignatedCapture");
                    break;
                case "WindowCapture":
                    button.Click += WindowCaptureButton_Click;
                    iconImage = CreateImage("/icons/window_cap.png");
                    labelText = LocalizationManager.Get("WindowCapture");
                    break;
                case "UnitCapture":
                    button.Click += UnitCaptureButton_Click;
                    iconImage = CreateImage("/icons/unit_capture.png");
                    labelText = LocalizationManager.Get("UnitCapture");
                    break;
                case "ScrollCapture":
                    button.Click += ScrollCaptureButton_Click;
                    iconImage = CreateImage("/icons/scroll_capture.png");
                    labelText = LocalizationManager.Get("ScrollCapture");
                    break;
                case "MultiCapture":
                    button.Click += MultiCaptureButton_Click;
                    iconImage = CreateImage("/icons/multi_capture.png");
                    labelText = LocalizationManager.Get("MultiCapture");
                    break;
                case "OcrCapture":
                    button.Click += OcrCaptureButton_Click;
                    iconImage = CreateImage("/icons/ocr_capture.png");
                    labelText = LocalizationManager.Get("OcrCapture");
                    break;
                case "ScreenRecord":
                    button.Click += ScreenRecordButton_Click;
                    iconImage = CreateImage("/icons/videocamera.png");
                    labelText = LocalizationManager.Get("Record");
                    break;
                case "Copy":
                    button.Click += CopyButton_Click;
                    iconImage = CreateImage("/icons/copy_selected.png");
                    labelText = LocalizationManager.Get("CopySelected");
                    break;
                case "CopyAll":
                    button.Click += CopyAllButton_Click;
                    iconImage = CreateImage("/icons/copy_all.png");
                    labelText = LocalizationManager.Get("CopyAll");
                    break;
                case "Save":
                    button.Click += SaveButton_Click;
                    iconImage = CreateImage("/icons/save_selected.png");
                    labelText = LocalizationManager.Get("Save");
                    break;
                case "SaveAll":
                    button.Click += SaveAllButton_Click;
                    iconImage = CreateImage("/icons/save_all.png");
                    labelText = LocalizationManager.Get("SaveAll");
                    break;
                case "Delete":
                    button.Click += DeleteButton_Click;
                    iconImage = CreateImage("/icons/delete_selected.png");
                    labelText = LocalizationManager.Get("Delete");
                    break;
                case "DeleteAll":
                    button.Click += DeleteAllButton_Click;
                    iconImage = CreateImage("/icons/delete_all.png");
                    labelText = LocalizationManager.Get("DeleteAll");
                    break;
                case "MyNote":
                    button.Click += (s, e) => mainWindow.OpenNoteExplorer(); 
                    iconImage = CreateImage("/icons/my_note.png");
                    labelText = LocalizationManager.Get("OpenMyNote");
                    break;
                case "History":
                    button.Click += (s, e) => {
                        var historyWindow = Application.Current.Windows.OfType<HistoryWindow>().FirstOrDefault();
                        if (historyWindow == null) { historyWindow = new HistoryWindow(); historyWindow.Show(); }
                        else { historyWindow.Activate(); if (historyWindow.WindowState == WindowState.Minimized) historyWindow.WindowState = WindowState.Normal; }
                    };
                    iconImage = CreateImage("/icons/histroy_note.png");
                    labelText = LocalizationManager.Get("History");
                    break;
                case "EdgeCapture":
                    // 저장된 반경으로 바로 캡처 (컨텍스트 메뉴 없음)
                    button.Click += async (s, e) => {
                        this.Hide();
                        var (radius, _, _, _, _) = CatchCapture.Utilities.EdgeCaptureHelper.GetPresetSettings(settings?.EdgeCapturePresetLevel ?? 3);
                        await mainWindow.StartAreaCaptureAsync(radius);
                    };
                    iconImage = CreateImage("/icons/edge_capture.png");
                    labelText = LocalizationManager.Get("EdgeCapture");
                    break;
                case "Settings":
                    button.Click += SettingsButton_Click;
                    iconImage = CreateImage("/icons/setting.png");
                    labelText = LocalizationManager.Get("Settings");
                    break;
            }
            
            // 아이콘 추가
            if (iconImage != null)
            {
                stackPanel.Children.Add(iconImage);
            }
            
            var textBlock = new TextBlock
            {
                Text = labelText,
                FontSize = GetOptimalFontSize(labelText),  // ✅ 동적 폰트 크기
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 1, 0, 0)
            };
            textBlock.SetResourceReference(TextBlock.ForegroundProperty, "ThemeForeground");
            stackPanel.Children.Add(textBlock);
            
            button.Content = stackPanel;
            
            return button;
        }

        private void MultiCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            mainWindow.TriggerMultiCapture();
        }

        private void OcrCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            mainWindow.TriggerOcrCapture();
        }

        private void ScreenRecordButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            mainWindow.TriggerScreenRecord();
        }

        private Image CreateImage(string source)
        {
            var image = new Image
            {
                Source = new BitmapImage(new Uri(source, UriKind.Relative)),
                Width = 24,
                Height = 24,
                SnapsToDevicePixels = true
            };
            
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            
            return image;
        }

        private ContextMenu CreateDelayContextMenu()
        {
            var menu = new ContextMenu();
            // dark style 적용 (리소스에 정의됨)
            if (this.TryFindResource("DarkContextMenu") is Style darkMenu)
                menu.Style = darkMenu;
            
            var mi3 = new MenuItem { Header = LocalizationManager.Get("Delay3Sec"), Tag = "3" };
            var mi5 = new MenuItem { Header = LocalizationManager.Get("Delay5Sec"), Tag = "5" };
            var mi10 = new MenuItem { Header = LocalizationManager.Get("Delay10Sec"), Tag = "10" };

            if (this.TryFindResource("DarkMenuItem") is Style darkItem)
            {
                mi3.Style = darkItem; mi5.Style = darkItem; mi10.Style = darkItem;
            }

            menu.Items.Add(mi3);
            menu.Items.Add(mi5);
            menu.Items.Add(mi10);
            
            foreach (MenuItem item in menu.Items)
            {
                item.Click += DelayMenuItem_Click;
            }
            
            return menu;
        }

        private ContextMenu CreateEdgeContextMenu()
        {
            var menu = new ContextMenu();
            if (this.TryFindResource("DarkContextMenu") is Style darkMenu)
                menu.Style = darkMenu;

            var items = new (string HeaderKey, int Radius, string Emoji)[]
            {
                ("EdgeSoft", 12, "🫧"),
                ("EdgeSmooth", 25, "📱"),
                ("EdgeClassic", 50, "🍪"),
                ("EdgeCapsule", 100, "💊"),
                ("EdgeCircle", 999, "🌕")
            };

            foreach (var t in items)
            {
                var mi = new MenuItem { 
                    Header = $"{t.Emoji} {LocalizationManager.Get(t.HeaderKey)}",
                    Tag = t.Radius.ToString()
                };
                if (this.TryFindResource("DarkMenuItem") is Style darkItem)
                    mi.Style = darkItem;
                
                mi.Click += async (s, e) =>
                {
                    if (settings != null)
                    {
                        settings.EdgeCaptureRadius = t.Radius;
                        Settings.Save(settings);
                    }
                    this.Hide();
                    await mainWindow.StartAreaCaptureAsync(t.Radius);
                };
                menu.Items.Add(mi);
            }

            return menu;
        }


        private Button CreateRemoveButton(string iconName)
        {
            // 작은 - 버튼 생성 (우측 상단, 기본 숨김)
            var btn = new Button
            {
                Content = "−",
                Width = 12,  // 14 → 12
                Height = 12, // 14 → 12
                FontSize = 10, // 12 → 10
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromArgb(200, 255, 80, 80)), // 반투명 빨강
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 0, 0), // (0, 1, 1, 0) → (0, 0, 0, 0) - 더 위로
                Cursor = Cursors.Hand,
                Padding = new Thickness(0, -5, 0, 0) // -4 → -5 (더 위로)
            };
            
            // 둥근 모서리
            var border = new Border
            {
                CornerRadius = new CornerRadius(6), // 7 → 6
                Background = btn.Background,
                Width = 8,  // 10 → 8
                Height = 8, // 10 → 8
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 0, 0), // (0, 1, 1, 0) → (0, 0, 0, 0)
                Child = new TextBlock
                {
                    Text = "−",
                    FontSize = 10, // 12 → 10
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, -3, 0, 0) // -2 → -3 (더 위로)
                }
            };
            
            border.MouseLeftButtonDown += (s, e) => 
            {
                RemoveIcon(iconName);
                e.Handled = true;
            };
            
            // Template 설정
            var template = new ControlTemplate(typeof(Button));
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6)); // 7 → 6
            factory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            
            var textFactory = new FrameworkElementFactory(typeof(TextBlock));
            textFactory.SetValue(TextBlock.TextProperty, "−");
            textFactory.SetValue(TextBlock.FontSizeProperty, 10.0); // 12.0 → 10.0
            textFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            textFactory.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            textFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            textFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            textFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, -3, 0, 0)); // 추가
            
            factory.AppendChild(textFactory);
            template.VisualTree = factory;
            
            btn.Template = template;
            btn.Click += (s, e) => RemoveIcon(iconName);
            
            return btn;
        }

        private void RemoveIcon(string iconName)
        {
            // 삭제 확인 메시지
            var result = CatchCapture.CustomMessageBox.Show(
                $"'{LocalizationManager.Get(iconName)}' 아이콘을 삭제하시겠습니까?",
                LocalizationManager.Get("DeleteIcon"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );
            
            if (result == MessageBoxResult.Yes)
            {
                // 설정 다시 로드 (최신 상태 확보)
                settings = Settings.Load();
                
                settings.SimpleModeIcons.Remove(iconName);
                Settings.Save(settings);
                
                BuildIconButtons();
            }
        }
        // 클래스 필드에 추가
        private Popup? iconPopup = null;

        // 확장/축소 토글 버튼 클릭 이벤트
        private void ExpandToggleButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (iconPopup == null || !iconPopup.IsOpen)
                {
                    // Popup 생성 및 표시
                    ShowIconPopup();
                }
                else
                {
                    // Popup 닫기
                    iconPopup.IsOpen = false;
                }
            }
            catch (Exception)
            {
            }
        }

        private void ShowIconPopup()
        {
            if (settings == null) return;
            
            // 숨겨진 아이콘 계산
            var hiddenIcons = GetHiddenIcons();
            var hiddenApps = GetHiddenApps();
            
            if (hiddenIcons.Count == 0 && hiddenApps.Count == 0)
            {
                return;
            }
            
            // Popup 생성
            iconPopup = new Popup
            {
                AllowsTransparency = true,
                StaysOpen = false,
                Placement = PlacementMode.Left,
                PlacementTarget = this,
                HorizontalOffset = -10
            };
            
            // Popup 내용
            var border = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 230, 240)),
                Padding = new Thickness(10)
            };
            
            border.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                ShadowDepth = 2,
                BlurRadius = 10,
                Opacity = 0.3,
                Direction = 270
            };
            
            // 세로로 나열 (StackPanel 사용)
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };
            
            // 숨겨진 아이콘 추가
            foreach (var iconName in hiddenIcons)
            {
                // Grid 생성 (아이콘 + 삭제 버튼 오버레이)
                var grid = new Grid();
                
                var button = CreateIconButton(iconName);
                button.Margin = new Thickness(2);
                button.Click += (s, e) => { iconPopup.IsOpen = false; };
                grid.Children.Add(button);
                
                // 삭제 버튼 추가 (Settings 제외)
                if (iconName != "Settings")
                {
                    var remove = CreateRemoveButton(iconName);
                    remove.Visibility = Visibility.Collapsed;
                    grid.Children.Add(remove);
                    
                    // 마우스 오버 시 삭제 버튼 표시
                    grid.MouseEnter += (s, e) => remove.Visibility = Visibility.Visible;
                    grid.MouseLeave += (s, e) => remove.Visibility = Visibility.Collapsed;
                }
                
                stackPanel.Children.Add(grid);
            }
            
            // 숨겨진 앱 추가
            foreach (var app in hiddenApps)
            {
                var grid = new Grid();
                
                var button = new Button
                {
                    Style = this.FindResource("IconButtonStyle") as Style,
                    ToolTip = app.DisplayName,
                    Margin = new Thickness(2)
                };
                
                var stack = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                
                var img = CreateAppImage(app);
                if (img != null) stack.Children.Add(img);
                
                var text = new TextBlock
                {
                    Text = TruncateForLabel(app.DisplayName),
                    FontSize = GetOptimalFontSize(TruncateForLabel(app.DisplayName)),  // ✅ 동적 폰트 크기
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 1, 0, 0),
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102))
                };
                stack.Children.Add(text);
                
                button.Content = stack;
                button.Click += (s, e) => 
                { 
                    HandleAppClick(app);
                    iconPopup.IsOpen = false; 
                };
                
                grid.Children.Add(button);
                
                // 앱 삭제 버튼 추가
                var remove = CreateRemoveButtonForApp(app);
                remove.Visibility = Visibility.Collapsed;
                grid.Children.Add(remove);
                
                grid.MouseEnter += (s, e) => remove.Visibility = Visibility.Visible;
                grid.MouseLeave += (s, e) => remove.Visibility = Visibility.Collapsed;
                
                stackPanel.Children.Add(grid);
            }
            
            border.Child = stackPanel;
            iconPopup.Child = border;
            iconPopup.IsOpen = true;
        }

        private void GetVisibilityCounts(out int visibleIcons, out int visibleApps)
        {
            if (settings == null) 
            {
                visibleIcons = 0;
                visibleApps = 0;
                return;
            }
            // 일단 다 보여준다고 가정
            visibleIcons = settings.SimpleModeIcons.Count;
            visibleApps = settings.SimpleModeApps.Count;
            
            if (settings == null) return;

            var workArea = SystemParameters.WorkArea;
            int maxHeight = (int)(workArea.Height * 0.9);
            
            // 고정 버튼 높이 합계
            int baseHeight = 20 + 26 + 30 + 30 + 21 + 30 + 30 + 30 + 10; 
            int iconHeight = 52;
            
            // 최대 슬롯 계산
            int maxSlots = (maxHeight - baseHeight) / iconHeight;
            int totalItems = settings.SimpleModeIcons.Count + settings.SimpleModeApps.Count;
            
            // 넘치면 토글 버튼 자리(-1) 확보 후 계산
            if (totalItems > maxSlots)
            {
                int effectiveSlots = maxSlots - 1;
                
                // 아이콘 우선 배정
                if (settings.SimpleModeIcons.Count > effectiveSlots)
                {
                    visibleIcons = effectiveSlots;
                    visibleApps = 0;
                }
                else
                {
                    visibleIcons = settings.SimpleModeIcons.Count;
                    int remaining = effectiveSlots - visibleIcons;
                    visibleApps = Math.Min(remaining, settings.SimpleModeApps.Count);
                }
            }
        }

        private List<string> GetHiddenIcons()
        {
            if (settings == null) return new List<string>();
            
            GetVisibilityCounts(out int visibleIconsCount, out int visibleAppsCount);
            
            var hiddenIcons = new List<string>();
            int iconsToHide = settings.SimpleModeIcons.Count - visibleIconsCount;
            
            if (iconsToHide > 0)
            {
                // ★ 핵심: 리스트의 '맨 뒤'에서부터 숨깁니다.
                int hiddenCount = 0;
                for (int i = settings.SimpleModeIcons.Count - 1; i >= 0 && hiddenCount < iconsToHide; i--)
                {
                    string iconName = settings.SimpleModeIcons[i];
                    
                    // ★ Settings는 절대 숨기지 않고 건너뜁니다.
                    if (iconName == "Settings") continue;
                    
                    hiddenIcons.Add(iconName);
                    hiddenCount++;
                }
            }
            return hiddenIcons;
        }

        private List<ExternalAppShortcut> GetHiddenApps()
        {
            if (settings == null) return new List<ExternalAppShortcut>();
            
            GetVisibilityCounts(out int visibleIconsCount, out int visibleAppsCount);
            
            var hiddenApps = new List<ExternalAppShortcut>();
            int appsToHide = settings.SimpleModeApps.Count - visibleAppsCount;
            
            if (appsToHide > 0)
            {
                // 앱도 '맨 뒤'에서부터 숨깁니다.
                int startIndex = settings.SimpleModeApps.Count - appsToHide;
                if (startIndex < 0) startIndex = 0;

                for (int i = startIndex; i < settings.SimpleModeApps.Count; i++)
                {
                    hiddenApps.Add(settings.SimpleModeApps[i]);
                }
            }
            return hiddenApps;
        }

        private Image? CreateMenuIcon(string relativePath)
        {
            try
            {
                BitmapImage bmp;
                // 1) Pack URI 시도 (리소스로 포함된 경우)
                try
                {
                    var packUri = new Uri($"pack://application:,,,{relativePath}", UriKind.Absolute);
                    bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = packUri;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bmp.DecodePixelWidth = 16;
                    bmp.DecodePixelHeight = 16;
                    bmp.EndInit();
                    bmp.Freeze();
                }
                catch
                {
                    // 2) 파일 시스템 경로 폴백 (앱 실행 폴더/icons)
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var trimmed = relativePath.TrimStart('/', '\\');
                    var fullPath = System.IO.Path.Combine(baseDir, trimmed);
                    if (!System.IO.File.Exists(fullPath))
                    {
                        // icons가 루트에 있다면
                        fullPath = System.IO.Path.Combine(baseDir, "icons", System.IO.Path.GetFileName(trimmed));
                    }
                    bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(fullPath, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bmp.DecodePixelWidth = 16;
                    bmp.DecodePixelHeight = 16;
                    bmp.EndInit();
                    bmp.Freeze();
                }
 
                var img = new Image
                {
                    Source = bmp,
                    Width = 16,
                    Height = 16,
                    Stretch = Stretch.Uniform,
                    SnapsToDevicePixels = true
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                return img;
            }
            catch { return null; }
        }

        private void RealTimeCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.TriggerRealTimeCapture();
        }
        private string GetIconDisplayName(string iconName)
        {
            // 아이콘 이름을 로컬라이즈된 텍스트로 변환
            return iconName switch
            {
                "AreaCapture" => LocalizationManager.Get("AreaCapture"),
                "DelayCapture" => LocalizationManager.Get("DelayCapture"),
                "RealTimeCapture" => LocalizationManager.Get("RealTimeCapture"), 
                "FullScreen" => LocalizationManager.Get("FullScreen"),
                "MultiCapture" => LocalizationManager.Get("MultiCapture"),
                "DesignatedCapture" => LocalizationManager.Get("DesignatedCapture"),
                "WindowCapture" => LocalizationManager.Get("WindowCapture"),
                "UnitCapture" => LocalizationManager.Get("UnitCapture"),
                "ScrollCapture" => LocalizationManager.Get("ScrollCapture"),
                "OcrCapture" => LocalizationManager.Get("OcrCapture"),
                "ScreenRecord" => LocalizationManager.Get("ScreenRecording"),
                "Copy" => LocalizationManager.Get("CopySelected"),
                "CopyAll" => LocalizationManager.Get("CopyAll"),
                "Save" => LocalizationManager.Get("Save"),
                "SaveAll" => LocalizationManager.Get("SaveAll"),
                "Delete" => LocalizationManager.Get("Delete"),
                "DeleteAll" => LocalizationManager.Get("DeleteAll"),
                "MyNote" => LocalizationManager.Get("OpenMyNote"),
                "History" => LocalizationManager.Get("History"),
                "Settings" => LocalizationManager.Get("Settings"),
                "EdgeCapture" => LocalizationManager.Get("EdgeCapture"),
                _ => iconName
            };
        }
        private void UpdateInstantEditToggleUI()
        {
            var settings = Settings.Load();
            if (settings.SimpleModeInstantEdit)
            {
                InstantEditToggleBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00E676"));
                InstantEditToggleCircle.Margin = new Thickness(16, 0, 0, 0);
            }
            else
            {
                InstantEditToggleBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
                InstantEditToggleCircle.Margin = new Thickness(2, 0, 0, 0);
            }
        }

        private void InstantEditToggle_Click(object sender, MouseButtonEventArgs e)
        {
            var settings = Settings.Load();
            settings.SimpleModeInstantEdit = !settings.SimpleModeInstantEdit;
            Settings.Save(settings);
            UpdateInstantEditToggleUI();
        }
        private void HideToTrayButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        // ShowAddIconMenu에서 사용하는 아이콘 추가 함수 (누락 복원)
        private void AddIcon(string iconName)
        {
            if (settings == null)
            {
                settings = Settings.Load();
            }
            
            if (!settings.SimpleModeIcons.Contains(iconName))
            {
                settings.SimpleModeIcons.Add(iconName);
                Settings.Save(settings);
            }
            BuildIconButtons();
        }
        // 외부 앱 버튼 생성
        private void AddAppButton(StackPanel panel, ExternalAppShortcut app)
        {
            var grid = new Grid();
            var button = new Button
            {
                Style = this.FindResource("IconButtonStyle") as Style,
                ToolTip = app.DisplayName
            };
            
            var stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            var img = CreateAppImage(app);
            if (img != null) stack.Children.Add(img);
            
            var text = new TextBlock
            {
                Text = TruncateForLabel(app.DisplayName),
                FontSize = GetOptimalFontSize(TruncateForLabel(app.DisplayName)),  // ✅ 동적 폰트 크기
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 1, 0, 0)
            };
            text.SetResourceReference(TextBlock.ForegroundProperty, "ThemeForeground");
            stack.Children.Add(text);
            
            button.Content = stack;
            button.Click += (s, e) => HandleAppClick(app);
            
            grid.Children.Add(button);
            var remove = CreateRemoveButtonForApp(app);
            remove.Visibility = Visibility.Collapsed;
            grid.Children.Add(remove);
            grid.MouseEnter += (s, e) => remove.Visibility = Visibility.Visible;
            grid.MouseLeave += (s, e) => remove.Visibility = Visibility.Collapsed;
            
            panel.Children.Add(grid);
        }

        private WpfImage? CreateAppImage(ExternalAppShortcut app)
        {
            try
            {
                string path = !string.IsNullOrEmpty(app.IconPath) ? app.IconPath! : app.TargetPath;
                if (string.IsNullOrEmpty(path)) return null;
                using DrawingIcon? icon = DrawingIcon.ExtractAssociatedIcon(path);
                if (icon == null) return null;
                var bmp = icon.ToBitmap();
                var hbitmap = bmp.GetHbitmap();
                var source = Imaging.CreateBitmapSourceFromHBitmap(hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(24, 24));
                return new WpfImage { Source = source, Width = 24, Height = 24, SnapsToDevicePixels = true };
            }
            catch { return null; }
        }

        private Button CreateRemoveButtonForApp(ExternalAppShortcut app)
        {
            var btn = new Button
            {
                Width = 12,
                Height = 12,
                Background = new SolidColorBrush(Color.FromArgb(200, 255, 80, 80)),
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 0, 0),
                Cursor = Cursors.Hand,
                ToolTip = "삭제"
            };
            
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            
            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetValue(TextBlock.TextProperty, "−");
            text.SetValue(TextBlock.FontSizeProperty, 10.0);
            text.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            text.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            text.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            text.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            text.SetValue(TextBlock.MarginProperty, new Thickness(0, -3, 0, 0));
            border.AppendChild(text);
            template.VisualTree = border;
            
            btn.Template = template;
            btn.Click += (s, e) => { RemoveApp(app); e.Handled = true; };
            return btn;
        }

        private void HandleAppClick(ExternalAppShortcut app)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = string.IsNullOrWhiteSpace(app.TargetPath) ? app.DisplayName : app.TargetPath,
                    WorkingDirectory = string.IsNullOrWhiteSpace(app.WorkingDirectory) ? null : app.WorkingDirectory,
                    Arguments = string.IsNullOrWhiteSpace(app.Arguments) ? null : app.Arguments,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                CatchCapture.CustomMessageBox.Show($"앱 실행 실패: {ex.Message}");
            }
        }

        private void RemoveApp(ExternalAppShortcut app)
        {
            var result = CatchCapture.CustomMessageBox.Show($"'{app.DisplayName}' 바로가기를 삭제하시겠습니까?", "바로가기 삭제", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes && settings != null)
            {
                settings.SimpleModeApps.RemoveAll(a => string.Equals(a.TargetPath, app.TargetPath, StringComparison.OrdinalIgnoreCase) && a.DisplayName == app.DisplayName);
                Settings.Save(settings);
                BuildIconButtons();
            }
        }

        private static string TruncateForLabel(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var trimmed = s.Trim();
            bool asciiOnly = true;
            foreach (var ch in trimmed)
            {
                if (ch > 127)
                {
                    asciiOnly = false;
                    break;
                }
            }
            int maxLen = asciiOnly ? 7 : 5;
            return trimmed.Length <= maxLen ? trimmed : trimmed.Substring(0, maxLen) + "…";
        }

        private async Task<ExternalAppShortcut?> OpenAppPickerAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var entries = EnumerateInstalledApplications();
                    // 간단한 선택 창 구성
                    ExternalAppShortcut? result = null;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var win = new Window
                        {
                            Title = "애플리케이션 선택",
                            Width = 420,
                            Height = 560,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            Owner = this,
                            ResizeMode = ResizeMode.CanResizeWithGrip
                        };
                        var grid = new Grid();
                        // 0: 헤더(필터), 1: 리스트, 2: 버튼바
                        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        grid.RowDefinitions.Add(new RowDefinition());
                        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                        // 헤더: 정렬 토글
                        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8,8,8,0), HorizontalAlignment = HorizontalAlignment.Right };
                        var sortBtn = new Button { Content = "가나다순 ▲", Padding = new Thickness(10,2,10,2), Height = 26, MinWidth = 96 };
                        header.Children.Add(sortBtn);
                        Grid.SetRow(header, 0);
                        grid.Children.Add(header);

                        // 리스트
                        var list = new ListBox { Margin = new Thickness(8) };
                        Grid.SetRow(list, 1);
                        grid.Children.Add(list);

                        // 정렬/새로고침 로직
                        bool asc = true;
                        void RefreshList()
                        {
                            list.Items.Clear();
                            var seq = asc 
                                ? entries.OrderBy(t => t.Item1, StringComparer.CurrentCultureIgnoreCase)
                                : entries.OrderByDescending(t => t.Item1, StringComparer.CurrentCultureIgnoreCase);
                            foreach (var e in seq)
                            {
                                var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4) };
                                var img = CreateImageFromPath(e.Item3);
                                if (img != null) sp.Children.Add(img);
                                sp.Children.Add(new TextBlock { Text = e.Item1, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
                                var li = new ListBoxItem { Content = sp, Tag = e };
                                list.Items.Add(li);
                            }
                            sortBtn.Content = asc ? "가나다순 ▲" : "가나다순 ▼";
                        }
                        sortBtn.Click += (ss, ee) => { asc = !asc; RefreshList(); };
                        RefreshList();

                        // 버튼 바
                        var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8) };
                        var browse = new Button { Content = "찾아보기…", Margin = new Thickness(0, 0, 8, 0) };
                        var ok = new Button { Content = "확인", IsDefault = true };
                        var cancel = new Button { Content = "취소", IsCancel = true, Margin = new Thickness(8,0,0,0) };
                        bottom.Children.Add(browse);
                        bottom.Children.Add(ok);
                        bottom.Children.Add(cancel);
                        Grid.SetRow(bottom, 2);
                        grid.Children.Add(bottom);
                        win.Content = grid;

                        browse.Click += (s, e) =>
                        {
                            var ofd = new OpenFileDialog
                            {
                                Title = "실행 파일/바로가기 선택",
                                Filter = "Programs|*.exe;*.lnk|All|*.*"
                            };
                            if (ofd.ShowDialog(win) == true)
                            {
                                var name = System.IO.Path.GetFileNameWithoutExtension(ofd.FileName);
                                result = new ExternalAppShortcut { DisplayName = name, TargetPath = ofd.FileName };
                                win.DialogResult = true;
                                win.Close();
                            }
                        };
                        ok.Click += (s, e) =>
                        {
                            if (list.SelectedItem is ListBoxItem lbi && lbi.Tag is object)
                            {
                                try
                                {
                                    var t = ((System.ValueTuple<string, string, string>)lbi.Tag);
                                    result = new ExternalAppShortcut { DisplayName = t.Item1, TargetPath = t.Item2, IconPath = t.Item3 };
                                    win.DialogResult = true;
                                    win.Close();
                                }
                                catch { /* ignore invalid selection */ }
                            }
                        };
                        win.ShowDialog();
                    });
                    return result;
                }
                catch { return null; }
            });
        }

        private List<(string, string, string)> EnumerateInstalledApplications()
        {
            var result = new List<(string, string, string)>();
            try
            {
                // 64/32비트, 사용자/컴퓨터 범위 레지스트리 모두 조회
                var roots = new (RegistryKey root, string path)[]
                {
                    (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                    (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                    (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                    (Registry.CurrentUser, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                };

                foreach (var (root, path) in roots)
                {
                    using var key = root.OpenSubKey(path);
                    if (key == null) continue;
                    foreach (var sub in key.GetSubKeyNames())
                    {
                        using var appKey = key.OpenSubKey(sub);
                        if (appKey == null) continue;

                        string? name = appKey.GetValue("DisplayName") as string;
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        // 필터: 업데이트/핫픽스 등 제외 (선택)
                        var systemComponent = appKey.GetValue("SystemComponent");
                        if (systemComponent is int sc && sc == 1) continue;

                        string? displayIcon = appKey.GetValue("DisplayIcon") as string;
                        string? installLocation = appKey.GetValue("InstallLocation") as string;
                        string? uninstallString = appKey.GetValue("UninstallString") as string;

                        string exePath = string.Empty;
                        string iconPath = string.Empty;

                        // 1) DisplayIcon 우선
                        if (!string.IsNullOrWhiteSpace(displayIcon))
                        {
                            iconPath = CleanIconPath(displayIcon);
                            if (File.Exists(iconPath))
                            {
                                if (Path.GetExtension(iconPath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                                    exePath = iconPath;
                            }
                        }

                        // 2) InstallLocation에서 exe 추정
                        if (string.IsNullOrEmpty(exePath) && !string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
                        {
                            // 가장 큰 exe 하나를 대표 실행파일로 추정
                            var exes = Directory.EnumerateFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly)
                                                .Select(p => new FileInfo(p))
                                                .OrderByDescending(fi => fi.Length)
                                                .ToList();
                            if (exes.Count > 0)
                            {
                                exePath = exes[0].FullName;
                                if (string.IsNullOrEmpty(iconPath)) iconPath = exePath;
                            }
                        }

                        // 3) UninstallString에서 경로 추출
                        if (string.IsNullOrEmpty(exePath) && !string.IsNullOrWhiteSpace(uninstallString))
                        {
                            string candidate = ExtractQuotedPath(uninstallString);
                            if (string.IsNullOrEmpty(candidate)) candidate = uninstallString.Split(' ').FirstOrDefault() ?? string.Empty;
                            if (File.Exists(candidate))
                            {
                                exePath = candidate;
                                if (string.IsNullOrEmpty(iconPath)) iconPath = exePath;
                            }
                        }

                        // 최종 유효성: 이름은 있고, 경로는 exe/lnk 아무거나
                        if (!string.IsNullOrWhiteSpace(name) && (!string.IsNullOrWhiteSpace(exePath) || !string.IsNullOrWhiteSpace(iconPath)))
                        {
                            var pathForLaunch = !string.IsNullOrWhiteSpace(exePath) ? exePath : iconPath;
                            var pathForIcon = !string.IsNullOrWhiteSpace(iconPath) ? iconPath : exePath;
                            result.Add((name!, pathForLaunch!, pathForIcon!));
                        }
                    }
                }
            }
            catch { }

            // 중복 제거 (표시명+경로 기준)
            result = result
                .GroupBy(t => (t.Item1, t.Item2))
                .Select(g => g.First())
                .OrderBy(t => t.Item1, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            return result;
        }

        private static string CleanIconPath(string raw)
        {
            // 예: "C:\\Program Files\\App\\app.exe,0" → exe 경로만
            string s = raw.Trim().Trim('"');
            int comma = s.IndexOf(',');
            if (comma > 0) s = s.Substring(0, comma);
            return s;
        }

        private static string ExtractQuotedPath(string raw)
        {
            // 가장 처음 따옴표 구간 추출
            int first = raw.IndexOf('"');
            if (first >= 0)
            {
                int second = raw.IndexOf('"', first + 1);
                if (second > first) return raw.Substring(first + 1, second - first - 1);
            }
            return string.Empty;
        }

        private WpfImage? CreateImageFromPath(string path)
        {
            try
            {
                using DrawingIcon? icon = DrawingIcon.ExtractAssociatedIcon(path);
                if (icon == null) return null;
                var bmp = icon.ToBitmap();
                var hbitmap = bmp.GetHbitmap();
                var source = Imaging.CreateBitmapSourceFromHBitmap(hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(20, 20));
                return new WpfImage { Source = source, Width = 20, Height = 20 };
            }
            catch { return null; }
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            try { UpdateUIText(); } catch { }
        }

        // TrayModeWindow UI 텍스트/툴팁 다국어 적용
        private void UpdateUIText()
        {
            try
            {
                // 창 제목
                this.Title = LocalizationManager.Get("AppName");

                // 상단 컨트롤 툴팁
                if (SwitchToNormalModeButton != null)
                    SwitchToNormalModeButton.ToolTip = LocalizationManager.Get("SwitchToNormalMode"); 
                if (SimpleModeButton != null)
                    SimpleModeButton.ToolTip = LocalizationManager.Get("SwitchToSimpleMode"); 

                // 즉시편집 토글 툴팁
                if (InstantEditToggleBorder != null)
                    InstantEditToggleBorder.ToolTip = LocalizationManager.Get("InstantEdit");

                // 항상 위 / 캡처 카운터
                if (TopmostButton != null)
                    TopmostButton.ToolTip = LocalizationManager.Get("Pin"); 
                if (CaptureCounterButton != null)
                    CaptureCounterButton.ToolTip = LocalizationManager.Get("ViewCaptureList");

                // 캡처 관련 버튼 툴팁
                if (AreaCaptureButton != null)
                    AreaCaptureButton.ToolTip = LocalizationManager.Get("AreaCapture");
                if (DelayCaptureButton != null)
                {
                    DelayCaptureButton.ToolTip = LocalizationManager.Get("DelayCapture");
                    if (Delay3MenuItem != null) Delay3MenuItem.Header = LocalizationManager.Get("Delay3Sec");
                    if (Delay5MenuItem != null) Delay5MenuItem.Header = LocalizationManager.Get("Delay5Sec");
                    if (Delay10MenuItem != null) Delay10MenuItem.Header = LocalizationManager.Get("Delay10Sec");
                }
                if (FullScreenButton != null)
                    FullScreenButton.ToolTip = LocalizationManager.Get("FullScreen");
                if (DesignatedCaptureButton != null)
                    DesignatedCaptureButton.ToolTip = LocalizationManager.Get("DesignatedCapture");
                if (WindowCaptureButton != null)
                    WindowCaptureButton.ToolTip = LocalizationManager.Get("WindowCapture");
                if (UnitCaptureButton != null)
                    UnitCaptureButton.ToolTip = LocalizationManager.Get("UnitCapture");

                // 복사/저장/삭제
                if (CopyButton != null)
                    CopyButton.ToolTip = LocalizationManager.Get("Copy");
                if (CopyAllButton != null)
                    CopyAllButton.ToolTip = LocalizationManager.Get("CopyAll");
                if (SaveButton != null)
                    SaveButton.ToolTip = LocalizationManager.Get("Save");
                if (SaveAllButton != null)
                    SaveAllButton.ToolTip = LocalizationManager.Get("SaveAll");
                if (DeleteButton != null)
                    DeleteButton.ToolTip = LocalizationManager.Get("Delete");
                if (DeleteAllButton != null)
                    DeleteAllButton.ToolTip = LocalizationManager.Get("DeleteAll");

                // 기타
                if (HideToTrayButton != null)
                    HideToTrayButton.ToolTip = LocalizationManager.Get("HideToTray");
                if (SettingsButton != null)
                    SettingsButton.ToolTip = LocalizationManager.Get("Settings");
                if (FoldButton != null)
                    FoldButton.ToolTip = isFolded ? LocalizationManager.Get("Unfold") : LocalizationManager.Get("Fold");
            }
            catch { }
        }

        private void ShowAddIconMenu(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            if (this.TryFindResource("DarkContextMenu") is Style darkMenu)
                menu.Style = darkMenu;
            
            string[] allIcons = {
                "AreaCapture", "EdgeCapture", "DelayCapture", "FullScreen", "RealTimeCapture", "MultiCapture",
                "DesignatedCapture", "WindowCapture", "UnitCapture", "ScrollCapture", "OcrCapture",
                "ScreenRecord", "History", "MyNote", "Copy", "CopyAll", "Save", "SaveAll", 
                "Delete", "DeleteAll", "Settings"
            };
            
            if (settings != null)
            {
                foreach (var icon in allIcons)
                {
                    if (!settings.SimpleModeIcons.Contains(icon))
                    {
                        var item = new MenuItem { Header = GetIconDisplayName(icon) };
                        item.Icon = icon switch
                        {
                            "AreaCapture" => CreateMenuIcon("/icons/area_capture.png"),
                            "DelayCapture" => CreateMenuIcon("/icons/clock.png"),
                            "FullScreen" => CreateMenuIcon("/icons/full_screen.png"),
                            "RealTimeCapture" => CreateMenuIcon("/icons/real-time.png"),
                            "MultiCapture" => CreateMenuIcon("/icons/multi_capture.png"),
                            "DesignatedCapture" => CreateMenuIcon("/icons/designated.png"),
                            "WindowCapture" => CreateMenuIcon("/icons/window_cap.png"),
                            "UnitCapture" => CreateMenuIcon("/icons/unit_capture.png"),
                            "ScrollCapture" => CreateMenuIcon("/icons/scroll_capture.png"),
                            "OcrCapture" => CreateMenuIcon("/icons/ocr_capture.png"),
                            "ScreenRecord" => CreateMenuIcon("/icons/videocamera.png"),
                            "Copy" => CreateMenuIcon("/icons/copy_selected.png"),
                            "CopyAll" => CreateMenuIcon("/icons/copy_all.png"),
                            "Save" => CreateMenuIcon("/icons/save_selected.png"),
                            "SaveAll" => CreateMenuIcon("/icons/save_all.png"),
                            "Delete" => CreateMenuIcon("/icons/delete_selected.png"),
                            "DeleteAll" => CreateMenuIcon("/icons/delete_all.png"),
                            "MyNote" => CreateMenuIcon("/icons/my_note.png"),
                            "History" => CreateMenuIcon("/icons/histroy_note.png"),
                            "Settings" => CreateMenuIcon("/icons/setting.png"),
                            "EdgeCapture" => CreateMenuIcon("/icons/edge_capture.png"),
                            _ => null
                        };
                        if (this.TryFindResource("DarkMenuItem") is Style darkItem)
                            item.Style = darkItem;
                        item.Click += (s2, e2) => AddIcon(icon);
                        menu.Items.Add(item);
                    }
                }
            }
            
            var appsItem = new MenuItem { Header = LocalizationManager.Get("ComputerApps") };
            if (this.TryFindResource("DarkMenuItem") is Style darkApps)
                appsItem.Style = darkApps;
            appsItem.Icon = CreateMenuIcon("/icons/app.png") ?? CreateMenuIcon("/icons/setting.png");
            appsItem.Click += async (s2, e2) =>
            {
                var picked = await OpenAppPickerAsync();
                if (picked != null)
                {
                    if (settings == null) settings = Settings.Load();
                    settings.SimpleModeApps.Add(picked);
                    Settings.Save(settings);
                    BuildIconButtons();
                }
            };
            menu.Items.Add(appsItem);
            
            menu.PlacementTarget = sender as Button;
            menu.IsOpen = true;
        }
        
        // 텍스트 길이에 따라 최적 폰트 크기 계산
        private double GetOptimalFontSize(string text)
        {
            if (string.IsNullOrEmpty(text)) return 9.0;
            
            int length = text.Length;
            
            // 한글/한자는 2배로 계산 (더 넓은 공간 차지)
            int adjustedLength = 0;
            foreach (char c in text)
            {
                if (c > 127) // 비ASCII 문자 (한글, 한자 등)
                    adjustedLength += 2;
                else
                    adjustedLength += 1;
            }
            
            // 조정된 길이에 따라 폰트 크기 결정
            if (adjustedLength <= 8)
                return 9.0;   // 짧은 텍스트
            else if (adjustedLength <= 12)
                return 8.0;   // 중간 텍스트
            else if (adjustedLength <= 16)
                return 7.5;   // 긴 텍스트
            else
                return 7.0;   // 매우 긴 텍스트
        }
    }
}