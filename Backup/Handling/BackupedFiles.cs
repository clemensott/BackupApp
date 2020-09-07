using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BackupApp.Backup.Handling
{
    public class BackupedFiles
    {
        public readonly IDictionary<string, string> hashes, fileNames, missingFileNames;
        private readonly List<string> dbNames;

        public BackupedFiles(IDictionary<string, string> hashes, IEnumerable<string> existingFileNames, IEnumerable<string> dbNames)
        {
            this.hashes = hashes;
            fileNames = hashes.Values.ToDictionary(v => Path.GetFileNameWithoutExtension(v));
            missingFileNames = hashes.Values.ToDictionary(f => f);

            foreach (string existingFileName in existingFileNames)
            {
                if (missingFileNames.ContainsKey(existingFileName))
                {
                    missingFileNames.Remove(existingFileName);
                }
            }

            this.dbNames = dbNames.ToList();
        }

        public bool Add(string hash, string extension, out string backupFileName)
        {
            lock (hashes)
            {
                if (hashes.TryGetValue(hash, out backupFileName))
                {
                    if (missingFileNames.ContainsKey(backupFileName)) { }
                    return missingFileNames.ContainsKey(backupFileName);
                }

                backupFileName = GetRandomFileName(extension);
                hashes.Add(hash, backupFileName);
                return true;
            }
        }

        private string GetRandomFileName(string extension)
        {
            string name;
            do
            {
                name = Guid.NewGuid().ToString().Replace("-", "");
            } while (fileNames.ContainsKey(name));

            string fileName = name + extension;
            fileNames.Add(name, fileName);

            return fileName;
        }

        public void AddDbName(string dbName)
        {
            dbNames.Add(dbName);
        }

        public IEnumerable<KeyValuePair<string, string>> GetFilePairs()
        {
            return hashes;
        }

        public IEnumerable<string> GetDbNames()
        {
            return dbNames;
        }
    }
}
