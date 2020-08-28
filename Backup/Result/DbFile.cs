using System;
using System.Collections.Generic;

namespace BackupApp.Backup.Result
{
    public struct DbFile : IEquatable<DbFile>
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

        public override bool Equals(object obj)
        {
            return obj is DbFile && Equals((DbFile)obj);
        }

        public bool Equals(DbFile other)
        {
            return ID == other.ID &&
                   Hash == other.Hash &&
                   FileName == other.FileName;
        }

        public override int GetHashCode()
        {
            var hashCode = 668932993;
            hashCode = hashCode * -1521134295 + ID.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Hash);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(FileName);
            return hashCode;
        }
    }
}
