using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BackupApp.Backup.Handling
{
    public class BackupedFiles
    {
        public readonly IDictionary<string, string> hashes, fileNames;

        public BackupedFiles(IDictionary<string, string> hashes)
        {
            this.hashes = hashes;
            fileNames = hashes.Values.ToDictionary(v => Path.GetFileNameWithoutExtension(v));
        }

        public void Add(string hash, string backupFileName)
        {
            hashes.Add(hash, backupFileName);
        }

        public bool TryGetBackupFileName(string hash, out string backupFileName)
        {
            return hashes.TryGetValue(hash, out backupFileName);
        }

        public string GetRandomFileName(string extension)
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
    }
}
