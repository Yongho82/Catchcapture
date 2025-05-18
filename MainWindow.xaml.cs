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

    public MainWindow()
    {
        InitializeComponent();
        settings = Settings.Load();
        
        // 글로벌 단축키 등록
        RegisterGlobalHotkeys();
    }

    #region 캡처 기능

    private void AreaCaptureButton_Click(object sender, RoutedEventArgs e)
    {
        StartAreaCapture();
    }

    private void StartAreaCapture()
    {
        // 가이드 메시지 표시
        ShowGuideMessage("영역을 선택하세요 (ESC 키를 눌러 취소)", TimeSpan.FromSeconds(2));

        // 영역 선택 창 표시
        var snippingWindow = new SnippingWindow();
        this.Hide();

        if (snippingWindow.ShowDialog() == true)
        {
            // 선택된 영역 캡처
            var selectedArea = snippingWindow.SelectedArea;
            var capturedImage = ScreenCaptureUtility.CaptureArea(selectedArea);
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

    private void AddCaptureToList(BitmapSource image)
    {
        // 캡처 이미지 객체 생성
        var captureImage = new CaptureImage(image);
        captures.Add(captureImage);

        // UI에 이미지 추가
        var border = CreateCaptureItem(captureImage, captures.Count - 1);
        CaptureListPanel.Children.Add(border);

        // 추가된 이미지 선택
        SelectCapture(border, captures.Count - 1);

        // 버튼 상태 업데이트
        UpdateButtonStates();
        
        // 캡처 개수 업데이트
        UpdateCaptureCount();
    }

    private Border CreateCaptureItem(CaptureImage captureImage, int index)
    {
        // 썸네일 크기 계산 - 이제 너비를 최대한 채우도록 설정
        double maxWidth = 280; // 약간의 여백을 고려한 너비
        double scale = maxWidth / captureImage.Image.PixelWidth;
        double thumbWidth = maxWidth;
        double thumbHeight = captureImage.Image.PixelHeight * scale;

        // 이미지 컨트롤 생성
        Image image = new Image
        {
            Source = captureImage.Image,
            Width = thumbWidth,
            Height = thumbHeight,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // 이벤트 연결
        image.MouseDown += (s, e) => 
        {
            if (e.ClickCount == 2)
            {
                ShowPreviewWindow(captureImage.Image, index);
                e.Handled = true;
            }
        };

        // 이미지를 담을 그리드 생성
        Grid grid = new Grid();
        grid.Children.Add(image);

        // 테두리 생성
        Border border = new Border
        {
            Child = grid,
            Margin = new Thickness(0, 5, 0, 5),
            BorderThickness = new Thickness(2),
            BorderBrush = Brushes.Transparent,
            Background = Brushes.White,
            Effect = new DropShadowEffect
            {
                ShadowDepth = 2,
                BlurRadius = 5,
                Opacity = 0.2,
                Direction = 270
            }
        };

        // 이벤트 연결
        border.MouseLeftButtonDown += (s, e) => SelectCapture(border, index);
        border.MouseDown += (s, e) => 
        {
            if (e.ClickCount == 2)
            {
                ShowPreviewWindow(captureImage.Image, index);
                e.Handled = true;
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
                string fileName = System.IO.Path.Combine(folderPath, $"캡처_{timestamp}_{i + 1}.png");
                ScreenCaptureUtility.SaveImageToFile(captures[i].Image, fileName);
                captures[i].IsSaved = true;
                captures[i].SavedPath = fileName;
            }

            ShowGuideMessage("모든 이미지가 저장되었습니다.", TimeSpan.FromSeconds(1));
        }
    }

    private void SaveImageToFile(CaptureImage captureImage)
    {
        var dialog = new SaveFileDialog
        {
            Title = "이미지 저장",
            Filter = "PNG 이미지|*.png|JPEG 이미지|*.jpg|모든 파일|*.*",
            DefaultExt = ".png"
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

            // UI에서 제거
            CaptureListPanel.Children.RemoveAt(selectedIndex);

            // 데이터에서 제거
            captures.RemoveAt(selectedIndex);

            // 선택 초기화
            selectedBorder = null;
            selectedIndex = -1;

            // 버튼 상태 업데이트
            UpdateButtonStates();
            UpdateCaptureCount();
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
                
                // 썸네일 업데이트
                var border = CaptureListPanel.Children[e.Index] as Border;
                if (border != null && border.Child is Grid grid && grid.Children.Count > 0 && grid.Children[0] is Image thumbnailImage)
                {
                    thumbnailImage.Source = e.NewImage;
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
        simpleModeWindow.AreaCaptureRequested += (s, e) => StartAreaCapture();
        simpleModeWindow.FullScreenCaptureRequested += (s, e) => CaptureFullScreen();
        simpleModeWindow.Show();
    }

    private void HideSimpleMode()
    {
        simpleModeWindow?.Close();
        simpleModeWindow = null;
    }

    #endregion
}