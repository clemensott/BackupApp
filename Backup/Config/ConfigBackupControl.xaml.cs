using StdOttFramework;
using System.Windows;
using System.Windows.Controls;

namespace BackupApp.Backup.Config
{
    /// <summary>
    /// Interaktionslogik für ConfigBackupControl.xaml
    /// </summary>
    public partial class ConfigBackupControl : UserControl
    {
        public static readonly DependencyProperty ConfigProperty =
            DependencyProperty.Register(nameof(Config), typeof(BackupConfig), typeof(ConfigBackupControl),
                new PropertyMetadata(OnConfigPropertyChanged));

        private static void OnConfigPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            ConfigBackupControl s = (ConfigBackupControl)sender;
            s.gidMain.DataContext = (BackupConfig)e.NewValue;
        }

        public BackupConfig Config
        {
            get => (BackupConfig)GetValue(ConfigProperty);
            set => SetValue(ConfigProperty, value);
        }


        public ConfigBackupControl()
        {
            InitializeComponent();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            Config.BackupItems.Add(new BackupItem());
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            BackupItem item = FrameworkUtils.GetDataContext<BackupItem>(sender);
            Config.BackupItems.Remove(item);
        }

        private void BtnChangeTimes_Checked(object sender, RoutedEventArgs e)
        {
            btcChange.BackupTimes = Config.BackupTimes;
            btcChange.Visibility = Visibility.Visible;
        }

        private void BtnChangeTimes_Unchecked(object sender, RoutedEventArgs e)
        {
            btcChange.Visibility = Visibility.Collapsed;
        }

        private void BtcChange_Apply(object sender, RoutedEventArgs e)
        {
            Config.BackupTimes = btcChange.BackupTimes;
            btnChangeTimes.IsChecked = false;
        }

        private void BtcChange_Cancel(object sender, RoutedEventArgs e)
        {
            btnChangeTimes.IsChecked = false;
        }
    }
}
