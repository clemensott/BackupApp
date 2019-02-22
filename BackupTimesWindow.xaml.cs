using System.Windows;
using System.Windows.Input;

namespace BackupApp
{
    /// <summary>
    /// Interaktionslogik für BackupTimesWindow.xaml
    /// </summary>
    public partial class BackupTimesWindow : Window
    {
        public OffsetIntervalViewModel BackupTimes;

        public BackupTimesWindow(OffsetIntervalViewModel oivm)
        {
            InitializeComponent();

            DataContext = BackupTimes = new OffsetIntervalViewModel((OffsetInterval)oivm);
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
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
    }
}
