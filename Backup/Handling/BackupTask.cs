using BackupApp.Backup.Config;
using BackupApp.Backup.Result;
using BackupApp.Helper;
using FolderFile;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BackupApp.Backup.Handling
{
    public class BackupTask : INotifyPropertyChanged
    {
        private bool isMoving, failed, isBackuping;
        private Exception failedException;
        private Task<BackupModel> task;
        private DateTime started;
        private readonly BackupedFiles backupedFiles;

        public bool IsBackuping
        {
            get { return isBackuping; }
            private set
            {
                if (value == isBackuping) return;

                isBackuping = value;
                OnPropertyChanged(nameof(IsBackuping));
            }
        }

        public bool IsMoving
        {
            get { return isMoving; }
            private set
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

        public Folder DestFolder { get; }

        public TaskBackupItem[] Items { get; }

        public CancelToken CancelToken { get; }

        public Task<BackupModel> Task
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

        private BackupTask(Folder backupFolder, IEnumerable<BackupItem> backupItems, BackupedFiles backupedFiles)
        {
            CancelToken = new CancelToken();
            DestFolder = backupFolder?.Clone();
            Items = backupItems.Select(ToTaskItem).ToArray();
            this.backupedFiles = backupedFiles;
        }

        public static BackupTask Run(Folder backupFolder, IEnumerable<BackupItem> backupItems, BackupedFiles backupedFiles)
        {
            BackupTask task = new BackupTask(backupFolder, backupItems, backupedFiles);
            task.Started = DateTime.Now;
            task.Task = task.Run();

            return task;
        }

        private static TaskBackupItem ToTaskItem(BackupItem item)
        {
            return new TaskBackupItem(item.Name, item.Folder.Clone());
        }

        private async Task<BackupModel> Run()
        {
            DebugEvent.SaveText("Backup");

            if (DestFolder == null || !Directory.Exists(DestFolder.FullName) || Items == null || Items.Length == 0) return null;

            IsBackuping = true;

            //foreach(var item in Items)
            //{
            //    for (int i = 0; i < 100; i++)
            //    {
            //        item.Progress = i / 100.0;
            //        await System.Threading.Tasks.Task.Delay(100);
            //    }
            //}
            //BackupModel backup = null;

            BackupModel backup = await Backup();
            IsBackuping = false;

            return backup;
        }

        private async Task<BackupModel> Backup()
        {
            DebugEvent.SaveText("HandleBackup");

            try
            {
                List<string> addedFiles = new List<string>();
                string filesBackupDir = BackupUtils.GetBackupedFilesFolderPath(DestFolder.FullName);
                BackupModel backup = await CreateBackup(filesBackupDir);

                if (backup == null) return null;

                IsMoving = true;

                BackupUtils.SaveBackup(DestFolder.FullName, backup);

                DebugEvent.SaveText("HandleBackupSuccessful");
                IsMoving = false;
                Failed = false;

                return backup;
            }
            catch (Exception e)
            {
                IsMoving = false;
                Failed = true;
                FailedException = e;

                return null;
            }
        }

        private async Task<BackupModel> CreateBackup(string backupFilesDir)
        {
            DebugEvent.SaveText("CreateBackup", "Path: " + backupFilesDir);

            BackupFolder[] folders;
            DateTime timestamp = DateTime.Now;
            List<string> addedFiles = new List<string>();

            try
            {
                Dictionary<Task, TaskBackupItem> dict = Items.ToDictionary(i => i.BeginBackup());

                if (!Directory.Exists(backupFilesDir)) Directory.CreateDirectory(backupFilesDir);

                folders = await System.Threading.Tasks.Task.Run(async () =>
                {
                    int i = 0;
                    BackupFolder[] backupFolders = new BackupFolder[dict.Count];

                    while (!CancelToken.IsCanceled && dict.Count > 0)
                    {
                        Task filesLoadedTask = await System.Threading.Tasks.Task.WhenAny(dict.Keys);

                        if (CancelToken.IsCanceled) break;

                        backupFolders[i++] = dict[filesLoadedTask].Backup(backupFilesDir, backupedFiles, CancelToken, addedFiles);

                        dict.Remove(filesLoadedTask);
                    }

                    return backupFolders;
                });
            }
            catch (Exception e)
            {
                DebugEvent.SaveText("CreateBackupItselfException", e.ToString());

                foreach (string addedFile in addedFiles)
                {
                    try
                    {
                        File.Delete(addedFile);
                    }
                    catch (Exception e2)
                    {
                        DebugEvent.SaveText("CreateBackupDeleteException", e2.ToString());
                    }
                }

                throw;
            }

            return addedFiles.Count > 0 && !CancelToken.IsCanceled ? new BackupModel(timestamp, folders) : null;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
