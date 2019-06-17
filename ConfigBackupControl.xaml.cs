using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BackupApp
{
    /// <summary>
    /// Interaktionslogik für ConfigBackupControl.xaml
    /// </summary>
    public partial class ConfigBackupControl : UserControl
    {
        private ViewModel viewModel;

        public ConfigBackupControl()
        {
            InitializeComponent();
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

        private async void BtnChangeTimes_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel == null) return;

            OffsetIntervalViewModel backupTimes = viewModel.BackupTimes;
            bool wasEnabled = viewModel.IsBackupEnabled;

            viewModel.IsBackupEnabled = false;

            bool accepted = await WindowManager.Current.SetBackupTimes(backupTimes);

            if (accepted) viewModel.BackupTimes = backupTimes;
            else viewModel.UpdateNextScheduledBackup();

            viewModel.IsBackupEnabled = wasEnabled;
        }

        private void Control_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            viewModel = e.NewValue as ViewModel;
        }

        private void CbxCompressDirect_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            viewModel.CompressDirect = null;
        }
    }
}
