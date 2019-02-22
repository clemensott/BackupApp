namespace BackupApp
{
    public class BackupCancelToken
    {
        public bool IsCanceled { get; private set; } = false;

        public void Cancel() => IsCanceled = true;
    }
}
