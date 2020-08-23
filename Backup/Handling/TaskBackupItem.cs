using System;
using FolderFile;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Threading.Tasks;
using StdOttStandard.Linq;
using BackupApp.Backup.Result;

namespace BackupApp.Backup.Handling
{
    public class TaskBackupItem : INotifyPropertyChanged
    {
        private bool finished;
        private int totalCount, currentCount;
        private double progress;
        private BackupFolder baseFolder;

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

        public double Progress
        {
            get => progress;
            private set
            {
                if (Math.Abs(value - progress) < 0.01) return;

                progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }

        public string Name { get; }

        public Folder Folder { get; }

        public CancelToken CancelToken { get; }

        public TaskBackupItem(string name, Folder folder, CancelToken cancelToken)
        {
            Name = name;
            Folder = folder;
            CancelToken = cancelToken;
        }

        public Task BeginBackup()
        {
            return Task.Run(() =>
            {
                baseFolder = BackupFolder.FromPath(Folder, Name);
                totalCount = GenerateUtils.SelectRecursive(baseFolder, f => f.Folders)
                    .Select(f => f.Files.Length).Sum();
            });
        }

        public void Backup(string filesDestFolderPath, BackupWriteDb db,
            BackupedFiles backupedFiles, IList<string> addedFiles)
        {
            currentCount = 0;

            try
            {
                Queue<BackupFolder> folderQueue = new Queue<BackupFolder>();
                Queue<BackupFile> fileQueue = new Queue<BackupFile>();
                folderQueue.Enqueue(baseFolder);

                while (folderQueue.Count > 0)
                {
                    BackupFolder currentFolder = folderQueue.Dequeue();
                    long folderId = db.AddFolder(currentFolder.Name, currentFolder.ParentId);

                    foreach (BackupFolder subFolder in currentFolder.Folders)
                    {
                        subFolder.ParentId = folderId;
                        folderQueue.Enqueue(subFolder);
                    }

                    foreach (BackupFile file in currentFolder.Files)
                    {
                        fileQueue.Enqueue(file);
                    }
                }

                Parallel.ForEach(fileQueue,
                    f => DoBackupFile(f.File, f.FolderId, filesDestFolderPath, db, backupedFiles, addedFiles));
            }
            finally
            {
                Finished = true;
            }
        }

        private void DoBackupFile(FileInfo file, long folderId, string filesDestFolderPath,
            BackupWriteDb db, BackupedFiles backupedFiles, IList<string> addedFiles)
        {
            try
            {
                if (CancelToken.IsCanceled || IsHidden(file) || db.Disposed) return;

                string backupFileName;
                string fileHash = GetHash(file.FullName);

                if (CancelToken.IsCanceled || db.Disposed) return;

                if (backupedFiles.Add(fileHash, file.Extension, out backupFileName))
                {
                    string destPath = Path.Combine(filesDestFolderPath, backupFileName);

                    if (File.Exists(destPath)) File.Delete(destPath);
                    File.Copy(file.FullName, destPath);

                    addedFiles.Add(destPath);
                }

                db.AddFile(file.Name, fileHash, backupFileName, folderId);
            }
            catch { }
            finally
            {
                currentCount++;

                Progress = totalCount > 0 ? Math.Round(currentCount / (double)totalCount, 2) : 0;
            }
        }

        private static bool IsHidden(FileSystemInfo info)
        {
            return ((int)info.Attributes & (int)FileAttributes.Hidden) > 0;
        }

        private static string GetHash(string filePath)
        {
            using (MD5 md5 = MD5.Create())
            {
                using (FileStream stream = File.OpenRead(filePath))
                {
                    return Convert.ToBase64String(md5.ComputeHash(stream)).Replace('/', '_');
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
