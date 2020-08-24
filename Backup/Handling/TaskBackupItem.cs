using System;
using FolderFile;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Threading.Tasks;
using BackupApp.Backup.Result;

namespace BackupApp.Backup.Handling
{
    public class TaskBackupItem : INotifyPropertyChanged
    {
        private bool finished;
        private int currentCount;
        private double progress;
        private readonly List<BackupFile> allFiles;

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

        public string FilesDestFolderPath { get; }

        public Folder Folder { get; }

        public PathPattern[] ExcludePatterns { get; }

        public IList<string> AddedFiles { get; }

        public CancelToken CancelToken { get; }

        public BackupWriteDb DB { get; private set; }

        public TaskBackupItem(string name, string filesDestFolderPath, Folder folder,
            PathPattern[] excludePatterns, IList<string> addedFiles, CancelToken cancelToken)
        {
            allFiles = new List<BackupFile>();
            Name = name;
            FilesDestFolderPath = filesDestFolderPath;
            Folder = folder;
            ExcludePatterns = excludePatterns;
            AddedFiles = addedFiles;
            CancelToken = cancelToken;
        }

        public Task BeginBackup(BackupWriteDb db)
        {
            DB = db;
            return Task.Run((Action)LoadFiles);
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

                long folderId = DB.AddFolder(currentFolder.Name, currentFolder.ParentId);

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
                            files = currentFolder.Directory.GetFiles().Select(BackupFile.FromPath);
                            break;

                        case SubfolderType.All:
                            folders = currentFolder.Directory.GetDirectories()
                                .Select(d => new BackupFolder(folderId, d, SubfolderType.All));
                            files = currentFolder.Directory.GetFiles().Select(BackupFile.FromPath);
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
            currentCount = 0;

            try
            {
                Parallel.ForEach(allFiles,
                    f => DoBackupFile(f.File, f.FolderId, backupedFiles));

                allFiles.Clear();
            }
            finally
            {
                Finished = true;
            }
        }

        private void DoBackupFile(FileInfo file, long folderId, BackupedFiles backupedFiles)
        {
            try
            {
                if (CancelToken.IsCanceled ||
                    IsHidden(file) || ExcludePath(file.FullName)) return;

                string backupFileName;
                string fileHash = GetHash(file.FullName);

                if (CancelToken.IsCanceled) return;

                if (backupedFiles.Add(fileHash, file.Extension, out backupFileName))
                {
                    string destPath = Path.Combine(FilesDestFolderPath, backupFileName);

                    if (File.Exists(destPath)) File.Delete(destPath);
                    File.Copy(file.FullName, destPath);

                    AddedFiles.Add(destPath);
                }

                DB.AddFile(file.Name, fileHash, backupFileName, folderId);
            }
            catch { }
            finally
            {
                currentCount++;

                Progress = allFiles.Count > 0 ? Math.Round(currentCount / (double)allFiles.Count, 2) : 0;
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
