using System.Collections.Generic;

namespace BackupApp.Backup.Result
{
    public interface IBackupNode
    {
        string Name { get; }

        IList<BackupFolder> Folders { get; }

        IList<BackupFile> Files { get; }
    }
}
