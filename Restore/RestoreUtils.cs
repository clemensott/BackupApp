using BackupApp.Backup.Result;
using BackupApp.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BackupApp.Restore
{
    static class RestoreUtils
    {
        public static int GetFilesCount(RestoreNode startNode)
        {
            int count = 0;
            Queue<RestoreNode> nodes = new Queue<RestoreNode>();

            nodes.Enqueue(startNode);

            while (nodes.Count > 0)
            {
                RestoreNode node = nodes.Dequeue();

                foreach (RestoreNode folder in node.Folders)
                {
                    nodes.Enqueue(folder);
                }

                count += node.Files?.Length ?? 0;
            }

            return count;
        }
    }
}
