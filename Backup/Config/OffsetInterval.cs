using System;
using System.Xml.Serialization;

namespace BackupApp.Backup.Config
{
    public class OffsetInterval
    {
        private TimeSpan interval = TimeSpan.FromTicks(1);

        public long OffsetTicks
        {
            get { return Offset.Ticks; }
            set { Offset = TimeSpan.FromTicks(value); }
        }

        public long IntervalTicks
        {
            get { return Interval.Ticks; }
            set { Interval = TimeSpan.FromTicks(value); }
        }

        [XmlIgnore]
        public TimeSpan Offset { get; set; }

        [XmlIgnore]
        public TimeSpan Interval
        {
            get { return interval; }
            set
            {
                if (value.Ticks == 0) return;

                interval = value;
            }
        }

        public OffsetInterval() : this(TimeSpan.Zero, TimeSpan.FromDays(1))
        {
        }

        public OffsetInterval(OffsetInterval oi) : this(oi.Offset, oi.Interval)
        {
        }

        public OffsetInterval(TimeSpan offset, TimeSpan interval)
        {
            Offset = offset;
            Interval = interval;
        }

        public DateTime? GetNextDateTime()
        {
            if (Interval.Ticks <= 0) return null;

            DateTime nowSub = DateTime.Now.Subtract(Offset);
            long times = (long)Math.Ceiling(nowSub.Ticks / (double)Interval.Ticks);

            return new DateTime(times * Interval.Ticks).Add(Offset);
        }
    }
}
