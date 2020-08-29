using BackupApp.Backup.Config;
using BackupApp.Backup.Result;
using BackupApp.Helper;
using BackupApp.Restore;
using StdOttStandard.Linq;
using StdOttStandard.Linq.Sort;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace BackupApp.Backup.Handling
{
    public class BackupTask : INotifyPropertyChanged
    {
        private bool isLoadingBackupedFiles, isFinishing;
        private TimeSpan? duration;
        private BackupTaskResult? result;
        private Exception failedException;
        private BackupWriteDb dB;
        private Task<BackupTaskResult> task;
        private readonly IList<string> addedFiles;

        public bool IsFinishing
        {
            get { return isFinishing; }
            private set
            {
                if (value == isFinishing) return;

                isFinishing = value;
                OnPropertyChanged(nameof(IsFinishing));
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

        public TimeSpan? Duration
        {
            get => duration;
            private set
            {
                if (value == duration) return;

                duration = value;
                OnPropertyChanged(nameof(Duration));
            }
        }

        public BackupTaskResult? Result
        {
            get => result;
            private set
            {
                if (value == result) return;

                result = value;
                OnPropertyChanged(nameof(Result));
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

        public TaskBackupItem[] Items { get; }

        public CancelToken CancelToken { get; }

        public DateTime Started { get; private set; }

        private BackupTask(string backupFolderPath, IEnumerable<BackupItem> backupItems)
        {
            CancelToken = new CancelToken();
            DestFolderPath = backupFolderPath;
            FilesBackupDir = BackupUtils.GetBackupedFilesFolderPath(DestFolderPath);
            addedFiles = new List<string>();
            Items = backupItems.Select(item => ToTaskItem(item, FilesBackupDir, addedFiles, CancelToken)).ToArray();
        }

        public static BackupTask Run(string backupFolderPath, IEnumerable<BackupItem> backupItems)
        {
            BackupTask task = new BackupTask(backupFolderPath, backupItems);
            task.task = task.Run();

            return task;
        }

        private static TaskBackupItem ToTaskItem(BackupItem item, string filesDestFolderPath,
            IList<string> addedFiles, CancelToken cancelToken)
        {
            PathPattern[] patterns = item.ExcludePatterns.ToNotNull().Select(p => new PathPattern(p)).ToArray();
            return new TaskBackupItem(item.Name, filesDestFolderPath,
                item.Folder.Clone(), patterns, addedFiles, cancelToken);
        }

        private async Task<BackupTaskResult> Run()
        {
            BackupTaskResult result = BackupTaskResult.Exception;
            Started = DateTime.Now;
            DebugEvent.SaveText("Backup");

            try
            {
                return result = await Backup();
            }
            catch (Exception e)
            {
                result = BackupTaskResult.Exception;
                FailedException = e;
                throw;
            }
            finally
            {
                Duration = DateTime.Now - Started;
                Result = result;
            }
        }

        private async Task<BackupTaskResult> Backup()
        {
            if (!Directory.Exists(DestFolderPath)) return BackupTaskResult.DestinationFolderNotFound;
            if (Items == null || Items.Length == 0) return BackupTaskResult.NoItemsToBackup;

            BackupTaskResult result;
            string destDbPath = null;
            DebugEvent.SaveText("HandleBackup");

            try
            {
                IsLoadingBackupedFiles = true;
                Task<BackupedFiles> allFilesTask =
                    Task.Run(() => BackupUtils.GetBackupedFiles(DestFolderPath, CancelToken));

                DB = BackupUtils.CreateDb(Path.GetTempPath(), Started);

                await Task.Run(() =>
                {
                    foreach (TaskBackupItem item in Items)
                    {
                        if (CancelToken.IsCanceled) return;
                        item.BeginBackup(DB);
                    }
                });

                BackupedFiles backupedFiles = await allFilesTask;
                IsLoadingBackupedFiles = false;

                if (!Directory.Exists(FilesBackupDir)) Directory.CreateDirectory(FilesBackupDir);

                await Task.Run(() =>
                {
                    foreach (TaskBackupItem item in Items)
                    {
                        if (CancelToken.IsCanceled) return;
                        item.Backup(backupedFiles);
                    }
                });

                if (!CancelToken.IsCanceled)
                {
                    IsFinishing = true;
                    DB.Commit();

                    if (!CancelToken.IsCanceled)
                    {
                        destDbPath = Path.Combine(DestFolderPath, Path.GetFileName(DB.Path));
                        backupedFiles.AddDbName(Path.GetFileName(destDbPath));

                        File.Copy(DB.Path, destDbPath);
                        DeleteFile(DB.Path);

                        bool isValid = await Task.Run(() => ValidateDB(destDbPath));
                        if (isValid)
                        {
                            BackupUtils.SaveLocalBackupedFilesCache(backupedFiles);
                            DebugEvent.SaveText("HandleBackupSuccessful");
                            return BackupTaskResult.Successful;
                        }
                        else if (!CancelToken.IsCanceled) result = BackupTaskResult.ValidationError;
                        else result = BackupTaskResult.Canceled;
                    }
                    else result = BackupTaskResult.Canceled;
                }
                else result = BackupTaskResult.Canceled;

                DB.Dispose();
                DeleteFile(DB.Path);
                if (destDbPath != null) DeleteFile(destDbPath);
                await Task.Run(() => DeleteAddedFiles(addedFiles));

                return result;
            }
            catch (Exception e)
            {
                DebugEvent.SaveText("CreateBackupException", e.ToString());
                CancelToken.Cancel();
                DB?.Dispose();
                await Task.Run(() => DeleteAddedFiles(addedFiles));
                if (DB != null) DeleteFile(DB.Path);

                try
                {
                    if (destDbPath != null && File.Exists(destDbPath)) File.Delete(destDbPath);
                }
                catch (Exception e2)
                {
                    DebugEvent.SaveText("CreateBackupDeleteDestDbException", e2.ToString());
                }

                FailedException = e;
                return BackupTaskResult.Exception;
            }
            finally
            {
                IsLoadingBackupedFiles = false;
                IsFinishing = false;

                foreach (TaskBackupItem item in Items)
                {
                    item.Clean();
                }

                // To reduce memory usage
                DB = null;
                addedFiles.Clear();
            }
        }

        private async Task<bool> ValidateDB(string dbPath)
        {
            using (BackupReadDb readDb = new BackupReadDb(dbPath))
            {
                IList<DbFolder> dbFolders = await readDb.GetAllFolders(CancelToken);
                IList<DbFolder> addedFolders = SortUtils.InsertionSort(Items.SelectMany(i => i.GetAddedFolders()), f => f.ID);

                if (dbFolders.Count != addedFolders.Count) return false;
                for (int i = 0; i < dbFolders.Count; i++)
                {
                    if (!dbFolders[i].Equals(addedFolders[i])) return false;
                }

                if (CancelToken.IsCanceled) return false;

                IList<DbFile> dbFiles = await readDb.GetAllFiles(CancelToken);
                IList<DbFile> addedFiles = SortUtils.InsertionSort(Items.SelectMany(i => i.GetAddedFiles()), f => f.ID);

                if (dbFiles.Count != addedFiles.Count) return false;
                for (int i = 0; i < dbFiles.Count; i++)
                {
                    if (!dbFiles[i].Equals(addedFiles[i])) return false;
                }

                if (CancelToken.IsCanceled) return false;

                int addedFoldersFilesCount = 0;
                ILookup<long, DbFolderFile> dbFoldersFiles =
                    (await readDb.GetAllFoldersFiles(CancelToken)).ToLookup(ff => ff.FolderID);
                IEnumerable<IGrouping<long, DbFolderFile>> addedFoldersFiles =
                    Items.SelectMany(i => i.GetAddedFoldersFiles().GroupBy(ff => ff.FolderID));

                foreach (IGrouping<long, DbFolderFile> addedGroup in addedFoldersFiles)
                {
                    if (!dbFoldersFiles.Contains(addedGroup.Key)) return false;

                    int addedGroupItemsCount = 0;
                    ILookup<long, DbFolderFile> dbLoopup = dbFoldersFiles[addedGroup.Key].ToLookup(ff => ff.FileID);

                    foreach (DbFolderFile addedFolderFile in addedGroup)
                    {
                        if (!dbLoopup.Contains(addedFolderFile.FileID) ||
                            !dbLoopup[addedFolderFile.FileID].Contains(addedFolderFile)) return false;

                        addedGroupItemsCount++;
                    }

                    if (addedGroupItemsCount != dbFoldersFiles[addedGroup.Key].Count()) return false;

                    addedFoldersFilesCount++;
                }

                return addedFoldersFilesCount == dbFoldersFiles.Count;
            }
        }

        // When SQLite database connection is not disposed proper
        // than it takes time until it gets actually disposed and can be deleted
        private static async void DeleteFile(string path)
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

        private static void DeleteAddedFiles(IEnumerable<string> filePaths)
        {
            foreach (string addedFile in filePaths)
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

        public TaskAwaiter<BackupTaskResult> GetAwaiter()
        {
            return task.GetAwaiter();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
