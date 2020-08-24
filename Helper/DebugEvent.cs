using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BackupApp
{
    public class DebugEvent
    {
        private const char partSplitter = ';', eventSplitter = '|', addChar = '&';
        private const string debugEventsFileName = "debug.txt";
        private const int maxLengthOfOneData = 1000;

        private static readonly object lockObj = new object();

        public bool IsChecked { get; }

        public long Time { get; }

        public int ThreadId { get; }

        public string Name { get; }

        public string[] Data { get; }

        private DebugEvent()
        {
            IsChecked = true;
            Time = DateTime.Now.Ticks;
            ThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
        }

        private DebugEvent(string name, object[] data) : this()
        {
            Name = name;

            Data = data.Select(ToString).ToArray();
        }

        public DebugEvent(string dataString) : this()
        {
            Time = long.Parse(GetUntil(ref dataString, partSplitter));
            Name = GetUntil(ref dataString, partSplitter);

            Data = Split(dataString, partSplitter).ToArray();
        }

        public static IEnumerable<DebugEvent> GetEvents(string eventsDataString)
        {
            var array = Split(eventsDataString, eventSplitter).ToArray();

            foreach (string eventDataString in array)
            {
                DebugEvent debugEvent = null;

                try
                {
                    debugEvent = new DebugEvent(eventDataString);
                }
                catch { }

                if (debugEvent != null) yield return debugEvent;
            }
        }

        private static IEnumerable<string> Split(string dataString, char seperator)
        {
            while (dataString.Length > 0)
            {
                yield return GetUntil(ref dataString, seperator);
            }
        }

        private static string GetUntil(ref string text, char seperator)
        {
            string part;
            int lenght = 0;

            bool isSeperatorChar, isAddChar;
            do
            {
                lenght++;

                if (lenght >= text.Length) break;

                isSeperatorChar = text.ElementAtOrDefault(lenght) == seperator;
                isAddChar = text.ElementAtOrDefault(lenght + 1) == addChar;

            } while (!isSeperatorChar || (isSeperatorChar && isAddChar));

            part = lenght < text.Length ? text.Remove(lenght) : text;
            text = text.Remove(0, lenght);

            if (text.Length != 0) text = text.Remove(0, 1);

            return part;
        }

        public static void SaveText(string name, params object[] data)
        {
            System.Diagnostics.Debug.WriteLine(name);
            DebugEvent debugEvent = new DebugEvent(name, data);

            Append(debugEvent.ToDataString());
        }

        private static void Append(string text)
        {
            lock (lockObj)
            {
                try
                {
                    using (StreamWriter writer = File.AppendText(debugEventsFileName))
                    {
                        writer.Write(text);
                    }
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("Save text error: " + e.Message);
                }
            }
        }

        public string ToDataString()
        {
            string dataString = string.Empty;

            AddToDataString(ref dataString, Time);
            AddToDataString(ref dataString, Name);
            AddToDataString(ref dataString, "ThreadId: " + ThreadId);

            foreach (string data in Data)
            {
                AddToDataString(ref dataString, data);
            }

            return dataString + eventSplitter;
        }

        private void AddToDataString(ref string dataString, object add)
        {
            dataString += add.ToString().Replace(partSplitter.ToString(), partSplitter.ToString() + addChar.ToString()).
                Replace(eventSplitter.ToString(), eventSplitter.ToString() + addChar.ToString()) + ";";
        }

        private string ToString(object obj)
        {
            long value;
            string text = obj.ToString();

            if (long.TryParse(text, out value) && value > TimeSpan.TicksPerDay * 10000)
            {
                return GetDateTimeString(value);
            }

            if (text.Length > maxLengthOfOneData) return text.Remove(maxLengthOfOneData);

            return text;
        }

        public static string GetDateTimeString(long ticks)
        {
            var dateTime = new DateTime(ticks);

            return string.Format("{0,2}.{1,2}.{2,4}", dateTime.Day, dateTime.Month, dateTime.Year).Replace(" ", "0")
                + " " + string.Format("{0,2}:{1,2}:{2,2},{3,3}", dateTime.Hour, dateTime.Minute,
                dateTime.Second, dateTime.Millisecond).Replace(" ", "0");
        }

        public override string ToString()
        {
            string output = string.Format("{0}\n{1}\n", GetDateTimeString(Time), Name);

            foreach (string data in Data) output += data + "\n";

            return output.TrimEnd('\n');
        }
    }

    static class ConvertDebugEvents
    {
        public static string GetDataEventsString(this IEnumerable<DebugEvent> events)
        {
            string dataString = string.Empty;

            foreach (DebugEvent debugEvent in events)
            {
                dataString += debugEvent.ToDataString();
            }

            return dataString;
        }
    }
}
