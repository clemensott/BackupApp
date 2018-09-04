using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackupApp
{
    public class OffsetIntervalViewModel : OffsetInterval, INotifyPropertyChanged
    {
        string nextBackupTimeText, intervalText;

        private bool IsViewModel { get { return this == ViewModel.Current.BackupTimes; } }

        public DateTime NextDateTime
        {
            get { return GetNextDateTime(); }
            set
            {
                if (!SetOffset(value)) return;

                SetAutoNextTimeTextShort();

                UpdateNext();
            }
        }

        public DateTime NextDate
        {
            get { return NextDateTime.Date; }
            set
            {
                if (SetOffset(value.Add(NextDateTime.TimeOfDay))) return;

                UpdateNext();
            }
        }

        public string NextBackupTimeTextShort
        {
            get { return nextBackupTimeText; }
            set
            {
                TimeSpan timeSpan;

                if (value == nextBackupTimeText) return;

                nextBackupTimeText = value;

                if (TryConvertStringToTimeSpan(value, out timeSpan)) SetOffset(NextDate.Add(timeSpan));

                UpdateNext();
            }
        }

        public string TimeToNextBackupText
        {
            get { return ConvertTimeSpanToStringLong(NextDateTime - DateTime.Now); }
        }

        public string IntervalTextShort
        {
            get { return intervalText; }
            set
            {
                TimeSpan timeSpan;

                if (value == intervalText) return;

                intervalText = value;
                OnPropertyChanged("IntervalTextShort");

                if (!TryConvertStringToTimeSpan(value, out timeSpan)) return;

                Interval = timeSpan;
                SetAutoNextTimeTextShort();

                OnPropertyChanged("Interval");
                UpdateNext();
            }
        }

        public string IntervalTextLong { get { return ConvertTimeSpanToStringLong(Interval); } }

        public OffsetIntervalViewModel() : this(new OffsetInterval())
        {

        }

        public OffsetIntervalViewModel(OffsetInterval offsetInterval) : base(offsetInterval)
        {
            SetAutoNextTimeTextShort();
            SetAutoIntervalTextShort();
        }

        public static string ConvertTimeSpanToStringShort(TimeSpan timeSpan)
        {
            string output = "";

            if (timeSpan.Days > 0) output += string.Format("{0:00}:", timeSpan.Days);
            if (timeSpan.Hours > 0 || output.Length > 0) output += string.Format("{0:00}:", timeSpan.Hours);

            output += string.Format("{0:00}:{1:00}", timeSpan.Minutes, timeSpan.Seconds);

            return output;
        }

        public static string ConvertTimeSpanToStringLong(TimeSpan timeSpan)
        {
            string output = "";

            if (timeSpan.Days > 0) AddTextToTimeSpanStringLong(ref output, timeSpan.Days, "Day");
            if (timeSpan.Hours > 0) AddTextToTimeSpanStringLong(ref output, timeSpan.Hours, "Hour");
            if (timeSpan.Minutes > 0) AddTextToTimeSpanStringLong(ref output, timeSpan.Minutes, "Minute");
            if (timeSpan.Seconds > 0) AddTextToTimeSpanStringLong(ref output, timeSpan.Seconds, "Second");

            return output;
        }

        private static void AddTextToTimeSpanStringLong(ref string output, int value, string valueNameSingular)
        {
            AddTextToTimeSpanStringLong(ref output, value, valueNameSingular, valueNameSingular + "s");
        }

        private static void AddTextToTimeSpanStringLong(ref string output,
            int value, string valueNameSingular, string valueNamePlural)
        {
            output += output.Length == 0 ? "" : " ";
            output += value.ToString();
            output += " ";
            output += value == 1 ? valueNameSingular : valueNamePlural;
        }

        public static bool TryConvertStringToTimeSpan(string s, out TimeSpan timeSpan)
        {
            int seconds = 0, minutes = 0, hours = 0, days = 0;
            string[] parts = s.Split(':').Reverse().ToArray();

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

        private DateTime GetNextDateTime()
        {
            DateTime nowSub = DateTime.Now.Subtract(Offset);
            long times = nowSub.Ticks / Interval.Ticks + 1;

            var next = new DateTime(times * Interval.Ticks);
            return new DateTime(times * Interval.Ticks).Add(Offset);
        }

        private bool SetOffset(DateTime timeDateTime)
        {
            long offsetTicks = timeDateTime.Ticks % Interval.Ticks;
            TimeSpan newOffset = TimeSpan.FromTicks(offsetTicks);

            if (Offset == newOffset) return false;

            Offset = newOffset;

            return true;
        }

        public void SetAutoNextTimeTextShort()
        {
            NextBackupTimeTextShort = ConvertTimeSpanToStringShort(NextDateTime.TimeOfDay);
        }

        public void SetAutoIntervalTextShort()
        {
            IntervalTextShort = ConvertTimeSpanToStringShort(Interval);
        }

        public void UpdateNext()
        {
            if (!ViewModel.IsLoaded) return;

            OnPropertyChanged("NextBackupTimeTextShort");
            OnPropertyChanged("NextDate");
            OnPropertyChanged("NextDateTime");

            ViewModel.Current.UpdateNextBackupDateTimeWithIntervalText();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged == null) return;

            PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
    }
}
