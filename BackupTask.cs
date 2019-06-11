using FolderFile;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace BackupApp
{
    public class BackupTask : INotifyPropertyChanged
    {
        private bool isMoving, failed, isBackuping;
        private Exception failedException;
        private Task task;
        private DateTime started;

        public bool IsBackuping
        {
            get { return isBackuping; }
            set
            {
                if (value == isBackuping) return;

                isBackuping = value;
                OnPropertyChanged(nameof(IsBackuping));
            }
        }

        public bool IsMoving
        {
            get { return isMoving; }
            set
            {
                if (value == isMoving) return;

                isMoving = value;
                OnPropertyChanged(nameof(IsMoving));
            }
        }

        public bool Failed
        {
            get { return failed; }
            private set
            {
                if (value == failed) return;

                failed = value;
                OnPropertyChanged(nameof(Failed));
            }
        }

        public Exception FailedException
        {
            get { return failedException; }
            private set
            {
                if (value == failedException) return;

                failedException = value;
                OnPropertyChanged(nameof(FailedException));
            }
        }

        public Folder DestFolder { get; private set; }

        public TaskBackupItem[] Items { get; private set; }

        public BackupCancelToken CancelToken { get; private set; }

        public Task Task
        {
            get { return task; }
            private set
            {
                if (value == task) return;

                task = value;
                OnPropertyChanged(nameof(Task));
            }
        }

        public DateTime Started
        {
            get { return started; }
            private set
            {
                if (value == started) return;

                started = value;
                OnPropertyChanged(nameof(Started));
            }
        }

        private BackupTask(Folder backupFolder, IEnumerable<BackupItem> backupItems)
        {
            CancelToken = new BackupCancelToken();
            DestFolder = backupFolder?.Clone();
            Items = backupItems.Select(ToTaskItem).ToArray();
        }

        public static BackupTask Run(Folder backupFolder, IEnumerable<BackupItem> backupItems)
        {
            BackupTask task = new BackupTask(backupFolder, backupItems);
            task.BackupAsync();

            return task;
        }

        public static TaskBackupItem ToTaskItem(BackupItem item)
        {
            return new TaskBackupItem(item.Name, item.Folder.Clone());
        }

        private Task BackupAsync()
        {
            Started = DateTime.Now;
            return Task = Backup();
        }

        private async Task Backup()
        {
            int threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
            DebugEvent.SaveText("Backup", "ThreadID: " + threadID);

            if (DestFolder == null || !Directory.Exists(DestFolder.FullName) || Items.Length == 0) return;

            IsBackuping = true;

            string backupZipPath = Path.Combine(DestFolder.FullName, ConvertDateTimeOfBackupToString(DateTime.Now) + ".zip");

            await HandleBackup(backupZipPath);

            IsBackuping = false;
        }

        public static string ConvertDateTimeOfBackupToString(DateTime dateTimeOfBackup)
        {
            DateTime dt = dateTimeOfBackup;

            return string.Format("{0:0000}-{1:00}-{2:00};{3:00}-{4:00}-{5:00}",
                dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second);
        }

        private async Task HandleBackup(string zipFilePath)
        {
            int threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
            DebugEvent.SaveText("HandleBackup", "ThreadID: " + threadID);

            string tmpZipFilePath;

            if (!CompressDirect(out tmpZipFilePath))
            {
                if (!await CreateBackup(tmpZipFilePath)) return;

                try
                {
                    if (CancelToken.IsCanceled) File.Delete(tmpZipFilePath);
                    else
                    {
                        IsMoving = true;
                        await Task.Run(() => File.Move(tmpZipFilePath, zipFilePath));
                        IsMoving = false;

                        DebugEvent.SaveText("HandleBackupSuccessful", "ThreadID: " + threadID);
                        Failed = false;
                    }
                }
                catch (Exception e)
                {
                    DebugEvent.SaveText("HandleBackupException", "ThreadID: " + threadID,
                        e.Message.Replace("\r\n", " "));

                    try
                    {
                        Failed = true;
                        FailedException = e;

                        File.Delete(tmpZipFilePath);
                    }
                    catch { }
                }
            }
            else await CreateBackup(zipFilePath);
        }

        private async Task<bool> CreateBackup(string zipFilePath)
        {
            int threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
            DebugEvent.SaveText("CreateBackup", "ThreadID: " + threadID, "Path: " + zipFilePath);

            try
            {
                if (File.Exists(zipFilePath)) File.Delete(zipFilePath);
            }
            catch (Exception e)
            {
                DebugEvent.SaveText("CreateBackupDelete1Exception", "ThreadID: " + threadID,
                    e.Message.Replace("\r\n", " "));
            }

            bool hasEntries = false;

            try
            {
                Dictionary<Task, TaskBackupItem> dict = Items.ToDictionary(i => i.BeginBackup());

                await Task.Run(async () =>
                {
                    using (ZipArchive archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
                    {
                        while (!CancelToken.IsCanceled && dict.Count > 0)
                        {
                            Task filesLoadedTask = await Task.WhenAny(dict.Keys);

                            if (CancelToken.IsCanceled) break;
                            if (dict[filesLoadedTask].Backup(archive, CancelToken)) hasEntries = true;

                            dict.Remove(filesLoadedTask);
                        }
                    }
                });
            }
            catch (Exception e)
            {
                DebugEvent.SaveText("CreateBackupItselfException", "ThreadID: " + threadID,
                    e.Message.Replace("\r\n", " "));
            }

            if (!hasEntries || CancelToken.IsCanceled) File.Delete(zipFilePath);

            return hasEntries;
        }

        private bool CompressDirect(out string itemTmpFilePath)
        {
            string backupRoot = Path.GetPathRoot(DestFolder.FullName);
            bool isAnyRootBackupRoot = Items.Any(i => Path.GetPathRoot(i.Folder.FullName) == backupRoot);

            itemTmpFilePath = "";

            if (isAnyRootBackupRoot) return true;

            itemTmpFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".zip");

            return false;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
