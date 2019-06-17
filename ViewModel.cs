using FolderFile;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace BackupApp
{
    public class ViewModel : INotifyPropertyChanged
    {
        private const int keepBackupConstant = 10;

        private bool isHidden, isBackupEnabled;
        private bool? compressDirect;
        private int backupItemsIndex;
        private OffsetIntervalViewModel backupTimes;
        private DateTime nextScheduledBackup;
        private DateTime? latestBackupDateTime;
        private Folder backupDestFolder;

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

        public int BackupItemsIndex
        {
            get { return backupItemsIndex; }
            set
            {
                if (value == backupItemsIndex) return;

                backupItemsIndex = value;
                OnPropertyChanged(nameof(BackupItemsIndex));
            }
        }

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

        public bool? CompressDirect
        {
            get => compressDirect;
            set
            {
                if (value == compressDirect) return;

                compressDirect = value;
                OnPropertyChanged(nameof(CompressDirect));
            }
        }

        public OffsetIntervalViewModel BackupTimes
        {
            get { return backupTimes; }
            set
            {
                if (value == backupTimes) return;

                backupTimes = value;
                OnPropertyChanged(nameof(BackupTimes));

                NextScheduledBackup = BackupTimes.Next;
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

                UpdateLatestBackupDateTime();
            }
        }

        public ObservableCollection<BackupItem> BackupItems { get; }

        public ViewModel(bool isHidden, bool isEnabled, bool? compressDirect, OffsetIntervalViewModel backupTimes,
            DateTime nextScheduledBackup, Folder backupDestFolder, IEnumerable<BackupItem> backupItems)
        {
            this.isHidden = isHidden;
            this.isBackupEnabled = isEnabled;
            this.compressDirect = compressDirect;
            this.backupTimes = backupTimes;
            this.nextScheduledBackup = nextScheduledBackup;
            this.backupDestFolder = backupDestFolder;
            BackupItems = new ObservableCollection<BackupItem>(backupItems);

            UpdateLatestBackupDateTime();
        }

        private IEnumerable<DateTime> GetBackupsDateTimes()
        {
            FileInfo[] backups = BackupDestFolder?.Refresh() ?? new FileInfo[0];

            foreach (FileInfo backup in backups)
            {
                DateTime dateTimeOfBackup;
                string name = backup.Name.Remove(backup.Name.Length - 4);

                if (TryConvertToDateTime(name, out dateTimeOfBackup)) yield return dateTimeOfBackup;
            }
        }

        private static bool TryConvertToDateTime(string name, out DateTime dateTime)
        {
            dateTime = new DateTime();
            string[] dateTimeParts = name.Split(';');

            if (dateTimeParts.Length < 2) return false;

            string[] dateParts = dateTimeParts[0].Split('-');
            string[] timeParts = dateTimeParts[1].Split('-');
            int year, month, day, hour, minute, second;

            if (dateParts.Length < 3 || !int.TryParse(dateParts[0], out year) ||
                !int.TryParse(dateParts[1], out month) || !int.TryParse(dateParts[2], out day) ||
                timeParts.Length < 3 || !int.TryParse(timeParts[0], out hour) ||
                !int.TryParse(timeParts[1], out minute) || !int.TryParse(timeParts[2], out second)) return false;

            dateTime = new DateTime(year, month, day, hour, minute, second);

            return true;
        }

        public void UpdateNextScheduledBackup() => NextScheduledBackup = BackupTimes.Next;

        public void UpdateLatestBackupDateTime()
        {
            DateTime[] times = GetBackupsDateTimes().ToArray();

            LatestBackupDateTime = times.Length > 0 ? (DateTime?)times.Last() : null;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
