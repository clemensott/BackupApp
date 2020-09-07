using System.Collections;
using System.Collections.Generic;
using StdOttStandard.Linq;

namespace BackupApp.Backup.Valitate
{
    public class DbErrorFiles : IEnumerable<DbErrorFile>
    {
        private readonly IDictionary<string, DbErrorFile> files;

        public string DbFileName { get; }

        public int Count => files.Count;

        public DbErrorFiles(string dbfileName)
        {
            files = new Dictionary<string, DbErrorFile>();
            DbFileName = dbfileName;
        }

        public void Add(string fileName, string backupedFileHash, string dbFileHash)
        {
            files.GetOrAdd
            (
                backupedFileHash ?? string.Empty,
                () => new DbErrorFile(fileName, backupedFileHash)
            ).Add(dbFileHash);
        }


        public IEnumerator<DbErrorFile> GetEnumerator()
        {
            return files.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
