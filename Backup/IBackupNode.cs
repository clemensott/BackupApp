namespace BackupApp
{
    public interface IBackupNode
    {
        string Name { get; }

        BackupFolder[] Folders { get; }

        BackupFile[] Files { get; }
    }
}
