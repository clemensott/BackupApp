using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BackupApp.Backup.Result;
using BackupApp.Restore;
using StdOttStandard.Linq;

namespace BackupApp.Helper
{
    static class BackupUtils
    {
        private const string dbExtension = ".db", backupFilesDirName = "files";

        public static string GetBackupedFilesFolderPath(string baseFolderPath)
        {
            return Path.Combine(baseFolderPath, backupFilesDirName);
        }

        public async static Task<IDictionary<string, string>> GetAllFiles(string folderPath, CancelToken cancelToken = null)
        {
            IDictionary<string, string> files = new Dictionary<string, string>();
            await Task.WhenAll(GetReadDBs(folderPath).ToNotNull().Select(db => db.GetAllFiles(files, cancelToken)));

            return files;
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

        public static Task<BackupWriteDb> CreateDb(string folderPath, DateTime timestamp)
        {
            string name = ConvertDateTimeOfBackupToString(timestamp);
            string backupPath = Path.Combine(folderPath, name + dbExtension);

            return BackupWriteDb.Create(backupPath);
        }

        private static string ConvertDateTimeOfBackupToString(DateTime dateTimeOfBackup)
        {
            DateTime dt = dateTimeOfBackup;

            return string.Format("{0:0000}-{1:00}-{2:00}_{3:00}-{4:00}-{5:00}",
                dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second);
        }
    }
}
