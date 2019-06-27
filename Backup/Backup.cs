using System;
using StdOttStandard;

namespace BackupApp
{
    public class Backup : IBackupNode, IEquatable<Backup>
    {
        public DateTime Timestamp { get; }

        public string Name => BackupUtils.ConvertDateTimeOfBackupToString(Timestamp);

        public BackupFolder[] Folders { get; }

        public BackupFile[] Files { get; }

        private Backup()
        {
            Files = new BackupFile[0];
        }

        public Backup(BackupFolder[] folders) : this()
        {
            Timestamp = DateTime.Now;
            Folders = folders;
        }

        public Backup(DateTime timestamp, BackupFolder[] folders) : this()
        {
            Timestamp = timestamp;
            Folders = folders;
        }

        public bool Equals(Backup other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Timestamp.Equals(other.Timestamp) && Folders.BothNullOrSequenceEqual(other.Folders) &&
                   Files.BothNullOrSequenceEqual(other.Files);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is Backup other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Timestamp.GetHashCode();
                hashCode = (hashCode * 397) ^ (Folders != null ? Folders.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Files != null ? Files.GetHashCode() : 0);
                return hashCode;
            }
        }

        private static string ConvertDateTimeOfBackupToString(DateTime dateTimeOfBackup)
        {
            DateTime dt = dateTimeOfBackup;

            return string.Format("{0:0000}-{1:00}-{2:00}_{3:00}-{4:00}-{5:00}",
                dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second);
        }
    }
}
