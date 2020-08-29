using System;
using FolderFile;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using BackupApp.Backup.Result;
using BackupApp.Helper;

namespace BackupApp.Backup.Handling
{
    public class TaskBackupItem : ProgressBase
    {
        private bool finished;
        private readonly List<DbFolder> addedToDbFolders;
        private readonly List<DbFile> addedToDbFiles;
        private readonly List<DbFolderFile> addedToDbFoldersFiles;
        private readonly List<BackupFile> allFiles;
        private readonly IList<string> addedFiles;
        private readonly CancelToken cancelToken;
        private BackupWriteDb db;

        public bool Finished
        {
            get { return finished; }
            private set
            {
                if (value == finished) return;

                finished = value;
                OnPropertyChanged(nameof(Finished));
            }
        }
        
        public string Name { get; }

        public string FilesDestFolderPath { get; }

        public Folder Folder { get; }

        public PathPattern[] ExcludePatterns { get; }

        public TaskBackupItem(string name, string filesDestFolderPath, Folder folder,
            PathPattern[] excludePatterns, IList<string> addedFiles, CancelToken cancelToken)
        {
            addedToDbFolders = new List<DbFolder>();
            addedToDbFiles = new List<DbFile>();
            addedToDbFoldersFiles = new List<DbFolderFile>();
            allFiles = new List<BackupFile>();

            Name = name;
            FilesDestFolderPath = filesDestFolderPath;
            Folder = folder;
            ExcludePatterns = excludePatterns;
            this.addedFiles = addedFiles;
            this.cancelToken = cancelToken;
        }

        public void BeginBackup(BackupWriteDb db)
        {
            this.db = db;
            LoadFiles();
        }

        public void LoadFiles()
        {
            Queue<BackupFolder> folderQueue = new Queue<BackupFolder>();
            folderQueue.Enqueue(new BackupFolder(Name, Folder));

            while (folderQueue.Count > 0)
            {
                BackupFolder currentFolder = folderQueue.Dequeue();
                if (IsHidden(currentFolder.Directory) ||
                    ExcludePath(currentFolder.Directory.FullName + "\\")) continue;

                DbFolder dbFolder = db.AddFolder(currentFolder.Name, currentFolder.ParentId);
                addedToDbFolders.Add(dbFolder);

                IEnumerable<BackupFolder> folders;
                IEnumerable<BackupFile> files;
                try
                {
                    switch (currentFolder.SubType)
                    {
                        case SubfolderType.No:
                            folders = new BackupFolder[0];
                            files = new BackupFile[0];
                            break;

                        case SubfolderType.This:
                            folders = new BackupFolder[0];
                            files = currentFolder.Directory.GetFiles().Select(f => new BackupFile(dbFolder.ID, f));
                            break;

                        case SubfolderType.All:
                            folders = currentFolder.Directory.GetDirectories()
                                .Select(d => new BackupFolder(dbFolder.ID, d, SubfolderType.All));
                            files = currentFolder.Directory.GetFiles().Select(f => new BackupFile(dbFolder.ID, f));
                            break;

                        default:
                            throw new ArgumentOutOfRangeException(nameof(currentFolder.SubType), currentFolder.SubType, null);
                    }
                }
                catch
                {
                    folders = new BackupFolder[0];
                    files = new BackupFile[0];
                }

                foreach (BackupFolder subFolder in folders)
                {
                    folderQueue.Enqueue(subFolder);
                }

                foreach (BackupFile file in files)
                {
                    allFiles.Add(file);
                }
            }
        }

        public void Backup(BackupedFiles backupedFiles)
        {
            try
            {
                Restart(allFiles.Count);
                Parallel.ForEach(allFiles, f => DoBackupFile(f, backupedFiles));
                SetProgress(1);
            }
            finally
            {
                Finished = true;
            }
        }

        private void DoBackupFile(BackupFile file, BackupedFiles backupedFiles)
        {
            try
            {
                if (cancelToken.IsCanceled ||
                    IsHidden(file.Info) ||
                    ExcludePath(file.Info.FullName)) return;

                string backupFileName;
                string fileHash = BackupUtils.GetHash(file.Info.FullName);

                if (cancelToken.IsCanceled) return;
                if (backupedFiles.Add(fileHash, file.Info.Extension, out backupFileName))
                {
                    string destPath = Path.Combine(FilesDestFolderPath, backupFileName);

                    do
                    {
                        File.Copy(file.Info.FullName, destPath, true);
                    } while (fileHash != BackupUtils.GetHash(file.Info.FullName));

                    addedFiles.Add(destPath);
                }

                lock (addedToDbFoldersFiles)
                {
                    (DbFile? dbFile, DbFolderFile folderFile) = db.AddFile(file.Info.Name, fileHash, backupFileName, file.FolderId);

                    if (dbFile.HasValue) addedToDbFiles.Add(dbFile.Value);
                    addedToDbFoldersFiles.Add(folderFile);
                }
            }
            catch { }
            finally
            {
                IncreaseProgressLocked();
            }
        }

        private bool ExcludePath(string path)
        {
            return ExcludePatterns.Any(p => p.Matches(path));
        }

        private static bool IsHidden(FileSystemInfo info)
        {
            return ((int)info.Attributes & (int)FileAttributes.Hidden) > 0;
        }

        public IEnumerable<DbFolder> GetAddedFolders()
        {
            return addedToDbFolders.AsReadOnly();
        }

        public IEnumerable<DbFile> GetAddedFiles()
        {
            return addedToDbFiles.AsReadOnly();
        }

        public IEnumerable<DbFolderFile> GetAddedFoldersFiles()
        {
            return addedToDbFoldersFiles.AsReadOnly();
        }

        // To reduce memory usage
        public void Clean()
        {
            allFiles.Clear();
            addedToDbFolders.Clear();
            addedToDbFiles.Clear();
            addedToDbFoldersFiles.Clear();
            db = null;
        }
    }
}
