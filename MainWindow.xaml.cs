using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Media.Effects;
using CatchCapture.Models;
using CatchCapture.Utilities;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CatchCapture;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private List<CaptureImage> captures = new List<CaptureImage>();
    private int selectedIndex = -1;
    private Border? selectedBorder = null;
    private Settings settings;
    private SimpleModeWindow? simpleModeWindow = null;
    private Point lastPosition;
    private int captureDelaySeconds = 0;

    public MainWindow()
    {
        InitializeComponent();
        settings = Settings.Load();
        
        // 글로벌 단축키 등록
        RegisterGlobalHotkeys();
        
        // 로컬 단축키 등록
        AddKeyboardShortcuts();
        
        // 타이틀바 드래그 이벤트 설정
        this.MouseLeftButtonDown += Window_MouseLeftButtonDown;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 타이틀바 영역에서만 드래그 가능하도록 설정
        if (e.GetPosition(this).Y <= 24)
        {
            lastPosition = e.GetPosition(this);
            this.DragMove();
        }
    }
    
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        // 창 최소화
        this.WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        // 프로그램 종료
        Application.Current.Shutdown();
    }

    private void AddKeyboardShortcuts()
    {
        // Ctrl+C 단축키 처리
        KeyDown += MainWindow_KeyDown;
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        var mods = Keyboard.Modifiers;

        // Ctrl+C: 선택 복사
        if (e.Key == Key.C && mods == ModifierKeys.Control)
        {
            if (selectedIndex >= 0)
            {
                CopySelectedImage();
                e.Handled = true;
            }
            return;
        }

        // Ctrl+Shift+C: 모두 복사
        if (e.Key == Key.C && mods == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (captures.Count > 0)
            {
                CopyAllImages();
                e.Handled = true;
            }
            return;
        }

        // Ctrl+S: 선택 저장
        if (e.Key == Key.S && mods == ModifierKeys.Control)
        {
            if (selectedIndex >= 0)
            {
                SaveSelectedImage();
                e.Handled = true;
            }
            return;
        }

        // Ctrl+Shift+S: 모두 저장
        if (e.Key == Key.S && mods == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (captures.Count > 0)
            {
                SaveAllImages();
                e.Handled = true;
            }
            return;
        }

        // Delete: 선택 삭제
        if (e.Key == Key.Delete && mods == ModifierKeys.None)
        {
            if (selectedIndex >= 0)
            {
                DeleteSelectedImage();
                e.Handled = true;
            }
            return;
        }

        // Ctrl+Shift+Delete: 모두 삭제
        if (e.Key == Key.Delete && mods == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (captures.Count > 0)
            {
                DeleteAllImages();
                e.Handled = true;
            }
            return;
        }

        // Ctrl+A: 영역 캡처 시작
        if (e.Key == Key.A && mods == ModifierKeys.Control)
        {
            StartAreaCapture();
            e.Handled = true;
            return;
        }

        // Ctrl+F: 전체 화면 캡처
        if (e.Key == Key.F && mods == ModifierKeys.Control)
        {
            CaptureFullScreen();
            e.Handled = true;
            return;
        }

        // Ctrl+M: 간편 모드 토글
        if (e.Key == Key.M && mods == ModifierKeys.Control)
        {
            ToggleSimpleMode();
            e.Handled = true;
            return;
        }

        // Ctrl+P: 선택 미리보기 열기
        if (e.Key == Key.P && mods == ModifierKeys.Control)
        {
            if (selectedIndex >= 0 && selectedIndex < captures.Count)
            {
                ShowPreviewWindow(captures[selectedIndex].Image, selectedIndex);
                e.Handled = true;
            }
            return;
        }
    }

    #region 캡처 기능

    private void AreaCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        StartAreaCapture();
    }

    private void DelayCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            if (fe.ContextMenu != null)
            {
                fe.ContextMenu.PlacementTarget = fe;
                fe.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
                fe.ContextMenu.IsOpen = true;
            }
        }
    }

    private void DelayMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is string tagStr && int.TryParse(tagStr, out int seconds))
        {
            captureDelaySeconds = seconds;
        }
        else if (sender is MenuItem mi2 && mi2.Tag is int tagInt)
        {
            captureDelaySeconds = tagInt;
        }
        else
        {
            captureDelaySeconds = 0;
        }

        // 실시간 카운트다운 표시 후 캡처 시작
        if (captureDelaySeconds <= 0)
        {
            StartAreaCapture();
            return;
        }

        var countdown = new GuideWindow("", null)
        {
            Owner = this
        };
        countdown.Show();
        countdown.StartCountdown(captureDelaySeconds, () =>
        {
            // UI 스레드에서 실행
            Dispatcher.Invoke(StartAreaCapture);
        });
    }

    private void StartAreaCapture()
    {
        // 먼저 메인 창을 숨긴 후 캡처 오버레이를 생성해야 정지 배경에 창이 비포함됨
        this.Hide();
        System.Threading.Thread.Sleep(120); // 완전히 숨겨질 시간 확보

        // 영역 선택 창 표시 (이 시점에 배경 스크린샷을 찍음)
        var snippingWindow = new SnippingWindow(showGuideText: false);

        if (snippingWindow.ShowDialog() == true)
        {
            // 선택된 영역 캡처 - 동결된 프레임 우선 사용
            var selectedArea = snippingWindow.SelectedArea;
            var capturedImage = snippingWindow.SelectedFrozenImage ?? ScreenCaptureUtility.CaptureArea(selectedArea);
            AddCaptureToList(capturedImage);
        }

        this.Show();
        this.Activate();
    }

    private void FullScreenCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        CaptureFullScreen();
    }

    private void CaptureFullScreen()
    {
        // 전체 화면 캡처
        this.Hide();
        System.Threading.Thread.Sleep(200); // 창이 완전히 사라질 때까지 대기

        var capturedImage = ScreenCaptureUtility.CaptureScreen();
        AddCaptureToList(capturedImage);

        this.Show();
        this.Activate();
    }

    private void ScrollCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        CaptureScrollableWindow();
    }
    
    private void CaptureScrollableWindow()
    {
        try
        {
            // 안내 메시지 표시
            var guideWindow = new GuideWindow("캡처할 창을 클릭하고 Enter 키를 누르세요", TimeSpan.FromSeconds(2.5));
            guideWindow.Owner = this;
            guideWindow.Show();
            
            // 사용자가 다른 창을 선택할 수 있도록 기다림
            this.Hide();
            
            // 스크롤 캡처 수행
            var capturedImage = ScreenCaptureUtility.CaptureScrollableWindow();
            
            if (capturedImage != null)
            {
                AddCaptureToList(capturedImage);
                ShowGuideMessage("스크롤 캡처가 완료되었습니다.", TimeSpan.FromSeconds(1.5));
                
                // 캡처된 이미지 클립보드에 복사
                ScreenCaptureUtility.CopyImageToClipboard(capturedImage);
                ShowGuideMessage("이미지가 클립보드에 복사되었습니다.", TimeSpan.FromSeconds(1.5));
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"스크롤 캡처 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            // 창이 닫히지 않도록 항상 메인 창을 다시 표시
            this.Show();
            this.Activate();
        }
    }

    private void AddCaptureToList(BitmapSource image)
    {
        // 캡처 이미지 객체 생성
        var captureImage = new CaptureImage(image);
        captures.Add(captureImage);

        // UI에 이미지 추가 - 최신 캡처를 위에 표시하기 위해 인덱스 0에 추가
        var border = CreateCaptureItem(captureImage, captures.Count - 1);
        CaptureListPanel.Children.Insert(0, border);

        // 추가된 이미지 선택
        SelectCapture(border, captures.Count - 1);

        // 버튼 상태 업데이트
        UpdateButtonStates();
        
        // 캡처 개수 업데이트
        UpdateCaptureCount();
    }

    private Border CreateCaptureItem(CaptureImage captureImage, int index)
    {
        // 썸네일 크기 고정
        double thumbWidth = 200;
        double thumbHeight = 120;

        // 그리드 생성
        Grid grid = new Grid();
        
        // 이미지 컨트롤 생성
        Image image = new Image
        {
            Source = captureImage.Image,
            Width = thumbWidth,
            Height = thumbHeight,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

        // 인덱스를 태그로 저장하여 나중에 참조할 수 있게 함
        image.Tag = index;

        // 이벤트 연결
        image.MouseDown += (s, e) => 
        {
            if (e.ClickCount == 2)
            {
                // 태그에서 실제 인덱스 가져오기
                int actualIndex = (int)((Image)s).Tag;
                ShowPreviewWindow(captureImage.Image, actualIndex);
                e.Handled = true;
            }
        };

        // 그리드에 이미지 추가
        grid.Children.Add(image);
        
        // 이미지 크기 텍스트 표시
        Border sizeBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 3, 6, 3),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 6, 6)
        };
        
        TextBlock sizeText = new TextBlock
        {
            Text = $"{captureImage.Image.PixelWidth} x {captureImage.Image.PixelHeight}",
            Foreground = Brushes.White,
            FontSize = 10
        };
        
        sizeBorder.Child = sizeText;
        grid.Children.Add(sizeBorder);

        // 테두리 생성
        Border border = new Border
        {
            Child = grid,
            Margin = new Thickness(0, 6, 0, 6),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
            Background = Brushes.White,
            CornerRadius = new CornerRadius(6),
            Effect = new DropShadowEffect
            {
                ShadowDepth = 1,
                BlurRadius = 5,
                Opacity = 0.2,
                Direction = 270
            },
            Tag = index, // 인덱스를 태그로 저장
            Width = thumbWidth,
            Height = thumbHeight
        };

        // 마우스 클릭 이벤트 추가
        border.MouseLeftButtonDown += (s, e) => 
        {
            if (s is Border clickedBorder)
            {
                int clickedIndex = (int)clickedBorder.Tag;
                SelectCapture(clickedBorder, clickedIndex);
            }
        };

        return border;
    }

    private void SelectCapture(Border border, int index)
    {
        // 이전 선택 해제
        if (selectedBorder != null)
        {
            selectedBorder.BorderBrush = Brushes.Transparent;
        }

        // 새 선택 적용
        selectedBorder = border;
        selectedIndex = index;
        border.BorderBrush = Brushes.DodgerBlue;

        // 버튼 상태 업데이트
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        bool hasCaptures = captures.Count > 0;
        bool hasSelection = selectedIndex >= 0;

        // 복사 버튼 상태 업데이트
        CopySelectedButton.IsEnabled = hasSelection;
        CopyAllButton.IsEnabled = hasCaptures;

        // 저장 버튼 상태 업데이트
        SaveSelectedButton.IsEnabled = hasSelection;
        SaveAllButton.IsEnabled = hasCaptures;

        // 삭제 버튼 상태 업데이트
        DeleteSelectedButton.IsEnabled = hasSelection;
        DeleteAllButton.IsEnabled = hasCaptures;
    }

    private void UpdateCaptureCount()
    {
        Title = $"캣치 - 캡처 {captures.Count}개";
    }

    #endregion

    #region 복사 기능

    private void CopySelectedButton_Click(object sender, RoutedEventArgs e)
    {
        CopySelectedImage();
    }

    private void CopySelectedImage()
    {
        if (selectedIndex >= 0 && selectedIndex < captures.Count)
        {
            var image = captures[selectedIndex].Image;
            ScreenCaptureUtility.CopyImageToClipboard(image);
            ShowGuideMessage("이미지가 클립보드에 복사되었습니다.", TimeSpan.FromSeconds(1));
        }
    }

    private void CopyAllButton_Click(object sender, RoutedEventArgs e)
    {
        CopyAllImages();
    }

    private void CopyAllImages()
    {
        if (captures.Count == 0) return;

        // 모든 이미지를 세로로 결합
        int totalWidth = 0;
        int totalHeight = 0;

        // 최대 너비와 총 높이 계산
        foreach (var capture in captures)
        {
            totalWidth = Math.Max(totalWidth, capture.Image.PixelWidth);
            totalHeight += capture.Image.PixelHeight;
        }

        // 결합된 이미지 생성
        DrawingVisual drawingVisual = new DrawingVisual();
        using (DrawingContext drawingContext = drawingVisual.RenderOpen())
        {
            int currentY = 0;
            foreach (var capture in captures)
            {
                drawingContext.DrawImage(capture.Image, new Rect(0, currentY, capture.Image.PixelWidth, capture.Image.PixelHeight));
                currentY += capture.Image.PixelHeight;
            }
        }

        RenderTargetBitmap combinedImage = new RenderTargetBitmap(
            totalWidth, totalHeight, 96, 96, PixelFormats.Pbgra32);
        combinedImage.Render(drawingVisual);

        // 클립보드에 복사
        ScreenCaptureUtility.CopyImageToClipboard(combinedImage);
        ShowGuideMessage("모든 이미지가 클립보드에 복사되었습니다.", TimeSpan.FromSeconds(1));
    }

    #endregion

    #region 저장 기능

    private void SaveSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSelectedImage();
    }

    private void SaveSelectedImage()
    {
        if (selectedIndex >= 0 && selectedIndex < captures.Count)
        {
            SaveImageToFile(captures[selectedIndex]);
        }
    }

    private void SaveAllButton_Click(object sender, RoutedEventArgs e)
    {
        SaveAllImages();
    }

    private void SaveAllImages()
    {
        if (captures.Count == 0) return;

        // 저장 폴더 선택
        var dialog = new SaveFileDialog
        {
            Title = "저장 폴더 선택",
            FileName = "folder_selection",
            Filter = "폴더|*.folder"
        };

        if (dialog.ShowDialog() == true)
        {
            string folderPath = System.IO.Path.GetDirectoryName(dialog.FileName) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            for (int i = 0; i < captures.Count; i++)
            {
                string fileName = System.IO.Path.Combine(folderPath, $"캡처_{timestamp}_{i + 1}.jpg");
                ScreenCaptureUtility.SaveImageToFile(captures[i].Image, fileName);
                captures[i].IsSaved = true;
                captures[i].SavedPath = fileName;
            }

            ShowGuideMessage("모든 이미지가 저장되었습니다.", TimeSpan.FromSeconds(1));
        }
    }

    private void SaveImageToFile(CaptureImage captureImage)
    {
        // 자동 파일 이름 생성
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HHmmss");
        string defaultFileName = $"캡처 {timestamp}.jpg";
        
        var dialog = new SaveFileDialog
        {
            Title = "이미지 저장",
            Filter = "JPEG 이미지|*.jpg|PNG 이미지|*.png|모든 파일|*.*",
            DefaultExt = ".jpg",
            FileName = defaultFileName
        };

        if (dialog.ShowDialog() == true)
        {
            ScreenCaptureUtility.SaveImageToFile(captureImage.Image, dialog.FileName);
            captureImage.IsSaved = true;
            captureImage.SavedPath = dialog.FileName;
            ShowGuideMessage("이미지가 저장되었습니다.", TimeSpan.FromSeconds(1));
        }
    }

    #endregion

    #region 삭제 기능

    private void DeleteSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        DeleteSelectedImage();
    }

    private void DeleteSelectedImage()
    {
        if (selectedIndex >= 0 && selectedIndex < captures.Count)
        {
            // 저장되지 않은 이미지인 경우 확인
            if (!captures[selectedIndex].IsSaved && settings.ShowSavePrompt)
            {
                var result = MessageBox.Show(
                    "저장되지 않은 이미지입니다. 삭제하시겠습니까?",
                    "확인",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            // UI에서 제거 - 실제 UI 인덱스 찾기
            int uiIndex = -1;
            for (int i = 0; i < CaptureListPanel.Children.Count; i++)
            {
                if (CaptureListPanel.Children[i] is Border border && (int)border.Tag == selectedIndex)
                {
                    uiIndex = i;
                    break;
                }
            }

            if (uiIndex >= 0)
            {
                CaptureListPanel.Children.RemoveAt(uiIndex);
            }

            // 데이터에서 제거
            captures.RemoveAt(selectedIndex);

            // 인덱스 업데이트
            UpdateCaptureItemIndexes();

            // 선택 초기화
            selectedBorder = null;
            selectedIndex = -1;

            // 버튼 상태 업데이트
            UpdateButtonStates();
            UpdateCaptureCount();
        }
    }

    // 캡처 아이템 인덱스 업데이트
    private void UpdateCaptureItemIndexes()
    {
        for (int i = 0; i < CaptureListPanel.Children.Count; i++)
        {
            if (CaptureListPanel.Children[i] is Border border)
            {
                // 현재 태그를 가져와 인덱스 확인
                int tagIndex = -1;
                if (border.Tag is int index)
                {
                    tagIndex = index;
                }

                // 삭제된 아이템 이후의 인덱스는 1씩 감소
                if (tagIndex > selectedIndex)
                {
                    border.Tag = tagIndex - 1;

                    // 이미지의 태그도 업데이트
                    if (border.Child is Grid grid && grid.Children.Count > 0 && grid.Children[0] is Image img)
                    {
                        img.Tag = tagIndex - 1;
                    }
                }
            }
        }
    }

    private void DeleteAllButton_Click(object sender, RoutedEventArgs e)
    {
        DeleteAllImages();
    }

    private void DeleteAllImages()
    {
        if (captures.Count == 0) return;

        // 저장되지 않은 이미지가 있는지 확인
        bool hasUnsavedImages = captures.Exists(c => !c.IsSaved);

        if (hasUnsavedImages && settings.ShowSavePrompt)
        {
            var result = MessageBox.Show(
                "저장되지 않은 이미지가 있습니다. 모두 삭제하시겠습니까?",
                "확인",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
            {
                return;
            }
        }

        // 모든 이미지 삭제
        CaptureListPanel.Children.Clear();
        captures.Clear();
        selectedBorder = null;
        selectedIndex = -1;

        // 버튼 상태 업데이트
        UpdateButtonStates();
        UpdateCaptureCount();
    }

    #endregion

    #region 미리보기 기능

    private void ShowPreviewWindow(BitmapSource image, int index)
    {
        // 미리보기 창 생성
        PreviewWindow previewWindow = new PreviewWindow(image, index);
        previewWindow.ImageUpdated += (sender, e) => 
        {
            if (e.Index >= 0 && e.Index < captures.Count)
            {
                // 이미지 업데이트
                captures[e.Index].Image = e.NewImage;
                
                // 썸네일 업데이트 - 데이터 인덱스에 해당하는 UI 인덱스 찾기
                for (int i = 0; i < CaptureListPanel.Children.Count; i++)
                {
                    if (CaptureListPanel.Children[i] is Border border && 
                        border.Tag is int borderTag && 
                        borderTag == e.Index)
                    {
                        if (border.Child is Grid grid && 
                            grid.Children.Count > 0 && 
                            grid.Children[0] is Image thumbnailImage)
                        {
                            thumbnailImage.Source = e.NewImage;
                        }
                        break;
                    }
                }
            }
        };
        
        previewWindow.Owner = this;
        previewWindow.ShowDialog();
    }

    #endregion

    #region 유틸리티 기능

    private void ShowGuideMessage(string message, TimeSpan? duration = null)
    {
        GuideWindow guideWindow = new GuideWindow(message, duration);
        guideWindow.Owner = this;
        guideWindow.Show();
    }

    private void RegisterGlobalHotkeys()
    {
        // 단축키 등록
        try
        {
            // 여기에 글로벌 단축키 등록 코드 추가 (필요시)
        }
        catch (Exception ex)
        {
            MessageBox.Show($"단축키 등록 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region 간편 모드

    private void SimpleModeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleSimpleMode();
    }

    private void ToggleSimpleMode()
    {
        if (simpleModeWindow == null || !simpleModeWindow.IsVisible)
        {
            ShowSimpleMode();
        }
        else
        {
            HideSimpleMode();
        }
    }

    private void ShowSimpleMode()
    {
        simpleModeWindow = new SimpleModeWindow();
        
        // 이벤트 핸들러 등록
        simpleModeWindow.AreaCaptureRequested += (s, e) => 
        {
            // 영역 캡처 수행
            var snippingWindow = new SnippingWindow();
            
            if (snippingWindow.ShowDialog() == true)
            {
                // 선택된 영역 캡처 - 동결된 프레임 우선 사용
                var selectedArea = snippingWindow.SelectedArea;
                var capturedImage = snippingWindow.SelectedFrozenImage ?? ScreenCaptureUtility.CaptureArea(selectedArea);

                // 클립보드에 복사
                ScreenCaptureUtility.CopyImageToClipboard(capturedImage);

                // 캡처 목록에 추가
                AddCaptureToList(capturedImage);

                // 간편모드 창 다시 표시
                simpleModeWindow?.Show();
            }
        };
        
        simpleModeWindow.FullScreenCaptureRequested += (s, e) => 
        {
            // 전체화면 캡처 수행
            System.Threading.Thread.Sleep(200); // 간편모드 창이 사라질 때까지 잠시 대기
            
            var capturedImage = ScreenCaptureUtility.CaptureScreen();
            
            // 클립보드에 복사
            ScreenCaptureUtility.CopyImageToClipboard(capturedImage);
            
            // 캡처 목록에 추가
            AddCaptureToList(capturedImage);
            
            // 간편모드 창 다시 표시
            simpleModeWindow?.Show();
        };
        
        simpleModeWindow.ExitSimpleModeRequested += (s, e) => 
        {
            HideSimpleMode();
            this.Show();
            this.Activate();
        };
        
        // 메인 창 숨기기
        this.Hide();
        
        // 간편모드 창 표시
        simpleModeWindow.Show();
    }

    private void HideSimpleMode()
    {
        if (simpleModeWindow != null)
        {
            simpleModeWindow.Close();
            simpleModeWindow = null;
        }
        
        // 메인 창 표시
        this.Show();
        this.Activate();
    }

    #endregion
}