using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Serialization;
using FolderFile;

namespace BackupApp
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string dataFilename = "Data.xml";
        private static readonly XmlSerializer serializer = new XmlSerializer(typeof(Settings));

        private ViewModel viewModel;

        public MainWindow()
        {
            InitializeComponent();

            SetViewModel();
        }

        private async void SetViewModel()
        {
            DataContext = viewModel = await LoadViewModel();

            Subscribe(viewModel);
        }

        private async Task<ViewModel> LoadViewModel()
        {
            bool isHidden = false;
            bool isEnabled = false;
            OffsetIntervalViewModel backupTimes = new OffsetIntervalViewModel(TimeSpan.Zero, TimeSpan.FromDays(1));
            DateTime nextScheduledBackup = backupTimes.Next;
            Folder backupDestFolder = null;
            IEnumerable<BackupItem> backupItems = new BackupItem[0];

            try
            {
                Settings settings = LoadSettings(dataFilename);

                if (settings.IsHidden) Hide();

                await Task.Run(() =>
                {
                    isHidden = settings.IsHidden;
                    isEnabled = settings.IsEnabled;
                    backupTimes = (OffsetIntervalViewModel)settings.BackupTimes;
                    nextScheduledBackup = new DateTime(settings.ScheduledBackupTicks);
                    backupDestFolder = Folder.CreateOrDefault(settings.BackupDestFolder);
                    backupItems = settings.Items;
                });
            }
            catch { }

            return new ViewModel(this, isHidden, isEnabled, backupTimes, nextScheduledBackup, backupDestFolder, backupItems);
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

            Subscribe(viewModel.BackupTimes);
            Subscribe(viewModel.BackupDestFolder);
            Subscribe(viewModel.BackupItems);
        }

        private void Unsubscribe(ViewModel viewModel)
        {
            if (viewModel == null) return;

            viewModel.PropertyChanged -= ViewModel_PropertyChanged;

            Unsubscribe(viewModel.BackupTimes);
            Unsubscribe(viewModel.BackupDestFolder);
            Unsubscribe(viewModel.BackupItems);
        }

        private void Subscribe(OffsetIntervalViewModel oivm)
        {
            if (oivm != null) oivm.PropertyChanged += Oivm_PropertyChanged;
        }

        private void Unsubscribe(OffsetIntervalViewModel oivm)
        {
            if (oivm != null) oivm.PropertyChanged -= Oivm_PropertyChanged;
        }

        private void Subscribe(Folder folder)
        {
            if (folder != null) folder.PropertyChanged += Folder_PropertyChanged;
        }

        private void Unsubscribe(Folder folder)
        {
            if (folder != null) folder.PropertyChanged -= Folder_PropertyChanged;
        }

        private void Subscribe(ObservableCollection<BackupItem> backupItems)
        {
            if (backupItems == null) return;

            backupItems.CollectionChanged += BackupItems_CollectionChanged;

            foreach (BackupItem item in backupItems) item.PropertyChanged += BackupItem_PropertyChanged;
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

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(viewModel.IsHidden) || e.PropertyName == nameof(viewModel.IsBackupEnabled) ||
                e.PropertyName == nameof(viewModel.NextScheduledBackup))
            {
                Save();
            }
            else if (e.PropertyName == nameof(viewModel.BackupDestFolder))
            {
                Subscribe(viewModel.BackupDestFolder);
                Save();
            }
            else if (e.PropertyName == nameof(viewModel.BackupTimes))
            {
                Subscribe(viewModel.BackupTimes);
                Save();
            }
        }

        private void Oivm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OffsetIntervalViewModel oivm = (OffsetIntervalViewModel)sender;

            if (viewModel.BackupTimes != oivm) Unsubscribe(oivm);
            else if (e.PropertyName == nameof(oivm.Interval) || e.PropertyName == nameof(oivm.Offset)) Save();
        }

        private void Folder_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Folder folder = (Folder)sender;

            if (folder == viewModel.BackupDestFolder || viewModel.BackupItems.Any(i => i.Folder == folder))
            {
                if (e.PropertyName == nameof(folder.SubType)) Save();
            }
            else folder.PropertyChanged -= Folder_PropertyChanged;
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

            if (e.PropertyName == nameof(item.Name)) Save();
            else if (e.PropertyName == nameof(item.Folder))
            {
                Subscribe(item.Folder);
                Save();
            }
        }

        private void Save()
        {
            System.Diagnostics.Debug.WriteLine("Save");

            Settings settings = new Settings()
            {
                IsHidden = viewModel.IsHidden,
                IsEnabled = viewModel.IsBackupEnabled,
                BackupTimes = (OffsetInterval)viewModel.BackupTimes,
                ScheduledBackupTicks = viewModel.NextScheduledBackup.Ticks,
                BackupDestFolder = (SerializableFolder?)viewModel.BackupDestFolder,
                Items = viewModel.BackupItems.ToArray()
            };

            SaveSettings(settings, dataFilename);
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

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            viewModel?.BackupItems.Add(new BackupItem());
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            int index = viewModel?.BackupItemsIndex ?? -1;

            if (index < 0 || index >= viewModel.BackupItems.Count) return;

            viewModel.BackupItems.RemoveAt(index);
        }

        private void BtnChangeTimes_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel == null) return;

            bool wasEnabled = viewModel.IsBackupEnabled;
            BackupTimesWindow window = new BackupTimesWindow(viewModel.BackupTimes);

            viewModel.IsBackupEnabled = false;

            if (window.ShowDialog() == true) viewModel.BackupTimes = window.BackupTimes;
            else viewModel.UpdateNextScheduledBackup();

            viewModel.IsBackupEnabled = wasEnabled;
        }

        private void BtnBackupNow_Click(object sender, RoutedEventArgs e)
        {
            viewModel?.BackupAsync();
        }
    }
}
