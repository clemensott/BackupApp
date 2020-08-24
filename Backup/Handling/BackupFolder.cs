using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using FolderFile;

namespace BackupApp.Backup.Handling
{
    public struct BackupFolder
    {
        public long? ParentId { get; }

        public string Name { get; }

        public DirectoryInfo Directory { get; }

        public SubfolderType SubType { get; }

        public BackupFolder(long? parentId, DirectoryInfo directory, SubfolderType subType)
        {
            ParentId = parentId;
            Directory = directory;
            Name = Directory.Name;
            SubType = subType;
        }

        public BackupFolder(string name, Folder folder)
        {
            ParentId = null;
            Name = name;
            Directory = folder.GetDirectory();
            SubType = folder.SubType;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
