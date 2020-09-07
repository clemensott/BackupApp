using System;
using System.Collections;
using System.Collections.Generic;

namespace BackupApp.Backup.Valitate
{
    public class DbErrorFile : IEnumerable<string>
    {
        private readonly IList<string> list;

        public string FileName { get; }

        public string BackupedFileHash { get; }

        public int Count => list.Count;

        public DbErrorFile(string fileName, string backupedFileHash)
        {
            list = new List<string>();
            FileName = fileName;
            BackupedFileHash = backupedFileHash;
        }

        public void Add(string dbFileHash)
        {
            list.Add(dbFileHash);
        }

        public IEnumerator<string> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
