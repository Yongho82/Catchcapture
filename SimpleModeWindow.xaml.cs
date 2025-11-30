using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CatchCapture.Utilities;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;
using CatchCapture.Models;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using WpfImage = System.Windows.Controls.Image;
using DrawingIcon = System.Drawing.Icon;
using System.Windows.Interop;
using System.Linq;

namespace CatchCapture
{
    public partial class SimpleModeWindow : Window
    {
        // Win32 API
        private const int HWND_TOPMOST = -1;
        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_NOACTIVATE = 0x0010;

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private enum DockSide { None, Left, Right, Top }
        private DockSide _dockSide = DockSide.None;
        private bool _isCollapsed = false;
        private DispatcherTimer _collapseTimer;
        
        private int _delaySeconds = 3;
        private MainWindow? _mainWindow; 
        private Settings? settings;     

        private Popup? iconPopup;
        private bool _isPopupOpen = false;
        // A+ 사이즈 스케일 (1.0 기본, 1.15 크게)
        private double _uiScale = 1.0;
        public event EventHandler? AreaCaptureRequested;
        public event EventHandler? FullScreenCaptureRequested;
        public event EventHandler? ExitSimpleModeRequested;
        public event EventHandler? DesignatedCaptureRequested;
        
        public SimpleModeWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            Topmost = true;
            ShowInTaskbar = true;
            
            // 설정 로드 및 버튼 생성 ← 이 두 줄 추가
            LoadSettings();
            
            // 초기 상태: 가로 모드
            ApplyLayout(false);

            _collapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _collapseTimer.Tick += CollapseTimer_Tick;

            this.Loaded += (_, __) => 
            {
                ForceTopmost();
                
                // UI가 완전히 로드된 후 토글 상태 설정
                if (InstantEditToggleH != null)
                    InstantEditToggleH.IsChecked = settings?.SimpleModeInstantEdit ?? false;
                if (InstantEditToggleV != null)
                    InstantEditToggleV.IsChecked = settings?.SimpleModeInstantEdit ?? false;
            };
            this.Deactivated += (_, __) => 
            {
                if (_dockSide != DockSide.None && !_isCollapsed)
                    _collapseTimer.Start();
                ForceTopmost();
            };
            this.Activated += (_, __) => ForceTopmost();
            
            this.MouseLeftButtonUp += Window_MouseLeftButtonUp;

            // 초기 스케일 적용 (필요시 Settings에서 불러오기 가능)
            ApplyUIScale();
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

        // ★ 창 닫힐 때 이벤트 구독 해제 (OnClosing 오버라이드 또는 새로 추가)
        protected override void OnClosed(EventArgs e)
        {
            Settings.SettingsChanged -= OnSettingsChanged;
            base.OnClosed(e);
        }

        
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                BeginAnimation(LeftProperty, null);
                BeginAnimation(TopProperty, null);

                _dockSide = DockSide.None;
                _isCollapsed = false;
                _collapseTimer.Stop();
                
                try 
                { 
                    DragMove(); 
                    CheckDocking();
                    Dispatcher.BeginInvoke(new Action(CheckDocking), DispatcherPriority.ApplicationIdle);
                } 
                catch { }
            }
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            CheckDocking();
            Dispatcher.BeginInvoke(new Action(CheckDocking), DispatcherPriority.ApplicationIdle);
        }

        private void CheckDocking()
        {
            var workArea = SystemParameters.WorkArea;
            double snapDist = 80;

            double currentLeft = Left;
            double currentTop = Top;
            double currentRight = currentLeft + ActualWidth;

            // 좌측 도킹 - 창이 화면 밖으로 나가도 도킹 감지
            if (currentLeft < workArea.Left + snapDist)
            {
                ApplyLayout(true); // 세로 모드
                UpdateLayout();
                Left = workArea.Left;
                _dockSide = DockSide.Left;
            }
            // 우측 도킹 - 창이 화면 밖으로 나가도 도킹 감지
            else if (currentRight > workArea.Right - snapDist)
            {
                ApplyLayout(true); // 세로 모드
                UpdateLayout();
                Left = workArea.Right - this.Width;
                _dockSide = DockSide.Right;
            }
            // 상단 도킹
            else if (Math.Abs(currentTop - workArea.Top) < snapDist)
            {
                ApplyLayout(false); // 가로 모드
                UpdateLayout();
                Top = workArea.Top;
                _dockSide = DockSide.Top;
            }
            else
            {
                _dockSide = DockSide.None;
                ApplyLayout(false); // 도킹 해제 시 가로 모드
            }

            if (_dockSide != DockSide.None)
            {
                // 도킹 시 화면 밖으로 나가지 않도록 보정
                if (_dockSide == DockSide.Left || _dockSide == DockSide.Right)
                {
                    if (Top < workArea.Top) Top = workArea.Top;
                    if (Top + Height > workArea.Bottom) Top = workArea.Bottom - Height;
                }
                else if (_dockSide == DockSide.Top)
                {
                    if (Left < workArea.Left) Left = workArea.Left;
                    if (Left + Width > workArea.Right) Left = workArea.Right - Width;
                }

                if (!IsMouseOver) _collapseTimer.Start();
            }
        }

        private void ApplyLayout(bool vertical)
        {
            if (vertical)
            {
                HorizontalRoot.Visibility = Visibility.Collapsed;
                VerticalRoot.Visibility = Visibility.Visible;
                // 버튼 폭 44 + 좌우 여백/테두리 고려하여 여유 폭 확보
                int baseWidth = 58;
                if (_uiScale > 1.0) baseWidth += 2; // A+ 활성 시 2px 추가 여유
                Width = baseWidth;
                
        // ★ 동적 높이 계산 (90% 제한)
        if (settings != null && ButtonsPanelV.Children.Count > 0)
        {
            // 실제 ButtonsPanelV에 추가된 버튼 개수 사용
            int actualButtonCount = ButtonsPanelV.Children.Count;
            
            // 높이 계산: 기본 85px + (실제 버튼 개수 * 52px) + 여유 30px
            int calculatedHeight = (int)((85 + actualButtonCount * 52 + 30) * _uiScale);
            
            // 90% 제한 적용
            var workArea = SystemParameters.WorkArea;
            int maxHeight = (int)(workArea.Height * 0.9);
            
            Height = Math.Min(calculatedHeight, maxHeight);
        }
        else if (settings != null)
        {
            Height = 300 * _uiScale;
        }
        else
        {
            Height = 300 * _uiScale;
        }
            }
            else
            {
                VerticalRoot.Visibility = Visibility.Collapsed;
                HorizontalRoot.Visibility = Visibility.Visible;
                
                // 동적 너비 계산
                if (settings != null)
                {
                    int builtIn = settings.SimpleModeIcons?.Count ?? 0;
                    int external = settings.SimpleModeApps?.Count ?? 0;
                    int iconCount = builtIn + external + 1; // + 버튼 포함
                    // 버튼 폭 44 + 좌우 마진 2 → 아이템당 약 48px 가정
                    Width = (iconCount * 48 + 16) * _uiScale; // 스케일 반영
                }
                else
                {
                    Width = 200 * _uiScale;
                }
                
                Height = 85 * _uiScale;
            }
        }

        private void GetVisibilityCountsVertical(out int visibleIcons, out int visibleApps)
        {
            visibleIcons = settings.SimpleModeIcons.Count;
            visibleApps = settings.SimpleModeApps.Count;
            
            if (settings == null) return;

            var workArea = SystemParameters.WorkArea;
            int maxHeight = (int)(workArea.Height * 0.9);
            
            // ★ 수정: 실제 사용하는 높이 값으로 계산
            int baseHeight = 85;  // 기본 UI 높이
            int iconHeight = 52;  // 아이콘 하나당 실제 높이 (48 → 52)
            int extraSpace = 30;  // 여유 공간
            
            // ★ + 버튼과 ⇄ 버튼을 위한 공간 확보
            int reservedSlots = 2;  // + 버튼(1) + ⇄ 버튼(1)
            
            // 사용 가능한 높이에서 최대 슬롯 계산
            int availableHeight = maxHeight - baseHeight - extraSpace;
            int maxSlots = availableHeight / iconHeight;
            
            // 예약된 슬롯 제외
            int effectiveSlots = Math.Max(1, maxSlots - reservedSlots);
            
            int totalItems = settings.SimpleModeIcons.Count + settings.SimpleModeApps.Count;
            
            if (totalItems > effectiveSlots)
            {
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
        private List<string> GetHiddenIconsVertical()
        {
            if (settings == null) return new List<string>();
            
            GetVisibilityCountsVertical(out int visibleIconsCount, out int visibleAppsCount);
            
            var hiddenIcons = new List<string>();
            int iconsToHide = settings.SimpleModeIcons.Count - visibleIconsCount;
            
            if (iconsToHide > 0)
            {
                int hiddenCount = 0;
                for (int i = settings.SimpleModeIcons.Count - 1; i >= 0 && hiddenCount < iconsToHide; i--)
                {
                    string iconName = settings.SimpleModeIcons[i];
                    if (iconName == "Settings") continue;
                    
                    hiddenIcons.Add(iconName);
                    hiddenCount++;
                }
            }
            return hiddenIcons;
        }

        private List<ExternalAppShortcut> GetHiddenAppsVertical()
        {
            if (settings == null) return new List<ExternalAppShortcut>();
            
            GetVisibilityCountsVertical(out int visibleIconsCount, out int visibleAppsCount);
            
            var hiddenApps = new List<ExternalAppShortcut>();
            int appsToHide = settings.SimpleModeApps.Count - visibleAppsCount;
            
            if (appsToHide > 0)
            {
                int startIndex = settings.SimpleModeApps.Count - appsToHide;
                if (startIndex < 0) startIndex = 0;

                for (int i = startIndex; i < settings.SimpleModeApps.Count; i++)
                {
                    hiddenApps.Add(settings.SimpleModeApps[i]);
                }
            }
            return hiddenApps;
        }
        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            _collapseTimer.Stop();
            if (_dockSide != DockSide.None && _isCollapsed)
            {
                Expand();
            }
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            // DelayMenu 체크 제거 (동적 생성으로 변경됨)
            
            if (_dockSide != DockSide.None && !_isCollapsed)
            {
                _collapseTimer.Start();
            }
        }

        private void CollapseTimer_Tick(object? sender, EventArgs e)
        {
            _collapseTimer.Stop();
            
            if (_isPopupOpen) return;
            
            if (IsMouseOver) return;
            
            Collapse();
        }

        private void Collapse()
        {
            if (_dockSide == DockSide.None) return;
            _isCollapsed = true;
            
            double targetLeft = Left;
            double targetTop = Top;
            double peekAmount = 8;

            if (_dockSide == DockSide.Left) targetLeft = -ActualWidth + peekAmount;
            else if (_dockSide == DockSide.Right) targetLeft = SystemParameters.WorkArea.Right - peekAmount;
            else if (_dockSide == DockSide.Top) targetTop = -ActualHeight + peekAmount;

            AnimateWindow(targetLeft, targetTop);
        }

        private void Expand()
        {
            if (_dockSide == DockSide.None) return;
            _isCollapsed = false;

            var workArea = SystemParameters.WorkArea;
            double targetLeft = Left;
            double targetTop = Top;

            if (_dockSide == DockSide.Left) targetLeft = workArea.Left;
            else if (_dockSide == DockSide.Right) targetLeft = workArea.Right - ActualWidth;
            else if (_dockSide == DockSide.Top) targetTop = workArea.Top;

            AnimateWindow(targetLeft, targetTop);
        }

        private void AnimateWindow(double toLeft, double toTop)
        {
            var animLeft = new DoubleAnimation(toLeft, TimeSpan.FromMilliseconds(200)) 
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
            var animTop = new DoubleAnimation(toTop, TimeSpan.FromMilliseconds(200)) 
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };

            animLeft.Completed += (s, e) => { Left = toLeft; };
            animTop.Completed += (s, e) => { Top = toTop; };
            
            BeginAnimation(LeftProperty, animLeft);
            BeginAnimation(TopProperty, animTop);
        }

        private void MinimizeToTrayButton_Click(object sender, RoutedEventArgs e)
        {
            // ★ 간편모드 상태 저장
            var settings = Settings.Load();
            settings.LastActiveMode = "Simple";
            Settings.Save(settings);
            
            if (_mainWindow != null)
            {
                _mainWindow.SwitchToTrayMode();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // ★ 간편모드 상태 저장
            var settings = Settings.Load();
            settings.LastActiveMode = "Simple";
            Settings.Save(settings);
            
            // ★ MainWindow에 간편모드가 숨겨졌음을 알림
            if (_mainWindow != null)
            {
                _mainWindow.OnSimpleModeHidden();
            }
            
            if (Application.Current.MainWindow is MainWindow mw && mw.notifyIcon != null)
            {
                mw.notifyIcon.Visible = true;
            }
            Hide();
        }

        private void ExitSimpleModeButton_Click(object sender, RoutedEventArgs e)
        {
            ExitSimpleModeRequested?.Invoke(this, EventArgs.Empty);
            Close();
        }

        private void AreaCaptureButton_Click(object sender, RoutedEventArgs e) => PerformCapture(AreaCaptureRequested);
        private void FullScreenCaptureButton_Click(object sender, RoutedEventArgs e) => PerformCapture(FullScreenCaptureRequested);
        private void DesignatedButton_Click(object sender, RoutedEventArgs e) => PerformCapture(DesignatedCaptureRequested);
        private async void PerformCapture(EventHandler? handler)
        {
            this.Opacity = 0;
            await Task.Delay(30);
            Hide();
            await Task.Delay(50);
            handler?.Invoke(this, EventArgs.Empty);
            this.Opacity = 1;
        }      
        private void ShowCopiedNotification()
        {
            var notification = new GuideWindow("클립보드에 복사되었습니다", TimeSpan.FromSeconds(0.4));
            notification.Owner = this;
            Show();
            notification.Show();
        }

        private void StartDelayedAreaCapture(int seconds)
        {
            var countdown = new GuideWindow("", null) { Owner = this };
            countdown.Show();
            countdown.StartCountdown(seconds, () => Dispatcher.Invoke(() => PerformCapture(AreaCaptureRequested)));
        }

        private void ForceTopmost()
        {
            try {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            } catch { }
        }

        private void SimpleModeWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.M && Keyboard.Modifiers == ModifierKeys.Control)
            {
                ExitSimpleModeButton_Click(sender, e);
                e.Handled = true;
            }
        }
        private void InstantEditToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (settings == null) return;
            
            var toggle = sender as ToggleButton;
            if (toggle == null) return;
            
            settings.SimpleModeInstantEdit = toggle.IsChecked == true;
            
            // 두 토글 버튼 동기화
            if (InstantEditToggleH != null && sender != InstantEditToggleH)
                InstantEditToggleH.IsChecked = settings.SimpleModeInstantEdit;
            if (InstantEditToggleV != null && sender != InstantEditToggleV)
                InstantEditToggleV.IsChecked = settings.SimpleModeInstantEdit;
            
            Settings.Save(settings);
        }

        private void LoadSettings()
        {
            settings = Settings.Load();
            BuildIconButtons();
            
            // 즉시편집 토글 상태 설정
            if (InstantEditToggleH != null)
                InstantEditToggleH.IsChecked = settings.SimpleModeInstantEdit;
            if (InstantEditToggleV != null)
                InstantEditToggleV.IsChecked = settings.SimpleModeInstantEdit;
        }

        private void BuildIconButtons()
        {
            if (settings == null) return;
            
            // 가로 모드 버튼 생성 (기존 코드 그대로)
            ButtonsPanelH.Children.Clear();
            foreach (var iconName in settings.SimpleModeIcons)
            {
                var button = CreateIconButton(iconName, false);
                ButtonsPanelH.Children.Add(button);
            }
            foreach (var app in settings.SimpleModeApps)
            {
                var button = CreateAppButton(app, false);
                ButtonsPanelH.Children.Add(button);
            }
            ButtonsPanelH.Children.Add(CreateAddButton(false));
            
        // ★ 세로 모드 버튼 생성 (90% 로직 적용)
        ButtonsPanelV.Children.Clear();

        var hiddenIcons = GetHiddenIconsVertical();
        var hiddenApps = GetHiddenAppsVertical();

        // 보이는 아이콘만 추가
        foreach (var iconName in settings.SimpleModeIcons)
        {
            if (iconName == "Settings" || !hiddenIcons.Contains(iconName))
            {
                var button = CreateIconButton(iconName, true);
                ButtonsPanelV.Children.Add(button);
            }
        }

        // 보이는 앱만 추가
        foreach (var app in settings.SimpleModeApps)
        {
            if (!hiddenApps.Contains(app))
            {
                var button = CreateAppButton(app, true);
                ButtonsPanelV.Children.Add(button);
            }
        }

        // ★ 숨겨진 항목이 있으면 ⇄ 버튼 추가
        if (hiddenIcons.Count > 0 || hiddenApps.Count > 0)
        {
            var expandButton = new Button
            {
                Content = "⇄",
                FontSize = 12,  // 16 → 12로 축소
                FontWeight = FontWeights.Bold,
                Style = this.FindResource("IconButtonStyle") as Style,
                ToolTip = "숨겨진 아이콘 보기",
                Opacity = 0.6,
                Height = 32  // 높이 32px로 제한
            };
            expandButton.Click += ExpandButton_Click;
            ButtonsPanelV.Children.Add(expandButton);
        }

        // + 버튼 추가
        ButtonsPanelV.Children.Add(CreateAddButton(true));
        // ★ 레이아웃 강제 업데이트
        ButtonsPanelV.UpdateLayout();

        // 창 크기 조정
        bool isVertical = VerticalRoot.Visibility == Visibility.Visible;
        ApplyLayout(isVertical);
        }

        private void ExpandButton_Click(object sender, RoutedEventArgs e)
        {
            ShowIconPopupVertical();
        }

        private void ShowIconPopupVertical()
        {
            if (settings == null) return;
            
            var hiddenIcons = GetHiddenIconsVertical();
            var hiddenApps = GetHiddenAppsVertical();
            
            if (hiddenIcons.Count == 0 && hiddenApps.Count == 0) return;
            
            // ★ Popup 열릴 때 collapse 타이머 중지
            _collapseTimer?.Stop();
            _isPopupOpen = true; // ★ 플래그 설정
            
            // Popup 생성
            iconPopup = new Popup
            {
                AllowsTransparency = true,
                StaysOpen = false,
                Placement = PlacementMode.Left,
                PlacementTarget = this,
                HorizontalOffset = -10
            };
            
            // ★ Popup 닫힐 때 이벤트 핸들러 추가
            iconPopup.Closed += (s, e) =>
            {
                _isPopupOpen = false; // ★ 플래그 해제
                
                // Popup 닫힌 후 마우스가 창 밖에 있으면 타이머 시작
                if (_dockSide != DockSide.None && !IsMouseOver && !_isCollapsed)
                {
                    _collapseTimer?.Start();
                }
            };
            
            // Popup 내용
            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5)
            };
            
            var stackPanel = new StackPanel { Orientation = Orientation.Vertical };
            
            // ... 나머지 코드는 그대로
            
            // 숨겨진 아이콘 버튼 추가
            foreach (var iconName in hiddenIcons)
            {
                var grid = new Grid { Height = 48 };
                
                var button = new Button
                {
                    Style = this.FindResource("IconButtonStyle") as Style,
                    ToolTip = GetIconDisplayName(iconName)
                };
                
                var stack = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                
                var img = CreateIconImage(iconName);
                if (img != null) stack.Children.Add(img);
                
                var text = new TextBlock
                {
                    Text = GetIconLabel(iconName),
                    FontSize = 9,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                stack.Children.Add(text);
                
                button.Content = stack;
                button.Click += (s, ev) =>
                {
                    HandleIconClick(iconName);
                    iconPopup.IsOpen = false;
                };
                
                grid.Children.Add(button);
                
                // 삭제 버튼
                var remove = CreateRemoveButton(iconName);
                remove.Visibility = Visibility.Collapsed;
                grid.Children.Add(remove);
                
                grid.MouseEnter += (s, ev) => remove.Visibility = Visibility.Visible;
                grid.MouseLeave += (s, ev) => remove.Visibility = Visibility.Collapsed;
                
                stackPanel.Children.Add(grid);
            }
            
            // 숨겨진 앱 버튼 추가
            foreach (var app in hiddenApps)
            {
                var grid = new Grid { Height = 48 };
                
                var button = new Button
                {
                    Style = this.FindResource("IconButtonStyle") as Style,
                    ToolTip = app.DisplayName
                };
                
                var stack = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                
                var img = CreateAppImage(app);
                if (img != null) stack.Children.Add(img);
                
                var text = new TextBlock
                {
                    Text = TruncateForLabel(app.DisplayName, 6),
                    FontSize = 9,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                stack.Children.Add(text);
                
                button.Content = stack;
                button.Click += (s, ev) =>
                {
                    HandleAppClick(app);
                    iconPopup.IsOpen = false;
                };
                
                grid.Children.Add(button);
                
                // 삭제 버튼
                var remove = CreateRemoveButtonForApp(app);
                remove.Visibility = Visibility.Collapsed;
                grid.Children.Add(remove);
                
                grid.MouseEnter += (s, ev) => remove.Visibility = Visibility.Visible;
                grid.MouseLeave += (s, ev) => remove.Visibility = Visibility.Collapsed;
                
                stackPanel.Children.Add(grid);
            }
            
            border.Child = stackPanel;
            iconPopup.Child = border;
            iconPopup.IsOpen = true;
        }


        private string TruncateForLabel(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (text.Length <= maxLength) return text;
            return text.Substring(0, maxLength);
        }
        private UIElement CreateIconButton(string iconName, bool isVertical)  // Button → UIElement로 변경
        {
            // Grid로 감싸기 (버튼 + 삭제 버튼)
            var grid = new Grid();
            
            var button = new Button
            {
                Style = this.FindResource("IconButtonStyle") as Style
            };
            
            // 아이콘과 텍스트를 담을 StackPanel
            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            // 아이콘 이미지
            var image = CreateIconImage(iconName);
            if (image != null)
            {
                stackPanel.Children.Add(image);
            }
            
            // 텍스트 레이블
            var textBlock = new TextBlock
            {
                Text = GetIconLabel(iconName),
                FontSize = 8 * _uiScale,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102))
            };
            stackPanel.Children.Add(textBlock);
            
            button.Content = stackPanel;
            button.ToolTip = GetIconDisplayName(iconName);
            
            // 클릭 이벤트 연결
            button.Click += (s, e) => HandleIconClick(iconName);
            
            grid.Children.Add(button);
            
            // - 버튼 (좌측 상단, 기본 숨김)
            var removeButton = CreateRemoveButton(iconName);
            removeButton.Visibility = Visibility.Collapsed;
            grid.Children.Add(removeButton);
            
            // 호버 이벤트
            grid.MouseEnter += (s, e) => removeButton.Visibility = Visibility.Visible;
            grid.MouseLeave += (s, e) => removeButton.Visibility = Visibility.Collapsed;
            
            return grid;  // Grid 반환
        }

        // 내장 아이콘용 제거 버튼 생성 (CreateIconButton에서 사용)
        private Button CreateRemoveButton(string iconName)
        {
            var btn = new Button
            {
                Content = "−",
                Width = 12,
                Height = 12,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromArgb(200, 255, 80, 80)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(2, 2, 0, 0),
                Cursor = Cursors.Hand,
                Padding = new Thickness(0, -5, 0, 0)
            };

            // 둥근 모서리 템플릿
            var template = new ControlTemplate(typeof(Button));
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            factory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));

            var textFactory = new FrameworkElementFactory(typeof(TextBlock));
            textFactory.SetValue(TextBlock.TextProperty, "−");
            textFactory.SetValue(TextBlock.FontSizeProperty, 10.0);
            textFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            textFactory.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            textFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            textFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            textFactory.SetValue(TextBlock.MarginProperty, new Thickness(0, -3, 0, 0));

            factory.AppendChild(textFactory);
            template.VisualTree = factory;

            btn.Template = template;
            btn.Click += (s, e) =>
            {
                RemoveIcon(iconName);
                e.Handled = true; // 상위 클릭 전파 방지
            };

            return btn;
        }

        private WpfImage? CreateIconImage(string iconName)
        {
            string? iconPath = iconName switch
            {
                "AreaCapture" => "/icons/area_capture.png",
                "DelayCapture" => "/icons/clock.png",
                "RealTimeCapture" => "/icons/real-time.png",
                "FullScreen" => "/icons/full_screen.png",
                "DesignatedCapture" => "/icons/designated.png",
                "WindowCapture" => "/icons/window_cap.png",
                "UnitCapture" => "/icons/unit_capture.png",
                "ScrollCapture" => "/icons/scroll_capture.png",
                // ★ 새로 추가
                "Copy" => "/icons/copy_selected.png",
                "CopyAll" => "/icons/copy_all.png",
                "Save" => "/icons/save_selected.png",
                "SaveAll" => "/icons/save_all.png",
                "Delete" => "/icons/delete_selected.png",
                "DeleteAll" => "/icons/delete_all.png",
                "Settings" => "/icons/setting.png",
                _ => null
            };
            
            if (iconPath == null) return null;
            
            var image = new WpfImage
            {
                Source = new BitmapImage(new Uri(iconPath!, UriKind.Relative)),
                Width = 20 * _uiScale,
                Height = 20 * _uiScale
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            
            return image;
        }

        private string GetIconLabel(string iconName)
        {
            return iconName switch
            {
                "AreaCapture" => CatchCapture.Models.LocalizationManager.Get("AreaCapture"),
                "DelayCapture" => CatchCapture.Models.LocalizationManager.Get("DelayCapture"),
                "RealTimeCapture" => CatchCapture.Models.LocalizationManager.Get("RealTimeCapture"),
                "FullScreen" => CatchCapture.Models.LocalizationManager.Get("FullScreen"),
                "DesignatedCapture" => CatchCapture.Models.LocalizationManager.Get("DesignatedCapture"),
                "WindowCapture" => CatchCapture.Models.LocalizationManager.Get("WindowCapture"),
                "UnitCapture" => CatchCapture.Models.LocalizationManager.Get("ElementCapture"),
                "ScrollCapture" => CatchCapture.Models.LocalizationManager.Get("ScrollCapture"),
                // ★ "Copy" → "CopySelected"로 변경
                "Copy" => CatchCapture.Models.LocalizationManager.Get("CopySelected"),
                "CopyAll" => CatchCapture.Models.LocalizationManager.Get("CopyAll"),
                "Save" => CatchCapture.Models.LocalizationManager.Get("Save"),
                "SaveAll" => CatchCapture.Models.LocalizationManager.Get("SaveAll"),
                "Delete" => CatchCapture.Models.LocalizationManager.Get("Delete"),
                "DeleteAll" => CatchCapture.Models.LocalizationManager.Get("DeleteAll"),
                "Settings" => CatchCapture.Models.LocalizationManager.Get("Settings"),
                _ => string.Empty
            };
        }

        private string GetIconDisplayName(string iconName)
        {
            return iconName switch
            {
                "AreaCapture" => CatchCapture.Models.LocalizationManager.Get("AreaCapture"),
                "DelayCapture" => CatchCapture.Models.LocalizationManager.Get("DelayCapture"),
                "RealTimeCapture" => CatchCapture.Models.LocalizationManager.Get("RealTimeCapture"),
                "FullScreen" => CatchCapture.Models.LocalizationManager.Get("FullScreen"),
                "DesignatedCapture" => CatchCapture.Models.LocalizationManager.Get("DesignatedCapture"),
                "WindowCapture" => CatchCapture.Models.LocalizationManager.Get("WindowCapture"),
                "UnitCapture" => CatchCapture.Models.LocalizationManager.Get("ElementCapture"),
                "ScrollCapture" => CatchCapture.Models.LocalizationManager.Get("ScrollCapture"),
                // ★ 새로 추가
                "Copy" => CatchCapture.Models.LocalizationManager.Get("Copy"),
                "CopyAll" => CatchCapture.Models.LocalizationManager.Get("CopyAll"),
                "Save" => CatchCapture.Models.LocalizationManager.Get("Save"),
                "SaveAll" => CatchCapture.Models.LocalizationManager.Get("SaveAll"),
                "Delete" => CatchCapture.Models.LocalizationManager.Get("Delete"),
                "DeleteAll" => CatchCapture.Models.LocalizationManager.Get("DeleteAll"),
                "Settings" => CatchCapture.Models.LocalizationManager.Get("Settings"),
                _ => iconName
            };
        }

        private void HandleIconClick(string iconName)
        {
            switch (iconName)
            {
                case "AreaCapture":
                    PerformCapture(AreaCaptureRequested);
                    break;
                case "DelayCapture":
                    StartDelayedAreaCapture(3);
                    break;
                case "FullScreen":
                    PerformCapture(FullScreenCaptureRequested);
                    break;
                case "DesignatedCapture":
                    PerformCapture(DesignatedCaptureRequested);
                    break;
                case "RealTimeCapture":
                    PerformCustomCapture(() => _mainWindow?.TriggerRealTimeCapture());
                    break;
                case "WindowCapture":
                    PerformCustomCapture(() => _mainWindow?.TriggerWindowCapture());
                    break;
                case "UnitCapture":
                    PerformCustomCapture(() => _mainWindow?.TriggerUnitCapture());
                    break;
                case "ScrollCapture":
                    PerformCustomCapture(() => _mainWindow?.TriggerScrollCapture());
                    break;
                // ★ 새로 추가
                case "Copy":
                    _mainWindow?.CopySelectedImage();
                    break;
                case "CopyAll":
                    _mainWindow?.CopyAllImages();
                    break;
                case "Save":
                    _mainWindow?.SaveSelectedImage();
                    break;
                case "SaveAll":
                    _mainWindow?.SaveAllImages();
                    break;
                case "Delete":
                    _mainWindow?.DeleteSelectedImage();
                    break;
                case "DeleteAll":
                    _mainWindow?.DeleteAllImages();
                    break;
                case "Settings":
                    _mainWindow?.OpenSettingsWindow();
                    break;
            }
        }

        private async void PerformCustomCapture(Action captureAction)
        {
            // 1단계: 투명하게
            this.Opacity = 0;
            await Task.Delay(50);
            
            // 2단계: 숨기기
            Hide();
            await Task.Delay(100);
            
            // 3단계: 캡처
            captureAction?.Invoke();
            
            // 4단계: 복원
            this.Opacity = 1;
            
            // 5단계: 간편모드 다시 표시 (이 부분 추가!)
            Show();
        }

        private Button CreateAddButton(bool isVertical)
        {
            var button = new Button
            {
                Content = "+",
                FontSize = isVertical ? 14 : 18,  // 세로 모드일 때 작게 (18 → 14)
                FontWeight = FontWeights.Bold,
                Style = this.FindResource("AddButtonStyle") as Style,
                ToolTip = CatchCapture.Models.LocalizationManager.Get("AddIcon")
            };
            
            // ★ 세로 모드일 때 높이 제한
            if (isVertical)
            {
                button.Height = 32;
            }
            
            button.Click += ShowAddIconMenu;
            
            // 호버 효과
            button.MouseEnter += (s, e) => button.Opacity = 1.0;
            button.MouseLeave += (s, e) => button.Opacity = 0.3;
            
            return button;
        }

        private void ShowAddIconMenu(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            // 다크 스타일 적용
            if (this.TryFindResource("DarkContextMenu") is Style darkMenu)
                menu.Style = darkMenu;

            // ★ 수정됨: 트레이 모드와 동일하게 모든 아이콘 포함
            var allIcons = new[] {
                "AreaCapture", "DelayCapture", "FullScreen", "RealTimeCapture",
                "DesignatedCapture", "WindowCapture", "UnitCapture", "ScrollCapture",
                "Copy", "CopyAll", "Save", "SaveAll", 
                "Delete", "DeleteAll", "Settings"
            };

            if (settings != null)
            {
                foreach (var icon in allIcons)
                {
                    if (!settings.SimpleModeIcons.Contains(icon))
                    {
                        var item = new MenuItem { Header = GetIconDisplayName(icon) };
                        // 아이콘 매핑 (PNG)
                        item.Icon = icon switch
                        {
                            "AreaCapture" => CreateMenuIcon("/icons/area_capture.png"),
                            "DelayCapture" => CreateMenuIcon("/icons/clock.png"),
                            "FullScreen" => CreateMenuIcon("/icons/full_screen.png"),
                            "RealTimeCapture" => CreateMenuIcon("/icons/real-time.png"),
                            "DesignatedCapture" => CreateMenuIcon("/icons/designated.png"),
                            "WindowCapture" => CreateMenuIcon("/icons/window_cap.png"),
                            "UnitCapture" => CreateMenuIcon("/icons/unit_capture.png"),
                            "ScrollCapture" => CreateMenuIcon("/icons/scroll_capture.png"),
                            "Copy" => CreateMenuIcon("/icons/copy_selected.png"),
                            "CopyAll" => CreateMenuIcon("/icons/copy_all.png"),
                            "Save" => CreateMenuIcon("/icons/save_selected.png"),
                            "SaveAll" => CreateMenuIcon("/icons/save_all.png"),
                            "Delete" => CreateMenuIcon("/icons/delete_selected.png"),
                            "DeleteAll" => CreateMenuIcon("/icons/delete_all.png"),
                            "Settings" => CreateMenuIcon("/icons/setting.png"),
                            _ => null
                        };
                        if (this.TryFindResource("DarkMenuItem") is Style darkItem)
                            item.Style = darkItem;
                        item.Click += (s2, e2) => AddIcon(icon);
                        menu.Items.Add(item);
                    }
                }

                // 구분선 및 컴퓨터 앱 추가 (아이콘이 있는 항목이 있을 때만)
                if (menu.Items.Count > 0)
                {
                    var sep = new Separator();
                    if (this.TryFindResource("LightSeparator") is Style sepStyle)
                        sep.Style = sepStyle;
                    menu.Items.Add(sep);
                }
                var appsItem = new MenuItem { Header = "컴퓨터 앱…" };
                if (this.TryFindResource("DarkMenuItem") is Style darkApps)
                    appsItem.Style = darkApps;
                // 앱 아이콘(옵션)
                appsItem.Icon = CreateMenuIcon("/icons/app.png") ?? CreateMenuIcon("/icons/setting.png");
                appsItem.Click += async (s2, e2) =>
                {
                    var picked = await OpenAppPickerAsync();
                    if (picked != null)
                    {
                        settings.SimpleModeApps.Add(picked);
                        Settings.Save(settings);
                        BuildIconButtons();
                    }
                };
                menu.Items.Add(appsItem);
            }

            menu.PlacementTarget = sender as Button;
            menu.IsOpen = true;
        }

        // PNG 아이콘 로더 (Pack URI 우선, 파일 경로 폴백) 16x16 디코드
        private Image? CreateMenuIcon(string relativePath)
        {
            try
            {
                BitmapImage bmp;
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
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var trimmed = relativePath.TrimStart('/', '\\');
                    var fullPath = System.IO.Path.Combine(baseDir, trimmed);
                    if (!System.IO.File.Exists(fullPath))
                        fullPath = System.IO.Path.Combine(baseDir, "icons", System.IO.Path.GetFileName(trimmed));

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

        // 외부 앱 버튼 생성
        private UIElement CreateAppButton(ExternalAppShortcut app, bool isVertical)
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
                FontSize = 8 * _uiScale,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102))
            };
            stack.Children.Add(text);
            
            button.Content = stack;
            button.Click += (s, e) => HandleAppClick(app);
            
            grid.Children.Add(button);
            var remove = CreateRemoveButtonForApp(app);
            remove.Visibility = Visibility.Collapsed;
            grid.Children.Add(remove);
            grid.MouseEnter += (s, e) => remove.Visibility = Visibility.Visible;
            grid.MouseLeave += (s, e) => remove.Visibility = Visibility.Collapsed;
            
            return grid;
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
                var source = Imaging.CreateBitmapSourceFromHBitmap(hbitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(20, 20));
                return new WpfImage { Source = source, Width = 20 * _uiScale, Height = 20 * _uiScale };
            }
            catch { return null; }
        }

        private Button CreateRemoveButtonForApp(ExternalAppShortcut app)
        {
            // 독립적인 작은 제거 버튼 생성 (CreateRemoveButton에 의존하지 않도록)
            var btn = new Button
            {
                Width = 12,
                Height = 12,
                Background = new SolidColorBrush(Color.FromArgb(200, 255, 80, 80)),
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(2, 2, 0, 0),
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
                MessageBox.Show($"앱 실행 실패: {ex.Message}");
            }
        }

        // 간단 앱 선택기: 시작 메뉴의 .lnk를 나열 + 찾아보기로 exe 선택
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
                    (Registry.LocalMachine, @"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall"),
                    (Registry.CurrentUser, @"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall"),
                    (Registry.LocalMachine, @"SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall"),
                    (Registry.CurrentUser, @"SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall"),
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
                return new WpfImage { Source = source, Width = 20 * _uiScale, Height = 20 * _uiScale };
            }
            catch { return null; }
        }

        // 외부 앱 바로가기 제거
        private void RemoveApp(ExternalAppShortcut app)
        {
            var result = MessageBox.Show($"'{app.DisplayName}' 바로가기를 삭제하시겠습니까?", "바로가기 삭제", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes && settings != null)
            {
                settings.SimpleModeApps.RemoveAll(a => string.Equals(a.TargetPath, app.TargetPath, StringComparison.OrdinalIgnoreCase) && a.DisplayName == app.DisplayName);
                Settings.Save(settings);
                BuildIconButtons();
            }
        }

        // 외부 앱 라벨은 7자까지 표시 (ASCII), 5자까지 표시 (한글)
        private static string TruncateForLabel(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var trimmed = s.Trim();
            bool asciiOnly = true;
            foreach (var ch in trimmed)
            {
                if (ch > 127)
                {
                    asciiOnly = false; break;
                }
                // 허용: 영문/숫자/공백/하이픈/언더스코어
                if (!(char.IsLetterOrDigit(ch) || ch == ' ' || ch == '-' || ch == '_'))
                {
                    // 다른 ASCII 기호가 있으면 영어 전용으로 간주하지 않음
                    asciiOnly = false; break;
                }
            }
            int max = asciiOnly ? 7 : 5;
            return trimmed.Length <= max ? trimmed : trimmed.Substring(0, max);
        }

        // A+ 버튼 클릭: UI 스케일 토글 및 아이콘/텍스트 크기 반영
        private void APlusButton_Click(object sender, RoutedEventArgs e)
        {
            _uiScale = (_uiScale < 1.1) ? 1.15 : 1.0;
            ApplyUIScale();
            // 현재 레이아웃 기준으로 창 크기 재적용
            bool isVertical = VerticalRoot.Visibility == Visibility.Visible;
            ApplyLayout(isVertical);
        }

        private void ApplyUIScale()
        {
            try
            {
                // 전체 루트에 LayoutTransform으로 스케일 적용 (텍스트/아이콘 가독성↑)
                var scale = new ScaleTransform(_uiScale, _uiScale);
                if (HorizontalRoot != null) HorizontalRoot.LayoutTransform = scale;
                if (VerticalRoot != null) VerticalRoot.LayoutTransform = scale;
            }
            catch { }
        }

        // 누락된 헬퍼 복원: + 메뉴에서 내장 아이콘 추가
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

        // 누락된 헬퍼 복원: 내장 아이콘 제거 (우측 상단 - 버튼에서 호출)
        private void RemoveIcon(string iconName)
        {
            var result = MessageBox.Show(
                $"'{GetIconDisplayName(iconName)}' 아이콘을 삭제하시겠습니까?",
                "아이콘 삭제",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                if (settings == null)
                {
                    settings = Settings.Load();
                }
                
                settings.SimpleModeIcons.Remove(iconName);
                Settings.Save(settings);
                BuildIconButtons();
            }
        }
    }
}