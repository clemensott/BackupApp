using FolderFile;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;

namespace BackupApp
{
    class BackupManager
    {
        private const int keepBackupConstant = 10;
        private static BackupManager instance;

        public static BackupManager Current
        {
            get
            {
                if (instance == null) instance = new BackupManager();

                return instance;
            }
        }

        private bool isBackuping;
        private Timer timer;
        private object moveLockObj;

        public bool IsBackuping
        {
            get { return isBackuping; }
            set
            {
                if (value == isBackuping) return;

                isBackuping = value;

                if (isBackuping) WindowManager.Current.ShowBackupWindow();
                else WindowManager.Current.HideBackupWindow();
            }
        }

        public bool Failed { get; private set; }

        public DateTime LatestBackupDateTime
        {
            get
            {
                List<DateTime> backupsDateTimes = GetBackupsDateTimes();

                return backupsDateTimes.Count != 0 ? backupsDateTimes.Last() : new DateTime();
            }
        }

        private BackupManager()
        {
            IsBackuping = false;
            moveLockObj = new object();

            CheckForBackup();
        }

        public void SetTimer()
        {
            int threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
            DebugEvent.SaveText("SetTimer", "ThreadID: " + threadID);

            if (timer != null) timer.Dispose();

            timer = new Timer();

            timer.AutoReset = false;
            timer.Enabled = true;
            timer.Elapsed += Timer_Elapsed;
            timer.Interval = (ViewModel.Current.BackupTimes.NextDateTime - DateTime.Now).TotalMilliseconds;
            timer.Start();

            ViewModel.Current.UpdateNextBackupDateTimeWithIntervalText();
            ViewModel.Current.BackupTimes.UpdateNext();
        }

        public void Close()
        {
            if (timer != null) timer.Dispose();
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            int threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
            DebugEvent.SaveText("TimerElapsed", "ThreadID: " + threadID);

            SetTimer();
            Backup();
        }

        private List<DateTime> GetBackupsDateTimes()
        {
            List<DateTime> backupsDateTimes = new List<DateTime>();

            if (!ViewModel.Current.BackupFolder.Info.Exists) return backupsDateTimes;

            FileInfo[] backups = ViewModel.Current.BackupFolder.Info.GetFiles();

            foreach (FileInfo backup in backups)
            {
                DateTime dateTimeOfBackup;
                string name = backup.Name.Remove(backup.Name.Length - 4);

                if (TryConvertToDateTime(name, out dateTimeOfBackup))
                {
                    backupsDateTimes.Add(dateTimeOfBackup);
                }
            }

            return backupsDateTimes;
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

        private string ConvertDateTimeOfBackupToString(DateTime dateTimeOfBackup)
        {
            DateTime dt = dateTimeOfBackup;

            return string.Format("{0:0000}-{1:00}-{2:00};{3:00}-{4:00}-{5:00}",
                dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second);
        }

        public void Backup()
        {
            int threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
            DebugEvent.SaveText("Backup", "ThreadID: " + threadID);

            if (!Directory.Exists(ViewModel.Current.BackupFolder.FullPath) ||
                ViewModel.Current.BackupItems.Count == 0) return;

            IsBackuping = true;

            string backupZipPath = Path.Combine(ViewModel.Current.BackupFolder.FullPath,
                  ConvertDateTimeOfBackupToString(DateTime.Now) + ".zip");

            try
            {
                HandleBackup(backupZipPath);

                if (IsBackuping) DeleteOldBackups();

                ViewModel.Current.UpdateLatestBackupDateTime();
                ViewModel.Current.SetNextScheduledBackup();
            }
            catch { }

            IsBackuping = false;
        }

        private void HandleBackup(string zipFilePath)
        {
            int threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
            DebugEvent.SaveText("HandleBackup", "ThreadID: " + threadID);

            string tmpZipFilePath = "";

            if (!CompressDirect(out tmpZipFilePath))
            {
                CreateBackup(tmpZipFilePath);

                try
                {
                    if (!IsBackuping) File.Delete(tmpZipFilePath);
                    else
                    {
                        ViewModel.Current.IsMoving = true;
                        File.Move(tmpZipFilePath, zipFilePath);
                        ViewModel.Current.IsMoving = false;

                        DebugEvent.SaveText("HandleBackupSucessfull", "ThreadID: " + threadID);
                        Failed = false;
                    }
                }
                catch (Exception e)
                {
                    DebugEvent.SaveText("HandleBackupException", "ThreadID: " + threadID, e.Message.Replace('\n', ' '));

                    try
                    {
                        Failed = true;

                        File.Delete(tmpZipFilePath);
                    }
                    catch { }
                }
            }
            else CreateBackup(zipFilePath);
        }

        private void CreateBackup(string zipFilePath)
        {
            int threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
            DebugEvent.SaveText("CreateBackup", "ThreadID: " + threadID, "Path: " + zipFilePath);

            try
            {
                if (File.Exists(zipFilePath)) File.Delete(zipFilePath);
            }
            catch (Exception e)
            {
                DebugEvent.SaveText("CreateBackupDelete1Exeption", "ThreadID: " + threadID, e.Message.Replace('\n', ' '));
            }

            bool haveEntries = false;

            try
            {
                using (ZipArchive archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
                {
                    foreach (BackupItem item in ViewModel.Current.BackupItems)
                    {
                        if (!IsBackuping) break;

                        if (item.Backup(archive)) haveEntries = true;
                    }
                }
            }
            catch (Exception e)
            {
                DebugEvent.SaveText("CreateBackupItselfException", "ThreadID: " + threadID, e.Message.Replace('\n', ' '));
            }

            if (!haveEntries) File.Delete(zipFilePath);
        }

        private bool CompressDirect(out string itemTmpFilePath)
        {
            bool sameItemsRoot;
            string firstItemRoot, backupRoot;
            List<BackupItem> items = ViewModel.Current.BackupItems;

            firstItemRoot = Path.GetPathRoot(items[0].Folder.FullPath);
            sameItemsRoot = items.TrueForAll(i => Path.GetPathRoot(i.Folder.FullPath) == firstItemRoot);

            backupRoot = Path.GetPathRoot(ViewModel.Current.BackupFolder.FullPath);
            itemTmpFilePath = "";

            if ((!sameItemsRoot) || (sameItemsRoot && firstItemRoot == backupRoot)) return true;

            itemTmpFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".zip");

            return false;
        }

        private void DeleteOldBackups()
        {
            List<DateTime> backupsDateTimes = GetBackupsDateTimes();

            if (backupsDateTimes.Count == 0) return;

            DateTime now = DateTime.Now;
            long intervalTimes = (now - backupsDateTimes[0]).Ticks / ViewModel.Current.BackupTimes.Interval.Ticks;

            if (intervalTimes <= 0) intervalTimes = 1;

            int levels = (int)Math.Log(intervalTimes, keepBackupConstant);
            int factor = (int)Math.Pow(keepBackupConstant, levels);

            DateTime preDateTime = backupsDateTimes[0];
            List<DateTime> leftDateTimes = backupsDateTimes.ToList();

            leftDateTimes.Remove(leftDateTimes.Last());

            for (int i = 0; i <= levels; i++)
            {
                TimeSpan curInterval = new TimeSpan(factor * ViewModel.Current.BackupTimes.Interval.Ticks);
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
            string backupPath = Path.Combine(ViewModel.Current.BackupFolder.FullPath,
                ConvertDateTimeOfBackupToString(dateTimeOfBackup) + ".zip");

            try
            {
                File.Delete(backupPath);
            }
            catch (Exception e)
            {
                int threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
                DebugEvent.SaveText("DeleteBackupExeption", "ThreadID: " + threadID, e.Message.Replace('\n', ' '));
            }
        }

        public void CheckForBackup()
        {
            int threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
            DebugEvent.SaveText("CheckForBackup", "ThreadID: " + threadID,
                "NextBackup: " + ViewModel.Current.NextScheduledBackup);

            if (ViewModel.Current.NextScheduledBackup <= DateTime.Now) new Task(Backup).Start();

            SetTimer();
        }
    }
}
