using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CatchCapture.Utilities;

namespace CatchCapture
{
    public partial class BackupRestoreWindow : Window
    {
        private bool _isHistory;

        public BackupRestoreWindow(bool isHistory = false)
        {
            InitializeComponent();
            _isHistory = isHistory;
            
            // Set Title
            string titleKey = _isHistory ? "HistoryBackupList" : "NoteBackupList";
            var titleRes = TryFindResource(titleKey) as string;
            if (!string.IsNullOrEmpty(titleRes))
            {
                TitleText.Text = titleRes;
            }
            else
            {
                TitleText.Text = _isHistory ? "히스토리 DB 복구" : "노트 DB 복구";
            }

            LoadBackups(_isHistory);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void LoadBackups(bool isHistory)
        {
            try
            {
                var backups = DatabaseManager.Instance.GetBackups(isHistory);
                BackupListView.ItemsSource = backups;
                
                if (backups.Count > 0)
                    BackupListView.SelectedIndex = 0;
            }
            catch (Exception)
            {
                // Silently fail or log
            }
        }

        private async void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (BackupListView.SelectedItem is DatabaseManager.BackupInfo selectedBackup)
            {
                string title = TryFindResource("RestoreFromBackup") as string ?? "DB Restore";
                string format = TryFindResource("ConfirmRestoreBackup") as string ?? "Restore to {0}?";
                string msg = string.Format(format, selectedBackup.DateDisplay);
                
                if (MessageBox.Show(msg, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    LoadingOverlay.Visibility = Visibility.Visible;
                    try
                    {
                        await Task.Run(() => 
                        {
                            DatabaseManager.Instance.RestoreFromBackup(selectedBackup.FullPath, selectedBackup.IsHistory);
                        });

                        string successMsg = TryFindResource("RestoreSuccessMsg") as string ?? "Success. Restarting...";
                        MessageBox.Show(successMsg, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        
                        // Restart App
                        string processPath = System.Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                        if (!string.IsNullOrEmpty(processPath))
                        {
                            System.Diagnostics.Process.Start(processPath);
                            Application.Current.Shutdown();
                        }
                    }
                    catch (Exception ex)
                    {
                        string errorTitle = TryFindResource("ErrorRestore") as string ?? "Error";
                        MessageBox.Show($"{errorTitle}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        LoadingOverlay.Visibility = Visibility.Collapsed;
                    }
                }
            }
            else
            {
                string selectMsg = TryFindResource("SelectBackupToRestore") as string ?? "Please select a backup.";
                MessageBox.Show(selectMsg, "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void BtnCreateManualBackup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BtnCreateManualBackup.IsEnabled = false;
                
                // 현재 창 모드에 따라 해당 DB만 백업
                await DatabaseManager.Instance.CreateBackup(backupNote: !_isHistory, backupHistory: _isHistory);
                
                // 리스트 새로고침
                LoadBackups(_isHistory);
                
                MessageBox.Show("백업이 완료되었습니다.", "백업 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"백업 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnCreateManualBackup.IsEnabled = true;
            }
        }
    }
}
