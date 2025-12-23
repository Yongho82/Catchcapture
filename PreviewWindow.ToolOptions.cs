using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using CatchCapture.Models;

namespace CatchCapture
{
    /// <summary>
    /// PreviewWindow의 도구 옵션 팝업 관련 기능 (partial class)
    /// </summary>
    public partial class PreviewWindow : Window
    {
        #region 형광펜 옵션

        private void ShowHighlightOptionsPopup()
        {
            // 공용 컨트롤 사용
            ToolOptionsPopupContent.Children.Clear();
            _toolOptionsControl.SetMode("형광펜");
            ToolOptionsPopupContent.Children.Add(_toolOptionsControl);
            
            ToolOptionsPopup.PlacementTarget = HighlightToolButton;
            ToolOptionsPopup.Placement = PlacementMode.Bottom;
            ToolOptionsPopup.IsOpen = true;
        }

        #endregion

        #region 펜 옵션

        private void ShowPenOptionsPopup()
        {
            // 공용 컨트롤 사용
            ToolOptionsPopupContent.Children.Clear();
            _toolOptionsControl.SetMode("펜");
            ToolOptionsPopupContent.Children.Add(_toolOptionsControl);
            
            ToolOptionsPopup.PlacementTarget = PenToolButton;
            ToolOptionsPopup.Placement = PlacementMode.Bottom;
            ToolOptionsPopup.IsOpen = true;
        }

        private void PenOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentEditMode != EditMode.Pen)
            {
                CancelCurrentEditMode();
                currentEditMode = EditMode.Pen;
                SetActiveToolButton(PenToolButton);
            }

            if (ToolOptionsPopup.IsOpen && ToolOptionsPopup.PlacementTarget == PenToolButton)
            {
                ToolOptionsPopup.IsOpen = false;
            }
            else
            {
                ShowPenOptionsPopup();
            }
        }

        #endregion

        #region 도형 옵션

        private void ShowShapeOptionsPopup()
        {
            // 공용 컨트롤 사용
            ToolOptionsPopupContent.Children.Clear();
            _toolOptionsControl.SetMode("도형");
            ToolOptionsPopupContent.Children.Add(_toolOptionsControl);
            
            ToolOptionsPopup.PlacementTarget = ShapeToolGrid;
            ToolOptionsPopup.Placement = PlacementMode.Bottom;
            ToolOptionsPopup.IsOpen = true;
        }

        #endregion

        #region 텍스트 옵션

        private void ShowTextOptions()
        {
            // 공용 컨트롤 사용
            ToolOptionsPopupContent.Children.Clear();
            _toolOptionsControl.SetMode("텍스트");
            ToolOptionsPopupContent.Children.Add(_toolOptionsControl);
            
            ToolOptionsPopup.PlacementTarget = TextToolButton;
            ToolOptionsPopup.Placement = PlacementMode.Bottom;
            ToolOptionsPopup.IsOpen = true;
        }

        #endregion

        #region 모자이크 옵션

        private void ShowMosaicOptions()
        {
            // 공용 컨트롤 사용
            ToolOptionsPopupContent.Children.Clear();
            _toolOptionsControl.SetMode("모자이크");
            ToolOptionsPopupContent.Children.Add(_toolOptionsControl);
            
            ToolOptionsPopup.PlacementTarget = MosaicToolButton;
            ToolOptionsPopup.Placement = PlacementMode.Bottom;
            ToolOptionsPopup.IsOpen = true;
        }

        #endregion

        #region 지우개 옵션

        private void ShowEraserOptions()
        {
            // 공용 컨트롤 사용
            ToolOptionsPopupContent.Children.Clear();
            _toolOptionsControl.SetMode("지우개");
            ToolOptionsPopupContent.Children.Add(_toolOptionsControl);
            
            ToolOptionsPopup.PlacementTarget = EraserToolButton;
            ToolOptionsPopup.Placement = PlacementMode.Bottom;
            ToolOptionsPopup.IsOpen = true;
        }

        #endregion

        #region 버튼 클릭 이벤트

        private void HighlightOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentEditMode != EditMode.Highlight)
            {
                CancelCurrentEditMode();
                currentEditMode = EditMode.Highlight;
                SetActiveToolButton(HighlightToolButton);
            }

            if (ToolOptionsPopup.IsOpen && ToolOptionsPopup.PlacementTarget == HighlightToolButton)
            {
                ToolOptionsPopup.IsOpen = false;
            }
            else
            {
                ShowHighlightOptionsPopup();
            }
        }

        private void TextOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentEditMode != EditMode.Text)
            {
                CancelCurrentEditMode();
                currentEditMode = EditMode.Text;
                SetActiveToolButton(TextToolButton);
            }

            if (ToolOptionsPopup.IsOpen && ToolOptionsPopup.PlacementTarget == TextToolButton)
            {
                ToolOptionsPopup.IsOpen = false;
            }
            else
            {
                ShowTextOptions();
            }
        }

        private void MosaicOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentEditMode != EditMode.Mosaic)
            {
                CancelCurrentEditMode();
                currentEditMode = EditMode.Mosaic;
                SetActiveToolButton(MosaicToolButton);
            }

            if (ToolOptionsPopup.IsOpen && ToolOptionsPopup.PlacementTarget == MosaicToolButton)
            {
                ToolOptionsPopup.IsOpen = false;
            }
            else
            {
                ShowMosaicOptions();
            }
        }

        private void EraserOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentEditMode != EditMode.Eraser)
            {
                CancelCurrentEditMode();
                currentEditMode = EditMode.Eraser;
                SetActiveToolButton(EraserToolButton);
            }

            if (ToolOptionsPopup.IsOpen && ToolOptionsPopup.PlacementTarget == EraserToolButton)
            {
                ToolOptionsPopup.IsOpen = false;
            }
            else
            {
                ShowEraserOptions();
            }
        }

        #endregion

        #region 넘버링 옵션

        private void ShowNumberingOptionsPopup()
        {
            // 공용 컨트롤 사용
            ToolOptionsPopupContent.Children.Clear();
            _toolOptionsControl.SetMode("넘버링");
            ToolOptionsPopupContent.Children.Add(_toolOptionsControl);
            
            ToolOptionsPopup.PlacementTarget = NumberingToolButton;
            ToolOptionsPopup.Placement = PlacementMode.Bottom;
            ToolOptionsPopup.IsOpen = true;
        }

        #endregion

        #region 마법봉 옵션
        
        private void ShowMagicWandOptions()
        {
            // 공용 컨트롤 사용
            ToolOptionsPopupContent.Children.Clear();
            _toolOptionsControl.SetMode("마법봉");
            ToolOptionsPopupContent.Children.Add(_toolOptionsControl);
            
            ToolOptionsPopup.PlacementTarget = MagicWandToolButton;
            ToolOptionsPopup.Placement = PlacementMode.Bottom;
            ToolOptionsPopup.IsOpen = true;
        }

        #endregion
    }
}
