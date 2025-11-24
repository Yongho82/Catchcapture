using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using CatchCapture.Models;

namespace CatchCapture
{
    /// <summary>
    /// PreviewWindow의 캡처 리스트 관리 기능 (partial class)
    /// </summary>
    public partial class PreviewWindow : Window
    {
        #region 캡처 리스트 관리

        private void LoadCaptureList()
        {
            if (CaptureListPanel == null || allCaptures == null) return;

            CaptureListPanel.Children.Clear();

            // 캡처 개수 업데이트
            if (CaptureCountText != null)
            {
                CaptureCountText.Text = $"({allCaptures.Count})";
            }

            // 각 캡처 아이템 생성
            for (int i = 0; i < allCaptures.Count; i++)
            {
                var captureImage = allCaptures[i];
                var itemBorder = CreateCaptureListItem(captureImage, i);
                CaptureListPanel.Children.Add(itemBorder);
            }
        }

        private Border CreateCaptureListItem(CaptureImage captureImage, int index)
        {
            // 썸네일 크기
            double aspectRatio = currentThumbnailSize / 120.0;
            double thumbWidth = 200 * aspectRatio;
            double thumbHeight = currentThumbnailSize;

            // 그리드 생성
            Grid grid = new Grid();

            // 이미지
            Image img = new Image
            {
                Source = captureImage.Image,
                Width = thumbWidth,
                Height = thumbHeight,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            grid.Children.Add(img);

            // 크기 정보 표시
            Border sizeBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 2, 4, 2),
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

            // 메인 테두리
            Border border = new Border
            {
                Child = grid,
                Margin = new Thickness(0, 6, 0, 6),
                BorderThickness = new Thickness(2),
                BorderBrush = index == imageIndex ? Brushes.DodgerBlue : new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
                Background = Brushes.White,
                CornerRadius = new CornerRadius(6),
                Effect = new DropShadowEffect { ShadowDepth = 1, BlurRadius = 5, Opacity = 0.2, Direction = 270 },
                Tag = index,
                Cursor = Cursors.Hand
            };

            // 클릭 이벤트
            border.MouseLeftButtonDown += (s, e) =>
            {
                if (s is Border clickedBorder && clickedBorder.Tag is int clickedIndex)
                {
                    // 다른 이미지 선택 시 이미지만 교체 (창 재생성 없이)
                    if (clickedIndex != imageIndex && allCaptures != null)
                    {
                        SwitchToCapture(clickedIndex);
                    }
                }
            };

            return border;
        }

        private void SwitchToCapture(int newIndex)
        {
            if (allCaptures == null || newIndex < 0 || newIndex >= allCaptures.Count) return;

            // 이전 인덱스 저장
            int oldIndex = imageIndex;

            // 새 이미지로 전환
            imageIndex = newIndex;
            originalImage = allCaptures[newIndex].Image;
            currentImage = allCaptures[newIndex].Image;

            // Undo/Redo 스택 초기화
            undoStack.Clear();
            redoStack.Clear();
            UpdateUndoRedoButtons();

            // 이미지 업데이트
            UpdatePreviewImage();

            // 리스트에서 선택 상태 업데이트
            UpdateCaptureListSelection(oldIndex, newIndex);
        }

        private void UpdateCaptureListSelection(int oldIndex, int newIndex)
        {
            if (CaptureListPanel == null) return;

            foreach (var child in CaptureListPanel.Children)
            {
                if (child is Border border && border.Tag is int index)
                {
                    if (index == newIndex)
                    {
                        // 새로 선택된 항목 강조
                        border.BorderBrush = Brushes.DodgerBlue;
                        border.BorderThickness = new Thickness(2);
                    }
                    else if (index == oldIndex)
                    {
                        // 이전 선택 항목 강조 해제
                        border.BorderBrush = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0));
                        border.BorderThickness = new Thickness(2);
                    }
                }
            }
        }

        #endregion

        #region 썸네일 크기 조절

        private double currentThumbnailSize = 120;

        private void ThumbnailSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ThumbnailSizeText != null)
            {
                ThumbnailSizeText.Text = ((int)e.NewValue).ToString();
                currentThumbnailSize = e.NewValue;
                
                // 기존 캡처 아이템들의 크기 업데이트
                UpdateAllThumbnailSizes();
            }
        }

        private void UpdateAllThumbnailSizes()
        {
            if (CaptureListPanel == null) return;
            
            foreach (var child in CaptureListPanel.Children)
            {
                if (child is Border border && border.Child is Grid grid)
                {
                    // 그리드 내부의 이미지 찾기
                    foreach (var gridChild in grid.Children)
                    {
                        if (gridChild is Image img)
                        {
                            double aspectRatio = currentThumbnailSize / 120.0; // 기본 120 기준
                            img.Width = 200 * aspectRatio;
                            img.Height = currentThumbnailSize;
                        }
                    }
                }
            }
        }

        #endregion
    }
}
