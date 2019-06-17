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
using System.Windows.Shapes;

namespace BackupApp
{
    /// <summary>
    /// Interaktionslogik für RestoreBackupWindow.xaml
    /// </summary>
    public partial class RestoreBackupWindow : Window
    {
        private RestoreTask task;

        public RestoreTask Task
        {
            get => task;
            set
            {
                task = value;
                Dispatcher.Invoke(() => DataContext = task);
            }
        }

        public RestoreBackupWindow()
        {
            InitializeComponent();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            task.CancelToken.Cancel();
        }
    }
}
