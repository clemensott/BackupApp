using BackupApp.Backup.Result;
using BackupApp.Helper;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BackupApp.Restore.Caching
{
    class BackupsCachingTask : CachingTask
    {
        public BackupModel[] Backups { get; }

        private BackupsCachingTask(IEnumerable<BackupModel> backups, RestoreDb db) : base(db)
        {
            Backups = backups.ToArray();
        }

        public static BackupsCachingTask Run(IEnumerable<BackupModel> backups, RestoreDb db)
        {
            BackupsCachingTask task = new BackupsCachingTask(backups, db);
            task.Task = Task.Run(task.Run);

            return task;
        }

        protected override async Task Run()
        {
            TotalFilesCount = Backups.Length;

            foreach (BackupModel backup in Backups)
            {
                CurrentFileName = backup.Name;

                int lineIndex = 0;
                double totalCount = BackupUtils.GetFilesCount(backup);
                await DB.InsertBackup(backup, () => CurrentFileLineProgess = ++lineIndex / totalCount);

                CurrentFileIndex++;
            }

            CurrentFileLineProgess = 1;
        }
    }
}
