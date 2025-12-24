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

            // 각 캡처 아이템 생성 (역순으로 - 최신이 위로)
            for (int i = allCaptures.Count - 1; i >= 0; i--)
            {
                var captureImage = allCaptures[i];
                var itemBorder = CreateCaptureListItem(captureImage, i);
                CaptureListPanel.Children.Add(itemBorder);
            }
        }

        private Border CreateCaptureListItem(CaptureImage captureImage, int index)
        {
            // 썸네일 크기 (정수로 반올림하여 서브픽셀 렌더링 방지)
            double aspectRatio = currentThumbnailSize / 120.0;
            double thumbWidth = Math.Round(200 * aspectRatio);
            double thumbHeight = Math.Round(currentThumbnailSize);

            // 그리드 생성
            Grid grid = new Grid();
            grid.SnapsToDevicePixels = true;
            grid.UseLayoutRounding = true;

            // 이미지
            Image img = new Image
            {
                Width = thumbWidth,
                Height = thumbHeight,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true
            };
            
            // 데이터 바인딩으로 Image 속성 연결
            System.Windows.Data.Binding binding = new System.Windows.Data.Binding("Image")
            {
                Source = captureImage,
                Mode = System.Windows.Data.BindingMode.OneWay
            };
            img.SetBinding(Image.SourceProperty, binding);
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
                Text = $"{captureImage.OriginalWidth} x {captureImage.OriginalHeight}",
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
                BorderBrush = index == imageIndex ? GetActiveToolBrush() : (Brush)Application.Current.Resources["ThemeBorder"],
                Background = (Brush)Application.Current.Resources["ThemeBackground"],
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

            // 현재 이미지의 드로잉 레이어 저장 (다른 이미지로 전환하기 전에)
            if (drawingLayers.Count > 0)
            {
                captureDrawingLayers[oldIndex] = drawingLayers.Select(layer => new CatchCapture.Models.DrawingLayer
                {
                    Type = layer.Type,
                    Points = layer.Points?.ToArray() ?? Array.Empty<System.Windows.Point>(),
                    Color = layer.Color,
                    Thickness = layer.Thickness,
                    IsErased = layer.IsErased
                }).ToList();
            }
            else
            {
                captureDrawingLayers[oldIndex] = new List<CatchCapture.Models.DrawingLayer>();
            }

            // 새 이미지로 전환
            imageIndex = newIndex;
            var newCapture = allCaptures[newIndex];
            originalImage = newCapture.GetOriginalImage();
            currentImage = originalImage;

            // Undo/Redo 스택 초기화
            undoStack.Clear();
            redoStack.Clear();
            UpdateUndoRedoButtons();

            // 새 이미지의 드로잉 레이어 로드 (저장된 게 있으면 복원, 없으면 빈 리스트)
            if (captureDrawingLayers.ContainsKey(newIndex))
            {
                drawingLayers = captureDrawingLayers[newIndex].Select(layer => new CatchCapture.Models.DrawingLayer
                {
                    Type = layer.Type,
                    Points = layer.Points?.ToArray() ?? Array.Empty<System.Windows.Point>(),
                    Color = layer.Color,
                    Thickness = layer.Thickness,
                    IsErased = layer.IsErased
                }).ToList();
            }
            else
            {
                drawingLayers = new List<CatchCapture.Models.DrawingLayer>();
            }

            // 이미지 업데이트 (저장된 드로잉 레이어 포함)
            currentImage = CatchCapture.Utilities.LayerRenderer.RenderLayers(originalImage, drawingLayers);
            
            // allCaptures에도 업데이트 (메인창 리스트에 반영)
            if (allCaptures != null && newIndex >= 0 && newIndex < allCaptures.Count)
            {
                allCaptures[newIndex].Image = currentImage;
            }
            
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
                        border.BorderBrush = GetActiveToolBrush();
                        border.BorderThickness = new Thickness(2);
                    }
                    else if (index == oldIndex)
                    {
                        // 이전 선택 항목 강조 해제
                        border.BorderBrush = (Brush)Application.Current.Resources["ThemeBorder"];
                        border.BorderThickness = new Thickness(2);
                    }
                }
            }
        }

        #endregion

        #region 썸네일 크기 조절

        private double currentThumbnailSize = 80;

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
                            img.Width = Math.Round(200 * aspectRatio);
                            img.Height = Math.Round(currentThumbnailSize);
                        }
                    }
                }
            }
        }

        #endregion
    }
}
