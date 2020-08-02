using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace BackupApp.Restore.Caching
{
    public abstract class CachingTask : INotifyPropertyChanged
    {
        private const double progessMinChange = 0.01;

        private int currentFileIndex, totalFileCount;
        private double currentFileLineProgess;
        private string currentFileName;

        public int CurrentFileIndex
        {
            get => currentFileIndex;
            protected set
            {
                if (value == currentFileIndex) return;

                currentFileIndex = value;
                OnPropertyChanged(nameof(CurrentFileIndex));
            }
        }

        public int TotalFilesCount
        {
            get => totalFileCount;
            protected set
            {
                if (value == totalFileCount) return;

                totalFileCount = value;
                OnPropertyChanged(nameof(TotalFilesCount));
            }
        }

        public double CurrentFileLineProgess
        {
            get => currentFileLineProgess;
            protected set
            {
                if (Math.Abs(value - currentFileLineProgess) < progessMinChange) return;

                currentFileLineProgess = value;
                OnPropertyChanged(nameof(CurrentFileLineProgess));
            }
        }

        public string CurrentFileName
        {
            get => currentFileName;
            protected set
            {
                if (value == currentFileName) return;

                currentFileName = value;
                OnPropertyChanged(nameof(CurrentFileName));
            }
        }

        public RestoreDb DB { get; }

        public Task Task { get; protected set; }

        protected CachingTask( RestoreDb db)
        {
            CurrentFileIndex = 0;
            DB = db;
        }

        protected abstract Task Run();

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
