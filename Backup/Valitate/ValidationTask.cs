using BackupApp.Backup.Result;
using BackupApp.Helper;
using BackupApp.Restore;
using StdOttStandard.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace BackupApp.Backup.Valitate
{
    public class ValidationTask : ProgressBase
    {
        private bool isCompleted;
        private int? unusedFilesCount, deletedFilesCount;
        private ValidationState state;
        private Exception failedException;
        private Task task;
        private IEnumerable<ErrorFile> errorFiles;
        private readonly object lockObj = new object();

        public bool IsCompleted
        {
            get => isCompleted;
            set
            {
                if (value == isCompleted) return;

                isCompleted = value;
                OnPropertyChanged(nameof(IsCompleted));
            }
        }

        public ValidationState State
        {
            get => state;
            private set
            {
                if (value == state) return;

                state = value;
                OnPropertyChanged(nameof(State));
            }
        }

        public int? UnusedFilesCount
        {
            get => unusedFilesCount;
            private set
            {
                if (value == unusedFilesCount) return;

                unusedFilesCount = value;
                OnPropertyChanged(nameof(UnusedFilesCount));
            }
        }

        public int? DeletedFilesCount
        {
            get => deletedFilesCount;
            private set
            {
                if (value == deletedFilesCount) return;

                deletedFilesCount = value;
                OnPropertyChanged(nameof(DeletedFilesCount));
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

        public IEnumerable<ErrorFile> ErrorFiles
        {
            get => errorFiles;
            private set
            {
                if (value == errorFiles) return;

                errorFiles = value;
                OnPropertyChanged(nameof(ErrorFiles));
            }
        }

        public string BackupFolderPath { get; }

        public CancelToken CancelToken { get; }

        private ValidationTask(string backupFolderPath)
        {
            State = ValidationState.WaitForStart;
            CancelToken = new CancelToken();
            BackupFolderPath = backupFolderPath;
        }

        public static ValidationTask Run(string backupFolderPath)
        {
            ValidationTask task = new ValidationTask(backupFolderPath);
            task.task = Task.Run(task.Validate);

            return task;
        }

        private async Task Validate()
        {
            State = ValidationState.Starting;
            try
            {

                string backupFilesFolderPath = BackupUtils.GetBackupedFilesFolderPath(BackupFolderPath);
                IDictionary<string, BackupedFile> backupedFiles = Directory.GetFiles(backupFilesFolderPath)
                    .ToDictionary(p => p, BackupedFile.FromExistingFile);

                BackupReadDb[] dbs = BackupUtils.GetReadDBs(BackupFolderPath).ToNotNull().ToArray();
                Restart(dbs.Length);
                State = ValidationState.ReadingDBs;

                foreach (BackupReadDb db in dbs)
                {
                    if (CancelToken.IsCanceled) return;

                    IList<DbFile> dbFiles = await db.GetAllFiles(CancelToken);

                    if (CancelToken.IsCanceled) return;
                    foreach (DbFile dbFile in dbFiles)
                    {
                        BackupedFile backupedFile;
                        string filePath = Path.Combine(backupFilesFolderPath, dbFile.FileName);

                        if (!backupedFiles.TryGetValue(filePath, out backupedFile))
                        {
                            backupedFile = BackupedFile.FromNotExistingFile(filePath);
                            backupedFiles.Add(filePath, backupedFile);
                        }

                        backupedFile.DbHashes.Add(dbFile.Hash, db);
                    }

                    IncreaseProgress();
                }

                State = ValidationState.LoadingHashes;
                BackupedFile[] usedBackupedFiles = backupedFiles.Values.Where(bf => bf.DbHashes.Count > 0).ToArray();
                Restart(usedBackupedFiles.Length);

                Parallel.ForEach(usedBackupedFiles, bf =>
                {
                    if (CancelToken.IsCanceled) return;

                    try
                    {
                        bf.Hash = BackupUtils.GetHash(bf.Path);
                    }
                    catch { }
                    finally
                    {
                        IncreaseProgressLocked();
                    }
                });

                State = ValidationState.SearchingErrorFiles;
                ErrorFiles = backupedFiles.Values.Where(IsErrorFile).Select(ErrorFile.Create).ToArray();

                State = ValidationState.DeletingUnusedFiles;

                string[] unusedFiles = backupedFiles.Values
                    .Where(bf => string.IsNullOrWhiteSpace(bf.Hash))
                    .Select(bf => bf.Path).ToArray();

                UnusedFilesCount = unusedFiles.Length;
                Restart(unusedFiles.Length);

                int deletedFilesCount = 0;
                foreach (string filePath in unusedFiles)
                {
                    try
                    {
                        File.Delete(filePath);
                        deletedFilesCount++;
                    }
                    catch { }
                    finally
                    {
                        IncreaseProgress();
                    }
                }

                DeletedFilesCount = deletedFilesCount;
                State = ValidationState.Finished;
            }
            catch (Exception e)
            {
                FailedException = e;
            }
            finally
            {
                if (CancelToken.IsCanceled) State = ValidationState.Canceled;
                IsCompleted = true;
            }
        }

        private static bool IsErrorFile(BackupedFile backupedFile)
        {
            return !backupedFile.Exists ||
                backupedFile.DbHashes.Count > 1 ||
                (
                    backupedFile.DbHashes.Count > 0 &&
                    backupedFile.Hash != backupedFile.DbHashes.Keys.First()
                );
        }

        public TaskAwaiter GetAwaiter()
        {
            return task.GetAwaiter();
        }
    }
}
