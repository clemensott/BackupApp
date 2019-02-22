﻿using FolderFile;

namespace BackupApp
{
    public class Settings
    {
        public bool IsHidden { get; set; }

        public OffsetInterval BackupTimes { get; set; }

        public long ScheduledBackupTicks { get; set; }

        public SerializableFolder? BackupDestFolder { get; set; }

        public BackupItem[] Items { get; set; }
    }
}