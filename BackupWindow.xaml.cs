using System;
using System.ComponentModel;
using System.Windows;

namespace BackupApp
{
    /// <summary>
    /// Interaktionslogik für BackupWindow.xaml
    /// </summary>
    public partial class BackupWindow : Window
    {
        private BackupTask currentBackupTask;

        public BackupTask CurrentBackupTask
        {
            get { return currentBackupTask; }
            set
            {
                currentBackupTask = value;

                Dispatcher.Invoke(() => DataContext = currentBackupTask);
            }
        }

        public BackupWindow()
        {
            InitializeComponent();

            StateChanged += BackupWindow_StateChanged;
        }

        private void BackupWindow_StateChanged(object sender, EventArgs e)
        {
            ShowInTaskbar = WindowState != WindowState.Minimized;

            if (ShowInTaskbar) Focus();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            if (CurrentBackupTask != null)
            {
                e.Cancel = CurrentBackupTask.IsBackuping;

                if (!CurrentBackupTask.IsMoving) CurrentBackupTask.CancelToken.Cancel();
            }
        }

        private void BtnCloseCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
