namespace BackupApp.Backup.Result
{
    struct DbFolder
    {
        public long ID { get; }

        public long? ParentID { get; }

        public string Name { get; }

        public DbFolder(long id, long? parentID, string name) : this()
        {
            ID = id;
            ParentID = parentID;
            Name = name;
        }
    }
}
