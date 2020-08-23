using System.IO;

namespace BackupApp.Backup.Handling
{
    public struct BackupFile
    {
        public int FolderId  { get; set; }

        public FileInfo File { get; }

        public string Base64Hash { get; set; }

        public BackupFile(FileInfo file) : this()
        {
            File = file;
        }

        public static BackupFile FromPath(FileInfo file)
        {
            return new BackupFile(file);
        }

        public override string ToString()
        {
            return File.FullName;
        }
    }
}
