using BackupApp.Backup.Config;
using BackupApp.Backup.Handling;
using BackupApp.Restore;
using BackupApp.Restore.Caching;
using BackupApp.Restore.Handling;
using FolderFile;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace BackupApp
{
    class ViewModel : INotifyPropertyChanged
    {
        private const int keepBackupConstant = 10;

        private bool isHidden;
        private DateTime? latestBackupDateTime;
        private Folder backupDestFolder;
        private BackupConfig config;
        private BackupTask backupTask;
        private RestoreTask restoreTask;
        private CachingTask cachingTask;
        private RestoreDb restoreDb;

        public bool IsHidden
        {
            get { return isHidden; }
            set
            {
                if (value == isHidden) return;

                isHidden = value;
                OnPropertyChanged(nameof(IsHidden));
            }
        }

        public DateTime? LatestBackupDateTime
        {
            get { return latestBackupDateTime; }
            set
            {
                if (value == latestBackupDateTime) return;

                latestBackupDateTime = value;
                OnPropertyChanged(nameof(LatestBackupDateTime));
            }
        }

        public Folder BackupDestFolder
        {
            get { return backupDestFolder; }
            set
            {
                if (value == backupDestFolder) return;

                backupDestFolder = value;
                OnPropertyChanged(nameof(BackupDestFolder));

                //UpdateLatestBackupDateTime();
            }
        }

        public BackupConfig Config
        {
            get => config;
            set
            {
                if (value == config) return;

                config = value;
                OnPropertyChanged(nameof(Config));
            }
        }

        public BackupTask BackupTask
        {
            get => backupTask;
            set
            {
                if (value == backupTask) return;

                backupTask = value;
                OnPropertyChanged(nameof(BackupTask));
            }
        }

        public RestoreTask RestoreTask
        {
            get => restoreTask;
            set
            {
                if (value == restoreTask) return;

                restoreTask = value;
                OnPropertyChanged(nameof(RestoreTask));
            }
        }

        public CachingTask CachingTask
        {
            get => cachingTask;
            set
            {
                if (value == cachingTask) return;

                cachingTask = value;
                OnPropertyChanged(nameof(CachingTask));
            }
        }

        public RestoreDb RestoreDb
        {
            get => restoreDb;
            set
            {
                if (value == restoreDb) return;

                restoreDb = value;
                OnPropertyChanged(nameof(RestoreDb));
            }
        }

        public ViewModel(bool isHidden, bool isEnabled, Folder backupDestFolder,
            OffsetInterval backupTimes, DateTime nextScheduledBackup, IEnumerable<BackupItem> backupItems)
        {
            this.isHidden = isHidden;
            this.backupDestFolder = backupDestFolder;
            Config = new BackupConfig(isEnabled, backupTimes, nextScheduledBackup, backupItems);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
