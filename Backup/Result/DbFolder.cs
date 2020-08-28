using System;
using System.Collections.Generic;

namespace BackupApp.Backup.Result
{
    public struct DbFolder : IEquatable<DbFolder>
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

        public override bool Equals(object obj)
        {
            return obj is DbFolder && Equals((DbFolder)obj);
        }

        public bool Equals(DbFolder other)
        {
            return ID == other.ID &&
                   EqualityComparer<long?>.Default.Equals(ParentID, other.ParentID) &&
                   Name == other.Name;
        }

        public override int GetHashCode()
        {
            var hashCode = 1717766030;
            hashCode = hashCode * -1521134295 + ID.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<long?>.Default.GetHashCode(ParentID);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
            return hashCode;
        }
    }
}
