using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace BackupApp.Backup.Valitate
{
    /// <summary>
    /// Interaction logic for ValidationHandlingContol.xaml
    /// </summary>
    public partial class ValidationHandlingContol : UserControl
    {
        public static readonly DependencyProperty TaskProperty =
            DependencyProperty.Register(nameof(Task), typeof(ValidationTask), typeof(ValidationHandlingContol),
                new PropertyMetadata(null, OnTaskPropertyChanged));

        private static void OnTaskPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            ValidationHandlingContol s = (ValidationHandlingContol)sender;
            s.gidMain.DataContext = (ValidationTask)e.NewValue;
        }

        public ValidationTask Task
        {
            get => (ValidationTask)GetValue(TaskProperty);
            set => SetValue(TaskProperty, value);
        }

        public ValidationHandlingContol()
        {
            InitializeComponent();
        }

        private object IsRunningConverter_ConvertEvent(object value, Type targetType, object parameter, CultureInfo culture)
        {
            ValidationState state = (ValidationState)value;

            return state != ValidationState.Canceled &&
                state != ValidationState.Failed &&
                state != ValidationState.Finished ?
                Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnCloseCancel_Click(object sender, RoutedEventArgs e)
        {
            Task?.CancelToken.Cancel();
        }
    }
}
