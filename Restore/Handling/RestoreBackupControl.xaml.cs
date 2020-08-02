using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BackupApp.Restore.Handling
{
    /// <summary>
    /// Interaktionslogik für RestoreBackupControl.xaml
    /// </summary>
    public partial class RestoreBackupControl : UserControl
    {

        public static readonly DependencyProperty TaskProperty =
            DependencyProperty.Register(nameof(Task), typeof(RestoreTask), typeof(RestoreBackupControl),
                new PropertyMetadata(OnTaskPropertyChanged));

        private static void OnTaskPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            RestoreBackupControl s = (RestoreBackupControl)sender;
            RestoreTask newValue = (RestoreTask)e.NewValue;

            s.gidMain.DataContext = newValue;
        }

        public RestoreTask Task
        {
            get => (RestoreTask)GetValue(TaskProperty);
            set => SetValue(TaskProperty, value);
        }

        public RestoreBackupControl()
        {
            InitializeComponent();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Task.CancelToken.Cancel();
        }
    }
}
