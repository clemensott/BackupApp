using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackupApp.Helper
{
    class StreamReaderEnumerator
    {
        private readonly StreamReader reader;

        public bool EndOfStream => reader.EndOfStream;

        public string Current { get; private set; }

        public StreamReaderEnumerator(StreamReader reader)
        {
            this.reader = reader;
        }

        public bool MoveNext()
        {
            return ReadLine() != null;
        }

        public string ReadLine()
        {
            return Current = reader.ReadLine();
        }

        public async Task<bool> MoveNextAsync()
        {
            return (await ReadLineAsync()) != null;
        }

        public async Task<string> ReadLineAsync()
        {
            return Current = await reader.ReadLineAsync();
        }
    }
}
