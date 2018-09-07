using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BackupApp
{
    /// <summary>
    /// Interaktionslogik für BackupWindow.xaml
    /// </summary>
    public partial class BackupWindow : Window
    {
        public BackupWindow()
        {
            InitializeComponent();

            DataContext = ViewModel.Current;

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

            e.Cancel = BackupManager.Current.IsBackuping;

            if (!ViewModel.Current.IsMoving) BackupManager.Current.IsBackuping = false;
        }

        private void btnCloseCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
