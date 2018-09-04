using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackupApp
{
    public class Settings
    {
        public bool IsHidden { get; set; }

        public OffsetInterval BackupTimes { get; set; }

        public long ScheduledBackupTicks { get; set; }

        public string BackupFolderPath { get; set; }

        public List<BackupItem> Items { get; set; }

        public Settings()
        {

        }
    }
}
