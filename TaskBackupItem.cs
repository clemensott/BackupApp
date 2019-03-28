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

            return LoadFilesCount();
        }

        private async Task LoadFilesCount()
        {
            await Folder.RefreshAsync();

            filesCount = Folder.Files.Length;
        }

        public bool Backup(ZipArchive archive, BackupCancelToken cancelToken)
        {
            if (!Directory.Exists(Folder.FullName)) return false;

            bool hasEntries = false;

            try
            {
                int removeLength = Folder.FullName.TrimEnd('\\').Length;

                foreach (FileInfo file in Folder.Files)
                {
                    if (cancelToken.IsCanceled) break;

                    if (!IsHidden(file))
                    {
                        try
                        {
                            string name = (Name + file.FullName.Substring(removeLength)).Replace('\\', '/');

                            archive.CreateEntryFromFile(file.FullName, name, CompressionLevel.Optimal);

                            hasEntries = true;
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
                DebugEvent.SaveText("BackupItemBackupException", "ThreadID: " + threadID,
                    e.Message.Replace('\n', ' '));
            }

            return hasEntries;
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
