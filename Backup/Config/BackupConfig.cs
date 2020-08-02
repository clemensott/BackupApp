using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace BackupApp.Backup.Config
{
    public class BackupConfig : INotifyPropertyChanged
    {
        private bool isBackupEnabled;
        private OffsetInterval backupTimes;
        private DateTime nextScheduledBackup;

        public bool IsBackupEnabled
        {
            get { return isBackupEnabled; }
            set
            {
                if (value == isBackupEnabled) return;

                isBackupEnabled = value;
                OnPropertyChanged(nameof(IsBackupEnabled));
            }
        }

        public OffsetInterval BackupTimes
        {
            get { return backupTimes; }
            set
            {
                if (value == backupTimes) return;

                backupTimes = value;
                OnPropertyChanged(nameof(BackupTimes));

                UpdateNextScheduledBackup();
            }
        }

        public DateTime NextScheduledBackup
        {
            get { return nextScheduledBackup; }
            set
            {
                if (value == nextScheduledBackup) return;

                nextScheduledBackup = value;
                OnPropertyChanged(nameof(NextScheduledBackup));
            }
        }

        public ObservableCollection<BackupItem> BackupItems { get; }

        public BackupConfig(bool isBackupEnabled, OffsetInterval backupTimes,
            DateTime nextScheduledBackup, IEnumerable<BackupItem> backupItems)
        {
            IsBackupEnabled = isBackupEnabled;
            BackupTimes = backupTimes;
            NextScheduledBackup = nextScheduledBackup;
            BackupItems = new ObservableCollection<BackupItem>(backupItems);
        }

        public void UpdateNextScheduledBackup() => NextScheduledBackup = BackupTimes.GetNextDateTime() ?? DateTime.MaxValue;

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
