using System;
using System.Collections.Generic;

namespace BackupApp.Backup.Result
{
    public struct DbFolderFile : IEquatable<DbFolderFile>
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

        public override bool Equals(object obj)
        {
            return obj is DbFolderFile && Equals((DbFolderFile)obj);
        }

        public bool Equals(DbFolderFile other)
        {
            return FolderID == other.FolderID &&
                   FileID == other.FileID &&
                   FileName == other.FileName;
        }

        public override int GetHashCode()
        {
            var hashCode = 438000814;
            hashCode = hashCode * -1521134295 + FolderID.GetHashCode();
            hashCode = hashCode * -1521134295 + FileID.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(FileName);
            return hashCode;
        }
    }
}
