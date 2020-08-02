using System.Windows;
using System.Windows.Controls;

namespace BackupApp.Backup.Handling
{
    /// <summary>
    /// Interaktionslogik für BackupControl.xaml
    /// </summary>
    public partial class BackupHandlingControl : UserControl
    {
        public static readonly DependencyProperty TaskProperty =
            DependencyProperty.Register(nameof(Task), typeof(BackupTask), typeof(BackupHandlingControl),
                new PropertyMetadata(OnTaskPropertyChanged));

        private static void OnTaskPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            BackupHandlingControl s = (BackupHandlingControl)sender;
            s.gidMain.DataContext = (BackupTask)e.NewValue;
        }

        public BackupTask Task
        {
            get => (BackupTask)GetValue(TaskProperty);
            set => SetValue(TaskProperty, value);
        }

        public BackupHandlingControl()
        {
            InitializeComponent();
        }

        private void BtnCloseCancel_Click(object sender, RoutedEventArgs e)
        {
            Task.CancelToken.Cancel();
        }
    }
}
