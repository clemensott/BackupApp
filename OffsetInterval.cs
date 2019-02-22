using System;
using System.Xml.Serialization;

namespace BackupApp
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
    }
}
