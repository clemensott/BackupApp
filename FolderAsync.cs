using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FolderFile;
using StdOttStandard.AsyncResult;

namespace BackupApp
{
    public class FolderAsync
    {
        private readonly AsyncResult<FolderAsync[]> foldersSetter;
        private readonly AsyncResult<FileInfo[]> filesSetter;

        public DirectoryInfo Info { get; }

        public Task<FolderAsync[]> Folders => foldersSetter.Task;

        public Task<FileInfo[]> Files => filesSetter.Task;

        private FolderAsync(DirectoryInfo info)
        {
            Info = info;
            foldersSetter = new AsyncResult<FolderAsync[]>();
            filesSetter = new AsyncResult<FileInfo[]>();
        }

        public static FolderAsync FromDirectory(Folder folder, out Task loadTask)
        {
            FolderAsync output = new FolderAsync(folder.GetDirectory());

            loadTask = Task.Run(() => LoadFolderRecursive(output, folder.SubType));

            return output;
        }

        private static void LoadFolderRecursive(FolderAsync folder, SubfolderType subType)
        {
            Queue<FolderAsync> queue = new Queue<FolderAsync>();

            queue.Enqueue(folder);

            while (queue.Count > 0)
            {
                FolderAsync[] folders;
                FileInfo[] files;

                folder = queue.Dequeue();

                try
                {
                    files = subType != SubfolderType.No ? folder.Info.GetFiles() : new FileInfo[0];
                }
                catch
                {
                    files = new FileInfo[0];
                }

                folder.filesSetter.SetValue(files);

                try
                {
                    folders = subType != SubfolderType.No ?
                        folder.Info.GetDirectories().Select(d => new FolderAsync(d)).ToArray() : new FolderAsync[0];
                }
                catch
                {
                    folders = new FolderAsync[0];
                }

                folder.foldersSetter.SetValue(folders);

                foreach (FolderAsync subFolder in folders)
                {
                    queue.Enqueue(subFolder);
                }

                if (subType == SubfolderType.This) subType = SubfolderType.No;
            }
        }
    }
}
