using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using BackupApp.Backup.Handling;
using BackupApp.Backup.Result;
using BackupApp.Restore;
using StdOttStandard.Linq;

namespace BackupApp.Helper
{
    static class BackupUtils
    {
        private const string dbExtension = ".db", backupFilesDirName = "files",
            localBackupedFilesCacheFileName = "backupedFilesCache.txt";

        public static string GetBackupedFilesFolderPath(string baseFolderPath)
        {
            return Path.Combine(baseFolderPath, backupFilesDirName);
        }

        public async static Task<BackupedFiles> GetBackupedFiles(string folderPath, CancelToken cancelToken = null)
        {
            BackupReadDb[] dbs = GetReadDBs(folderPath).ToNotNull().ToArray();
            IEnumerable<string> dbNames = dbs.Select(db => Path.GetFileName(db.Path));
            BackupedFiles cache = LoadLocalBackupedFilesCache();

            if (cache != null && ContainsSame(cache.GetDbNames(), dbNames)) return cache;

            IDictionary<string, string> files = new Dictionary<string, string>();
            await Task.WhenAll(dbs.Select(db => db.ImportAllFiles(files, cancelToken)));

            return new BackupedFiles(files, dbNames);
        }

        private static bool ContainsSame(IEnumerable<string> src1, IEnumerable<string> src2)
        {
            return src1.OrderBy(v => v).SequenceEqual(src2.OrderBy(v => v));
        }

        private static BackupedFiles LoadLocalBackupedFilesCache()
        {
            try
            {
                if (!File.Exists(localBackupedFilesCacheFileName)) return null;

                string[] lines = File.ReadAllLines(localBackupedFilesCacheFileName);

                List<string> dbNames = new List<string>();
                Dictionary<string, string> hashes = new Dictionary<string, string>();

                foreach (string line in lines)
                {
                    string[] parts = line.Split('|');

                    if (parts.Length == 1) dbNames.Add(parts[0]);
                    else hashes.Add(parts[0], parts[1]);
                }

                return new BackupedFiles(hashes, dbNames);
            }
            catch
            {
                return null;
            }
        }

        public static void SaveLocalBackupedFilesCache(BackupedFiles backupedFiles)
        {
            List<string> lines = new List<string>();

            lines.AddRange(backupedFiles.GetDbNames());
            lines.AddRange(backupedFiles.GetFilePairs().Select(p => $"{p.Key}|{p.Value}"));

            File.WriteAllLines(localBackupedFilesCacheFileName, lines);
        }

        public static IEnumerable<BackupReadDb> GetReadDBs(string folderPath)
        {
            return GetBackupResultFiles(folderPath)?.Select(f => new BackupReadDb(f));
        }

        public static IEnumerable<string> GetBackupResultFiles(string folderPath)
        {
            try
            {
                return Directory.GetFiles(folderPath)
                    .Where(f => f.EndsWith(dbExtension) &&
                                TryConvertToDateTime(Path.GetFileNameWithoutExtension(f), out _));
            }
            catch
            {
                return null;
            }
        }

        public static bool TryConvertToDateTime(string name, out DateTime dateTime)
        {
            dateTime = new DateTime();
            string[] dateTimeParts = name.Split('_');

            if (dateTimeParts.Length < 2) return false;

            string[] dateParts = dateTimeParts[0].Split('-');
            string[] timeParts = dateTimeParts[1].Split('-');
            int year, month, day, hour, minute, second;

            if (dateParts.Length < 3 || !int.TryParse(dateParts[0], out year) ||
                !int.TryParse(dateParts[1], out month) || !int.TryParse(dateParts[2], out day) ||
                timeParts.Length < 3 || !int.TryParse(timeParts[0], out hour) ||
                !int.TryParse(timeParts[1], out minute) || !int.TryParse(timeParts[2], out second)) return false;

            dateTime = new DateTime(year, month, day, hour, minute, second);

            return true;
        }

        public static BackupWriteDb CreateDb(string folderPath, DateTime timestamp)
        {
            string name = ConvertDateTimeOfBackupToString(timestamp);
            string backupPath = Path.Combine(folderPath, name + dbExtension);

            return new BackupWriteDb(backupPath);
        }

        private static string ConvertDateTimeOfBackupToString(DateTime dateTimeOfBackup)
        {
            DateTime dt = dateTimeOfBackup;

            return string.Format("{0:0000}-{1:00}-{2:00}_{3:00}-{4:00}-{5:00}",
                dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second);
        }

        public static string GetHash(string filePath)
        {
            using (MD5 md5 = MD5.Create())
            {
                using (FileStream stream = File.OpenRead(filePath))
                {
                    return Convert.ToBase64String(md5.ComputeHash(stream)).Replace('/', '_');
                }
            }
        }
    }
}
