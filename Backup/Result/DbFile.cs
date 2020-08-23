namespace BackupApp.Backup.Result
{
    struct DbFile
    {
        public long ID { get; }

        public string Hash { get; }

        public string FileName { get; }

        public DbFile(long id, string hash, string fileName) : this()
        {
            ID = id;
            Hash = hash;
            FileName = fileName;
        }
    }
}
