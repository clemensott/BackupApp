using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BackupApp
{
    public class RestoreTask : INotifyPropertyChanged
    {
        private bool isRestoring;
        private int totalCount, currentCount;
        private double progress;
        private readonly Queue<string> errorQueue;
        private readonly SemaphoreSlim errorsSem;

        public string DestFolder { get; }

        public IBackupNode Node { get; }

        public CancelToken CancelToken { get; }

        public Task Task { get; }

        public ObservableCollection<string> Errors { get; }

        public bool IsRestoring
        {
            get => isRestoring;
            private set
            {
                if (value == isRestoring) return;

                isRestoring = value;
                OnPropertyChanged(nameof(IsRestoring));
            }
        }

        public double Progress
        {
            get => progress;
            private set
            {
                if (Math.Abs(value - progress) < 0.01) return;

                progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }

        public string CurrentFile { get; private set; }

        private RestoreTask(string destFolder, IBackupNode node, CancelToken cancelToken)
        {
            errorQueue = new Queue<string>();
            errorsSem = new SemaphoreSlim(0);
            DestFolder = destFolder;
            Node = node;
            CancelToken = cancelToken ?? new CancelToken();
            Errors = new ObservableCollection<string>();
            IsRestoring = true;
            Task = Task.Run(new Action(Restore));

            UpdateCurrentFile();
            AddErrorHandler();
        }

        private async void UpdateCurrentFile()
        {
            string lastCurrentFile = CurrentFile;

            while (IsRestoring)
            {
                await Task.Delay(200);

                if (lastCurrentFile != CurrentFile) OnPropertyChanged(nameof(CurrentFile));
            }
        }

        public static RestoreTask Run(string destFolder, IBackupNode node, CancelToken cancelToken = null)
        {
            return new RestoreTask(destFolder, node, cancelToken);
        }

        private void Restore()
        {
            string srcDirPath = Path.Combine(DestFolder, BackupUtils.BackupFilesDirName);

            totalCount = BackupUtils.GetFiles(Node).Count();
            Restore(Node, DestFolder, srcDirPath);

            IsRestoring = false;
        }

        private void Restore(IBackupNode node, string currentPath, string srcDirPath)
        {
            currentPath = Path.Combine(currentPath, node.Name);

            if (!Directory.Exists(currentPath))
            {
                try
                {
                    Directory.CreateDirectory(currentPath);
                }
                catch (Exception e)
                {
                    AddError("Create directory: " + currentPath + "\r\n" + e);
                    IncreaseCurrentCount(BackupUtils.GetFiles(node).Count());
                    return;
                }
            }

            foreach (BackupFile file in node.Files)
            {
                if (CancelToken.IsCanceled) return;

                string srcFilePath = Path.Combine(srcDirPath, file.SourcePath);
                string destFilePath = Path.Combine(currentPath, file.Name);

                try
                {
                    CurrentFile = destFilePath;

                    if (File.Exists(destFilePath)) File.Delete(destFilePath);

                    File.Copy(srcFilePath, destFilePath);
                }
                catch (Exception e)
                {
                    AddError(destFilePath + "\r\n" + e);
                }

                IncreaseCurrentCount();
            }

            foreach (BackupFolder subFolder in node.Folders)
            {
                if (CancelToken.IsCanceled) return;

                Restore(subFolder, currentPath, srcDirPath);
            }
        }

        private void IncreaseCurrentCount(int increase = 1)
        {
            currentCount += increase;
            Progress = Math.Round(currentCount / (double)totalCount, 2);
        }

        private void AddError(string error)
        {
            errorQueue.Enqueue(error);
            errorsSem.Release();
        }

        private async void AddErrorHandler()
        {
            while (IsRestoring)
            {
                await errorsSem.WaitAsync();

                Errors.Insert(0, errorQueue.Dequeue());
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
