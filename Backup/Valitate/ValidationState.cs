namespace BackupApp.Backup.Valitate
{
    public enum ValidationState
    {
        WaitForStart,
        Starting,
        ReadingDBs,
        LoadingHashes,
        SearchingErrorFiles,
        DeletingUnusedFiles,
        Finished,
        Failed,
        Canceled,
    }
}
