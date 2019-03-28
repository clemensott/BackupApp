using System;
using System.ComponentModel;
using System.Linq;

namespace BackupApp
{
    public class OffsetIntervalViewModel : INotifyPropertyChanged
    {
        string nextBackupTimeText, intervalText;

        public OffsetInterval Base { get; set; }

        public long OffsetTicks
        {
            get => Base.OffsetTicks;
            set
            {
                if (value == Base.OffsetTicks) return;

                Base.OffsetTicks = value;
                OnPropertyChanged(nameof(Base.OffsetTicks));
            }
        }


        public long IntervalTicks
        {
            get => Base.IntervalTicks;
            set
            {
                if (value == Base.IntervalTicks) return;

                Base.IntervalTicks = value;
                OnPropertyChanged(nameof(Base.IntervalTicks));
            }
        }


        public TimeSpan Offset
        {
            get => Base.Offset;
            set
            {
                if (value == Base.Offset) return;

                Base.Offset = value;
                OnPropertyChanged(nameof(Base.Offset));
            }
        }


        public TimeSpan Interval
        {
            get => Base.Interval;
            set
            {
                if (value == Base.Interval) return;

                Base.Interval = value;
                OnPropertyChanged(nameof(Base.Interval));
            }
        }


        public DateTime Next
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
            get { return Next.Date; }
            set
            {
                if (SetOffset(value.Add(Next.TimeOfDay))) return;

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

        public string TimeToNextBackupText { get { return ConvertTimeSpanToStringLong(Next - DateTime.Now); } }

        public string IntervalTextShort
        {
            get { return intervalText; }
            set
            {
                TimeSpan timeSpan;

                if (value == intervalText) return;

                intervalText = value;
                OnPropertyChanged(nameof(IntervalTextShort));

                if (!TryConvertStringToTimeSpan(value, out timeSpan)) return;

                Interval = timeSpan;
                SetAutoNextTimeTextShort();

                OnPropertyChanged(nameof(Interval));
                UpdateNext();
            }
        }

        public string IntervalTextLong { get { return ConvertTimeSpanToStringLong(Interval); } }

        public OffsetIntervalViewModel() : this(new OffsetInterval())
        {
        }

        public OffsetIntervalViewModel(TimeSpan offset, TimeSpan interval) : this(new OffsetInterval(offset, interval))
        {
        }

        public OffsetIntervalViewModel(OffsetInterval offsetInterval)
        {
            Base = offsetInterval;

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
            NextBackupTimeTextShort = ConvertTimeSpanToStringShort(Next.TimeOfDay);
        }

        public void SetAutoIntervalTextShort()
        {
            IntervalTextShort = ConvertTimeSpanToStringShort(Interval);
        }

        public void UpdateNext()
        {
            OnPropertyChanged(nameof(NextBackupTimeTextShort));
            OnPropertyChanged(nameof(NextDate));
            OnPropertyChanged(nameof(Next));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public static explicit operator OffsetInterval(OffsetIntervalViewModel oivm)
        {
            return oivm.Base;
        }

        public static explicit operator OffsetIntervalViewModel(OffsetInterval io)
        {
            return new OffsetIntervalViewModel(io);
        }
    }
}
