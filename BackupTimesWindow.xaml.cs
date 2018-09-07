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

namespace BackupApp
{
    /// <summary>
    /// Interaktionslogik für BackupTimesWindow.xaml
    /// </summary>
    public partial class BackupTimesWindow : Window
    {
        private OffsetIntervalViewModel viewModel;

        public BackupTimesWindow()
        {
            InitializeComponent();

            DataContext = viewModel = new OffsetIntervalViewModel(ViewModel.Current.BackupTimes);

            if (WindowManager.Current.Icon == null) Icon = WindowManager.Current.Icon;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Current.BackupTimes = viewModel;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void tbxNextTime_LostFocus(object sender, RoutedEventArgs e)
        {
            viewModel.SetAutoNextTimeTextShort();
        }

        private void tbxInterval_LostFocus(object sender, RoutedEventArgs e)
        {
            viewModel.SetAutoIntervalTextShort();
        }

        private void tbxNextTime_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) viewModel.SetAutoNextTimeTextShort();
        }

        private void tbxInterval_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) viewModel.SetAutoIntervalTextShort();
        }
    }
}
