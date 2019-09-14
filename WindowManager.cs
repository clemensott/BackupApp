using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using StdOttStandard.AsyncResult;

namespace BackupApp
{
    public class WindowManager : INotifyPropertyChanged
    {
        public static WindowManager Current { get; private set; }

        public static void CreateInstance(MainWindow mainWindow, ViewModel viewModel)
        {
            Current = new WindowManager(mainWindow, viewModel);
        }

        private NotifyIcon notifyIcon;
        private IContainer components;
        private readonly MainWindow mainWindow;
        private readonly BackupTimesWindow timesWindows;
        private readonly BackupWindow backupWindow;
        private readonly RestoreBackupWindow restoreWindow;
        private readonly ViewModel viewModel;

        public BitmapImage Icon { get; }

        private WindowManager(MainWindow mainWindow, ViewModel viewModel)
        {
            try
            {
                Icon = new BitmapImage(new Uri(Path.GetFullPath("Backomat.ico")));
            }
            catch
            {
                Icon = null;
            }

            SetNotifyIcon();
            SetBalloonTip();

            this.mainWindow = mainWindow;
            mainWindow.Activated += MainWindow_Activated;
            mainWindow.StateChanged += MainWindow_StateChanged;
            mainWindow.Closing += MainWindow_Closing;

            timesWindows = new BackupTimesWindow();
            timesWindows.StateChanged += TimesWindows_StateChanged;

            backupWindow = new BackupWindow();
            backupWindow.StateChanged += BackupWindow_StateChanged;

            restoreWindow = new RestoreBackupWindow();
            restoreWindow.StateChanged += RestoreWindow_StateChanged;

            if (Icon != null)
            {
                mainWindow.Icon = Icon;
                timesWindows.Icon = Icon;
                backupWindow.Icon = Icon;
                restoreWindow.Icon = Icon;
            }

            this.viewModel = viewModel;

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
                notifyIcon.Text = backupWindow.Task?.IsBackuping == true
                    ? "Is Backuping"
                    : string.Format("Next Backup in: {0}", viewModel.BackupTimes.TimeToNextBackupText);
            }
            catch (Exception exc)
            {
                DebugEvent.SaveText("NotifyIcon_MouseMove", exc);
            }
        }

        private void NotifyIcon_DoubleClick(object sender, EventArgs e)
        {
            ShowCurrentWindows();
        }

        public void SetBalloonTip()
        {
            notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            notifyIcon.BalloonTipClicked += NotifyIcon_BalloonTipClicked;
        }

        private void NotifyIcon_BalloonTipClicked(object sender, EventArgs e)
        {
            ShowCurrentWindows();
        }

        private void ShowCurrentWindows()
        {
            bool hasOtherWindow = false;

            if (!mainWindow.IsVisible) ShowWindow(mainWindow, false);

            if (backupWindow.Task?.Task.IsCompleted == false)
            {
                ShowWindow(backupWindow, true);
                hasOtherWindow = true;
            }

            if (timesWindows.IsAccepted?.Task.IsCompleted == false)
            {
                ShowWindow(timesWindows, !hasOtherWindow);
                hasOtherWindow = true;
            }

            if (restoreWindow.Task?.Task.IsCompleted == false) ShowWindow(restoreWindow, !hasOtherWindow);
        }

        private async void ShowWindow(Window window, bool showDialog)
        {
            viewModel.IsHidden = false;

            if (window.IsVisible) return;

            if (showDialog)
            {
                ShowDialog();

                await Task.Delay(50);

                window.Activate();

                if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            }
            else
            {
                window.Show();
                window.Activate();

                if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            }

            async void ShowDialog()
            {
                await Task.Delay(10);

                window.ShowDialog();
            }
        }

        private void HideWindows()
        {
            viewModel.IsHidden = true;

            backupWindow.Hide();
            timesWindows.Hide();
            restoreWindow.Hide();
            mainWindow.Hide();
        }

        private void MainWindow_Activated(object sender, EventArgs e)
        {
            //if (viewModel.IsHidden) HideWindows();
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (mainWindow.WindowState == WindowState.Minimized) HideWindows();
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            components?.Dispose();
            notifyIcon?.Dispose();
            backupWindow?.Close();
            timesWindows?.Close();
            restoreWindow?.Close();
        }

        private void TimesWindows_StateChanged(object sender, EventArgs e)
        {
            CheckHide();
        }

        private void BackupWindow_StateChanged(object sender, EventArgs e)
        {
            if (backupWindow.WindowState == WindowState.Minimized) HideWindows();
        }

        private void RestoreWindow_StateChanged(object sender, EventArgs e)
        {
            CheckHide();
        }

        private void CheckHide()
        {
            if ((!backupWindow.IsVisible || backupWindow.WindowState == WindowState.Minimized) &&
                (!timesWindows.IsVisible || timesWindows.WindowState == WindowState.Minimized) &&
                (!restoreWindow.IsVisible || restoreWindow.WindowState == WindowState.Minimized) ||
                mainWindow.WindowState == WindowState.Minimized) HideWindows();
        }

        public async void SetBackupTask(BackupTask task)
        {
            backupWindow.Task = task;

            await ShowBackupWindow(task);
            await task.Task;
            await HideBackupWindow();
        }

        private DispatcherOperation ShowBackupWindow(BackupTask task)
        {
            return backupWindow.Dispatcher.BeginInvoke((Action)(() =>
            {
                if (viewModel.IsHidden)
                {
                    int itemsCount = task.Items.Length;
                    string balloonTipText = itemsCount + (itemsCount == 1 ? " Directory" : " Directories");

                    notifyIcon.ShowBalloonTip(5000, "Backup started.", balloonTipText, ToolTipIcon.Info);
                }
                else ShowCurrentWindows();
            }));
        }

        private async Task HideBackupWindow()
        {
            if (backupWindow.Task?.Task.IsCompleted == false) return;

            await backupWindow.Dispatcher.BeginInvoke((Action)(() =>
            {
                if (viewModel.IsHidden) ShowNotifyIcon();
                else backupWindow.Hide();
            }));
        }

        private void ShowNotifyIcon()
        {
            BackupTask task = backupWindow.Task;

            if (task.Failed)
            {
                notifyIcon.ShowBalloonTip(5000, "Backup failed.",
                    task.FailedException.Message, ToolTipIcon.Error);
            }
            else
            {
                int itemsCount = task.Items.Length;
                TimeSpan backupTimeSpan = DateTime.Now - task.Started;
                string balloonTipText = itemsCount + (itemsCount == 1 ? " Directory\n" : " Directories\n") +
                    OffsetIntervalViewModel.ConvertTimeSpanToStringLong(backupTimeSpan);

                notifyIcon.ShowBalloonTip(5000, "Backup finished.", balloonTipText, ToolTipIcon.Info);
            }
        }

        public async Task<bool> SetBackupTimes(OffsetIntervalViewModel backupTimes)
        {
            AsyncResult<bool> value = timesWindows.SetBackupTimes(backupTimes);

            await value.Task;

            if (timesWindows.IsAccepted?.Task.IsCompleted != false) timesWindows.Hide();

            return value.Result;
        }

        public async void SetRestoreTask(RestoreTask task)
        {
            restoreWindow.Task = task;

            ShowCurrentWindows();

            await task.Task;

            if (restoreWindow.Task?.Task.IsCompleted != false) restoreWindow.Hide();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
