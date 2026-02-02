using System.Windows;
using CatchCapture.Utilities;

namespace CatchCapture
{
    public partial class DraftListWindow : Window
    {
        public DraftData? SelectedDraft { get; private set; }

        public DraftListWindow()
        {
            InitializeComponent();
            LoadDrafts();
        }

        private void LoadDrafts()
        {
            ListDrafts.ItemsSource = DraftManager.GetDraftList();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is DraftData data)
            {
                SelectedDraft = data;
                DialogResult = true;
                Close();
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is DraftData data && data.FileName != null)
            {
                DraftManager.DeleteDraft(data.FileName);
                LoadDrafts();
            }
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (CustomMessageBox.Show(
                CatchCapture.Resources.LocalizationManager.GetString("ConfirmClearAll") ?? "모든 항목을 삭제하시겠습니까?", 
                CatchCapture.Resources.LocalizationManager.GetString("ClearAllDrafts") ?? "전체 삭제", 
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                DraftManager.DeleteAllDrafts();
                LoadDrafts();
            }
        }
    }
}
