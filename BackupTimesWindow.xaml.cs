using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using StdOttStandard;

namespace BackupApp
{
    /// <summary>
    /// Interaktionslogik für BackupTimesWindow.xaml
    /// </summary>
    public partial class BackupTimesWindow : Window
    {
        private OffsetIntervalViewModel backupTimes;

        public OffsetIntervalViewModel BackupTimes
        {
            get => backupTimes;
            private set => DataContext = backupTimes = value;

        }

        public SetableValue<bool> IsAccepted { get; private set; }

        public BackupTimesWindow()
        {
            InitializeComponent();
        }

        public SetableValue<bool> SetBackupTimes(OffsetIntervalViewModel value)
        {
            BackupTimes = value;

            return IsAccepted = new SetableValue<bool>();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            IsAccepted.SetValue(true);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            IsAccepted.SetValue(false);
        }

        private void TbxNextTime_LostFocus(object sender, RoutedEventArgs e)
        {
            BackupTimes.SetAutoNextTimeTextShort();
        }

        private void TbxInterval_LostFocus(object sender, RoutedEventArgs e)
        {
            BackupTimes.SetAutoIntervalTextShort();
        }

        private void TbxNextTime_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) BackupTimes.SetAutoNextTimeTextShort();
        }

        private void TbxInterval_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) BackupTimes.SetAutoIntervalTextShort();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();

            base.OnClosing(e);
        }
    }
}
