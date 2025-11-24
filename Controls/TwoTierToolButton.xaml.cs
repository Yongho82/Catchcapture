using System.Windows;
using System.Windows.Controls;

namespace CatchCapture.Controls
{
    /// <summary>
    /// 2단 도구 버튼 (메인 버튼 + 옵션 버튼)
    /// </summary>
    public partial class TwoTierToolButton : UserControl
    {
        // 라벨 텍스트
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register("Label", typeof(string), typeof(TwoTierToolButton), new PropertyMetadata("도구"));

        public string Label
        {
            get { return (string)GetValue(LabelProperty); }
            set { SetValue(LabelProperty, value); }
        }

        // 아이콘 콘텐츠 (Image, Canvas 등)
        public static readonly DependencyProperty IconContentProperty =
            DependencyProperty.Register("IconContent", typeof(object), typeof(TwoTierToolButton), new PropertyMetadata(null));

        public object IconContent
        {
            get { return GetValue(IconContentProperty); }
            set { SetValue(IconContentProperty, value); }
        }

        // 툴팁
        public static readonly DependencyProperty ToolTipTextProperty =
            DependencyProperty.Register("ToolTipText", typeof(string), typeof(TwoTierToolButton), new PropertyMetadata(""));

        public string ToolTipText
        {
            get { return (string)GetValue(ToolTipTextProperty); }
            set { SetValue(ToolTipTextProperty, value); }
        }

        // 메인 버튼 클릭 이벤트
        public event RoutedEventHandler? MainButtonClick;

        // 옵션 버튼 클릭 이벤트
        public event RoutedEventHandler? OptionsButtonClick;

        public TwoTierToolButton()
        {
            InitializeComponent();
        }

        private void MainButton_Click(object sender, RoutedEventArgs e)
        {
            MainButtonClick?.Invoke(this, e);
        }

        private void OptionsButton_Click(object sender, RoutedEventArgs e)
        {
            OptionsButtonClick?.Invoke(this, e);
        }
    }
}
