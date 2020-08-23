using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackupApp.Backup.Result
{
    class SqlStatementBuilder
    {
        private readonly string sqlBegin;
        private readonly StringBuilder builder;
        private readonly List<SQLiteParameter> parameters;

        public int DataCount { get; private set; }

        public SqlStatementBuilder(string sqlBegin)
        {
            this.sqlBegin = sqlBegin;

            builder = new StringBuilder(sqlBegin);
            parameters = new List<SQLiteParameter>();
        }

        public (string sql, SQLiteParameter[] parameters) Reset()
        {
            (string sql, SQLiteParameter[] parameters) tuple = (builder.ToString(), parameters.ToArray());

            builder.Clear();
            builder.Append(sqlBegin);
            parameters.Clear();
            DataCount = 0;

            return tuple;
        }

        public void Add(string sql, params SQLiteParameter[] parameters)
        {
            builder.Append(sql);
            this.parameters.AddRange(parameters);
            DataCount++;
        }
    }
}
