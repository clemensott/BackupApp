using FolderFile;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace BackupApp
{
    public class BackupItem : INotifyPropertyChanged
    {
        private string name;
        private Folder folder;

        public string Name
        {
            get { return name; }
            set
            {
                if (value == name) return;
                foreach (char c in Path.GetInvalidFileNameChars()) if (value.Contains(c)) return;

                name = value;

                OnPropertyChanged("Name");
            }
        }

        public SerializableFolder? SerialFolder
        {
            get { return (SerializableFolder?)folder; }
            set { Folder = Folder.CreateOrDefault(value); }
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

                if (Name == "" && Folder != null) Name = Folder.Name;
            }
        }

        public BackupItem() : this("")
        {
        }

        public BackupItem(string name)
        {
            this.name = name;

            folder = null;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public override string ToString()
        {
            return string.Format("{0} | {1}", Name, Folder?.OriginalPath);
        }
    }
}
