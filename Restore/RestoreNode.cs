using System.ComponentModel;

namespace BackupApp.Restore
{
    public enum RestoreNodeType { Backup, Folder }

    public class RestoreNode : INotifyPropertyChanged
    {
        private RestoreNode[] folders;
        private RestoreFile[] files;

        public long ID { get; }

        public string Name { get; }

        public RestoreNodeType Type { get; }

        public RestoreNode[] Folders
        {
            get => folders;
            set
            {
                if (value == folders) return;

                folders = value;
                OnPropertyChanged(nameof(Folders));
            }
        }

        public RestoreFile[] Files
        {
            get => files;
            set
            {
                if (value == files) return;

                files = value;
                OnPropertyChanged(nameof(Files));
            }
        }

        public RestoreNode(long id, string name, RestoreNodeType type)
        {
            ID = id;
            Name = name;
            Type = type;
        }

        public static RestoreNode CreateBackup(long id, string name)
        {
            return new RestoreNode(id, name, RestoreNodeType.Backup);
        }

        public static RestoreNode CreateFolder(long id, string name)
        {
            return new RestoreNode(id, name, RestoreNodeType.Folder);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
