using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BackupApp.Backup.Result;

namespace BackupApp.Helper
{
    public static class BackupUtils
    {
        private const int folderDepthChars = 3;
        private const string txtExtension = ".txt", backupFilesDirName = "files";

        public static string GetBackupedFilesFolderPath(string baseFolderPath)
        {
            return Path.Combine(baseFolderPath, backupFilesDirName);
        }

        public static bool TryDecodeFolder(string line, out int depth, out string folderName)
        {
            depth = -1;
            folderName = null;

            if (line.Length <= 3) return false;

            folderName = line.Substring(folderDepthChars);
            return int.TryParse(line.Remove(folderDepthChars), out depth);
        }

        public static IEnumerable<string> GetBackupResultFiles(string folderPath)
        {
            try
            {
                return Directory.GetFiles(folderPath)
                    .Where(f => f.EndsWith(txtExtension) &&
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

        public static int GetFilesCount(IBackupNode node)
        {
            int count = 0;
            Queue<IBackupNode> nodes = new Queue<IBackupNode>();

            nodes.Enqueue(node);

            while (nodes.Count > 0)
            {
                node = nodes.Dequeue();

                foreach (IBackupNode folder in node.Folders)
                {
                    nodes.Enqueue(folder);
                }

                count += node.Files?.Count ?? 0;
            }

            return count;
        }

        public static void SaveBackup(string folderPath, BackupModel backup)
        {
            string backupPath = Path.Combine(folderPath, backup.Name + txtExtension);

            try
            {
                var backupLines = SerializeLines(backup);
                File.WriteAllLines(backupPath, backupLines);
            }
            catch (Exception e)
            {
                try
                {
                    File.Delete(backupPath);
                }
                catch (Exception e1)
                {
                    DebugEvent.SaveText("HandleBackupMoveDeleteBackupException", e1.ToString());
                }

                throw e;
            }
        }

        private static IEnumerable<string> SerializeLines(BackupModel backup)
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

        //public static async Task<BackupModel> Deserialize(StreamReader reader)
        //{
        //    StreamReaderEnumerator enumerator = new StreamReaderEnumerator(reader);
        //    string ticksText = await enumerator.ReadLineAsync();
        //    DateTime timestamp = new DateTime(long.Parse(ticksText));

        //    string name = await enumerator.ReadLineAsync();

        //    await enumerator.MoveNextAsync();
        //    List<BackupFolder> folders = await DeserializeFolders(enumerator, 1);

        //    return new BackupModel(timestamp, folders);
        //}

        //private static async Task<List<BackupFolder>> DeserializeFolders(StreamReaderEnumerator enumerator, int depth)
        //{
        //    int lineDepth;
        //    List<BackupFolder> folders = new List<BackupFolder>();

        //    while (!enumerator.EndOfStream && int.TryParse(enumerator.Current.Remove(FolderDepthChars), out lineDepth) && lineDepth == depth)
        //    {
        //        string folderName = enumerator.Current.Substring(FolderDepthChars);
        //        List<BackupFile> files = await DeserializeFiles(enumerator);
        //        List<BackupFolder> subFolders = await DeserializeFolders(enumerator, depth + 1);

        //        folders.Add(new BackupFolder(folderName, subFolders, files));
        //    }

        //    return folders;
        //}

        //private static async Task<List<BackupFile>> DeserializeFiles(StreamReaderEnumerator enumerator)
        //{
        //    List<BackupFile> files = new List<BackupFile>();

        //    while (await enumerator.MoveNextAsync())
        //    {
        //        if (enumerator.Current[0] != '>') break;

        //        string line = enumerator.Current.Substring(1);
        //        string[] parts = line.Split('|');

        //        string hash = parts[0];
        //        string sourcePath = parts[1];
        //        string fileName = parts[2];

        //        files.Add(new BackupFile(fileName, sourcePath, hash));
        //    }

        //    return files;
        //}
    }
}
