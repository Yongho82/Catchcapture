using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CatchCapture.Models;

namespace CatchCapture
{
    public partial class TrayModeWindow : Window
    {
        private MainWindow mainWindow;
        private Settings settings;

        public TrayModeWindow(MainWindow owner)
        {
            InitializeComponent();
            mainWindow = owner;
            
            // 설정 로드 및 초기화
            LoadSettings();
            
            // 창 위치 설정
            PositionWindow();
        }

        private void LoadSettings()
        {
            settings = Settings.Load();
            BuildIconButtons();
        }

        private void PositionWindow()
        {
            // 주 모니터의 작업 영역 가져오기
            var workArea = SystemParameters.WorkArea;
            
            // 우측 하단에 위치 (트레이 근처)
            this.Left = workArea.Right - this.Width - 10;
            this.Top = workArea.Bottom - this.Height - 10;
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
            // 창 닫힐 때 현재 설정 저장
            Settings.Save(settings);
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
            // 아이콘 투명도 업데이트 (활성화 시 진하게, 비활성화 시 연하게)
            if (this.Topmost)
            {
                TopmostIcon.Opacity = 1.0; // 진한 회색 (활성)
            }
            else
            {
                TopmostIcon.Opacity = 0.3; // 연한 회색 (비활성)
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
            // 컨텍스트 메뉴 표시
            if (sender is FrameworkElement fe && fe.ContextMenu != null)
            {
                fe.ContextMenu.PlacementTarget = fe;
                fe.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
                fe.ContextMenu.IsOpen = true;
            }
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
        }
        private void BuildIconButtons()
        {
            // ButtonsPanel을 찾아서 기존 버튼들 제거 (캡처 카운터와 상단 컨트롤 제외)
            // settings.TrayModeIcons 리스트를 기반으로 동적으로 버튼 생성
            
            var buttonsPanel = FindButtonsPanel();
            if (buttonsPanel == null) return;
            
            // 기존 캡처 버튼들 제거 (구분선 이후)
            ClearCaptureButtons(buttonsPanel);
            
            // 설정에 있는 아이콘들만 추가
            foreach (var iconName in settings.TrayModeIcons)
            {
                AddIconButton(buttonsPanel, iconName);
            }
            
            // 빈 슬롯 추가 (+ 버튼)
            AddEmptySlot(buttonsPanel);
    
            // 창 높이 자동 조절
            AdjustWindowHeight();
        }
        private void AdjustWindowHeight()
        {
            // 아이콘 개수에 따라 창 높이 계산
            // 상단 컨트롤: ~30px
            // TopmostButton: 50px
            // CaptureCounter: 50px
            // 첫 번째 Separator: 21px
            // 각 아이콘: 50px
            // + 버튼: 50px
            // 여백: 30px (상하)
            
            int iconCount = settings.TrayModeIcons.Count;
            int baseHeight = 30 + 50 + 50 + 21 + 50 + 30; // 상단 + TopmostButton + Counter + Separator + + 버튼 + 여백
            int iconsHeight = iconCount * 50;
            
            int newHeight = baseHeight + iconsHeight;
            
            // 최소/최대 높이 제한
            newHeight = Math.Max(200, Math.Min(newHeight, 800));
            
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
            
            // 아이콘에 따라 클릭 이벤트 연결
            switch (iconName)
            {
                case "AreaCapture":
                    button.Click += AreaCaptureButton_Click;
                    button.Content = CreateImage("/icons/area_capture.png");
                    break;
                case "DelayCapture":
                    button.Click += DelayCaptureButton_Click;
                    button.Content = CreateImage("/icons/clock.png");
                    // ContextMenu 추가
                    button.ContextMenu = CreateDelayContextMenu();
                    break;
                case "RealTimeCapture":
                    button.Click += RealTimeCaptureButton_Click;
                    button.Content = CreateImage("/icons/clock.png");
                    break;
                case "FullScreen":
                    button.Click += FullScreenButton_Click;
                    button.Content = CreateImage("/icons/full_screen.png");
                    break;
                case "DesignatedCapture":
                    button.Click += DesignatedCaptureButton_Click;
                    button.Content = CreateImage("/icons/area_capture.png");
                    break;
                case "WindowCapture":
                    button.Click += WindowCaptureButton_Click;
                    button.Content = CreateImage("/icons/window_capture.png");
                    break;
                case "UnitCapture":
                    button.Click += UnitCaptureButton_Click;
                    button.Content = CreateImage("/icons/unit_capture.png");
                    break;
                case "Copy":
                    button.Click += CopyButton_Click;
                    button.Content = CreateImage("/icons/copy_selected.png");
                    break;
                case "CopyAll":
                    button.Click += CopyAllButton_Click;
                    button.Content = CreateImage("/icons/copy_all.png");
                    break;
                case "Save":
                    button.Click += SaveButton_Click;
                    button.Content = CreateImage("/icons/save_selected.png");
                    break;
                case "SaveAll":
                    button.Click += SaveAllButton_Click;
                    button.Content = CreateImage("/icons/save_all.png");
                    break;
                case "Delete":
                    button.Click += DeleteButton_Click;
                    button.Content = CreateImage("/icons/delete_selected.png");
                    break;
                case "DeleteAll":
                    button.Click += DeleteAllButton_Click;
                    button.Content = CreateImage("/icons/delete_all.png");
                    break;
                case "Settings":
                    button.Click += SettingsButton_Click;
                    button.Content = CreateImage("/icons/clock.png");
                    break;
            }
            
            return button;
        }

        private Image CreateImage(string source)
        {
            var image = new Image
            {
                Source = new BitmapImage(new Uri(source, UriKind.Relative)),
                Width = 24,
                Height = 24
            };
            
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            
            return image;
        }

        private ContextMenu CreateDelayContextMenu()
        {
            var menu = new ContextMenu();
            menu.Items.Add(new MenuItem { Header = "3초 후 캡처", Tag = "3" });
            menu.Items.Add(new MenuItem { Header = "5초 후 캡처", Tag = "5" });
            menu.Items.Add(new MenuItem { Header = "10초 후 캡처", Tag = "10" });
            
            foreach (MenuItem item in menu.Items)
            {
                item.Click += DelayMenuItem_Click;
            }
            
            return menu;
        }


        private Button CreateRemoveButton(string iconName)
        {
            // 작은 - 버튼 생성 (우측 상단 배치)
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
            var result = MessageBox.Show(
                $"'{GetIconDisplayName(iconName)}' 아이콘을 삭제하시겠습니까?",
                "아이콘 삭제",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );
            
            if (result == MessageBoxResult.Yes)
            {
                // 설정 다시 로드 (최신 상태 확보)
                settings = Settings.Load();
                
                settings.TrayModeIcons.Remove(iconName);
                Settings.Save(settings);
                
                BuildIconButtons();
            }
        }

        private void AddEmptySlot(StackPanel panel)
        {
            // + 버튼 (호버 시만 표시)
            var grid = new Grid 
            { 
                Height = 50,
                Background = Brushes.Transparent  // 투명 배경 추가 (호버 감지용)
            };
            
            var addButton = new Button
            {
                Content = "+",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Style = this.FindResource("IconButtonStyle") as Style,
                ToolTip = "아이콘 추가",
                Opacity = 0.3  // 기본적으로 반투명하게 표시
            };
            
            addButton.Click += ShowAddIconMenu;
            grid.Children.Add(addButton);
            
            // 호버 시 완전히 보이게
            grid.MouseEnter += (s, e) => addButton.Opacity = 1.0;
            grid.MouseLeave += (s, e) => addButton.Opacity = 0.3;
            
            panel.Children.Add(grid);
        }

        private void ShowAddIconMenu(object sender, RoutedEventArgs e)
        {
            // 추가 가능한 아이콘 목록 표시 (ContextMenu)
            var menu = new ContextMenu();
            
            var allIcons = new[] { 
                "AreaCapture", "DelayCapture", "FullScreen", "RealTimeCapture",
                "DesignatedCapture", "WindowCapture", "UnitCapture",
                "Copy", "CopyAll", "Save", "SaveAll", 
                "Delete", "DeleteAll", "Settings"
            };
            
            foreach (var icon in allIcons)
            {
                if (!settings.TrayModeIcons.Contains(icon))
                {
                    var item = new MenuItem { Header = GetIconDisplayName(icon) };
                    item.Click += (s2, e2) => AddIcon(icon);
                    menu.Items.Add(item);
                }
            }
            
            menu.PlacementTarget = sender as Button;
            menu.IsOpen = true;
        }

        private void AddIcon(string iconName)
        {
            // 설정 다시 로드 (최신 상태 확보)
            settings = Settings.Load();
            
            settings.TrayModeIcons.Add(iconName);
            Settings.Save(settings);
            
            BuildIconButtons();
        }

        private void RealTimeCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            mainWindow.TriggerRealTimeCapture();
        }
        private string GetIconDisplayName(string iconName)
        {
            // 아이콘 이름을 한글로 변환
            return iconName switch
            {
                "AreaCapture" => "영역 캡처",
                "DelayCapture" => "지연 캡처",
                "RealTimeCapture" => "실시간 캡처", 
                "FullScreen" => "전체화면",
                "DesignatedCapture" => "지정 캡처",
                "WindowCapture" => "창 캡처",
                "UnitCapture" => "단위 캡처",
                "Copy" => "복사",
                "CopyAll" => "전체 복사",
                "Save" => "저장",
                "SaveAll" => "전체 저장",
                "Delete" => "삭제",
                "DeleteAll" => "전체 삭제",
                "Settings" => "설정",
                _ => iconName
            };
        }
    }
}
