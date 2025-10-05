using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CatchCapture
{
    public partial class ShortcutSettingsWindow : Window
    {
        // 단축키 설정을 저장할 딕셔너리
        private Dictionary<string, string> shortcutSettings = new Dictionary<string, string>();
        
        public ShortcutSettingsWindow()
        {
            InitializeComponent();
            LoadShortcutSettings();
            DisplayShortcutSettings();
        }
        
        // 단축키 설정 로드
        private void LoadShortcutSettings()
        {
            // 현재는 기본값으로 설정 (향후 설정 파일에서 로드 가능)
            shortcutSettings = new Dictionary<string, string>
            {
                // 메인 창 단축키
                {"CopySelected", "Ctrl+C"},
                {"CopyAll", "Ctrl+Shift+C"},
                {"SaveSelected", "Ctrl+S"},
                {"SaveAll", "Ctrl+Shift+S"},
                {"DeleteSelected", "Delete"},
                {"DeleteAll", "Ctrl+Shift+Delete"},
                {"AreaCapture", "Ctrl+A"},
                {"FullScreenCapture", "Ctrl+F"},
                {"ToggleSimpleMode", "Ctrl+M"},
                {"PreviewWindow", "Ctrl+P"},
                {"Undo", "Ctrl+Z"},
                {"ScrollCapture", "Ctrl+R"},
                {"DesignatedCapture", "Ctrl+D"},
                {"OpenFile", "Ctrl+O"},
                {"NewCapture", "Ctrl+N"},
                {"ToggleTopmost", "Ctrl+T"},
                
                // 미리보기 창 단축키
                {"PreviewCopy", "Ctrl+C"},
                {"PreviewUndo", "Ctrl+Z"},
                {"PreviewRedo", "Ctrl+Y"},
                {"PreviewSave", "Ctrl+S"},
                {"ZoomIn", "Ctrl+Shift++"},
                {"ZoomOut", "Ctrl+-"},
                {"ResetZoom", "Ctrl+0"},
                {"CancelEditMode", "Esc"}
            };
        }
        
        // 단축키 설정 표시
        private void DisplayShortcutSettings()
        {
            // 메인 창 단축키
            CopySelectedKey!.Text = shortcutSettings["CopySelected"];
            CopySelectedKey!.Tag = "CopySelected";
            CopySelectedKey!.MouseDown += ShortcutKey_MouseDown;
            
            CopyAllKey!.Text = shortcutSettings["CopyAll"];
            CopyAllKey!.Tag = "CopyAll";
            CopyAllKey!.MouseDown += ShortcutKey_MouseDown;
            
            SaveSelectedKey!.Text = shortcutSettings["SaveSelected"];
            SaveSelectedKey!.Tag = "SaveSelected";
            SaveSelectedKey!.MouseDown += ShortcutKey_MouseDown;
            
            SaveAllKey!.Text = shortcutSettings["SaveAll"];
            SaveAllKey!.Tag = "SaveAll";
            SaveAllKey!.MouseDown += ShortcutKey_MouseDown;
            
            DeleteSelectedKey!.Text = shortcutSettings["DeleteSelected"];
            DeleteSelectedKey!.Tag = "DeleteSelected";
            DeleteSelectedKey!.MouseDown += ShortcutKey_MouseDown;
            
            DeleteAllKey!.Text = shortcutSettings["DeleteAll"];
            DeleteAllKey!.Tag = "DeleteAll";
            DeleteAllKey!.MouseDown += ShortcutKey_MouseDown;
            
            AreaCaptureKey!.Text = shortcutSettings["AreaCapture"];
            AreaCaptureKey!.Tag = "AreaCapture";
            AreaCaptureKey!.MouseDown += ShortcutKey_MouseDown;
            
            FullScreenCaptureKey!.Text = shortcutSettings["FullScreenCapture"];
            FullScreenCaptureKey!.Tag = "FullScreenCapture";
            FullScreenCaptureKey!.MouseDown += ShortcutKey_MouseDown;
            
            ToggleSimpleModeKey!.Text = shortcutSettings["ToggleSimpleMode"];
            ToggleSimpleModeKey!.Tag = "ToggleSimpleMode";
            ToggleSimpleModeKey!.MouseDown += ShortcutKey_MouseDown;
            
            PreviewWindowKey!.Text = shortcutSettings["PreviewWindow"];
            PreviewWindowKey!.Tag = "PreviewWindow";
            PreviewWindowKey!.MouseDown += ShortcutKey_MouseDown;
            
            UndoKey!.Text = shortcutSettings["Undo"];
            UndoKey!.Tag = "Undo";
            UndoKey!.MouseDown += ShortcutKey_MouseDown;
            
            ScrollCaptureKey!.Text = shortcutSettings["ScrollCapture"];
            ScrollCaptureKey!.Tag = "ScrollCapture";
            ScrollCaptureKey!.MouseDown += ShortcutKey_MouseDown;
            
            DesignatedCaptureKey!.Text = shortcutSettings["DesignatedCapture"];
            DesignatedCaptureKey!.Tag = "DesignatedCapture";
            DesignatedCaptureKey!.MouseDown += ShortcutKey_MouseDown;
            
            OpenFileKey!.Text = shortcutSettings["OpenFile"];
            OpenFileKey!.Tag = "OpenFile";
            OpenFileKey!.MouseDown += ShortcutKey_MouseDown;
            
            NewCaptureKey!.Text = shortcutSettings["NewCapture"];
            NewCaptureKey!.Tag = "NewCapture";
            NewCaptureKey!.MouseDown += ShortcutKey_MouseDown;
            
            ToggleTopmostKey!.Text = shortcutSettings["ToggleTopmost"];
            ToggleTopmostKey!.Tag = "ToggleTopmost";
            ToggleTopmostKey!.MouseDown += ShortcutKey_MouseDown;
            
            // 미리보기 창 단축키
            PreviewCopyKey!.Text = shortcutSettings["PreviewCopy"];
            PreviewCopyKey!.Tag = "PreviewCopy";
            PreviewCopyKey!.MouseDown += ShortcutKey_MouseDown;
            
            PreviewUndoKey!.Text = shortcutSettings["PreviewUndo"];
            PreviewUndoKey!.Tag = "PreviewUndo";
            PreviewUndoKey!.MouseDown += ShortcutKey_MouseDown;
            
            PreviewRedoKey!.Text = shortcutSettings["PreviewRedo"];
            PreviewRedoKey!.Tag = "PreviewRedo";
            PreviewRedoKey!.MouseDown += ShortcutKey_MouseDown;
            
            PreviewSaveKey!.Text = shortcutSettings["PreviewSave"];
            PreviewSaveKey!.Tag = "PreviewSave";
            PreviewSaveKey!.MouseDown += ShortcutKey_MouseDown;
            
            ZoomInKey!.Text = shortcutSettings["ZoomIn"];
            ZoomInKey!.Tag = "ZoomIn";
            ZoomInKey!.MouseDown += ShortcutKey_MouseDown;
            
            ZoomOutKey!.Text = shortcutSettings["ZoomOut"];
            ZoomOutKey!.Tag = "ZoomOut";
            ZoomOutKey!.MouseDown += ShortcutKey_MouseDown;
            
            ResetZoomKey!.Text = shortcutSettings["ResetZoom"];
            ResetZoomKey!.Tag = "ResetZoom";
            ResetZoomKey!.MouseDown += ShortcutKey_MouseDown;
            
            CancelEditModeKey!.Text = shortcutSettings["CancelEditMode"];
            CancelEditModeKey!.Tag = "CancelEditMode";
            CancelEditModeKey!.MouseDown += ShortcutKey_MouseDown;
        }
        
        // 단축키 클릭 이벤트 처리
        private void ShortcutKey_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock && textBlock.Tag is string actionName)
            {
                ChangeShortcut(actionName, textBlock);
            }
        }
        
        // 단축키 변경
        private void ChangeShortcut(string actionName, TextBlock displayTextBlock)
        {
            // 단축키 입력 대화상자 표시
            var inputWindow = new Window
            {
                Title = "단축키 변경",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 249, 252)),
                ResizeMode = ResizeMode.NoResize
            };
            
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            
            var instructionText = new TextBlock
            {
                Text = $"{actionName}의 새 단축키를 입력하세요:",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10)
            };
            Grid.SetRow(instructionText, 0);
            
            var keyDisplay = new TextBlock
            {
                Text = "키를 누르세요...",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                Margin = new Thickness(10)
            };
            Grid.SetRow(keyDisplay, 1);
            
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };
            Grid.SetRow(buttonPanel, 2);
            
            var okButton = new Button
            {
                Content = "확인",
                Width = 70,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0)
            };
            
            var cancelButton = new Button
            {
                Content = "취소",
                Width = 70,
                Height = 30
            };
            
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            
            grid.Children.Add(instructionText);
            grid.Children.Add(keyDisplay);
            grid.Children.Add(buttonPanel);
            
            inputWindow.Content = grid;
            
            // 키 입력 감지
            Key? newKey = null;
            ModifierKeys newModifiers = ModifierKeys.None;
            
            inputWindow.KeyDown += (s, e) =>
            {
                newKey = e.Key;
                newModifiers = Keyboard.Modifiers;
                
                // 표시 텍스트 업데이트
                string modifierText = "";
                if (newModifiers.HasFlag(ModifierKeys.Control)) modifierText += "Ctrl+";
                if (newModifiers.HasFlag(ModifierKeys.Shift)) modifierText += "Shift+";
                if (newModifiers.HasFlag(ModifierKeys.Alt)) modifierText += "Alt+";
                
                string keyText = newKey?.ToString() ?? string.Empty;
                // 특수 키 처리
                if (newKey == Key.OemPlus) keyText = "+";
                else if (newKey == Key.OemMinus) keyText = "-";
                else if (newKey == Key.OemQuestion) keyText = "/";
                else if (newKey == Key.OemComma) keyText = ",";
                else if (newKey == Key.OemPeriod) keyText = ".";
                else if (newKey == Key.Oem1) keyText = ";";
                else if (newKey == Key.Oem3) keyText = "`";
                else if (newKey == Key.Oem4) keyText = "[";
                else if (newKey == Key.Oem5) keyText = "\\";
                else if (newKey == Key.Oem6) keyText = "]";
                else if (newKey == Key.Oem7) keyText = "'";
                
                keyDisplay.Text = modifierText + keyText;
            };
            
            bool confirmed = false;
            okButton.Click += (s, e) =>
            {
                if (newKey.HasValue)
                {
                    confirmed = true;
                    inputWindow.Close();
                }
                else
                {
                    MessageBox.Show("키를 입력해주세요.", "입력 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            
            cancelButton.Click += (s, e) => inputWindow.Close();
            
            inputWindow.ShowDialog();
            
            // 확인 버튼을 눌렀을 때만 단축키 업데이트
            if (confirmed && newKey.HasValue)
            {
                string modifierText = "";
                if (newModifiers.HasFlag(ModifierKeys.Control)) modifierText += "Ctrl+";
                if (newModifiers.HasFlag(ModifierKeys.Shift)) modifierText += "Shift+";
                if (newModifiers.HasFlag(ModifierKeys.Alt)) modifierText += "Alt+";
                
                string keyText = newKey.Value.ToString();
                // 특수 키 처리
                if (newKey == Key.OemPlus) keyText = "+";
                else if (newKey == Key.OemMinus) keyText = "-";
                else if (newKey == Key.OemQuestion) keyText = "/";
                else if (newKey == Key.OemComma) keyText = ",";
                else if (newKey == Key.OemPeriod) keyText = ".";
                else if (newKey == Key.Oem1) keyText = ";";
                else if (newKey == Key.Oem3) keyText = "`";
                else if (newKey == Key.Oem4) keyText = "[";
                else if (newKey == Key.Oem5) keyText = "\\";
                else if (newKey == Key.Oem6) keyText = "]";
                else if (newKey == Key.Oem7) keyText = "'";
                else if (newKey >= Key.D0 && newKey <= Key.D9) keyText = keyText.Substring(1); // D0 -> 0
                else if (newKey >= Key.A && newKey <= Key.Z) keyText = keyText; // A-Z는 그대로
                else if (newKey >= Key.F1 && newKey <= Key.F24) keyText = keyText; // F1-F24는 그대로
                // 다른 키는 기본 이름 사용
                
                string newShortcut = modifierText + keyText;
                
                // 중복 검사
                bool isDuplicate = false;
                foreach (var kvp in shortcutSettings)
                {
                    if (kvp.Key != actionName && kvp.Value == newShortcut)
                    {
                        isDuplicate = true;
                        break;
                    }
                }
                
                if (isDuplicate)
                {
                    MessageBox.Show($"'{newShortcut}' 단축키는 이미 다른 기능에 사용 중입니다.", "중복 단축키", 
                                   MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    // 단축키 업데이트
                    shortcutSettings[actionName] = newShortcut;
                    displayTextBlock!.Text = newShortcut;
                    MessageBox.Show($"'{actionName}' 단축키가 '{newShortcut}'으로 변경되었습니다.", "단축키 변경 완료", 
                                   MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
        
        // 기본값으로 재설정
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("모든 단축키를 기본값으로 재설정하시겠습니까?", "단축키 재설정", 
                                        MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                LoadShortcutSettings(); // 기본값으로 재로드
                
                // 모든 텍스트 블록의 이벤트 핸들러 제거
                RemoveEventHandlers();
                
                // UI 업데이트
                DisplayShortcutSettings();
                MessageBox.Show("단축키가 기본값으로 재설정되었습니다.", "재설정 완료", 
                               MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        // 이벤트 핸들러 제거
        private void RemoveEventHandlers()
        {
            // 메인 창 단축키
            CopySelectedKey!.MouseDown -= ShortcutKey_MouseDown;
            CopyAllKey!.MouseDown -= ShortcutKey_MouseDown;
            SaveSelectedKey!.MouseDown -= ShortcutKey_MouseDown;
            SaveAllKey!.MouseDown -= ShortcutKey_MouseDown;
            DeleteSelectedKey!.MouseDown -= ShortcutKey_MouseDown;
            DeleteAllKey!.MouseDown -= ShortcutKey_MouseDown;
            AreaCaptureKey!.MouseDown -= ShortcutKey_MouseDown;
            FullScreenCaptureKey!.MouseDown -= ShortcutKey_MouseDown;
            ToggleSimpleModeKey!.MouseDown -= ShortcutKey_MouseDown;
            PreviewWindowKey!.MouseDown -= ShortcutKey_MouseDown;
            UndoKey!.MouseDown -= ShortcutKey_MouseDown;
            ScrollCaptureKey!.MouseDown -= ShortcutKey_MouseDown;
            DesignatedCaptureKey!.MouseDown -= ShortcutKey_MouseDown;
            OpenFileKey!.MouseDown -= ShortcutKey_MouseDown;
            NewCaptureKey!.MouseDown -= ShortcutKey_MouseDown;
            ToggleTopmostKey!.MouseDown -= ShortcutKey_MouseDown;
            
            // 미리보기 창 단축키
            PreviewCopyKey!.MouseDown -= ShortcutKey_MouseDown;
            PreviewUndoKey!.MouseDown -= ShortcutKey_MouseDown;
            PreviewRedoKey!.MouseDown -= ShortcutKey_MouseDown;
            PreviewSaveKey!.MouseDown -= ShortcutKey_MouseDown;
            ZoomInKey!.MouseDown -= ShortcutKey_MouseDown;
            ZoomOutKey!.MouseDown -= ShortcutKey_MouseDown;
            ResetZoomKey!.MouseDown -= ShortcutKey_MouseDown;
            CancelEditModeKey!.MouseDown -= ShortcutKey_MouseDown;
        }
        
        // 닫기
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}