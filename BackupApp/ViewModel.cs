using FolderFile;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Serialization;

namespace BackupApp
{
    class ViewModel : INotifyPropertyChanged
    {
        private const string datafilename = "Data.txt";
        private static ViewModel instance;

        public static ViewModel Current
        {
            get
            {
                if (instance == null) instance = Load();

                return instance;
            }
        }

        public static bool IsLoaded { get { return instance != null; } }

        private bool isHidden, isMoving;
        private int itemIndex;
        private DateTime nextScheduledBackup;
        private OffsetIntervalViewModel backupTimes;
        private Folder backupFolder;
        private List<BackupItem> items;

        public bool IsHidden
        {
            get { return isHidden; }
            set
            {
                if (value == isHidden) return;

                isHidden = value;

                ViewModel.SaveData();
            }
        }

        public bool IsMoving
        {
            get { return isMoving; }
            set
            {
                if (value == isMoving) return;

                isMoving = value;

                OnPropertyChanged("IsMoving");
                OnPropertyChanged("IsEnabled");
                OnPropertyChanged("IsMovingTextVisibility");
            }
        }

        public bool IsEnabled { get { return !isMoving; } }

        public Visibility IsMovingTextVisibility
        {
            get { return IsMoving ? Visibility.Visible : Visibility.Hidden; }
        }

        public int BackupItemsIndex
        {
            get { return itemIndex; }
            set
            {
                if (value == itemIndex && itemIndex < items.Count) return;

                itemIndex = value;

                if (itemIndex >= items.Count) itemIndex = items.Count - 1;

                OnPropertyChanged("BackupItemsIndex");
            }
        }

        public string CloseCancelButtonText
        {
            get { return BackupManager.Current.IsBackuping ? "Cancel" : "Close"; }
        }

        public string NextBackupDateTimeWithIntervalText
        {
            get
            {
                return string.Format("{0} ({1})", BackupTimes.NextDateTime.GetDateTimeFormats()[14],
                    BackupTimes.IntervalTextLong);
            }
        }

        public string LatestBackupDateTimeText
        {
            get
            {
                return BackupManager.Current.LatestBackupDateTime.Ticks == 0 ? "None" :
                    BackupManager.Current.LatestBackupDateTime.GetDateTimeFormats()[14];
            }
        }

        public OffsetIntervalViewModel BackupTimes
        {
            get { return backupTimes; }
            set
            {
                if (value == backupTimes) return;

                backupTimes = value;

                SetNextScheduledBackup();
                BackupManager.Current.SetTimer();

                OnPropertyChanged("BackupTimes");
                UpdateNextBackupDateTimeWithIntervalText();
                SaveData();
            }
        }

        public DateTime NextScheduledBackup
        {
            get { return nextScheduledBackup.Ticks > 0 ? nextScheduledBackup : BackupTimes.NextDateTime; }
        }

        public Folder BackupFolder
        {
            get { return backupFolder; }
            set
            {
                if (value == backupFolder) return;

                backupFolder = value;

                OnPropertyChanged("BackupFolder");
                SaveData();
            }
        }

        public List<BackupItem> BackupItems
        {
            get { return items.ToList(); }
            set
            {
                if (value == items) return;

                items = value;

                OnPropertyChanged("BackupItems");
            }
        }

        public ViewModel()
        {
            backupTimes = new OffsetIntervalViewModel();
            backupFolder = new Folder("", SubfolderType.This);
            items = new List<BackupItem>();
        }

        public ViewModel(Settings settings)
        {
            IsHidden = settings.IsHidden;
            backupTimes = new OffsetIntervalViewModel(settings.BackupTimes);
            nextScheduledBackup = new DateTime(settings.ScheduledBackupTicks);
            System.Diagnostics.Debug.WriteLine(nextScheduledBackup);
            backupFolder = new Folder(settings.BackupFolderPath, SubfolderType.This);
            items = settings.Items;
        }

        private static ViewModel Load()
        {
            try
            {
                using (Stream stream = System.IO.File.OpenRead(datafilename))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(Settings));
                    Settings settings = (Settings)serializer.Deserialize(stream);

                    return new ViewModel(settings);
                }
            }
            catch
            {
                return new ViewModel();
            }
        }

        public static void SaveData()
        {
            if (!IsLoaded || !WindowManager.Current.IsLoaded) return;

            try
            {
                using (Stream stream = new FileStream(datafilename, FileMode.Create))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(Settings));
                    Settings settings = new Settings();

                    settings.IsHidden = Current.IsHidden;
                    settings.BackupTimes = new OffsetInterval(Current.BackupTimes);
                    settings.ScheduledBackupTicks = Current.nextScheduledBackup.Ticks;
                    settings.BackupFolderPath = Current.BackupFolder.FullPath;
                    settings.Items = Current.BackupItems;

                    serializer.Serialize(stream, settings);
                }
            }
            catch { }
        }

        public void SetNextScheduledBackup()
        {
            nextScheduledBackup = BackupTimes.NextDateTime;

            SaveData();
        }

        public void AddBackupItem()
        {
            items.Add(new BackupItem());

            UpdateBackupItems();
        }

        public void RemoveSelectedBackupItem()
        {
            if (BackupItemsIndex == -1) return;

            int itemIndexBackup = BackupItemsIndex;

            items.RemoveAt(BackupItemsIndex);

            UpdateBackupItems();

            BackupItemsIndex = itemIndexBackup;
        }

        public void UpdateBackupItems()
        {
            OnPropertyChanged("BackupItems");

            SaveData();
        }

        public void UpdateNextBackupDateTimeWithIntervalText()
        {
            OnPropertyChanged("NextBackupDateTimeWithIntervalText");
        }

        public void UpdateLatestBackupDateTime()
        {
            OnPropertyChanged("LatestBackupDateTimeText");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged == null) return;

            PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
    }
}
