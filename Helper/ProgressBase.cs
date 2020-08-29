using System;
using System.ComponentModel;

namespace BackupApp.Helper
{
    public class ProgressBase : INotifyPropertyChanged
    {
        private int totalCount, currentCount;
        private double progress, minProgressChange;
        private readonly object lockObj;

        public double Progress
        {
            get => progress;
            private set
            {
                if (Math.Abs(value - progress) < minProgressChange) return;

                progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }

        protected double MinProgressChange
        {
            get => minProgressChange;
            set
            {
                if (value == minProgressChange) return;

                minProgressChange = value;
                IncreaseProgress(0);
            }
        }

        public ProgressBase()
        {
            minProgressChange = 0.01;
            lockObj = new object();
        }

        protected void IncreaseProgress(int by = 1)
        {
            currentCount += by;
            Progress = totalCount > 0 ? currentCount / (double)totalCount : 0;
        }

        protected void IncreaseProgressLocked(int by = 1)
        {
            lock (lockObj) IncreaseProgress(by);
        }

        protected void SetProgress(double value)
        {
            progress = value;
            OnPropertyChanged(nameof(Progress));
        }

        protected void SetMinProgressChange(double value)
        {
            minProgressChange = value;
            IncreaseProgress(0);
        }

        protected void Restart(int totalCount)
        {
            this.totalCount = totalCount;
            currentCount = 0;

            SetProgress(0);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
