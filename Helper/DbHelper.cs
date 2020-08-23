using StdOttStandard.Linq;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Threading.Tasks;

namespace BackupApp.Helper
{
    static class DbHelper
    {
        public static Task<int> ExecuteNonQueryAsync(this SQLiteConnection connection,
            string sql, IEnumerable<SQLiteParameter> parameters = null)
        {
            using (SQLiteCommand cmd = connection.CreateCommand(sql, parameters))
            {
                return cmd.ExecuteNonQueryAsync();
            }
        }

        public static Task<object> ExecuteScalarAsync(this SQLiteConnection connection,
            string sql, IEnumerable<SQLiteParameter> parameters = null)
        {
            using (SQLiteCommand cmd = connection.CreateCommand(sql, parameters))
            {
                return cmd.ExecuteScalarAsync();
            }
        }

        public static Task<DbDataReader> ExecuteReaderAsync(this SQLiteConnection connection, 
            string sql, IEnumerable<SQLiteParameter> parameters = null)
        {
            using (SQLiteCommand cmd = connection.CreateCommand(sql, parameters))
            {
                return cmd.ExecuteReaderAsync();
            }
        }

        public static SQLiteCommand CreateCommand(this SQLiteConnection connection,
            string sql, IEnumerable<SQLiteParameter> parameters = null)
        {
            SQLiteCommand cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            foreach (SQLiteParameter parameter in parameters.ToNotNull())
            {
                cmd.Parameters.Add(parameter);
            }
            return cmd;
        }

        public static SQLiteParameter GetParam(string name, string value)
        {
            return GetParam(name, DbType.String, value);
        }

        public static SQLiteParameter GetParam(string name, long? value)
        {
            return GetParam(name, DbType.Int64, value);
        }

        public static SQLiteParameter GetParam(string name, DbType type, object value)
        {
            return new SQLiteParameter(name, type)
            {
                Value = value
            };
        }
    }
}
