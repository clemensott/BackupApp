using System;
using System.Globalization;
using System.Windows.Data;

namespace BackupApp
{
    class ProgressTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is double ? Math.Round((double)value * 100) + " %" : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
