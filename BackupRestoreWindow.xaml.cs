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
            _tickerTimer = new System.Windows.Threading.DispatcherTimer();
            
            // UI Init
            BtnExport.Visibility = _isHistory ? Visibility.Collapsed : Visibility.Visible;
            BtnImport.Visibility = _isHistory ? Visibility.Collapsed : Visibility.Visible;

            // Set Title
            string titleKey = _isHistory ? "HistoryBackupList" : "NoteBackupList";
            var titleRes = TryFindResource(titleKey) as string;
            if (!string.IsNullOrEmpty(titleRes))
            {
                TitleText.Text = titleRes;
            }
            else
            {
                TitleText.Text = _isHistory ? "히스토리 DB 복구" : "데이터 백업 및 복구";
            }

            LoadBackups(_isHistory);
            StartTicker();
        }

        private System.Windows.Threading.DispatcherTimer _tickerTimer;
        private int _tickerIndex = 0;
        private string[] _tickerMessages = new string[] 
        {
            "노트 DB는 종료 시 자동으로 백업됩니다.",
            "주기적인 데이터 백업을 권장합니다.",
            "초기화 전에는 반드시 전체 내보내기를 진행하세요.",
            "DB 복구 시 현재 데이터는 덮어씌워집니다."
        };

        private void StartTicker()
        {
            _tickerTimer.Interval = TimeSpan.FromSeconds(4);
            _tickerTimer.Tick += (s, e) => 
            {
                if (_tickerMessages.Length == 0) return;
                TxtTicker.Text = _tickerMessages[_tickerIndex];
                _tickerIndex = (_tickerIndex + 1) % _tickerMessages.Length;
            };
            _tickerTimer.Start();
            
            // Initial call
            if (_tickerMessages.Length > 0) TxtTicker.Text = _tickerMessages[0];
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
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
                await DatabaseManager.Instance.CreateBackup(backupNote: !_isHistory, backupHistory: _isHistory, force: true);
                
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
    

        private void DeleteDirectoryContents(string path)
        {
            try
            {
                foreach (string file in System.IO.Directory.GetFiles(path))
                {
                    try { System.IO.File.Delete(file); } catch { }
                }
                foreach (string dir in System.IO.Directory.GetDirectories(path))
                {
                    try { System.IO.Directory.Delete(dir, true); } catch { }
                }
            }
            catch { }
        }

        private void BtnDeleteBackup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is DatabaseManager.BackupInfo info)
            {
                if (MessageBox.Show($"정말로 이 백업 파일을 삭제하시겠습니까?\n\n{info.FileName}", 
                    "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        if (System.IO.File.Exists(info.FullPath))
                        {
                            System.IO.File.Delete(info.FullPath);
                            LoadBackups(_isHistory); // Refresh list
                            MessageBox.Show("백업 파일이 삭제되었습니다.", "삭제 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show("파일을 찾을 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                            LoadBackups(_isHistory); // Refresh anyway
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"파일 삭제 중 오류가 발생했습니다: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sfd = new Microsoft.Win32.SaveFileDialog();
                sfd.Filter = "Zip Files (*.zip)|*.zip";
                sfd.FileName = $"CatchCapture_All_Data_{DateTime.Now:yyyyMMdd}.zip";
                if (sfd.ShowDialog() == true)
                {
                    string sourceDir = DatabaseManager.Instance.DbPath; 
                    sourceDir = System.IO.Path.GetDirectoryName(sourceDir)!;
                     // Usually notedb is in notedata/notedb or CatchCapture/notedb. 
                     // We need the PARENT of notedb to include img/attachments.
                     
                     // However, DbPath is local cache path? No. DbPath public property returns _localDbPath.
                     // But for Export, we want the Cloud Path data usually? Or local?
                     // The user said "Export Img+DB". Source of truth is Cloud path for notes.
                     
                    string noteRoot = System.IO.Path.GetDirectoryName(DatabaseManager.Instance.CloudDbFilePath)!; // .../notedb
                    noteRoot = System.IO.Path.GetDirectoryName(noteRoot)!; // .../CatchCapture (or notedata)
                    
                    LoadingOverlay.Visibility = Visibility.Visible;
                    DatabaseManager.Instance.ExportNoteData(sfd.FileName, noteRoot);
                    LoadingOverlay.Visibility = Visibility.Collapsed;

                    MessageBox.Show("전체 데이터 내보내기 완료!", "성공", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"내보내기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var ofd = new Microsoft.Win32.OpenFileDialog();
                ofd.Filter = "Zip Files (*.zip)|*.zip";
                if (ofd.ShowDialog() == true)
                {
                    if (MessageBox.Show("불러오기를 진행하면 현재 노트 데이터와 병합되거나 덮어씌워질 수 있습니다.\n계속 하시겠습니까?", 
                        "확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        LoadingOverlay.Visibility = Visibility.Visible;
                        
                        string targetDir = System.IO.Path.GetDirectoryName(DatabaseManager.Instance.CloudDbFilePath)!; // .../notedb
                        targetDir = System.IO.Path.GetDirectoryName(targetDir)!; // .../CatchCapture

                        DatabaseManager.Instance.ImportNoteData(ofd.FileName, targetDir);
                        
                        LoadingOverlay.Visibility = Visibility.Collapsed;
                        MessageBox.Show("데이터 가져오기 완료! 앱이 재시작될 수 있습니다.", "성공", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"가져오기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
