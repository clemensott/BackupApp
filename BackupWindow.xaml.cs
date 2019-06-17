using System.ComponentModel;
using System.Windows;

namespace BackupApp
{
    public partial class BackupWindow : Window
    {
        private BackupTask task;

        public BackupTask Task
        {
            get => task;
            set
            {
                task = value;

                Dispatcher.Invoke(() => DataContext = task);
            }
        }

        public BackupWindow()
        {
            InitializeComponent();
        }

        private void BtnCloseCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            if (Task == null) return;

            e.Cancel = Task?.IsBackuping == true;

            if (!Task.IsMoving) Task.CancelToken.Cancel();
        }
    }
}
