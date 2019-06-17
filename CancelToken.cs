using System.ComponentModel;

namespace BackupApp
{
    public class CancelToken : INotifyPropertyChanged
    {
        private bool isCanceled;

        public bool IsCanceled
        {
            get => isCanceled;
            private set
            {
                if (value == isCanceled) return;

                isCanceled = value;
                OnPropertyChanged(nameof(IsCanceled));
            }
        }

        public void Cancel() => IsCanceled = true;

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
