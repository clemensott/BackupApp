namespace BackupApp.Backup.Handling
{
    public enum BackupTaskResult
    {
        Successful,
        DestinationFolderNotFound,
        NoItemsToBackup,
        Exception,
        ValidationError,
        Canceled,
    }
}
