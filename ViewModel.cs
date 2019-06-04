using FolderFile;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace BackupApp
{
    public class ViewModel : INotifyPropertyChanged
    {
        private const int keepBackupConstant = 10;

        private bool isHidden, isBackupEnabled;
        private int backupItemsIndex;
        private Timer timer;
        private OffsetIntervalViewModel backupTimes;
        private DateTime nextScheduledBackup;
        private DateTime? latestBackupDateTime;
        private Folder backupDestFolder;
        private readonly WindowManager windowManager;

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

                SetTimer();
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

                SetTimer();
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

        public ObservableCollection<BackupItem> BackupItems { get; private set; }

        public ViewModel(MainWindow mainWindow, bool isHidden, bool isEnabled, OffsetIntervalViewModel backupTimes,
            DateTime nextScheduledBackup, Folder backupDestFolder, IEnumerable<BackupItem> backupItems)
        {
            this.isHidden = isHidden;
            this.isBackupEnabled = isEnabled;
            this.backupTimes = backupTimes;
            this.nextScheduledBackup = nextScheduledBackup;
            this.backupDestFolder = backupDestFolder;
            BackupItems = new ObservableCollection<BackupItem>(backupItems);

            windowManager = new WindowManager(mainWindow, this);

            UpdateLatestBackupDateTime();

            SystemEvents.PowerModeChanged += OnPowerChange;

            CheckForBackup();
        }

        private async void OnPowerChange(object s, PowerModeChangedEventArgs e)
        {
            int threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
            DebugEvent.SaveText("OnPowerChange", "ThreadID: " + threadID, e.Mode);

            if (e.Mode == PowerModes.Resume)
            {
                await Task.Delay(TimeSpan.FromSeconds(10));

                CheckForBackup();
            }
        }

        public void SetTimer()
        {
            int threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
            DebugEvent.SaveText("SetTimer", "ThreadID: " + threadID);

            Close();

            if (NextScheduledBackup > DateTime.Now)
            {
                timer = new Timer
                {
                    AutoReset = false,
                    Enabled = true,
                    Interval = (NextScheduledBackup - DateTime.Now).TotalMilliseconds
                };

                timer.Elapsed += Timer_Elapsed;
                timer.Start();
            }
            else RunBackup();
        }

        public void Close()
        {
            timer?.Dispose();
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            RunBackup();
        }

        private void RunBackup()
        {
            int threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
            DebugEvent.SaveText("RunBackup", "ThreadID: " + threadID);

            NextScheduledBackup = BackupTimes.Next;

            if (IsBackupEnabled) BackupAsync();
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

        private bool TryConvertToDateTime(string name, out DateTime dateTime)
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

        public BackupTask BackupAsync()
        {
            BackupTask task = windowManager.CurrentBackupTask = BackupTask.Run(BackupDestFolder, BackupItems);

            DeleteOldBackupsAfterTask(task);

            return task;
        }

        private async void DeleteOldBackupsAfterTask(BackupTask task)
        {
            await task.Task;

            if (windowManager.CurrentBackupTask == task && !task.Failed && !task.CancelToken.IsCanceled)
            {
                DeleteOldBackups();
                UpdateNextScheduledBackup();
            }

            UpdateLatestBackupDateTime();
        }

        private void DeleteOldBackups()
        {
            DateTime[] backupsDateTimes = GetBackupsDateTimes().ToArray();

            if (backupsDateTimes.Length == 0) return;

            DateTime now = DateTime.Now;
            long intervalTimes = (now - backupsDateTimes[0]).Ticks / BackupTimes.Interval.Ticks;

            if (intervalTimes <= 0) intervalTimes = 1;

            int levels = (int)Math.Log(intervalTimes, keepBackupConstant);
            int factor = (int)Math.Pow(keepBackupConstant, levels);

            DateTime preDateTime = backupsDateTimes[0];
            List<DateTime> leftDateTimes = backupsDateTimes.ToList();

            leftDateTimes.Remove(leftDateTimes.Last());

            for (int i = 0; i <= levels; i++)
            {
                TimeSpan curInterval = new TimeSpan(factor * BackupTimes.Interval.Ticks);
                DateTime untilDateTime = now.Subtract(curInterval);

                while (preDateTime <= untilDateTime)
                {
                    var nearestDateTime = backupsDateTimes.OrderBy(d => Math.Abs((d - preDateTime).Ticks)).First();

                    if ((nearestDateTime - preDateTime).Ticks < curInterval.Ticks / 2.0)
                    {
                        leftDateTimes.Remove(nearestDateTime);
                    }

                    preDateTime = preDateTime.Add(curInterval);
                }

                if (i + 1 == levels) preDateTime = now.Subtract(curInterval);

                factor /= keepBackupConstant;
            }

            foreach (DateTime deleteDateTime in leftDateTimes) DeleteBackup(deleteDateTime);
        }

        private void DeleteBackup(DateTime dateTimeOfBackup)
        {
            string backupPath = Path.Combine(BackupDestFolder.FullName,
                BackupTask.ConvertDateTimeOfBackupToString(dateTimeOfBackup) + ".zip");

            try
            {
                File.Delete(backupPath);
            }
            catch (Exception e)
            {
                int threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
                DebugEvent.SaveText("DeleteBackupException",
                    "ThreadID: " + threadID, e.Message.Replace('\n', ' '));
            }
        }

        public void CheckForBackup()
        {
            int threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
            DebugEvent.SaveText("CheckForBackup", "ThreadID: " + threadID, "NextBackup: " + NextScheduledBackup);

            if (NextScheduledBackup <= DateTime.Now) BackupAsync();
            else SetTimer();
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
