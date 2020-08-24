using BackupApp.Helper;
using StdOttStandard.Linq;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.Threading;
using System.Threading.Tasks;

namespace BackupApp.Restore
{
    class BackupReadDb : IDisposable
    {
        private int connectionCount;
        private SQLiteConnection connection;
        private readonly SemaphoreSlim sem;

        public string Path { get; }

        public BackupReadDb(string filePath)
        {
            sem = new SemaphoreSlim(1);
            Path = filePath;
        }

        public BackupFolder AsFolder()
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(Path);
            return new BackupFolder(null, name, this);
        }

        public async Task<List<BackupFolder>> GetFolders(long? parentId = null)
        {
            string sql = @"
                SELECT id, name
                FROM folders
                WHERE parent_id = @parentId
                ORDER BY name;
            ";
            IEnumerable<SQLiteParameter> parameters;

            if (parentId.HasValue)
            {
                sql = @"
                    SELECT id, name
                    FROM folders
                    WHERE parent_id = @parentId
                    ORDER BY name;
                ";
                parameters = GenerateUtils.ConcatParams(DbHelper.GetParam("@parentId", parentId));
            }
            else
            {
                sql = @"
                    SELECT id, name
                    FROM folders
                    WHERE parent_id IS NULL
                       OR parent_id = 0
                    ORDER BY name;
                ";
                parameters = null;
            }

            SQLiteConnection connection = await GetConnection(Path);
            try
            {
                DbDataReader reader = await connection.ExecuteReaderAsync(sql, parameters);

                int idIndex = reader.GetOrdinal("id");
                int nameIndex = reader.GetOrdinal("name");
                List<BackupFolder> nodes = new List<BackupFolder>();

                while (await reader.ReadAsync())
                {
                    long id = reader.GetInt64(idIndex);
                    string name = reader.GetString(nameIndex);

                    nodes.Add(new BackupFolder(id, name, this));
                }

                return nodes;
            }
            finally
            {
                ReleaseConnection();
            }
        }

        public async Task<List<BackupFile>> GetFiles(long? folderId)
        {
            const string sql = @"
                SELECT ff.file_name as fileName, f.hash, f.file_name as backupFileName
                FROM folders_files ff
                         JOIN files f on ff.file_id = f.id
                WHERE folder_id = @folderId
                ORDER BY ff.file_name;
            ";
            IEnumerable<SQLiteParameter> parameters = GenerateUtils.ConcatParams(DbHelper.GetParam("@folderId", folderId));

            SQLiteConnection connection = await GetConnection(Path);
            try
            {
                DbDataReader reader = await connection.ExecuteReaderAsync(sql, parameters);

                int nameIndex = reader.GetOrdinal("fileName");
                int hashIndex = reader.GetOrdinal("hash");
                int backupNameIndex = reader.GetOrdinal("backupFileName");
                List<BackupFile> nodes = new List<BackupFile>();

                while (await reader.ReadAsync())
                {
                    string name = reader.GetString(nameIndex);
                    string hash = reader.GetString(hashIndex);
                    string backupFileName = reader.GetString(backupNameIndex);

                    nodes.Add(new BackupFile(name, backupFileName, hash));
                }

                return nodes;
            }
            finally
            {
                ReleaseConnection();
            }
        }

        public async Task GetAllFiles(IDictionary<string, string> dict, CancelToken cancelToken = null)
        {
            const string sql = @"
                SELECT hash, file_name
                FROM files
                WHERE id in (SELECT file_id FROM folders_files);
            ";

            SQLiteConnection connection = await GetConnection(Path);
            try
            {
                if (cancelToken?.IsCanceled == true) return;

                using (DbDataReader reader = await connection.ExecuteReaderAsync(sql))
                {
                    if (cancelToken?.IsCanceled == true) return;

                    int hashIndex = reader.GetOrdinal("hash");
                    int fileNameIndex = reader.GetOrdinal("file_name");

                    while (await reader.ReadAsync() && cancelToken?.IsCanceled != true)
                    {
                        dict[reader.GetString(hashIndex)] = reader.GetString(fileNameIndex);
                    }
                }
            }
            finally
            {
                ReleaseConnection();
            }
        }

        private async Task<SQLiteConnection> GetConnection(string path)
        {
            await sem.WaitAsync();

            if (connection == null)
            {
                connection = new SQLiteConnection($"Data Source={path};Version=3;Read Only=True;", true);
                await connection.OpenAsync();
            }

            return connection;
        }

        private async void ReleaseConnection()
        {
            int currentCount = ++connectionCount;
            sem.Release();

            await Task.Delay(TimeSpan.FromSeconds(10));
            if (currentCount == connectionCount && sem.CurrentCount > 0)
            {
                connection?.Close();
                connection = null;
            }
        }

        public void Dispose()
        {
            connection?.Dispose();
            connection = null;
        }
    }
}
