namespace BackupApp.Restore
{
    public struct BackupFile
    {
        public string Name { get; }

        public string BackupFileName { get; }

        public string Hash { get; }

        public BackupFile(string name, string backupFileName, string hash) : this()
        {
            Name = name;
            BackupFileName = backupFileName;
            Hash = hash;
        }

        public override string ToString()
        {
            return BackupFileName;
        }
    }
}
