using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
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

namespace BackupApp
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // \\fritz.box\FRITZ.NAS\ASMT-2115-01\Cache\Backup

        public MainWindow()
        {
            InitializeComponent();

            DataContext = ViewModel.Current;

            WindowManager.Current.SetMainWindow(this);
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Current.AddBackupItem();
        }

        private void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Current.RemoveSelectedBackupItem();
        }

        private void btnChangeTimes_Click(object sender, RoutedEventArgs e)
        {
            new BackupTimesWindow().ShowDialog();
        }

        private void btnBackupNow_Click(object sender, RoutedEventArgs e)
        {
            new Task(BackupManager.Current.Backup).Start();
        }
    }
}
