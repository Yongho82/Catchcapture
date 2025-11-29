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
                Width = 50;
                
                // 동적 높이 계산
                if (settings != null)
                {
                    int iconCount = settings.SimpleModeIcons.Count + 1;
                    Height = 24 + iconCount * 54 + 35;
                }
                else
                {
                    Height = 230;
                }
            }
            else
            {
                HorizontalRoot.Visibility = Visibility.Visible;
                VerticalRoot.Visibility = Visibility.Collapsed;
                
                // 동적 너비 계산
                if (settings != null)
                {
                    int iconCount = settings.SimpleModeIcons.Count + 1;
                    Width = iconCount * 40 + 10;
                }
                else
                {
                    Width = 200;
                }
                
                Height = 85;
            }
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
            if (IsMouseOver) return;
            // DelayMenu 체크 제거 (동적 생성으로 변경됨)
            
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
            if (_mainWindow != null)
            {
                _mainWindow.SwitchToTrayMode();
            }
        }
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
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
            
            // 가로 모드 버튼 생성
            ButtonsPanelH.Children.Clear();
            foreach (var iconName in settings.SimpleModeIcons)
            {
                var button = CreateIconButton(iconName, false);
                ButtonsPanelH.Children.Add(button);
            }
            // + 버튼 추가 (가로)
            ButtonsPanelH.Children.Add(CreateAddButton(false));
            
            // 세로 모드 버튼 생성
            ButtonsPanelV.Children.Clear();
            foreach (var iconName in settings.SimpleModeIcons)
            {
                var button = CreateIconButton(iconName, true);
                ButtonsPanelV.Children.Add(button);
            }
            // + 버튼 추가 (세로)
            ButtonsPanelV.Children.Add(CreateAddButton(true));
            
            // 창 크기 조정
            AdjustWindowSize();
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
                FontSize = 8,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0),
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

        private Image? CreateIconImage(string iconName)
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
                _ => null
            };
            
            if (iconPath == null) return null;
            
            var image = new Image
            {
                Source = new BitmapImage(new Uri(iconPath!, UriKind.Relative)),
                Width = 20,
                Height = 20
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            
            return image;
        }

        private string GetIconLabel(string iconName)
        {
            // Use full names for clarity, matching main keys
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
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Style = this.FindResource("AddButtonStyle") as Style,
                ToolTip = CatchCapture.Models.LocalizationManager.Get("AddIcon")
            };
            
            button.Click += ShowAddIconMenu;
            
            // 호버 효과
            button.MouseEnter += (s, e) => button.Opacity = 1.0;
            button.MouseLeave += (s, e) => button.Opacity = 0.3;
            
            return button;
        }

        private void ShowAddIconMenu(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            
            var allIcons = new[] { 
                "AreaCapture", "DelayCapture", "FullScreen", "RealTimeCapture",
                "DesignatedCapture", "WindowCapture", "UnitCapture", "ScrollCapture"
            };
            
            if (settings != null)
            {
                foreach (var icon in allIcons)
                {
                    if (!settings.SimpleModeIcons.Contains(icon))
                    {
                        var item = new MenuItem { Header = GetIconDisplayName(icon) };
                        item.Click += (s2, e2) => AddIcon(icon);
                        menu.Items.Add(item);
                    }
                }
            }
            
            menu.PlacementTarget = sender as Button;
            menu.IsOpen = true;
        }

        private void AddIcon(string iconName)
        {
            settings = Settings.Load();
            settings.SimpleModeIcons.Add(iconName);
            Settings.Save(settings);
            BuildIconButtons();
        }

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
                settings = Settings.Load();
                settings.SimpleModeIcons.Remove(iconName);
                Settings.Save(settings);
                BuildIconButtons();
            }
        }

        private void AdjustWindowSize()
        {
            if (settings == null) return;
            
            // 현재 모드 확인 (가로 모드인지 세로 모드인지)
            bool isVertical = VerticalRoot.Visibility == Visibility.Visible;
            
            // ApplyLayout을 다시 호출하여 창 크기 조정
            ApplyLayout(isVertical);
        }
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
                e.Handled = true;  // 버튼 클릭 이벤트가 부모로 전파되지 않도록
            };
            
            return btn;
        }
    }
}