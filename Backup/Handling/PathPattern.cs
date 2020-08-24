using System;
using System.Collections.Generic;

namespace BackupApp.Backup.Handling
{
    public class PathPattern
    {
        enum SplitType
        {
            SingleAny,          // ?
            SingleOrMoreAny,   // |
            AnyCountAny,        // *
        }

        private readonly string[] parts;
        private readonly SplitType[] splits;

        public string Pattern { get; }

        public PathPattern(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                throw new ArgumentException("Value must not be null or empty.", nameof(pattern));
            }

            Pattern = pattern;

            bool isNew = true;
            List<string> parts = new List<string>();
            List<SplitType> splits = new List<SplitType>();


            foreach (char c in pattern)
            {
                if (isNew) parts.Add(string.Empty);
                isNew = false;

                switch (c)
                {
                    case '?':
                        splits.Add(SplitType.SingleAny);
                        isNew = true;
                        break;

                    case '|':
                        splits.Add(SplitType.SingleOrMoreAny);
                        isNew = true;
                        break;

                    case '*':
                        splits.Add(SplitType.AnyCountAny);
                        isNew = true;
                        break;

                    default:
                        parts[parts.Count - 1] += c;
                        break;
                }
            }

            this.parts = parts.ToArray();
            this.splits = splits.ToArray();
        }

        public bool Matches(string path)
        {
            int maxAnys = int.MaxValue;
            int index = 0;

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                {
                    int nextIndex = path.IndexOf(parts[i], index, StringComparison.OrdinalIgnoreCase);
                    if (nextIndex == -1 || nextIndex - index > maxAnys) return false;

                    index = nextIndex;
                }

                if (i == splits.Length) continue;

                switch (splits[i])
                {
                    case SplitType.SingleAny:
                        index++;
                        break;

                    case SplitType.SingleOrMoreAny:
                        index++;
                        maxAnys = int.MaxValue;
                        break;

                    case SplitType.AnyCountAny:
                        maxAnys = int.MaxValue;
                        break;
                }
            }

            return path.Length > index;
        }
    }
}
