using FolderFile;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using StdOttStandard;

namespace BackupApp
{
    public class BackupTask : INotifyPropertyChanged
    {
        private const int minBufferSize = 1000, maxBufferSize = 1000 * 1000;

        private bool isMoving, failed, isBackuping;
        private double moveProgress;
        private Exception failedException;
        private Task<Backup> task;
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

        public double MoveProgress
        {
            get => moveProgress;
            private set
            {
                if (Math.Abs(value - moveProgress) < 0.01) return;

                moveProgress = value;
                OnPropertyChanged(nameof(MoveProgress));
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

        public bool? CompressDirect { get; }

        public Folder DestFolder { get; }

        public TaskBackupItem[] Items { get; }

        public CancelToken CancelToken { get; }

        public Task<Backup> Task
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

        private BackupTask(Folder backupFolder, IEnumerable<BackupItem> backupItems, bool? compressDirect)
        {
            CancelToken = new CancelToken();
            DestFolder = backupFolder?.Clone();
            Items = backupItems.Select(ToTaskItem).ToArray();
            CompressDirect = compressDirect;
        }

        public static BackupTask Run(Folder backupFolder, IEnumerable<BackupItem> backupItems, bool? compressDirect)
        {
            BackupTask task = new BackupTask(backupFolder, backupItems, compressDirect);
            task.BackupAsync();

            return task;
        }

        private static TaskBackupItem ToTaskItem(BackupItem item)
        {
            return new TaskBackupItem(item.Name, item.Folder.Clone());
        }

        private Task<Backup> BackupAsync()
        {
            Started = DateTime.Now;
            return Task = Backup();
        }

        private async Task<Backup> Backup()
        {
            DebugEvent.SaveText("Backup");

            if (DestFolder == null || !Directory.Exists(DestFolder.FullName) || Items.Length == 0) return null;

            IsBackuping = true;
            Backup backup = await HandleBackup();
            IsBackuping = false;

            return backup;
        }

        private async Task<Backup> HandleBackup()
        {
            DebugEvent.SaveText("HandleBackup");

            IDictionary<string, string> backupedFiles;

            try
            {
                IEnumerable<Backup> backups = await BackupUtils.GetBackups(DestFolder);
                backupedFiles = BackupUtils.GetFiles(backups);
            }
            catch (Exception e)
            {
                DebugEvent.SaveText("HandleBackupGetBackupsException", e.ToString());

                Failed = true;
                FailedException = e;

                return null;
            }

            bool compressDirect = true;     // CompressDirect ?? ShouldCompressDirect();
            string tmpZipPath = GetTmpFilePath(compressDirect);

            tmpZipPath = Path.Combine(DestFolder.FullName, BackupUtils.BackupFilesDirName);
            Backup backup = await CreateBackup(tmpZipPath, backupedFiles);

            if (backup == null) return null;

            string backupPath = Path.Combine(DestFolder.FullName, backup.Name + BackupUtils.TxtExtension);
            string zipPath = Path.Combine(DestFolder.FullName, backup.Name + BackupUtils.ZipExtension);

            try
            {
                IsMoving = true;

                if (compressDirect) File.Move(tmpZipPath, zipPath);
                else
                {
                    using (Stream writer = new FileStream(zipPath, FileMode.CreateNew))
                    {
                        using (Stream reader = new FileStream(tmpZipPath, FileMode.Open))
                        {
                            Task writeTask = System.Threading.Tasks.Task.CompletedTask;
                            byte[] buffer = new byte[GetBufferSize(reader.Length)];

                            while (reader.Position < reader.Length)
                            {
                                int readCount = await reader.ReadAsync(buffer, 0, buffer.Length);

                                await writeTask;

                                if (CancelToken.IsCanceled) break;

                                MoveProgress = Math.Round(reader.Position / (double)reader.Length, 2);

                                writeTask = writer.WriteAsync(buffer, 0, readCount);
                            }
                        }
                    }

                    try
                    {
                        if (CancelToken.IsCanceled) File.Delete(zipPath);
                    }
                    catch (Exception e)
                    {
                        DebugEvent.SaveText("DeleteZipFileAfterCopyException", CancelToken.IsCanceled, e.ToString());
                    }

                    try
                    {
                        File.Delete(tmpZipPath);
                    }
                    catch (Exception e)
                    {
                        DebugEvent.SaveText("DeleteTmpFileAfterCopyException", e.ToString());
                    }
                }

                string backupText = BackupUtils.Serialize(backup);
                File.WriteAllText(backupPath, backupText);

                DebugEvent.SaveText("HandleBackupSuccessful");
                IsMoving = false;
                Failed = false;
            }
            catch (Exception e)
            {
                DebugEvent.SaveText("HandleBackupMoveException", e.ToString());

                IsMoving = false;
                Failed = true;
                FailedException = e;

                try
                {
                    File.Delete(tmpZipPath);
                }
                catch (Exception e1)
                {
                    DebugEvent.SaveText("HandleBackupMoveDeleteTmpException", e1.ToString());
                }

                try
                {
                    File.Delete(zipPath);
                }
                catch (Exception e2)
                {
                    DebugEvent.SaveText("HandleBackupMoveDeleteZipException", e2.ToString());
                }
            }

            return backup;
        }

        private async Task<Backup> CreateBackup(string backupFilesDir, IDictionary<string, string> backupedFiles)
        {
            DebugEvent.SaveText("CreateBackup", "Path: " + backupFilesDir);

            BackupFolder[] folders;
            List<string> addedFiles = new List<string>();
            DateTime timestamp = DateTime.Now;

            try
            {
                Dictionary<Task, TaskBackupItem> dict = Items.ToDictionary(i => i.BeginBackup());

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

                folders = null;
            }

            if (addedFiles.Count > 0 && !CancelToken.IsCanceled) return new Backup(timestamp, folders);

            try
            {
                foreach (string addedFile in addedFiles)
                {
                    File.Delete(addedFile);
                }
            }
            catch (Exception e)
            {
                DebugEvent.SaveText("CreateBackupDeleteException", e.ToString());
            }

            return null;
        }

        private bool ShouldCompressDirect()
        {
            string backupRoot = Path.GetPathRoot(DestFolder.FullName);
            bool isAnyRootBackupRoot = Items.Any(i => Path.GetPathRoot(i.Folder.FullName) == backupRoot);

            return isAnyRootBackupRoot;
        }

        private string GetTmpFilePath(bool direct)
        {
            return direct ?
                Path.Combine(DestFolder.FullName, Path.GetRandomFileName() + ".zip") :
                Path.GetTempFileName();
        }

        private static int GetBufferSize(long fileSize)
        {
            long bufferSize = fileSize / 100;

            if (bufferSize > maxBufferSize) return maxBufferSize;
            if (bufferSize < minBufferSize) return minBufferSize;

            return (int)bufferSize;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
