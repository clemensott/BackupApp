using System;
using System.IO;

namespace BackupApp.Backup.Result
{
    public struct BackupFile : IEquatable<BackupFile>
    {
        public string Name { get; }

        public string SourcePath { get; }

        public string Base64Hash { get; }

        public BackupFile(string name, string sourcePath, string base64Hash)
        {
            Name = name;
            SourcePath = sourcePath;
            Base64Hash = base64Hash;
        }

        public bool Equals(BackupFile other)
        {
            return string.Equals(Name, other.Name) &&
                   string.Equals(SourcePath, other.SourcePath) && 
                   string.Equals(Base64Hash, other.Base64Hash);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;

            return obj is BackupFile other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (SourcePath != null ? SourcePath.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Base64Hash != null ? Base64Hash.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static BackupFile FromPath(string path)
        {
            return new BackupFile(Path.GetFileName(path), path, null);
        }

        public override string ToString()
        {
            return SourcePath;
        }
    }
}
