using BackupApp.Backup.Config;
using BackupApp.Backup.Result;
using BackupApp.Helper;
using StdOttStandard.Linq;
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
        private BackupWriteDb dB;

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

        public BackupWriteDb DB
        {
            get => dB;
            private set
            {
                if (value == dB) return;

                dB = value;
                OnPropertyChanged(nameof(DB));
            }
        }

        public string DestFolderPath { get; }

        public string FilesBackupDir { get; }

        public IList<string> AddedFiles { get; }

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
            FilesBackupDir = BackupUtils.GetBackupedFilesFolderPath(DestFolderPath);
            AddedFiles = new List<string>();
            Items = backupItems.Select(item => ToTaskItem(item, FilesBackupDir, AddedFiles, CancelToken)).ToArray();
        }

        public static BackupTask Run(string backupFolderPath, IEnumerable<BackupItem> backupItems)
        {
            BackupTask task = new BackupTask(backupFolderPath, backupItems);
            task.Started = DateTime.Now;
            task.Task = task.Run();

            return task;
        }

        private static TaskBackupItem ToTaskItem(BackupItem item, string filesDestFolderPath,
            IList<string> addedFiles, CancelToken cancelToken)
        {
            PathPattern[] patterns = item.ExcludePatterns.ToNotNull().Select(p => new PathPattern(p)).ToArray();
            return new TaskBackupItem(item.Name, filesDestFolderPath,
                item.Folder.Clone(), patterns, addedFiles, cancelToken);
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
            DebugEvent.SaveText("HandleBackup");

            try
            {
                IsLoadingBackupedFiles = true;
                Task<IDictionary<string, string>> allFilesTask =
                    Task.Run(() => BackupUtils.GetAllFiles(DestFolderPath, CancelToken));

                DB = BackupUtils.CreateDb(Path.GetTempPath(), Started);
                Dictionary<Task, TaskBackupItem> dict = Items.ToDictionary(i => i.BeginBackup(DB));

                IDictionary<string, string> allFiles = await allFilesTask;
                BackupedFiles backupedFiles = new BackupedFiles(allFiles);
                IsLoadingBackupedFiles = false;

                if (!Directory.Exists(FilesBackupDir)) Directory.CreateDirectory(FilesBackupDir);

                while (!CancelToken.IsCanceled && dict.Count > 0)
                {
                    Task filesLoadedTask = await Task.WhenAny(dict.Keys);

                    if (CancelToken.IsCanceled) break;

                    await Task.Run(() => dict[filesLoadedTask].Backup(backupedFiles));
                    dict.Remove(filesLoadedTask);
                }

                if (AddedFiles.Count > 0 && !CancelToken.IsCanceled)
                {
                    IsFlushing = true;

                    DB.Commit();

                    if (!CancelToken.IsCanceled)
                    {
                        destDbPath = Path.Combine(DestFolderPath, Path.GetFileName(DB.Path));
                        File.Copy(DB.Path, destDbPath);

                        DeleteFile(DB.Path);

                        DebugEvent.SaveText("HandleBackupSuccessful");
                        Failed = false;
                        return;
                    }
                }

                DB.Dispose();

                DeleteFile(DB.Path);

                foreach (string addedFile in AddedFiles)
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
            }
            catch (Exception e)
            {
                DebugEvent.SaveText("CreateBackupException", e.ToString());
                CancelToken.Cancel();
                DB?.Dispose();

                foreach (string addedFile in AddedFiles)
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
                    if (DB != null) DeleteFile(DB.Path);
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

        public static async void DeleteFile(string path)
        {
            while (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                    return;
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine("Failed deleting file: " + path);
                }

                await Task.Delay(5000);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
