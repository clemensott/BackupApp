using StdOttStandard.Linq;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace BackupApp.Backup.Config
{
    /// <summary>
    /// Interaktionslogik für BackupTimesControl.xaml
    /// </summary>
    public partial class BackupTimesControl : UserControl
    {
        public static readonly DependencyProperty BackupTimesProperty =
            DependencyProperty.Register(nameof(BackupTimes), typeof(OffsetInterval), typeof(BackupTimesControl),
                new PropertyMetadata(OnBackupTimesPropertyChanged));

        private static void OnBackupTimesPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            BackupTimesControl s = (BackupTimesControl)sender;
            OffsetInterval newValue = (OffsetInterval)e.NewValue;

            DateTime? next = newValue.GetNextDateTime();
            s.dprNextDate.SelectedDate = next?.Date;
            s.tbxNextTime.Text = ConvertTimeSpanToStringShort(next?.TimeOfDay ?? TimeSpan.Zero);
            s.tbxInterval.Text = ConvertTimeSpanToStringShort(newValue.Interval);
        }

        public event EventHandler<RoutedEventArgs> Apply, Cancel;

        public OffsetInterval BackupTimes
        {
            get => (OffsetInterval)GetValue(BackupTimesProperty);
            set => SetValue(BackupTimesProperty, value);
        }

        public BackupTimesControl()
        {
            InitializeComponent();
        }

        private void TbxNextTime_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateOffsetAndInterval();
        }

        private void TbxNextTime_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) UpdateOffsetAndInterval();
        }

        private void TbxInterval_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateOffsetAndInterval();
        }

        private void TbxInterval_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) UpdateOffsetAndInterval();
        }

        private void UpdateOffsetAndInterval()
        {
            TimeSpan nextTime, interval;
            DateTime? nextDate = dprNextDate.SelectedDate;

            if (!nextDate.HasValue ||
                !TryConvertStringToTimeSpan(tbxNextTime.Text, out nextTime) ||
                !TryConvertStringToTimeSpan(tbxInterval.Text, out interval) ||
                interval.Ticks <= 0) return;

            DateTime next = nextDate.Value.Add(nextTime);
            TimeSpan offset = TimeSpan.FromTicks(next.Ticks % interval.Ticks);
            BackupTimes = new OffsetInterval(offset, interval);
        }

        public static bool TryConvertStringToTimeSpan(string s, out TimeSpan timeSpan)
        {
            int seconds = 0, minutes = 0, hours = 0, days = 0;
            string[] parts = s.Split(':').ReverseAsIList().ToArray();

            timeSpan = TimeSpan.FromTicks(0);

            if (parts.Length > 0) if (!int.TryParse(parts[0], out seconds)) return false;
            if (parts.Length > 1) if (!int.TryParse(parts[1], out minutes)) return false;
            if (parts.Length > 2) if (!int.TryParse(parts[2], out hours)) return false;
            if (parts.Length > 3) if (!int.TryParse(parts[3], out days)) return false;

            timeSpan = timeSpan.Add(TimeSpan.FromSeconds(seconds));
            timeSpan = timeSpan.Add(TimeSpan.FromMinutes(minutes));
            timeSpan = timeSpan.Add(TimeSpan.FromHours(hours));
            timeSpan = timeSpan.Add(TimeSpan.FromDays(days));

            return true;
        }

        public static string ConvertTimeSpanToStringShort(TimeSpan timeSpan)
        {
            string output = "";

            if (timeSpan.Days > 0) output += string.Format("{0:00}:", timeSpan.Days);
            if (timeSpan.Hours > 0 || output.Length > 0) output += string.Format("{0:00}:", timeSpan.Hours);

            output += string.Format("{0:00}:{1:00}", timeSpan.Minutes, timeSpan.Seconds);

            return output;
        }

        private void BtnApplyTimes_Click(object sender, RoutedEventArgs e)
        {
            Apply?.Invoke(this, e);
        }

        private void BtnCancelTimes_Click(object sender, RoutedEventArgs e)
        {
            Cancel?.Invoke(this, e);
        }
    }
}
