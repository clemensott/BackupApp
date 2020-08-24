using BackupApp.Backup.Config;
using FolderFile;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace BackupApp.LocalSave
{
    class LoadSaveHandler
    {
        private static readonly XmlSerializer serializer = new XmlSerializer(typeof(Settings));

        private readonly string path;
        private ViewModel model;
        private BackupConfig currentSubscribeConfig;

        public ViewModel Model
        {
            get => model;
            set
            {
                if (value == model) return;

                Unsubscribe(model);
                model = value;
                Subscribe(model);

            }
        }

        public LoadSaveHandler(string path, ViewModel model)
        {
            this.path = path;
            Model = model;
        }

        public static async Task<LoadSaveHandler> Load(string path)
        {
            bool isHidden = false;
            bool isEnabled = false;
            OffsetInterval backupTimes = new OffsetInterval(TimeSpan.Zero, TimeSpan.FromDays(1));
            DateTime nextScheduledBackup = backupTimes.GetNextDateTime() ?? DateTime.Now;
            Folder backupDestFolder = null;
            IEnumerable<BackupItem> backupItems = new BackupItem[0];

            try
            {
                Settings settings = LoadSettings(path);

                await Task.Run(() =>
                {
                    isHidden = settings.IsHidden;
                    isEnabled = settings.IsEnabled;
                    backupTimes = settings.BackupTimes;
                    nextScheduledBackup = new DateTime(settings.ScheduledBackupTicks);
                    backupDestFolder = Folder.CreateOrDefault(settings.BackupDestFolder);
                    backupItems = settings.Items;
                });
            }
            catch { }

            return new LoadSaveHandler(path, new ViewModel(isHidden, isEnabled,
                backupDestFolder, backupTimes, nextScheduledBackup, backupItems));
        }

        private static Settings LoadSettings(string path)
        {
            using (Stream stream = File.OpenRead(path))
            {
                return (Settings)serializer.Deserialize(stream);
            }
        }

        private void Subscribe(ViewModel viewModel)
        {
            if (viewModel == null) return;

            viewModel.PropertyChanged += ViewModel_PropertyChanged;

            Subscribe(viewModel.Config);
            currentSubscribeConfig = viewModel.Config;
        }

        private void Unsubscribe(ViewModel viewModel)
        {
            if (viewModel == null) return;

            viewModel.PropertyChanged -= ViewModel_PropertyChanged;

            Unsubscribe(viewModel.Config);
        }

        private void Subscribe(BackupConfig config)
        {
            if (config == null) return;

            config.PropertyChanged += Config_PropertyChanged;

            Subscribe(config.BackupItems);
        }

        private void Unsubscribe(BackupConfig config)
        {
            if (config == null) return;

            config.PropertyChanged -= Config_PropertyChanged;

            Unsubscribe(config.BackupItems);
        }

        private void Subscribe(ObservableCollection<BackupItem> backupItems)
        {
            if (backupItems == null) return;

            backupItems.CollectionChanged += BackupItems_CollectionChanged;

            foreach (BackupItem item in backupItems) Subscribe(item);
        }

        private void Unsubscribe(ObservableCollection<BackupItem> backupItems)
        {
            if (backupItems == null) return;

            backupItems.CollectionChanged -= BackupItems_CollectionChanged;

            foreach (BackupItem item in backupItems) Unsubscribe(item);
        }

        private void Subscribe(BackupItem item)
        {
            if (item == null) return;

            item.PropertyChanged += BackupItem_PropertyChanged;
            Subscribe(item.Folder);
        }

        private void Unsubscribe(BackupItem item)
        {
            if (item == null) return;

            item.PropertyChanged -= BackupItem_PropertyChanged;
            Unsubscribe(item.Folder);
        }

        private void Subscribe(Folder folder)
        {
            if (folder != null) folder.PropertyChanged += Folder_PropertyChanged;
        }

        private void Unsubscribe(Folder folder)
        {
            if (folder != null) folder.PropertyChanged -= Folder_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ViewModel.IsHidden):
                case nameof(ViewModel.BackupDestFolder):
                    Save();
                    break;

                case nameof(ViewModel.Config):
                    Unsubscribe(currentSubscribeConfig);
                    Subscribe(Model.Config);
                    currentSubscribeConfig = Model.Config;

                    Save();
                    break;
            }
        }

        private void Config_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(BackupConfig.IsBackupEnabled):
                case nameof(BackupConfig.BackupTimes):
                case nameof(BackupConfig.NextScheduledBackup):
                    Save();
                    break;
            }
        }

        private void Folder_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Folder folder = (Folder)sender;
            if (e.PropertyName == nameof(folder.SubType)) Save();
        }

        private void BackupItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (BackupItem item in e.NewItems?.Cast<BackupItem>() ?? Enumerable.Empty<BackupItem>())
            {
                Subscribe(item);
            }

            foreach (BackupItem item in e.OldItems?.Cast<BackupItem>() ?? Enumerable.Empty<BackupItem>())
            {
                Subscribe(item);
            }

            Save();
        }

        private void BackupItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            BackupItem item = (BackupItem)sender;

            switch (e.PropertyName)
            {
                case nameof(item.Name):
                case nameof(item.ExcludePatterns):
                    Save();
                    break;

                case nameof(item.Folder):
                    Subscribe(item.Folder);
                    Save();
                    break;
            }
        }

        private void Save()
        {
            Settings settings = new Settings()
            {
                IsHidden = Model.IsHidden,
                IsEnabled = Model.Config?.IsBackupEnabled ?? false,
                BackupTimes = Model.Config?.BackupTimes,
                ScheduledBackupTicks = (Model.Config?.NextScheduledBackup ?? DateTime.MaxValue).Ticks,
                BackupDestFolder = (SerializableFolder?)Model.BackupDestFolder,
                Items = Model.Config?.BackupItems.ToArray() ?? new BackupItem[0],
            };

            SaveSettings(settings, path);
        }

        private static void SaveSettings(Settings settings, string path)
        {
            try
            {
                lock (serializer)
                {
                    using (Stream stream = new FileStream(path, FileMode.Create))
                    {
                        serializer.Serialize(stream, settings);
                    }
                }
            }
            catch { }
        }
    }
}
