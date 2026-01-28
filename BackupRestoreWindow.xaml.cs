using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CatchCapture.Utilities;
using LocalizationManager = CatchCapture.Resources.LocalizationManager;

namespace CatchCapture
{
    public partial class BackupRestoreWindow : Window
    {
        private bool _isHistory;

        public BackupRestoreWindow(bool isHistory = false)
        {
            InitializeComponent();
            _isHistory = isHistory;
            
            UpdateUIText();

            _tickerTimer = new System.Windows.Threading.DispatcherTimer();
            
            // UI Init
            BtnExport.Visibility = _isHistory ? Visibility.Collapsed : Visibility.Visible;
            BtnImport.Visibility = _isHistory ? Visibility.Collapsed : Visibility.Visible;

            LoadBackups(_isHistory);
            StartTicker();
        }

        private void UpdateUIText()
        {
            // Title
            string titleKey = _isHistory ? "BackupHistoryDBRestore" : "BackupDataBackupAndRestore";
            TitleText.Text = LocalizationManager.GetString(titleKey);

            // Grid Headers
            if (ColType != null) ColType.Header = LocalizationManager.GetString("BackupColType");
            if (ColSize != null) ColSize.Header = LocalizationManager.GetString("BackupColSize");
            if (ColDate != null) ColDate.Header = LocalizationManager.GetString("BackupColDate");
            if (ColFileName != null) ColFileName.Header = LocalizationManager.GetString("BackupColFileName");
            if (ColTimeAgo != null) ColTimeAgo.Header = LocalizationManager.GetString("BackupColTimeAgo");
            if (ColManage != null) ColManage.Header = LocalizationManager.GetString("BackupColManage");

            // Buttons
            if (TxtBtnExport != null) TxtBtnExport.Text = LocalizationManager.GetString("ExportAllWithImages");
            if (TxtBtnImport != null) TxtBtnImport.Text = LocalizationManager.GetString("ImportAllWithImages");
            
            if (TxtBtnCreateManualBackup != null)
            {
                TxtBtnCreateManualBackup.Text = _isHistory ? LocalizationManager.GetString("CreateManualBackupHistory") : LocalizationManager.GetString("CreateManualBackupNote");
            }

            if (TxtBtnRestore != null) TxtBtnRestore.Text = LocalizationManager.GetString("RestoreFromBackup");

            // Messages for ticker
            _tickerMessages = new string[] 
            {
                LocalizationManager.GetString("BackupTicker1"),
                LocalizationManager.GetString("BackupTicker2"),
                LocalizationManager.GetString("BackupTicker3"),
                LocalizationManager.GetString("BackupTicker4")
            };
        }

        private System.Windows.Threading.DispatcherTimer _tickerTimer;
        private int _tickerIndex = 0;
        private string[] _tickerMessages = new string[0];

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
                string title = LocalizationManager.GetString("RestoreFromBackup");
                string format = LocalizationManager.GetString("ConfirmRestoreBackup");
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

                        string successMsg = LocalizationManager.GetString("RestoreSuccessMsg");
                        MessageBox.Show(successMsg, LocalizationManager.GetString("Success"), MessageBoxButton.OK, MessageBoxImage.Information);
                        
                        // Restart App
                        try
                        {
                            CatchCapture.App.Restart();
                        }
                        catch { Application.Current.Shutdown(); }
                    }
                    catch (Exception ex)
                    {
                        string errorTitle = LocalizationManager.GetString("ErrorRestore");
                        MessageBox.Show($"{errorTitle}: {ex.Message}", LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        LoadingOverlay.Visibility = Visibility.Collapsed;
                    }
                }
            }
            else
            {
                string selectMsg = LocalizationManager.GetString("SelectBackupToRestore");
                MessageBox.Show(selectMsg, "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void BtnCreateManualBackup_Click(object sender, RoutedEventArgs e)
        {
            // Show guide message about missing images
            string guideMsg = LocalizationManager.GetString("DbBackupExcludeImages");
            if (MessageBox.Show(guideMsg, LocalizationManager.GetString("Info"), MessageBoxButton.OKCancel, MessageBoxImage.Information) != MessageBoxResult.OK)
            {
                return;
            }

            try
            {
                BtnCreateManualBackup.IsEnabled = false;
                
                // 현재 창 모드에 따라 해당 DB만 백업
                await DatabaseManager.Instance.CreateBackup(backupNote: !_isHistory, backupHistory: _isHistory, force: true);
                
                // 리스트 새로고침
                LoadBackups(_isHistory);
                
                MessageBox.Show(LocalizationManager.GetString("BackupCompleted"), LocalizationManager.GetString("BackupCompletedTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{LocalizationManager.GetString("BackupFailed")}: {ex.Message}", LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
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
                if (MessageBox.Show($"{LocalizationManager.GetString("ConfirmDeleteBackup")}\n\n{info.FileName}", 
                    LocalizationManager.GetString("DeleteConfirmation"), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        if (System.IO.File.Exists(info.FullPath))
                        {
                            System.IO.File.Delete(info.FullPath);
                            LoadBackups(_isHistory); // Refresh list
                            MessageBox.Show(LocalizationManager.GetString("BackupDeleted"), LocalizationManager.GetString("DeleteCompletedTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show(LocalizationManager.GetString("FileNotFound"), LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
                            LoadBackups(_isHistory); // Refresh anyway
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"{LocalizationManager.GetString("DeleteError")}: {ex.Message}", LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
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

                    MessageBox.Show(LocalizationManager.GetString("ExportSuccess"), LocalizationManager.GetString("Success"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"{LocalizationManager.GetString("ExportFailed")}: {ex.Message}", LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
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
                    if (MessageBox.Show(LocalizationManager.GetString("ImportConfirmation"), 
                        LocalizationManager.GetString("Confirm"), MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        LoadingOverlay.Visibility = Visibility.Visible;
                        
                        string targetDir = System.IO.Path.GetDirectoryName(DatabaseManager.Instance.CloudDbFilePath)!; // .../notedb
                        targetDir = System.IO.Path.GetDirectoryName(targetDir)!; // .../CatchCapture

                        DatabaseManager.Instance.ImportNoteData(ofd.FileName, targetDir);
                        
                        LoadingOverlay.Visibility = Visibility.Collapsed;
                        MessageBox.Show(LocalizationManager.GetString("ImportSuccess"), LocalizationManager.GetString("Success"), MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"{LocalizationManager.GetString("ImportFailed")}: {ex.Message}", LocalizationManager.GetString("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
