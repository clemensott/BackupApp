using FolderFile;
using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace BackupApp
{
    public class TaskBackupItem : INotifyPropertyChanged
    {
        private bool finished;
        private int filesCount, compressCount;

        public bool Finished
        {
            get { return finished; }
            set
            {
                if (value == finished) return;

                finished = value;
                OnPropertyChanged(nameof(Finished));
            }
        }

        public double Progress => compressCount / (double)filesCount;

        public string Name { get; private set; }

        public Folder Folder { get; private set; }

        public TaskBackupItem(string name, Folder folder)
        {
            Name = name;
            Folder = folder;
        }

        public Task BeginBackup()
        {
            compressCount = 0;
            filesCount = int.MaxValue;

            OnPropertyChanged(nameof(Progress));

            return Task.Run(new Action(LoadFilesCount));
        }

        private void LoadFilesCount()
        {
            Folder.Refresh();

            filesCount = Folder.Files.Length;
        }

        public bool Backup(ZipArchive archive, BackupCancelToken cancelToken)
        {
            if (!Directory.Exists(Folder.FullName)) return false;

            bool hasEnties = false;

            try
            {
                int removeLength = Folder.Directory.FullName.TrimEnd('\\').Length;

                foreach (FileInfo file in Folder.Files)
                {
                    if (cancelToken.IsCanceled) break;

                    if (!IsHidden(file))
                    {
                        try
                        {
                            string name = (Name + file.FullName.Substring(removeLength)).Replace('\\', '/');

                            archive.CreateEntryFromFile(file.FullName, name, CompressionLevel.Optimal);

                            hasEnties = true;
                        }
                        catch { }
                    }

                    IncreaseCompressCount();
                }

                OnPropertyChanged(nameof(Progress));
            }
            catch (Exception e)
            {
                int threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
                DebugEvent.SaveText("BackupItemBackupException", "ThreadID: " + threadID, e.Message.Replace('\n', ' '));
            }

            return hasEnties;
        }

        private string GetRelativePath(FileInfo file, DirectoryInfo dir)
        {
            return file.FullName.Substring(dir.FullName.Length);
        }

        private void AddEntries(ZipArchive archive, string directoryPath, DirectoryInfo directory, BackupCancelToken cancelToken)
        {
            if (IsHidden(directory)) return;

            foreach (FileInfo file in Folder.Files)
            {
                if (cancelToken.IsCanceled) return;

                if (!IsHidden(file))
                {
                    try
                    {
                        string name = directoryPath + "/" + file.Name;

                        archive.CreateEntryFromFile(file.FullName, name, CompressionLevel.Optimal);
                    }
                    catch { }
                }

                IncreaseCompressCount();
            }

            //foreach (DirectoryInfo subDir in directory.GetDirectories())
            //{
            //    AddEntries(archive, directoryPath + "/" + subDir.Name, subDir, cancelToken);
            //}
        }

        private bool IsHidden(FileSystemInfo info)
        {
            return ((int)info.Attributes & (int)FileAttributes.Hidden) > 0;
        }

        private void IncreaseCompressCount()
        {
            int progressBefore = Convert.ToInt32(Progress * 100);

            compressCount++;

            int progressAfter = Convert.ToInt32(Progress * 100);

            if (progressBefore == progressAfter) return;

            OnPropertyChanged(nameof(Progress));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
