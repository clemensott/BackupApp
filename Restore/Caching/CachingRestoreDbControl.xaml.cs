using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace BackupApp.Restore.Caching
{
    /// <summary>
    /// Interaktionslogik für CachingRestoreDbControl.xaml
    /// </summary>
    public partial class CachingRestoreDbControl : UserControl
    {
        public static readonly DependencyProperty TaskProperty =
            DependencyProperty.Register(nameof(Task), typeof(CachingTask), typeof(CachingRestoreDbControl),
                new PropertyMetadata(OnTaskPropertyChanged));

        private static void OnTaskPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            CachingRestoreDbControl s = (CachingRestoreDbControl)sender;
            s.gidMain.DataContext = (CachingTask)e.NewValue;
        }

        public CachingTask Task
        {
            get => (CachingTask)GetValue(TaskProperty);
            set => SetValue(TaskProperty, value);
        }

        public CachingRestoreDbControl()
        {
            InitializeComponent();
        }

        private object ProgressCon_ConvertEvent(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return $"{ Math.Round((double)value * 100)}%";
        }
    }
}
