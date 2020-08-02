using System;
using System.Collections.Generic;
using StdOttStandard.Linq;

namespace BackupApp.Backup.Result
{
    public class BackupModel : IBackupNode, IEquatable<BackupModel>
    {
        public DateTime Timestamp { get; }

        public string Name => ConvertDateTimeOfBackupToString(Timestamp);

        public IList<BackupFolder> Folders { get; }

        public IList<BackupFile> Files { get; }

        private BackupModel()
        {
            Files = new BackupFile[0];
        }

        public BackupModel(IList<BackupFolder> folders) : this()
        {
            Timestamp = DateTime.Now;
            Folders = folders;
        }

        public BackupModel(DateTime timestamp, IList<BackupFolder> folders) : this()
        {
            Timestamp = timestamp;
            Folders = folders;
        }

        public bool Equals(BackupModel other)
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
            return obj is BackupModel other && Equals(other);
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
