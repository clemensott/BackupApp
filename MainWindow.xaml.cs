using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml.Serialization;

namespace BackupApp
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // \\fritz.box\FRITZ.NAS\ASMT-2115-01\Cache\Backup

        private const string dataFilename = "Data.xml";
        private static readonly XmlSerializer serializer = new XmlSerializer(typeof(Settings));

        private ViewModel viewModel;

        public MainWindow()
        {
            InitializeComponent();

            Settings settings;
            try
            {
                settings = LoadSettings(dataFilename);
            }
            catch
            {
                settings = new Settings()
                {
                    BackupTimes = new OffsetInterval(TimeSpan.Zero, TimeSpan.FromDays(1)),
                    ScheduledBackupTicks = 0,
                    BackupDestFolder = null,
                    Items = new BackupItem[0]
                };
            }

            DataContext = viewModel = new ViewModel(this, settings);

            Subscribe(viewModel);
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
            Subscribe(viewModel.BackupItems);
        }

        private void Unsubscribe(ViewModel viewModel)
        {
            if (viewModel == null) return;

            viewModel.PropertyChanged -= ViewModel_PropertyChanged;

            Unsubscribe(viewModel.BackupTimes);
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

            foreach (BackupItem item in backupItems) item.PropertyChanged -= BackupItem_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.BackupDestFolder) || e.PropertyName == nameof(ViewModel.NextScheduledBackup)) Save();
            else if (e.PropertyName == nameof(ViewModel.BackupTimes))
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

        private void BackupItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (BackupItem item in e.NewItems?.Cast<BackupItem>() ?? Enumerable.Empty<BackupItem>())
            {
                item.PropertyChanged += BackupItem_PropertyChanged;
            }

            foreach (BackupItem item in e.OldItems?.Cast<BackupItem>() ?? Enumerable.Empty<BackupItem>())
            {
                item.PropertyChanged -= BackupItem_PropertyChanged;
            }

            Save();
        }

        private void Save()
        {
            System.Diagnostics.Debug.WriteLine("Save");

            Settings settings = new Settings()
            {
                IsHidden = viewModel.IsHidden,
                BackupTimes = (OffsetInterval)viewModel.BackupTimes,
                ScheduledBackupTicks = viewModel.NextScheduledBackup.Ticks,
                BackupDestFolder = viewModel.BackupDestFolder,
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

        private void BackupItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            BackupItem item = (BackupItem)sender;

            if (e.PropertyName == nameof(item.Name) || e.PropertyName == nameof(item.Folder)) Save();
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
            BackupTimesWindow window = new BackupTimesWindow(viewModel.BackupTimes);

            if (window.ShowDialog() == true)
            {
                viewModel.BackupTimes = window.BackupTimes;
            }
        }

        private void BtnBackupNow_Click(object sender, RoutedEventArgs e)
        {
            viewModel?.BackupAsync();
        }
    }
}
