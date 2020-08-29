using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using BackupApp.Backup.Config;
using BackupApp.Backup.Handling;
using BackupApp.Backup.Valitate;
using BackupApp.LocalSave;
using BackupApp.Restore.Handling;

namespace BackupApp
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string dataFilename = "Data.xml";

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
            DebugEvent.SaveText("CheckForBackup", "NextBackup: " + viewModel.Config.NextScheduledBackup);

            BackupConfig config = viewModel.Config;
            if (config == null || config.NextScheduledBackup > DateTime.Now) return;

            BackupTask backupTask = viewModel.BackupTask;
            if (config.IsBackupEnabled && (backupTask == null || backupTask.Result.HasValue)) await BackupAsync();
        }

        private async Task BackupAsync()
        {
            if (!btnStartBackup.IsEnabled) return;

            try
            {
                btnStartBackup.IsEnabled = btnStartValidation.IsEnabled = false;

                string destFolderPath = viewModel.BackupDestFolder?.FullName;
                ICollection<BackupItem> backupItems = viewModel.Config.BackupItems;

                if (string.IsNullOrWhiteSpace(destFolderPath) ||
                    !Directory.Exists(destFolderPath) ||
                    backupItems == null ||
                    backupItems.Count == 0) return;

                BackupTask task = BackupTask.Run(destFolderPath, backupItems);
                viewModel.BackupTask = task;

                BackupTaskResult result = await task;
                viewModel.Config.UpdateNextScheduledBackup();
                if (result == BackupTaskResult.Successful) await bbc.ReloadBackups();
            }
            finally
            {
                btnStartBackup.IsEnabled = btnStartValidation.IsEnabled = true;
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

        private async void BtnValidateNow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnStartBackup.IsEnabled = btnStartValidation.IsEnabled = false;

                ValidationTask task = ValidationTask.Run(viewModel.BackupDestFolder?.FullName);
                viewModel.ValidationTask = task;
                await task;
            }
            finally
            {
                btnStartBackup.IsEnabled = btnStartValidation.IsEnabled = true;
            }
        }

        private void BrowseBackupsControl_Restore(object sender, Restore.BackupFolder e)
        {
            viewModel.RestoreTask = RestoreTask.Run(viewModel.BackupDestFolder.FullName, e);
        }
    }
}
