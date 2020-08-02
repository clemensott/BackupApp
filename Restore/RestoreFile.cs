namespace BackupApp.Restore
{
    public struct RestoreFile
    {
        public string Name { get; }

        public string Hash { get; }

        public string BackupFileName { get; }

        public RestoreFile(string name, string hash, string backupFileName) : this()
        {
            Name = name;
            Hash = hash;
            BackupFileName = backupFileName;
        }
    }
}
