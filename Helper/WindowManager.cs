using BackupApp.Backup.Config;
using BackupApp.Backup.Handling;
using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Imaging;

namespace BackupApp
{
    class WindowManager
    {
        private NotifyIcon notifyIcon;
        private IContainer components;
        private readonly MainWindow mainWindow;
        private readonly ViewModel viewModel;

        public BitmapImage Icon { get; private set; }

        private WindowManager(MainWindow mainWindow, ViewModel viewModel)
        {
            this.mainWindow = mainWindow;
            this.viewModel = viewModel;
        }

        public static WindowManager Start(MainWindow mainWindow, ViewModel viewModel)
        {
            WindowManager manager = new WindowManager(mainWindow, viewModel);
            manager.Start();

            return manager;
        }

        private void Start()
        {
            try
            {
                Icon = new BitmapImage(new Uri(Path.GetFullPath(@".\Assets\icon.ico")));
            }
            catch
            {
                Icon = null;
            }

            SetNotifyIcon();
            SetBalloonTip();

            mainWindow.StateChanged += MainWindow_StateChanged;
            mainWindow.Closing += MainWindow_Closing;

            if (Icon != null) mainWindow.Icon = Icon;

            viewModel.PropertyChanged += ViewModel_PropertyChanged;

            if (viewModel.IsHidden) HideWindows();
        }

        private void SetNotifyIcon()
        {
            components = new Container();
            notifyIcon = new NotifyIcon(components);

            notifyIcon.Icon = SystemIcons.Shield;
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
            notifyIcon.MouseMove += NotifyIcon_MouseMove;
        }

        private void NotifyIcon_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                BackupTask backupTask = viewModel.BackupTask;
                if (backupTask != null && backupTask.IsBackuping) notifyIcon.Text = "Is Backuping";
                else
                {
                    BackupConfig config = viewModel.Config;
                    if (config == null || config.NextScheduledBackup == DateTime.MaxValue || !config.IsBackupEnabled)
                    {
                        notifyIcon.Text = "No Backup scheduled";
                    }
                    else
                    {
                        TimeSpan timeUntilNextBackup = config.NextScheduledBackup - DateTime.Now;
                        notifyIcon.Text = ConvertTimeSpanToStringLong(timeUntilNextBackup);
                    }
                }
            }
            catch (Exception exc)
            {
                DebugEvent.SaveText("NotifyIcon_MouseMove", exc);
            }
        }

        private void NotifyIcon_DoubleClick(object sender, EventArgs e)
        {
            mainWindow?.Show();
        }

        public void SetBalloonTip()
        {
            notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            notifyIcon.BalloonTipClicked += NotifyIcon_BalloonTipClicked;
        }

        private void NotifyIcon_BalloonTipClicked(object sender, EventArgs e)
        {
            mainWindow?.Show();
        }

        private void HideWindows()
        {
            viewModel.IsHidden = true;

            mainWindow.Hide();
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (mainWindow.WindowState == WindowState.Minimized)
            {
                HideWindows();
                viewModel.IsHidden = true;
            }
            else viewModel.IsHidden = false;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            components?.Dispose();
            notifyIcon?.Dispose();
        }

        private async void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.BackupTask))
            {
                BackupTask backupTask = viewModel.BackupTask;
                if (backupTask != null) await ShowNotifyIcon(backupTask);
            }
        }

        private async Task ShowNotifyIcon(BackupTask task)
        {
            int itemsCount = task.Items.Length;
            if (viewModel.IsHidden)
            {
                string balloonTipText = itemsCount + (itemsCount == 1 ? " Directory" : " Directories");

                notifyIcon.ShowBalloonTip(5000, "Backup started.", balloonTipText, ToolTipIcon.Info);
            }

            BackupTaskResult result = await task;
            if (!viewModel.IsHidden) return;

            TimeSpan backupTimeSpan = DateTime.Now - task.Started;
            switch (result)
            {
                case BackupTaskResult.Successful:
                    string balloonTipText = itemsCount + (itemsCount == 1 ? " Directory\n" : " Directories\n") +
                        ConvertTimeSpanToStringLong(backupTimeSpan);

                    notifyIcon.ShowBalloonTip(5000, "Backup finished.", balloonTipText, ToolTipIcon.Info);
                    break;

                case BackupTaskResult.DestinationFolderNotFound:
                    notifyIcon.ShowBalloonTip(5000, "Destination folder not found.", string.Empty, ToolTipIcon.Warning);
                    break;

                case BackupTaskResult.NoItemsToBackup:
                    notifyIcon.ShowBalloonTip(5000, "No items to backup.", string.Empty, ToolTipIcon.Warning);
                    break;

                case BackupTaskResult.Exception:
                    notifyIcon.ShowBalloonTip(5000, "Backup failed.", task.FailedException.Message, ToolTipIcon.Error);
                    break;

                case BackupTaskResult.ValidationError:
                    notifyIcon.ShowBalloonTip(5000, "Validation of backup failed.", string.Empty, ToolTipIcon.Warning);
                    break;

                case BackupTaskResult.Canceled:
                    notifyIcon.ShowBalloonTip(5000, "Backup got canceled.", string.Empty, ToolTipIcon.Warning);
                    break;
            }
        }

        public static string ConvertTimeSpanToStringLong(TimeSpan timeSpan)
        {
            string output = "";

            if (timeSpan.Days > 0) AddTextToTimeSpanStringLong(ref output, timeSpan.Days, "Day");
            if (timeSpan.Hours > 0) AddTextToTimeSpanStringLong(ref output, timeSpan.Hours, "Hour");
            if (timeSpan.Minutes > 0) AddTextToTimeSpanStringLong(ref output, timeSpan.Minutes, "Minute");
            if (timeSpan.Seconds > 0) AddTextToTimeSpanStringLong(ref output, timeSpan.Seconds, "Second");

            return output;
        }

        private static void AddTextToTimeSpanStringLong(ref string output, int value, string valueNameSingular)
        {
            AddTextToTimeSpanStringLong(ref output, value, valueNameSingular, valueNameSingular + "s");
        }

        private static void AddTextToTimeSpanStringLong(ref string output,
            int value, string valueNameSingular, string valueNamePlural)
        {
            output += output.Length == 0 ? "" : " ";
            output += value.ToString();
            output += " ";
            output += value == 1 ? valueNameSingular : valueNamePlural;
        }
    }
}
