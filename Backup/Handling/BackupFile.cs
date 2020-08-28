using System.IO;

namespace BackupApp.Backup.Handling
{
    public struct BackupFile
    {
        public long FolderId { get; }

        public FileInfo Info { get; }

        public BackupFile(long folderId, FileInfo file) : this()
        {
            FolderId = folderId;
            Info = file;
        }

        public override string ToString()
        {
            return Info.FullName;
        }
    }
}
