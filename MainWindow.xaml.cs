using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using BackupApp.Backup.Config;
using BackupApp.Backup.Handling;
using BackupApp.Backup.Result;
using BackupApp.LocalSave;
using BackupApp.Restore;
using BackupApp.Restore.Caching;
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
            loadSaveHandler.Model.RestoreDb = await RestoreDb.Open(restoreDbFilename);

            loadSaveHandler.Model.PropertyChanged += Model_PropertyChanged;
            DataContext = viewModel = loadSaveHandler.Model;

            string destFolderPath = viewModel.BackupDestFolder?.FullName;
            if (!string.IsNullOrWhiteSpace(destFolderPath))
            {
                viewModel.CachingTask = FolderCachingTask.Run(destFolderPath, viewModel.RestoreDb);
            }

            WindowManager.Start(this, viewModel);

            await CheckForBackup();

            if (viewModel.CachingTask != null)
            {
                string testDbPath = @"D:\bak.db";
                if (File.Exists(testDbPath)) File.Delete(testDbPath);
                RestoreDb db = await RestoreDb.Open(testDbPath);
                string[] testLines = File.ReadAllLines(@"Z:\Backup\Clemens\X1\2020-08-02_15-17-29.txt");

                await Task.Run(() => db.InsertBackupTest(testLines));
            }
        }

        private async void Model_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            string destFolderPath;
            BackupTask backupTask;
            CachingTask cachingTask;

            switch (e.PropertyName)
            {
                case nameof(viewModel.BackupDestFolder):
                    destFolderPath = viewModel.BackupDestFolder?.FullName;
                    if (!string.IsNullOrWhiteSpace(destFolderPath))
                    {
                        viewModel.CachingTask = FolderCachingTask.Run(destFolderPath, viewModel.RestoreDb);
                    }
                    break;

                case nameof(viewModel.CachingTask):
                    cachingTask = viewModel.CachingTask;
                    await cachingTask.Task;
                    if (viewModel.CachingTask == cachingTask) await bbc.ReloadBackups();
                    break;

                case nameof(viewModel.BackupTask):
                    backupTask = viewModel.BackupTask;
                    destFolderPath = viewModel.BackupDestFolder?.FullName;

                    BackupModel backup = await backupTask.Task;

                    if (backup != null && viewModel.RestoreDb != null && destFolderPath == viewModel.BackupDestFolder?.FullName)
                    {
                        cachingTask = BackupsCachingTask.Run(new BackupModel[] { backup }, viewModel.RestoreDb);
                        viewModel.CachingTask = cachingTask;
                        await cachingTask.Task;
                        await bbc.ReloadBackups();
                    }
                    break;
            }
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
                return;
                btnStartBackup.IsEnabled = false;

                if (viewModel.CachingTask != null) await viewModel.CachingTask.Task;

                IDictionary<string, string> dict = await viewModel.RestoreDb.GetAllFiles();
                BackupedFiles files = new BackupedFiles(dict);
                BackupTask task = BackupTask.Run(viewModel.BackupDestFolder, viewModel.Config?.BackupItems, files);
                viewModel.BackupTask = task;

                await task.Task;
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

        private void BrowseBackupsControl_Restore(object sender, RestoreNode e)
        {
            viewModel.RestoreTask = RestoreTask.Run(viewModel.BackupDestFolder.FullName, e);
        }
    }
}
