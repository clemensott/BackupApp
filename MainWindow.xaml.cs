using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Xml.Serialization;
using FolderFile;
using Microsoft.Win32;

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
        private Timer timer;

        public MainWindow()
        {
            InitializeComponent();

            SetViewModel();
        }

        private async void SetViewModel()
        {
            DataContext = viewModel = await LoadViewModel();

            Subscribe(viewModel);

            WindowManager.CreateInstance(this, viewModel);

            SystemEvents.PowerModeChanged += OnPowerChange;
            CheckForBackup();
        }

        private async Task<ViewModel> LoadViewModel()
        {
            bool isHidden = false;
            bool isEnabled = false;
            bool? compressDirect = true;
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
                    compressDirect = settings.CompressDirect;
                    backupTimes = (OffsetIntervalViewModel)settings.BackupTimes;
                    nextScheduledBackup = new DateTime(settings.ScheduledBackupTicks);
                    backupDestFolder = Folder.CreateOrDefault(settings.BackupDestFolder);
                    backupItems = settings.Items;
                });
            }
            catch { }

            return new ViewModel(isHidden, isEnabled, compressDirect,
                backupTimes, nextScheduledBackup, backupDestFolder, backupItems);
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

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(viewModel.IsHidden):
                case nameof(viewModel.IsBackupEnabled):
                case nameof(viewModel.CompressDirect):
                case nameof(viewModel.NextScheduledBackup):
                    Save();
                    break;

                case nameof(viewModel.BackupDestFolder):
                    Subscribe(viewModel.BackupDestFolder);
                    Save();
                    break;

                case nameof(viewModel.BackupTimes):
                    Subscribe(viewModel.BackupTimes);
                    Save();
                    break;
            }

            if (e.PropertyName == nameof(viewModel.IsBackupEnabled) ||
                e.PropertyName == nameof(viewModel.NextScheduledBackup)) CheckForBackup();
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
                CompressDirect = viewModel.CompressDirect,
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

        private async void OnPowerChange(object sender, PowerModeChangedEventArgs e)
        {
            DebugEvent.SaveText("OnPowerChange");

            if (e.Mode != PowerModes.Resume) return;

            await Task.Delay(TimeSpan.FromSeconds(10));

            CheckForBackup();
        }

        private void CheckForBackup()
        {
            DebugEvent.SaveText("CheckForBackup", "NextBackup: " + viewModel.NextScheduledBackup);

            if (viewModel.NextScheduledBackup <= DateTime.Now)
            {
                if (viewModel.IsBackupEnabled) BackupAsync();
                else viewModel.NextScheduledBackup = viewModel.BackupTimes.Next;
            }
            else SetTimer();
        }

        private BackupTask BackupAsync()
        {
            BackupTask task = BackupTask.Run(viewModel.BackupDestFolder,
                viewModel.BackupItems, viewModel.CompressDirect);

            WindowManager.Current.SetBackupTask(task);

            UpdateBackupBrowser(task);

            return task;
        }

        private async void UpdateBackupBrowser(BackupTask task)
        {
            await task.Task;
            await bbcBackups.UpdateBackups();
        }

        private void SetTimer()
        {
            DebugEvent.SaveText("SetTimer");

            timer?.Dispose();

            double interval = Math.Max((viewModel.NextScheduledBackup - DateTime.Now).TotalMilliseconds, 0);

            timer = new Timer
            {
                AutoReset = false,
                Enabled = true,
                Interval = interval
            };

            timer.Elapsed += Timer_Elapsed;
            timer.Start();
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            CheckForBackup();
        }

        protected override void OnClosed(EventArgs e)
        {
            timer?.Dispose();

            base.OnClosed(e);
        }

        private void BtnBackupNow_Click(object sender, RoutedEventArgs e)
        {
            BackupAsync();
        }
    }
}
