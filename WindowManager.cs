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
    class WindowManager : INotifyPropertyChanged
    {
        private NotifyIcon notifyIcon;
        private IContainer components;
        private readonly MainWindow mainWindow;
        private readonly BackupWindow backupWindow;
        private readonly ViewModel viewModel;

        public BitmapImage Icon { get; private set; }

        public BackupTask CurrentBackupTask
        {
            get { return backupWindow.CurrentBackupTask; }
            set
            {
                backupWindow.CurrentBackupTask = value;

                ShowBackupWindowOrToolTip();
            }
        }

        public WindowManager(MainWindow mainWindow, ViewModel viewModel)
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

            backupWindow = new BackupWindow();
            backupWindow.StateChanged += BackupWindow_StateChanged;

            if (Icon != null)
            {
                mainWindow.Icon = Icon;
                backupWindow.Icon = Icon;
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

        private void NotifyIcon_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            try
            {
                notifyIcon.Text = CurrentBackupTask?.IsBackuping == true
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
            ShowCurrentWindow();
        }

        public void SetBalloonTip()
        {
            notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
            notifyIcon.BalloonTipClicked += NotifyIcon_BalloonTipClicked;
        }

        private void NotifyIcon_BalloonTipClicked(object sender, EventArgs e)
        {
            ShowCurrentWindow();
        }

        private void ShowCurrentWindow()
        {
            if (CurrentBackupTask?.IsBackuping != true) ShowWindow(mainWindow);
            else ShowWindow(backupWindow);
        }

        private void ShowWindow(Window window)
        {
            viewModel.IsHidden = false;

            window.Show();

            window.WindowState = WindowState.Normal;
            window.ShowInTaskbar = true;

            window.Focus();
        }

        private void HideWindows()
        {
            viewModel.IsHidden = true;

            HideWindow(mainWindow);
            HideWindow(backupWindow);
        }

        private void HideWindow(Window window)
        {
            window.WindowState = WindowState.Minimized;
            window.ShowInTaskbar = false;

            window.Hide();
        }

        private void MainWindow_Activated(object sender, EventArgs e)
        {
            if (viewModel.IsHidden) HideWindows();
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (mainWindow.WindowState == WindowState.Minimized) HideWindows();
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            viewModel.Close();

            components?.Dispose();
            notifyIcon?.Dispose();
            backupWindow?.Close();
        }

        private void BackupWindow_StateChanged(object sender, EventArgs e)
        {
            if (backupWindow.WindowState == WindowState.Minimized) HideWindows();
        }

        public async Task ShowBackupWindowOrToolTip()
        {
            await backupWindow.Dispatcher.BeginInvoke((Action)(() =>
            {
                if (viewModel.IsHidden)
                {
                    int itemsCount = CurrentBackupTask.Items.Length;
                    string balloonTipText = itemsCount + (itemsCount == 1 ? " Directory" : " Directories");

                    notifyIcon.ShowBalloonTip(5000, "Backup started.", balloonTipText, ToolTipIcon.Info);
                }
                else
                {
                    HideWindow(mainWindow);
                    ShowWindow(backupWindow);
                }
            }));

            await CurrentBackupTask.Task;

            await HideBackupWindow();
        }

        private async Task HideBackupWindow()
        {
            await backupWindow.Dispatcher.BeginInvoke((Action)(() =>
            {
                if (viewModel.IsHidden) ShowNotifyIcon();
                else
                {
                    HideWindow(backupWindow);
                    ShowWindow(mainWindow);
                }
            }));
        }

        private void ShowNotifyIcon()
        {
            if (CurrentBackupTask.Failed)
            {
                notifyIcon.ShowBalloonTip(5000, "Backup failed.", "", ToolTipIcon.Info);
            }
            else
            {
                int itemsCount = CurrentBackupTask.Items.Length;
                TimeSpan backupTimeSpan = DateTime.Now - CurrentBackupTask.Started;
                string balloonTipText = itemsCount + (itemsCount == 1 ? " Directory\n" : " Directories\n") +
                    OffsetIntervalViewModel.ConvertTimeSpanToStringLong(backupTimeSpan);

                notifyIcon.ShowBalloonTip(5000, "Backup finished.", balloonTipText, ToolTipIcon.Info);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
