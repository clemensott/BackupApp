using BackupApp.Restore;
using StdOttStandard.Linq.DataStructures;

namespace BackupApp.Backup.Valitate
{
    class BackupedFile
    {
        public bool Exists { get; }

        public string Path { get; }

        public string Hash { get; set; }

        public ChangeableLookup<string, BackupReadDb> DbHashes { get; }

        public BackupedFile(bool exists, string path, string hash)
        {
            Exists = exists;
            Path = path;
            Hash = hash;
            DbHashes = new ChangeableLookup<string, BackupReadDb>();
        }

        public static BackupedFile FromExistingFile(string path)
        {
            return new BackupedFile(true, path, null);
        }

        public static BackupedFile FromNotExistingFile(string path)
        {
            return new BackupedFile(false, path, null);
        }
    }
}
