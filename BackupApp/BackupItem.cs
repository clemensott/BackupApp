using FolderFile;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Xml.Serialization;

namespace BackupApp
{
    public class BackupItem : INotifyPropertyChanged
    {
        private bool backuped;
        private int compressCount, filesCount;
        private string name;
        private Folder folder;

        public double Progress
        {
            get
            {
                if (backuped) return 1;
                if (filesCount > 0) return compressCount / Convert.ToDouble(filesCount);

                return 0;
            }
        }

        public string ProgressText { get { return string.Format("{0} %", Math.Round(Progress * 100)); } }

        public string Name
        {
            get { return name; }
            set
            {
                if (value == name) return;
                foreach (char c in Path.GetInvalidFileNameChars()) if (value.Contains(c)) return;

                name = value;

                OnPropertyChanged("Name");

                ViewModel.SaveData();
            }
        }

        public string FolderPath
        {
            get { return folder.FullPath; }
            set
            {
                if (value == FolderPath) return;

                Folder = new Folder(value, SubfolderType.No);
            }
        }

        [XmlIgnore]
        public Folder Folder
        {
            get { return folder; }
            set
            {
                if (value == folder) return;

                folder = value;

                OnPropertyChanged("Folder");

                if (Name == "") Name = Folder.Info.Name;

                ViewModel.SaveData();
            }
        }

        public BackupItem() : this("")
        {

        }

        public BackupItem(string name)
        {
            this.name = name;

            backuped = false;
            folder = new Folder("", SubfolderType.All);
        }

        public void BeginBackup()
        {
            backuped = false;
            compressCount = 0;
            filesCount = int.MaxValue;

            OnPropertyChanged("Progress");
            OnPropertyChanged("ProgressText");

            new Task(new Action(LoadFilesCount)).Start();
        }

        private void LoadFilesCount()
        {
            Folder.RefreshFolderAndFiles(SubfolderType.All);

            filesCount = Folder.GetFiles(true).Length;
        }

        public bool Backup(ZipArchive archive)
        {
            if (!Directory.Exists(Folder.Info.FullName)) return false;

            try
            {
                AddEntries(archive, Name, Folder.Info);

                backuped = true;

                OnPropertyChanged("Progress");
                OnPropertyChanged("ProgressText");
            }
            catch (Exception e)
            {
                int threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
                DebugEvent.SaveText("BackupItemBackupException", "ThreadID: " + threadID, e.Message.Replace('\n', ' '));
            }

            return true;
        }

        private void AddEntries(ZipArchive archive, string directoryPath, DirectoryInfo directory)
        {
            if (IsHidden(directory)) return;

            foreach (FileInfo file in directory.GetFiles())
            {
                if (!BackupManager.Current.IsBackuping) return;

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

            foreach (DirectoryInfo subDir in directory.GetDirectories())
            {
                AddEntries(archive, directoryPath + "/" + subDir.Name, subDir);
            }
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

            OnPropertyChanged("Progress");
            OnPropertyChanged("ProgressText");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged == null) return;

            PropertyChanged(this, new PropertyChangedEventArgs(name));
        }

        public override string ToString()
        {
            return string.Format("{0} | {1}", Name, FolderPath);
        }
    }
}
