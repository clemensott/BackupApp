using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using BackupApp.Backup.Config;
using BackupApp.Backup.Handling;
using BackupApp.LocalSave;
using BackupApp.Restore.Handling;

namespace BackupApp
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string dataFilename = "Data.xml", restoreDbFilename = "D:\\restore_cache.db";

        private ViewModel viewModel;
        private readonly DispatcherTimer timer;

        public MainWindow()
        {
            InitializeComponent();

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMinutes(1);
            timer.Tick += Timer_Tick;
            timer.Start();

            SetViewModel();
        }

        private async void SetViewModel()
        {
            LoadSaveHandler loadSaveHandler = await LoadSaveHandler.Load(dataFilename);
            DataContext = viewModel = loadSaveHandler.Model;

            WindowManager.Start(this, viewModel);

            await CheckForBackup();
        }

        private async void Timer_Tick(object sender, EventArgs e)
        {
            await CheckForBackup();
        }

        private async Task CheckForBackup()
        {
            DebugEvent.SaveText("CheckForBackup", "NextBackup: " + viewModel.Config?.NextScheduledBackup);

            BackupConfig config = viewModel.Config;
            if (config == null || config.NextScheduledBackup > DateTime.Now) return;

            if (config.IsBackupEnabled && viewModel.BackupTask?.IsBackuping != true) await BackupAsync();
            else config.UpdateNextScheduledBackup();
        }

        private async Task BackupAsync()
        {
            try
            {
                btnStartBackup.IsEnabled = false;

                string destFolderPath = viewModel.BackupDestFolder?.FullName;
                ICollection<BackupItem> backupItems = viewModel.Config?.BackupItems;
                if (string.IsNullOrWhiteSpace(destFolderPath) || !Directory.Exists(destFolderPath) ||
                    backupItems == null || backupItems.Count == 0) return;

                BackupTask task = BackupTask.Run(destFolderPath, backupItems);
                viewModel.BackupTask = task;

                BackupTaskResult result = await task;
                if (result == BackupTaskResult.Successful) await bbc.ReloadBackups();
            }
            finally
            {
                btnStartBackup.IsEnabled = true;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            timer.Stop();

            base.OnClosed(e);
        }

        private async void BtnBackupNow_Click(object sender, RoutedEventArgs e)
        {
            await BackupAsync();
        }

        private void BrowseBackupsControl_Restore(object sender, Restore.BackupFolder e)
        {
            viewModel.RestoreTask = RestoreTask.Run(viewModel.BackupDestFolder.FullName, e);
        }
    }
}
