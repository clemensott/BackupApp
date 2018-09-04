using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Imaging;

namespace BackupApp
{
    class WindowManager
    {
        private static WindowManager instance;

        public static WindowManager Current
        {
            get
            {
                if (instance == null) instance = new WindowManager();

                return instance;
            }
        }

        private DateTime backupStartedDateTime;
        private BitmapImage icon;
        private NotifyIcon notifyIcon;
        private IContainer components;
        private MainWindow mainWindow;
        private BackupWindow backupWindow;

        public bool IsLoaded
        {
            get
            {
                if (mainWindow == null) return false;
                if (Thread.CurrentThread == mainWindow.Dispatcher.Thread) return mainWindow.IsLoaded;

                bool isLoaded = false;

                mainWindow.Dispatcher.BeginInvoke((Action)(() => { isLoaded = mainWindow.IsLoaded; })).Wait();

                return isLoaded;
            }
        }

        public BitmapImage Icon { get { return icon; } }

        private WindowManager()
        {
            backupWindow = new BackupWindow();

            backupWindow.StateChanged += BackupWindow_StateChanged;

            try
            {
                icon = new BitmapImage(new Uri(Path.GetFullPath("Backomat.ico")));

                backupWindow.Icon = icon;
            }
            catch
            {
                icon = null;
            }

            SetNotifyIcon();
            SetBalloonTip();

            SystemEvents.PowerModeChanged += OnPowerChange;
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
            if (BackupManager.Current.IsBackuping) notifyIcon.Text = "Is Backuping";
            else notifyIcon.Text = string.Format("Next Backup in: {0}",
                ViewModel.Current.BackupTimes.TimeToNextBackupText);
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
            if (!BackupManager.Current.IsBackuping) ShowWindow(mainWindow);
            else ShowWindow(backupWindow);
        }

        private void ShowWindow(Window window)
        {
            ViewModel.Current.IsHidden = false;

            window.Show();

            window.WindowState = WindowState.Normal;
            window.ShowInTaskbar = true;

            window.Focus();
        }

        private void HideWindows()
        {
            ViewModel.Current.IsHidden = true;

            HideWindow(mainWindow);
            HideWindow(backupWindow);
        }

        private void HideWindow(Window window)
        {
            window.WindowState = WindowState.Minimized;
            window.ShowInTaskbar = false;

            window.Hide();
        }

        private void OnPowerChange(object s, PowerModeChangedEventArgs e)
        {
            Thread.Sleep(TimeSpan.FromSeconds(10));

            int threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
            DebugEvent.SaveText("OnPowerChange", "ThreadID: " + threadID, e.Mode);

            if (e.Mode == PowerModes.Resume) BackupManager.Current.CheckForBackup();
        }

        public void SetMainWindow(MainWindow window)
        {
            mainWindow = window;
            mainWindow.Activated += MainWindow_Activated;
            mainWindow.StateChanged += MainWindow_StateChanged;
            mainWindow.Closing += MainWindow_Closing;

            if (icon != null) mainWindow.Icon = icon;
        }

        private void MainWindow_Activated(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine(ViewModel.Current.IsHidden);
            if (ViewModel.Current.IsHidden) HideWindows();
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (mainWindow.WindowState == WindowState.Minimized) HideWindows();
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            BackupManager.Current.Close();

            if (components != null) components.Dispose();
            if (notifyIcon != null) notifyIcon.Dispose();
            if (backupWindow != null) backupWindow.Close();
        }

        private void BackupWindow_StateChanged(object sender, EventArgs e)
        {
            if (backupWindow.WindowState == WindowState.Minimized) HideWindows();
        }

        public void ShowBackupWindow()
        {
            foreach (BackupItem item in ViewModel.Current.BackupItems) item.BeginBackup();

            backupWindow.Dispatcher.BeginInvoke((Action)(() =>
            {
                if (ViewModel.Current.IsHidden)
                {
                    int itemsCount = ViewModel.Current.BackupItems.Count;
                    string balloonTipText = itemsCount + (itemsCount == 1 ? " Directory" : " Directories");

                    notifyIcon.ShowBalloonTip(5000, "Backup started.", balloonTipText, ToolTipIcon.Info);
                }
                else
                {
                    HideWindow(mainWindow);
                    ShowWindow(backupWindow);
                }

                backupStartedDateTime = DateTime.Now;
            }));
        }

        public void HideBackupWindow()
        {
            backupWindow.Dispatcher.BeginInvoke((Action)(() =>
            {
                if (ViewModel.Current.IsHidden) ShowNotifyIcon();
                else
                {
                    HideWindow(backupWindow);
                    ShowWindow(mainWindow);
                }
            }));
        }

        private void ShowNotifyIcon()
        {
            if (BackupManager.Current.Failed)
            {
                notifyIcon.ShowBalloonTip(5000, "Backup failed.", "", ToolTipIcon.Info);
            }
            else
            {
                int itemsCount = ViewModel.Current.BackupItems.Count;
                TimeSpan backupTimeSpan = DateTime.Now - backupStartedDateTime;
                string balloonTipText = itemsCount + (itemsCount == 1 ? " Directory\n" : " Directories\n") +
                    OffsetIntervalViewModel.ConvertTimeSpanToStringLong(backupTimeSpan);

                notifyIcon.ShowBalloonTip(5000, "Backup finished.", balloonTipText, ToolTipIcon.Info);
            }
        }
    }
}
