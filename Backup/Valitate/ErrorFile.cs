using BackupApp.Restore;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BackupApp.Backup.Valitate
{
    public class ErrorFile : IEnumerable<KeyValuePair<string, IEnumerable<string>>>
    {
        private readonly ICollection<KeyValuePair<string, IEnumerable<string>>> hashes;

        public string FileName { get; }

        public string Hash { get; }

        public int Count { get; }

        public ErrorFile(string fileName, string hash, ICollection<KeyValuePair<string, IEnumerable<string>>> hashes)
        {
            FileName = fileName;
            Hash = hash;
            this.hashes = hashes;
            Count = hashes.Count;
        }

        internal static ErrorFile Create(BackupedFile backupedFile)
        {
            return new ErrorFile
            (
                Path.GetFileName(backupedFile.Path),
                backupedFile.Hash, 
                backupedFile.DbHashes.Select(CreateErrorHash).ToArray()
            );
        }

        private static KeyValuePair<string, IEnumerable<string>> CreateErrorHash(KeyValuePair<string, IReadOnlyList<BackupReadDb>> pair)
        {
            return new KeyValuePair<string, IEnumerable<string>>
            (
                pair.Key,
                pair.Value.Select(db => Path.GetFileName(db.Path)).ToArray()
            );
        }

        public IEnumerator<KeyValuePair<string, IEnumerable<string>>> GetEnumerator()
        {
            return hashes.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
