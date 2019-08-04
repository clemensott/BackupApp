using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FolderFile;
using StdOttStandard;

namespace BackupApp
{
    public static class BackupUtils
    {
        private const int folderDepthChars = 3;
        public const string ZipExtension = ".zip", TxtExtension = ".txt", BackupFilesDirName = "files";

        public static IDictionary<string, string> GetFiles(IEnumerable<Backup> backups)
        {
            Dictionary<string, string> files = new Dictionary<string, string>();

            foreach (BackupFile file in backups.SelectMany(GetFiles))
            {
                if (files.ContainsKey(file.Base64Hash)) continue;

                files.Add(file.Base64Hash, file.SourcePath);
            }

            return files;
        }

        public static IEnumerable<BackupFile> GetFiles(IBackupNode startNode)
        {
            Queue<IBackupNode> nodes = new Queue<IBackupNode>();

            nodes.Enqueue(startNode);

            while (nodes.Count > 0)
            {
                IBackupNode node = nodes.Dequeue();

                foreach (BackupFile file in node.Files)
                {
                    yield return file;
                }

                foreach (BackupFolder folder in node.Folders)
                {
                    nodes.Enqueue(folder);
                }
            }
        }

        public static Task<IEnumerable<Backup>> GetBackups(Folder folder)
        {
            return GetBackups(folder.FullName);
        }

        public static async Task<IEnumerable<Backup>> GetBackups(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return null;

            string[] files = Directory.GetFiles(folderPath);
            IEnumerable<Task<Backup>> backupTasks = files
                .Select(file => Task.Run(() =>
                {
                    if (!file.EndsWith(TxtExtension)
                    /*||                        !files.Contains(file.Remove(file.Length - TxtExtension.Length) + ZipExtension)*/) return null;

                    try
                    {
                        string[] backupLines = File.ReadAllLines(file);
                        return Deserialize(backupLines);
                    }
                    catch (Exception e)
                    {
                        DebugEvent.SaveText("GetBackupsException", e.ToString());
                        return null;
                    }
                }));

            return (await Task.WhenAll(backupTasks)).Where(b => b != null);
        }

        public static string ConvertDateTimeOfBackupToString(DateTime dateTimeOfBackup)
        {
            DateTime dt = dateTimeOfBackup;

            return string.Format("{0:0000}-{1:00}-{2:00};{3:00}-{4:00}-{5:00}",
                dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second);
        }

        public static bool TryConvertToDateTime(string name, out DateTime dateTime)
        {
            dateTime = new DateTime();
            string[] dateTimeParts = name.Split(';');

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

        public static string Serialize(Backup backup)
        {
            return string.Join("\r\n", SerializeLines(backup));
        }

        private static IEnumerable<string> SerializeLines(Backup backup)
        {
            yield return backup.Timestamp.Ticks.ToString();

            foreach (string line in SerializeNode(backup, 0))
            {
                yield return line;
            }
        }

        private static IEnumerable<string> SerializeNode(IBackupNode node, int depth)
        {
            yield return depth.ToString().PadLeft(folderDepthChars) + node.Name;

            foreach (BackupFile file in node.Files)
            {
                yield return string.Format(">{0}|{1}|{2}", file.Base64Hash, file.SourcePath, file.Name);
            }

            foreach (BackupFolder folder in node.Folders)
            {
                foreach (string line in SerializeNode(folder, depth + 1))
                {
                    yield return line;
                }
            }
        }

        public static Backup Deserialize(string serial)
        {
            return Deserialize(serial.Split("\r\n"));
        }

        public static Backup Deserialize(IEnumerable<string> lines)
        {
            using (IEnumerator<string> enumerator = lines.GetEnumerator())
            {
                enumerator.MoveNext();
                DateTime timestamp = new DateTime(long.Parse(enumerator.Current));

                enumerator.MoveNext();
                //string name = enumerator.Current;

                enumerator.MoveNext();
                BackupFolder[] folders = DeserializeFolders(enumerator, 1, new RefValue<bool>(false)).ToArray();

                return new Backup(timestamp, folders);
            }
        }

        private static IEnumerable<BackupFolder> DeserializeFolders(IEnumerator<string> enumerator, int depth, RefValue<bool> ended)
        {
            int lineDepth;

            while (!ended && int.TryParse(enumerator.Current.Remove(folderDepthChars), out lineDepth) && lineDepth == depth)
            {
                string folderName = enumerator.Current.Substring(folderDepthChars);
                BackupFile[] files = DeserializeFiles(enumerator, ended).ToArray();
                BackupFolder[] folders = DeserializeFolders(enumerator, depth + 1, ended).ToArray();

                yield return new BackupFolder(folderName, folders, files);
            }
        }

        private static IEnumerable<BackupFile> DeserializeFiles(IEnumerator<string> enumerator, RefValue<bool> ended)
        {
            while (true)
            {
                if (!enumerator.MoveNext())
                {
                    ended.Value = true;
                    yield break;
                }

                if (enumerator.Current[0] != '>') yield break;

                string line = enumerator.Current.Substring(1);
                string[] parts = line.Split('|');

                string hash = parts[0];
                string sourcePath = parts[1];
                string fileName = parts[2];

                yield return new BackupFile(fileName, sourcePath, hash);
            }
        }
    }
}
