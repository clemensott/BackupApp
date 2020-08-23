namespace BackupApp.Backup.Result
{
    struct DbFolderFile
    {
        public long FolderID { get; }

        public long FileID { get; }

        public string FileName { get; }

        public DbFolderFile(long folderID, long fileID, string fileName) : this()
        {
            FolderID = folderID;
            FileID = fileID;
            FileName = fileName;
        }
    }
}
