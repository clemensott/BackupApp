using BackupApp.Backup.Result;
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
            string sql;
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

        public async Task ImportAllFiles(IDictionary<string, string> dict, CancelToken cancelToken = null)
        {
            const string sql = @"
                SELECT f.hash, f.file_name
                FROM files f
                         JOIN folders_files ff ON f.id = ff.file_id;
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

        public async Task<IList<DbFolder>> GetAllFolders(CancelToken cancelToken = null)
        {
            const string sql = @"
                SELECT id, name, parent_id
                FROM folders
                ORDER BY id;
            ";

            SQLiteConnection connection = await GetConnection(Path);
            try
            {
                if (cancelToken?.IsCanceled == true) return null;

                using (DbDataReader reader = await connection.ExecuteReaderAsync(sql))
                {
                    if (cancelToken?.IsCanceled == true) return null;

                    int idIndex = reader.GetOrdinal("id");
                    int nameIndex = reader.GetOrdinal("name");
                    int parentIdIndex = reader.GetOrdinal("parent_id");

                    List<DbFolder> folders = new List<DbFolder>();
                    while (await reader.ReadAsync())
                    {
                        if (cancelToken?.IsCanceled == true) return null;

                        long id = reader.GetInt64(idIndex);
                        string name = reader.GetString(nameIndex);
                        long? parentId = reader.IsDBNull(parentIdIndex) ? (long?)null : reader.GetInt64(parentIdIndex);
                        folders.Add(new DbFolder(id, parentId, name));
                    }

                    return folders;
                }
            }
            finally
            {
                ReleaseConnection();
            }
        }

        public async Task<IList<DbFile>> GetAllFiles(CancelToken cancelToken = null)
        {
            const string sql = @"
                SELECT id, hash, file_name
                FROM files
                ORDER BY id;
            ";

            SQLiteConnection connection = await GetConnection(Path);
            try
            {
                if (cancelToken?.IsCanceled == true) return null;

                using (DbDataReader reader = await connection.ExecuteReaderAsync(sql))
                {
                    if (cancelToken?.IsCanceled == true) return null;

                    int idIndex = reader.GetOrdinal("id");
                    int hashIndex = reader.GetOrdinal("hash");
                    int fileNameIndex = reader.GetOrdinal("file_name");

                    List<DbFile> files = new List<DbFile>();
                    while (await reader.ReadAsync())
                    {
                        if (cancelToken?.IsCanceled == true) return null;

                        long id = reader.GetInt64(idIndex);
                        string hash = reader.GetString(hashIndex);
                        string fileName = reader.GetString(fileNameIndex);

                        files.Add(new DbFile(id, hash, fileName));
                    }

                    return files;
                }
            }
            finally
            {
                ReleaseConnection();
            }
        }

        public async Task<IList<DbFolderFile>> GetAllFoldersFiles(CancelToken cancelToken = null)
        {
            const string sql = @"
                SELECT folder_id, file_id, file_name
                FROM folders_files
                ORDER BY folder_id, file_id;
            ";

            SQLiteConnection connection = await GetConnection(Path);
            try
            {
                if (cancelToken?.IsCanceled == true) return null;

                using (DbDataReader reader = await connection.ExecuteReaderAsync(sql))
                {
                    if (cancelToken?.IsCanceled == true) return null;

                    int folderIdIndex = reader.GetOrdinal("folder_id");
                    int fileIdIndex = reader.GetOrdinal("file_id");
                    int fileNameIndex = reader.GetOrdinal("file_name");

                    List<DbFolderFile> foldersFiles = new List<DbFolderFile>();
                    while (await reader.ReadAsync())
                    {
                        if (cancelToken?.IsCanceled == true) return null;

                        long folderId = reader.GetInt64(folderIdIndex);
                        long fileId = reader.GetInt64(fileIdIndex);
                        string fileName = reader.GetString(fileNameIndex);

                        foldersFiles.Add(new DbFolderFile(folderId, fileId, fileName));
                    }

                    return foldersFiles;
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
