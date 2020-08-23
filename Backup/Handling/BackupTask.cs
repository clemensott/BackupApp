using BackupApp.Backup.Config;
using BackupApp.Backup.Result;
using BackupApp.Helper;
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
        private bool failed, isBackuping, isLoadingBackupedFiles, isFlushing;
        private Exception failedException;
        private DateTime started;

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

        public bool IsFlushing
        {
            get { return isFlushing; }
            private set
            {
                if (value == isFlushing) return;

                isFlushing = value;
                OnPropertyChanged(nameof(IsFlushing));
            }
        }

        public bool IsLoadingBackupedFiles
        {
            get => isLoadingBackupedFiles;
            private set
            {
                if (value == isLoadingBackupedFiles) return;

                isLoadingBackupedFiles = value;
                OnPropertyChanged(nameof(IsLoadingBackupedFiles));
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

        public string DestFolderPath { get; }

        public TaskBackupItem[] Items { get; }

        public CancelToken CancelToken { get; }

        public Task Task { get; private set; }

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

        private BackupTask(string backupFolderPath, IEnumerable<BackupItem> backupItems)
        {
            CancelToken = new CancelToken();
            DestFolderPath = backupFolderPath;
            Items = backupItems.Select(item => ToTaskItem(item, CancelToken)).ToArray();
        }

        public static BackupTask Run(string backupFolderPath, IEnumerable<BackupItem> backupItems)
        {
            BackupTask task = new BackupTask(backupFolderPath, backupItems);
            task.Started = DateTime.Now;
            task.Task = task.Run();

            return task;
        }

        private static TaskBackupItem ToTaskItem(BackupItem item, CancelToken cancelToken)
        {
            return new TaskBackupItem(item.Name, item.Folder.Clone(), cancelToken);
        }

        private async Task Run()
        {
            DebugEvent.SaveText("Backup");

            if (!Directory.Exists(DestFolderPath) || Items == null || Items.Length == 0) return;

            IsBackuping = true;
            await Backup();
            IsBackuping = false;
        }

        private async Task Backup()
        {
            string destDbPath = null;
            List<string> addedFiles = new List<string>();
            BackupWriteDb db = null;
            DebugEvent.SaveText("HandleBackup");

            try
            {
                IsLoadingBackupedFiles = true;
                Task<IDictionary<string, string>> allFilesTask = Task.Run(() => BackupUtils.GetAllFiles(DestFolderPath));
                Dictionary<Task, TaskBackupItem> dict = Items.ToDictionary(i => i.BeginBackup());
                db = await BackupUtils.CreateDb(Path.GetTempPath(), Started);
                //db = await BackupUtils.CreateDb(@"C:\Sharp", Started);

                IDictionary<string, string> allFiles = await allFilesTask;
                BackupedFiles backupedFiles = new BackupedFiles(allFiles);
                IsLoadingBackupedFiles = false;

                string filesBackupDir = BackupUtils.GetBackupedFilesFolderPath(DestFolderPath);
                if (!Directory.Exists(filesBackupDir)) Directory.CreateDirectory(filesBackupDir);

                while (!CancelToken.IsCanceled && dict.Count > 0)
                {
                    Task filesLoadedTask = await Task.WhenAny(dict.Keys);

                    if (CancelToken.IsCanceled) break;

                    await Task.Run(() => dict[filesLoadedTask].Backup(filesBackupDir, db, backupedFiles, addedFiles));
                    dict.Remove(filesLoadedTask);
                }

                db.Finish();

                if (addedFiles.Count == 0 || CancelToken.IsCanceled)
                {
                    db.Dispose();
                    await db.FlushTask;

                    File.Delete(db.Path);

                    foreach (string addedFile in addedFiles)
                    {
                        try
                        {
                            File.Delete(addedFile);
                        }
                        catch (Exception e)
                        {
                            DebugEvent.SaveText("CancelBackupDeleteAddedFileException", e.ToString());
                        }
                    }

                    return;
                }

                IsFlushing = true;

                destDbPath = Path.Combine(DestFolderPath, Path.GetFileName(db.Path));
                await db.FlushTask;
                File.Move(db.Path, destDbPath);

                DebugEvent.SaveText("HandleBackupSuccessful");
                Failed = false;
            }
            catch (Exception e)
            {
                DebugEvent.SaveText("CreateBackupException", e.ToString());
                CancelToken.Cancel();
                db?.Dispose();

                foreach (string addedFile in addedFiles)
                {
                    try
                    {
                        File.Delete(addedFile);
                    }
                    catch (Exception e2)
                    {
                        DebugEvent.SaveText("CreateBackupDeleteAddedFileException", e2.ToString());
                    }
                }

                try
                {
                    // if (db != null) File.Delete(db.Path);
                }
                catch (Exception e2)
                {
                    DebugEvent.SaveText("CreateBackupDeleteLocalDbException", e2.ToString());
                }

                try
                {
                    if (destDbPath != null && File.Exists(destDbPath)) File.Delete(destDbPath);
                }
                catch (Exception e2)
                {
                    DebugEvent.SaveText("CreateBackupDeleteDestDbException", e2.ToString());
                }

                Failed = true;
                FailedException = e;
            }
            finally
            {
                IsLoadingBackupedFiles = false;
                IsFlushing = false;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
