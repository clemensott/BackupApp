using System;
using FolderFile;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Threading.Tasks;
using StdOttStandard;

namespace BackupApp
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

        public TaskBackupItem(string name, Folder folder)
        {
            Name = name;
            Folder = folder;
        }

        public Task BeginBackup()
        {
            return Task.Run(() =>
            {
                baseFolder = BackupFolder.FromPath(Folder);
                totalCount = baseFolder.SelectRecursive(f => f.Folders)
                    .SelectMany(f => f.Files).Count();
            });
        }

        public BackupFolder Backup(string dirPath, IDictionary<string, string> backupedFiles,
            CancelToken cancelToken, List<string> addedFiles)
        {
            currentCount = 0;

            try
            {
                return BackupDir(baseFolder, Name, DoBackupFiles, cancelToken);
            }
            finally
            {
                Finished = true;
            }

            BackupFile[] DoBackupFiles(IEnumerable<string> files)
            {
                return files.Select(f => new FileInfo(f))
                    .Where(f => !cancelToken.IsCanceled && !IsHidden(f))
                    .Select(DoBackupFile).OfType<BackupFile>().ToArray();
            }

            BackupFile? DoBackupFile(FileInfo file)
            {
                try
                {
                    string sourcePath, destPath;
                    string fileHash = GetHash(file.FullName);

                    if (backupedFiles.TryGetValue(fileHash, out sourcePath))
                    {
                        destPath = Path.Combine(dirPath, sourcePath);

                        if (!File.Exists(destPath))
                        {
                            File.Copy(file.FullName, destPath);
                        }

                        return new BackupFile(file.Name, sourcePath, fileHash);
                    }

                    sourcePath = fileHash + file.Extension;
                    destPath = Path.Combine(dirPath, sourcePath);

                    if (File.Exists(destPath)) File.Delete(destPath);

                    File.Copy(file.FullName, destPath);

                    backupedFiles.Add(fileHash, sourcePath);
                    addedFiles.Add(destPath);

                    return new BackupFile(file.Name, sourcePath, fileHash);
                }
                catch
                {
                    return null;
                }
                finally
                {
                    currentCount++;

                    Progress = totalCount > 0 ? Math.Round(currentCount / (double)totalCount, 2) : 0;
                }
            }
        }

        private static BackupFolder BackupDir(BackupFolder folder, string dirName,
            Func<IEnumerable<string>, BackupFile[]> doBackupFiles, CancelToken cancelToken)
        {
            if (cancelToken.IsCanceled) return null;

            BackupFile[] backupFiles = GetBackupFiles();
            BackupFolder[] backupFolders = GetBackupFolders();

            return new BackupFolder(dirName, backupFolders, backupFiles);

            BackupFile[] GetBackupFiles()
            {
                return doBackupFiles(folder.Files.Select(f => f.SourcePath));
            }

            BackupFolder[] GetBackupFolders()
            {
                return folder.Folders.Select(f => BackupDir(f, f.Name, doBackupFiles, cancelToken)).ToArray();
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
