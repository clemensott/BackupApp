using System.ComponentModel;
using System.Threading.Tasks;

namespace BackupApp.Restore
{
    public class BackupFolder : INotifyPropertyChanged
    {
        private BackupFolder[] folders;
        private BackupFile[] files;

        public long? ID { get; }

        public string Name { get; }

        internal BackupReadDb DB { get; }

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

        internal BackupFolder(long? id, string name, BackupReadDb db)
        {
            ID = id;
            Name = name;
            DB = db;
        }

        public async Task LoadFolders()
        {
            if (Folders == null) Folders = (await DB.GetFolders(ID))?.ToArray();
        }

        public async Task LoadFiles()
        {
            if (Files == null) Files = (await DB.GetFiles(ID))?.ToArray();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
