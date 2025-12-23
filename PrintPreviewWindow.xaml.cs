using System;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CatchCapture.Models;

namespace CatchCapture
{
    public partial class PrintPreviewWindow : Window
    {
        private BitmapSource imageSource;
        
        public PrintPreviewWindow(BitmapSource image)
        {
            InitializeComponent();
            imageSource = image;
            UpdateUIText();
            LocalizationManager.LanguageChanged += LocalizationManager_LanguageChanged;
            GeneratePreview();
        }

        private void LocalizationManager_LanguageChanged(object? sender, EventArgs e)
        {
            try { UpdateUIText(); } catch { }
            try { GeneratePreview(); } catch { }
        }
        private void UpdateUIText()
        {
            try
            {
                this.Title = LocalizationManager.Get("PrintPreviewTitle");
                if (OrientationLabelText != null)
                    OrientationLabelText.Text = LocalizationManager.Get("OrientationLabel");
                if (PortraitItem != null)
                    PortraitItem.Content = LocalizationManager.Get("Portrait");
                if (LandscapeItem != null)
                    LandscapeItem.Content = LocalizationManager.Get("Landscape");
                if (PrintOptionsLabelText != null)
                    PrintOptionsLabelText.Text = LocalizationManager.Get("PrintOptionsLabel");
                if (FitToPageItem != null)
                    FitToPageItem.Content = LocalizationManager.Get("FitToPage");
                if (ActualSizeItem != null)
                    ActualSizeItem.Content = LocalizationManager.Get("ActualSize");
                if (FillPageItem != null)
                    FillPageItem.Content = LocalizationManager.Get("FillPage");
                if (PrintButton != null)
                    PrintButton.Content = LocalizationManager.Get("Print");
                if (CloseButton != null)
                    CloseButton.Content = LocalizationManager.Get("Close");
            }
            catch { }
        }
        private void GeneratePreview()
        {
            try
            {
                // 방향 선택 (0: 세로, 1: 가로)
                int orientationIndex = OrientationComboBox?.SelectedIndex ?? 0;
                bool isLandscape = orientationIndex == 1;
                
                // 페이지 크기 설정 (A4)
                double baseWidth = 793.7;  // A4 세로 모드 너비
                double baseHeight = 1122.5; // A4 세로 모드 높이
                
                // 방향에 따라 크기 조정
                double pageWidth = isLandscape ? baseHeight : baseWidth;
                double pageHeight = isLandscape ? baseWidth : baseHeight;
                
                // 선택된 옵션 가져오기
                int selectedIndex = PrintOptionsComboBox?.SelectedIndex ?? 0;
                
                double imageWidth = imageSource.PixelWidth;
                double imageHeight = imageSource.PixelHeight;
                double scaledWidth, scaledHeight, x, y;
                
                switch (selectedIndex)
                {
                    case 0: // 페이지에 맞춤 (여백 포함)
                        {
                            double margin = 50;
                            double printableWidth = pageWidth - (margin * 2);
                            double printableHeight = pageHeight - (margin * 2);
                            double scale = Math.Min(printableWidth / imageWidth, printableHeight / imageHeight);
                            
                            scaledWidth = imageWidth * scale;
                            scaledHeight = imageHeight * scale;
                            
                            // 중앙 정렬
                            x = (pageWidth - scaledWidth) / 2;
                            y = (pageHeight - scaledHeight) / 2;
                        }
                        break;
                        
                    case 1: // 실제 크기 (100%)
                        {
                            // 96 DPI 기준으로 픽셀을 포인트로 변환
                            scaledWidth = imageWidth * 96.0 / 96.0;
                            scaledHeight = imageHeight * 96.0 / 96.0;
                            
                            // 페이지보다 크면 잘림 - 중앙 정렬
                            x = (pageWidth - scaledWidth) / 2;
                            y = (pageHeight - scaledHeight) / 2;
                        }
                        break;
                        
                    case 2: // 페이지 채우기 (여백 없음)
                        {
                            double scale = Math.Min(pageWidth / imageWidth, pageHeight / imageHeight);
                            
                            scaledWidth = imageWidth * scale;
                            scaledHeight = imageHeight * scale;
                            
                            // 중앙 정렬
                            x = (pageWidth - scaledWidth) / 2;
                            y = (pageHeight - scaledHeight) / 2;
                        }
                        break;
                        
                    default:
                        {
                            // 기본값: 페이지에 맞춤
                            double margin = 50;
                            double printableWidth = pageWidth - (margin * 2);
                            double printableHeight = pageHeight - (margin * 2);
                            double scale = Math.Min(printableWidth / imageWidth, printableHeight / imageHeight);
                            
                            scaledWidth = imageWidth * scale;
                            scaledHeight = imageHeight * scale;
                            
                            x = (pageWidth - scaledWidth) / 2;
                            y = (pageHeight - scaledHeight) / 2;
                        }
                        break;
                }

                // 고정 문서 생성
                FixedDocument fixedDocument = new FixedDocument();
                fixedDocument.DocumentPaginator.PageSize = new Size(pageWidth, pageHeight);

                // 페이지 추가
                PageContent pageContent = new PageContent();
                FixedPage fixedPage = new FixedPage
                {
                    Width = pageWidth,
                    Height = pageHeight,
                    Background = Brushes.White
                };

                Image image = new Image
                {
                    Source = imageSource,
                    Width = scaledWidth,
                    Height = scaledHeight,
                    Stretch = System.Windows.Media.Stretch.Uniform
                };
                
                Canvas.SetLeft(image, x);
                Canvas.SetTop(image, y);
                
                fixedPage.Children.Add(image);
                ((IAddChild)pageContent).AddChild(fixedPage);
                fixedDocument.Pages.Add(pageContent);

                // DocumentViewer에 표시
                DocumentViewer.Document = fixedDocument;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{LocalizationManager.Get("PreviewGenerationError")}: {ex.Message}", LocalizationManager.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OrientationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 방향 변경 시 미리보기 갱신
            if (imageSource != null && DocumentViewer != null)
            {
                GeneratePreview();
            }
        }

        private void PrintOptionsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 옵션 변경 시 미리보기 갱신
            if (imageSource != null && DocumentViewer != null)
            {
                GeneratePreview();
            }
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PrintDialog printDialog = new PrintDialog();
                
                // 페이지 방향 설정
                int orientationIndex = OrientationComboBox?.SelectedIndex ?? 0;
                bool isLandscape = orientationIndex == 1;
                
                if (printDialog.ShowDialog() == true)
                {
                    if (DocumentViewer.Document != null)
                    {
                        // PrintTicket에 페이지 방향 설정
                        if (printDialog.PrintTicket != null)
                        {
                            if (isLandscape)
                            {
                                printDialog.PrintTicket.PageOrientation = System.Printing.PageOrientation.Landscape;
                            }
                            else
                            {
                                printDialog.PrintTicket.PageOrientation = System.Printing.PageOrientation.Portrait;
                            }
                        }
                        
                        printDialog.PrintDocument(DocumentViewer.Document.DocumentPaginator, LocalizationManager.Get("PrintJobName"));
                        MessageBox.Show(LocalizationManager.Get("PrintingStarted"), LocalizationManager.Get("Info"), MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{LocalizationManager.Get("PrintingError")}: {ex.Message}", LocalizationManager.Get("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
