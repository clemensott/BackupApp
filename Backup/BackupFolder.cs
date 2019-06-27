using System;
using System.IO;
using System.Linq;
using FolderFile;
using StdOttStandard;

namespace BackupApp
{
    public class BackupFolder : IBackupNode, IEquatable<BackupFolder>
    {
        public string Name { get; }

        public BackupFolder[] Folders { get; }

        public BackupFile[] Files { get; }

        public BackupFolder(string name, BackupFolder[] folders, BackupFile[] files)
        {
            Name = name;
            Folders = folders;
            Files = files;
        }

        public static BackupFolder FromPath(Folder folder)
        {
            return FromPath(folder.FullName, folder.SubType);
        }

        public static BackupFolder FromPath(string path, SubfolderType subType)
        {
            BackupFolder[] folders;
            BackupFile[] files;

            switch (subType)
            {
                case SubfolderType.No:
                    folders = new BackupFolder[0];
                    files = new BackupFile[0];
                    break;

                case SubfolderType.This:
                    folders = new BackupFolder[0];
                    files = Directory.GetFiles(path).Select(BackupFile.FromPath).ToArray();
                    break;

                case SubfolderType.All:
                    folders = Directory.GetDirectories(path).Select(d => FromPath(d, SubfolderType.All)).ToArray();
                    files = Directory.GetFiles(path).Select(BackupFile.FromPath).ToArray();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(subType), subType, null);
            }

            return new BackupFolder(Path.GetFileName(path), folders, files);
        }

        public bool Equals(BackupFolder other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return string.Equals(Name, other.Name) && Folders.BothNullOrSequenceEqual(other.Folders) &&
                   Files.BothNullOrSequenceEqual(other.Files);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;

            return obj is BackupFolder other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Folders != null ? Folders.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Files != null ? Files.GetHashCode() : 0);
                return hashCode;
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
