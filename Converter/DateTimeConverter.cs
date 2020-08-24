using System;
using System.Globalization;
using System.Windows.Data;

namespace BackupApp
{
    class DateTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            DateTime? dateTime = (DateTime?)value;

            return dateTime.HasValue ? dateTime.Value.ToString("F") : "None";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            DateTime dateTime;

            return DateTime.TryParse((string)value, out dateTime) ? (DateTime?)dateTime : null;
        }
    }
}
