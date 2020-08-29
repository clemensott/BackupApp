using BackupApp.Helper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using StdOttStandard.Linq;
using StdOttStandard.Linq.DataStructures;

namespace BackupApp.Restore.Handling
{
    public class RestoreTask : ProgressBase
    {
        private bool isRestoring;
        private readonly AsyncQueue<string> errorQueue;

        public string DestFolder { get; }

        public BackupFolder Node { get; }

        public CancelToken CancelToken { get; }

        public Task Task { get; private set; }

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

        public string CurrentFile { get; private set; }

        private RestoreTask(string destFolder, BackupFolder node, CancelToken cancelToken)
        {
            errorQueue = new AsyncQueue<string>();
            DestFolder = destFolder;
            Node = node;
            CancelToken = cancelToken ?? new CancelToken();
            Errors = new ObservableCollection<string>();
        }

        private async Task UpdateCurrentFile()
        {
            string lastCurrentFile = null;

            while (IsRestoring)
            {
                await Task.Delay(200);

                if (lastCurrentFile == CurrentFile) continue;

                lastCurrentFile = CurrentFile;
                OnPropertyChanged(nameof(CurrentFile));
            }
        }

        public static RestoreTask Run(string destFolder, BackupFolder node, CancelToken cancelToken = null)
        {
            RestoreTask task = new RestoreTask(destFolder, node, cancelToken);
            task.Task = Task.Run(task.Run);

            return task;
        }

        private Task Run()
        {
            return Task.WhenAll(Task.Run(Restore), AddErrorHandler(), UpdateCurrentFile());
        }

        private async Task Restore()
        {
            try
            {
                IsRestoring = true;
                string srcDirPath = BackupUtils.GetBackupedFilesFolderPath(DestFolder);

                CurrentFile = "Load folder structure";
                await LoadAllFiles(Node);
                if (CancelToken.IsCanceled) return;

                Restart(GetFilesCount(Node));
                await Restore(Node, DestFolder, srcDirPath);
            }
            finally
            {
                IsRestoring = false;
            }
        }

        private async Task LoadAllFiles(BackupFolder node)
        {
            Queue<BackupFolder> nodes = new Queue<BackupFolder>();
            nodes.Enqueue(node);

            while (nodes.Count > 0)
            {
                if (CancelToken.IsCanceled) return;
                node = nodes.Dequeue();

                await node.LoadFolders();
                await node.LoadFiles();

                foreach (BackupFolder folder in node.Folders.ToNotNull())
                {
                    nodes.Enqueue(folder);
                }
            }
        }

        private async Task Restore(BackupFolder node, string currentPath, string srcDirPath)
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
                    await errorQueue.Enqueue("Create directory: " + currentPath + "\r\n" + e);
                    IncreaseProgress(GetFilesCount(node));
                    return;
                }
            }

            foreach (BackupFile file in node.Files.ToNotNull())
            {
                if (CancelToken.IsCanceled) return;

                string srcFilePath = Path.Combine(srcDirPath, file.BackupFileName);
                string destFilePath = Path.Combine(currentPath, file.Name);

                try
                {
                    CurrentFile = destFilePath;

                    if (File.Exists(destFilePath)) File.Delete(destFilePath);

                    File.Copy(srcFilePath, destFilePath);
                }
                catch (Exception e)
                {
                    await errorQueue.Enqueue(destFilePath + "\r\n" + e);
                }

                IncreaseProgress();
            }

            foreach (BackupFolder subFolder in node.Folders)
            {
                if (CancelToken.IsCanceled) return;

                await Restore(subFolder, currentPath, srcDirPath);
            }
        }

        private static int GetFilesCount(BackupFolder node)
        {
            int count = 0;
            Queue<BackupFolder> nodes = new Queue<BackupFolder>();
            nodes.Enqueue(node);

            while (nodes.Count > 0)
            {
                node = nodes.Dequeue();

                foreach (BackupFolder folder in node.Folders)
                {
                    nodes.Enqueue(folder);
                }

                count += node.Files?.Length ?? 0;
            }

            return count;
        }

        private async Task AddErrorHandler()
        {
            while (true)
            {
                (bool isEnd, string item) = await errorQueue.Dequeue();

                if (isEnd) return;

                Errors.Insert(0, item);
            }
        }
    }
}
