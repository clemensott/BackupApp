using BackupApp.Helper;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BackupApp.Restore.Caching
{
    class FolderCachingTask : CachingTask
    {
        public string SrcFolderPath { get; }

        private FolderCachingTask(string srcFolderPath, RestoreDb db) : base(db)
        {
            SrcFolderPath = srcFolderPath;
        }

        public static FolderCachingTask Run(string srcFolderPath, RestoreDb db)
        {
            FolderCachingTask task = new FolderCachingTask(srcFolderPath, db);
            task.Task = Task.Run(task.Run);

            return task;
        }

        protected override async Task Run()
        {
            double totalCount;
            string[] files = BackupUtils.GetBackupResultFiles(SrcFolderPath)?.ToArray() ?? new string[0];
            RestoreNode[] baseNodes = new RestoreNode[files.Length];

            TotalFilesCount = files.Length + 1;

            foreach (string filePath in files)
            {
                CurrentFileName = Path.GetFileName(filePath);

                int lineIndex = 0;
                string[] lines = File.ReadAllLines(filePath);
                totalCount = lines.Length;
                RestoreNode baseNode = await DB.InsertBackup(lines, () => CurrentFileLineProgess = ++lineIndex / totalCount);

                baseNodes[CurrentFileIndex] = baseNode;

                CurrentFileIndex++;
            }

            CurrentFileLineProgess = 0;
            CurrentFileName = "Remove not existing backups";
            int removeBackupIndex = 0;
            IEnumerable<RestoreNode> backups = await DB.GetBackups();
            long[] removeBackupIds = backups.Select(b => b.ID).Except(baseNodes.Select(b => b.ID)).ToArray();
            totalCount = removeBackupIds.Length + 1;

            foreach (long backupId in removeBackupIds)
            {
                await DB.RemoveBackup(backupId);
                CurrentFileLineProgess = ++removeBackupIndex / totalCount;
            }

            await DB.RemoveUnusedFiles();

            CurrentFileLineProgess = 1;
            CurrentFileIndex++;
        }
    }
}
