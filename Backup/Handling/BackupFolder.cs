using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using FolderFile;

namespace BackupApp.Backup.Handling
{
    public class BackupFolder : INotifyPropertyChanged
    {
        private BackupFolder[] folders;
        private BackupFile[] files;

        public long ParentId { get; set; }

        public string Name { get; }

        public BackupFolder[] Folders
        {
            get => folders;
            set
            {
                if (value == folders) return;

                folders = value;
                OnPropertyChanged(nameof(Folders));
            }
        }

        public BackupFile[] Files
        {
            get => files;
            set
            {
                if (value == files) return;

                files = value;
                OnPropertyChanged(nameof(Files));
            }
        }

        public BackupFolder(string name, BackupFolder[] folders, BackupFile[] files)
        {
            Name = name;
            Folders = folders;
            Files = files;
        }

        public static BackupFolder FromPath(Folder folder, string name)
        {
            return FromPath(folder.GetDirectory(), folder.SubType, name);
        }

        public static BackupFolder FromPath(DirectoryInfo dir, SubfolderType subType, string name = null)
        {
            BackupFolder[] folders;
            BackupFile[] files;

            try
            {
                switch (subType)
                {
                    case SubfolderType.No:
                        folders = new BackupFolder[0];
                        files = new BackupFile[0];
                        break;

                    case SubfolderType.This:
                        folders = new BackupFolder[0];
                        files = dir.GetFiles().Select(BackupFile.FromPath).ToArray();
                        break;

                    case SubfolderType.All:
                        folders = dir.GetDirectories().Select(d => FromPath(d, SubfolderType.All)).ToArray();
                        files = dir.GetFiles().Select(BackupFile.FromPath).ToArray();
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(subType), subType, null);
                }
            }
            catch
            {
                folders = new BackupFolder[0];
                files = new BackupFile[0];
            }

            return new BackupFolder(name ?? dir.Name, folders, files);
        }

        public override string ToString()
        {
            return Name;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
