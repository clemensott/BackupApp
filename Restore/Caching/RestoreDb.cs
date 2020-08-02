using BackupApp.Backup.Result;
using BackupApp.Helper;
using StdOttStandard.Linq;
using StdOttStandard.Linq.DataStructures.Enumerators;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackupApp.Restore.Caching
{
    public class RestoreDb : IDisposable
    {
        private readonly SQLiteConnection connection;

        private RestoreDb(SQLiteConnection connection)
        {
            this.connection = connection;
        }

        public static async Task<RestoreDb> Open(string filePath)
        {
            bool dbExists = File.Exists(filePath);
            SQLiteConnection connection = new SQLiteConnection($"Data Source={filePath}");
            await connection.OpenAsync();

            if (!dbExists) await CreateTables(connection);

            return new RestoreDb(connection);
        }

        private static Task CreateTables(SQLiteConnection connection)
        {
            const string sql = @"
                CREATE TABLE backups
                (
                    id   INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL
                );

                CREATE TABLE folders
                (
                    id        INTEGER PRIMARY KEY AUTOINCREMENT,
                    name      TEXT    NOT NULL,
                    parent_id INTEGER,
                    backup_id INTEGER NOT NULL,
                    FOREIGN KEY (parent_id) REFERENCES folders (id),
                    FOREIGN KEY (backup_id) REFERENCES backups (id)
                );

                CREATE TABLE files
                (
                    id        INTEGER PRIMARY KEY AUTOINCREMENT,
                    hash      TEXT NOT NULL UNIQUE,
                    file_name TEXT NOT NULL UNIQUE
                );

                CREATE INDEX files_hash_index ON files (hash);

                CREATE TABLE folders_files
                (
                    id        INTEGER PRIMARY KEY AUTOINCREMENT,
                    file_name TEXT    NOT NULL,
                    folder_id INTEGER NOT NULL,
                    file_id   INTEGER NOT NULL,
                    FOREIGN KEY (folder_id) REFERENCES folders (id),
                    FOREIGN KEY (file_id) REFERENCES files (id)
                );
            ";

            SQLiteCommand createCmd = connection.CreateCommand();
            createCmd.CommandText = sql;

            return createCmd.ExecuteNonQueryAsync();
        }

        public async Task InsertBackup(BackupModel backup, Action callback)
        {
            long? id = await GetBackupId(backup.Name);
            if (id.HasValue) return;

            long backupId = await InsertBackup(backup.Name);

            foreach (BackupFolder folder in backup.Folders)
            {
                await InsertFolder(folder, null, backupId, callback);
            }
        }

        private async Task InsertFolder(BackupFolder folder, long? parentId, long backupId, Action callback)
        {
            long folderId = await InsertFolder(folder.Name, parentId, backupId);

            foreach (BackupFile file in folder.Files)
            {
                await InsertFile(file.Base64Hash, file.Name, file.SourcePath, folderId);
                callback?.Invoke();
            }

            foreach (BackupFolder subFolder in folder.Folders)
            {
                await InsertFolder(subFolder, folderId, backupId, callback);
            }
        }

        public async Task<RestoreNode> InsertBackup(IEnumerable<string> lines, Action callback)
        {
            using (EndedEnumerator<string> enumerator = new EndedEnumerator<string>(lines.GetEnumerator()))
            {
                if (!enumerator.MoveNext()) throw new Exception("Invalid backup. No lines");
                callback?.Invoke();
                string ticksText = enumerator.Current;

                string name;
                if (!enumerator.MoveNext()) throw new Exception("Invalid backup. Only one line");
                callback?.Invoke();
                if (!BackupUtils.TryDecodeFolder(enumerator.Current, out _, out name))
                {
                    throw new Exception("Backup name is not valid");
                }

                long? id = await GetBackupId(name);
                if (id.HasValue) return RestoreNode.CreateBackup(id.Value, name);

                long backupId = await InsertBackup(name);

                if (!enumerator.MoveNext()) throw new Exception("Invalid backup. Only two lines");
                callback?.Invoke();
                await InsertFolder(enumerator, 1, null, backupId, callback);

                return RestoreNode.CreateBackup(backupId, name);
            }
        }

        private async Task InsertFolder(EndedEnumerator<string> enumerator,
            int depth, long? parentId, long backupId, Action callback)
        {
            int lineDepth;
            string folderName;

            while (!enumerator.Ended &&
                BackupUtils.TryDecodeFolder(enumerator.Current, out lineDepth, out folderName) &&
                lineDepth == depth)
            {
                long folderId = await InsertFolder(folderName, parentId, backupId);

                await InsertFiles(enumerator, folderId, callback);
                await InsertFolder(enumerator, depth + 1, folderId, backupId, callback);
            }
        }

        private async Task InsertFiles(EndedEnumerator<string> enumerator, long folderId, Action callback)
        {
            while (enumerator.MoveNext())
            {
                callback?.Invoke();
                if (enumerator.Current[0] != '>') break;

                string line = enumerator.Current.Substring(1);
                string[] parts = line.Split('|');

                string hash = parts[0];
                string sourcePath = parts[1];
                string fileName = parts[2];

                await InsertFile(hash, fileName, sourcePath, folderId);
            }
        }

        private async Task<long> InsertBackup(string name)
        {
            const string sql = @"
                INSERT INTO backups (name) VALUES (@name);
                SELECT last_insert_rowid();
            ";
            IEnumerable<SQLiteParameter> parmeters = GenerateUtils.ConcatParams(GetParam("@name", name));
            return (long)await ExecuteScalarAsync(sql, parmeters);
        }

        private async Task<long> InsertFolder(string name, long? parentId, long backupId)
        {
            string sql = @"
                INSERT INTO folders (name, parent_id, backup_id) VALUES (@name, @parentId, @backupId);
                SELECT last_insert_rowid();
            ";
            IEnumerable<SQLiteParameter> parmeters = GenerateUtils.ConcatParams(
                GetParam("@name", name), GetParam("@parentId", parentId), GetParam("@backupId", backupId));

            return (long)await ExecuteScalarAsync(sql, parmeters);
        }

        private async Task InsertFile(string hash, string fileName, string bakFileName, long folderId)
        {
            string sql;
            IEnumerable<SQLiteParameter> parmeters;
            long? fileId = await GetFileId(hash);

            if (fileId.HasValue)
            {
                sql = @"
                    INSERT INTO folders_files (file_name, folder_id, file_id)
                    VALUES(@fileName, @folderId, @fileId);
                ";
                parmeters = GenerateUtils.ConcatParams(GetParam("@fileName", fileName),
                    GetParam("@folderId", folderId), GetParam("@fileId", fileId.Value));
            }
            else
            {
                sql = @"
                    INSERT INTO files (hash, file_name)
                    VALUES (@hash, @bakName);
                    INSERT INTO folders_files (file_name, folder_id, file_id)
                    VALUES (@fileName, @folderId, last_insert_rowid());
                ";
                parmeters = GenerateUtils.ConcatParams(
                    GetParam("@hash", hash), GetParam("@bakName", bakFileName),
                    GetParam("@fileName", fileName), GetParam("@folderId", folderId));
            }

            await ExecuteNonQueryAsync(sql, parmeters);
        }

        private async Task<long?> GetBackupId(string name)
        {
            string sql = "SELECT id FROM backups WHERE name = @name";
            IEnumerable<SQLiteParameter> parmeters = GenerateUtils.ConcatParams(GetParam("@name", name));
            object id = await ExecuteScalarAsync(sql, parmeters);

            return id as long?;
        }

        private async Task<long?> GetFileId(string hash)
        {
            const string sql = "SELECT id FROM files WHERE hash = @hash;";
            IEnumerable<SQLiteParameter> parmeters = GenerateUtils.ConcatParams(GetParam("@hash", hash));
            object obj = await ExecuteScalarAsync(sql, parmeters);

            return obj as long?;
        }

        public async Task RemoveBackup(long backupId)
        {
            const string sql = @"
                DELETE
                FROM folders_files
                WHERE folder_id IN (SELECT id FROM folders WHERE backup_id = @backupId);
                DELETE
                FROM folders
                WHERE backup_id = @backupId;
                DELETE
                FROM backups
                WHERE id = @backupId;
            ";
            IEnumerable<SQLiteParameter> parmeters = GenerateUtils.ConcatParams(GetParam("@backupId", backupId));
            await ExecuteNonQueryAsync(sql, parmeters);
        }

        public async Task RemoveUnusedFiles()
        {
            const string sql = "DELETE FROM files WHERE id NOT IN (SELECT file_id FROM folders_files);";
            await ExecuteNonQueryAsync(sql);
        }

        public async Task<IDictionary<string, string>> GetAllFiles()
        {
            const string sql = @"
                SELECT hash, file_name
                FROM files
                WHERE id in (SELECT file_id FROM folders_files);
            ";
            IDictionary<string, string> dict = new Dictionary<string, string>();
            DbDataReader reader = await ExecuteReaderAsync(sql);

            int hashIndex = reader.GetOrdinal("hash");
            int fileNameIndex = reader.GetOrdinal("file_name");

            while (await reader.ReadAsync())
            {
                dict.Add(reader.GetString(hashIndex), reader.GetString(fileNameIndex));
            }

            return dict;
        }

        public async Task<IEnumerable<RestoreNode>> GetBackups()
        {
            const string sql = "SELECT id, name FROM backups;";
            DbDataReader reader = await ExecuteReaderAsync(sql);

            int idIndex = reader.GetOrdinal("id");
            int nameIndex = reader.GetOrdinal("name");
            List<RestoreNode> nodes = new List<RestoreNode>();

            while (await reader.ReadAsync())
            {
                long id = reader.GetInt64(idIndex);
                string name = reader.GetString(nameIndex);

                nodes.Add(RestoreNode.CreateBackup(id, name));
            }

            return nodes;
        }

        public async Task<IEnumerable<RestoreNode>> GetFolders(RestoreNode node)
        {
            string sql;
            IEnumerable<SQLiteParameter> parmeters;

            switch (node.Type)
            {
                case RestoreNodeType.Backup:
                    sql = @"
                        SELECT id, name
                        FROM folders
                        WHERE backup_id = @backupId
                          AND parent_id IS NULL
                        ORDER BY name;
                    ";
                    parmeters = GenerateUtils.ConcatParams(GetParam("@backupId", node.ID));
                    break;

                case RestoreNodeType.Folder:
                    sql = @"
                        SELECT id, name
                        FROM folders
                        WHERE parent_id = @parentId
                        ORDER BY name;
                    ";
                    parmeters = GenerateUtils.ConcatParams(GetParam("@parentId", node.ID));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(node.Type), "Type is not implemented");
            }

            DbDataReader reader = await ExecuteReaderAsync(sql, parmeters);

            int idIndex = reader.GetOrdinal("id");
            int nameIndex = reader.GetOrdinal("name");
            List<RestoreNode> nodes = new List<RestoreNode>();

            while (await reader.ReadAsync())
            {
                long id = reader.GetInt64(idIndex);
                string name = reader.GetString(nameIndex);

                nodes.Add(RestoreNode.CreateFolder(id, name));
            }

            return nodes;
        }

        public async Task<IEnumerable<RestoreFile>> GetFiles(RestoreNode node)
        {
            if (node.Type == RestoreNodeType.Backup) return null;

            const string sql = @"
                SELECT ff.file_name as fileName, f.hash, f.file_name as backupFileName
                FROM folders_files ff
                         JOIN files f on ff.file_id = f.id
                WHERE folder_id = @folderId
                ORDER BY ff.file_name;
            ";
            IEnumerable<SQLiteParameter> parmeters = GenerateUtils.ConcatParams(GetParam("@folderId", node.ID));
            DbDataReader reader = await ExecuteReaderAsync(sql, parmeters);

            int nameIndex = reader.GetOrdinal("fileName");
            int hashIndex = reader.GetOrdinal("hash");
            int backupNameIndex = reader.GetOrdinal("backupFileName");
            List<RestoreFile> nodes = new List<RestoreFile>();

            while (await reader.ReadAsync())
            {
                string name = reader.GetString(nameIndex);
                string hash = reader.GetString(hashIndex);
                string backupFileName = reader.GetString(backupNameIndex);

                nodes.Add(new RestoreFile(name, hash, backupFileName));
            }

            return nodes;
        }

        private Task ExecuteNonQueryAsync(string sql, IEnumerable<SQLiteParameter> paramerters = null)
        {
            using (SQLiteCommand cmd = connection.CreateCommand())
            {
                cmd.CommandText = sql;
                foreach (SQLiteParameter parameter in paramerters.ToNotNull())
                {
                    cmd.Parameters.Add(parameter);
                }

                return cmd.ExecuteNonQueryAsync();
            }
        }

        private Task<object> ExecuteScalarAsync(string sql, IEnumerable<SQLiteParameter> paramerters = null)
        {
            using (SQLiteCommand cmd = connection.CreateCommand())
            {
                cmd.CommandText = sql;
                foreach (SQLiteParameter parameter in paramerters.ToNotNull())
                {
                    cmd.Parameters.Add(parameter);
                }

                return cmd.ExecuteScalarAsync();
            }
        }

        private Task<DbDataReader> ExecuteReaderAsync(string sql, IEnumerable<SQLiteParameter> paramerters = null)
        {
            using (SQLiteCommand cmd = connection.CreateCommand())
            {
                cmd.CommandText = sql;
                foreach (SQLiteParameter parameter in paramerters.ToNotNull())
                {
                    cmd.Parameters.Add(parameter);
                }

                return cmd.ExecuteReaderAsync();
            }
        }

        public void Dispose()
        {
            connection.Dispose();
        }

        private static SQLiteParameter GetParam(string name, string value)
        {
            return GetParam(name, DbType.String, value);
        }

        private static SQLiteParameter GetParam(string name, long? value)
        {
            return GetParam(name, DbType.Int64, value);
        }

        private static SQLiteParameter GetParam(string name, DbType type, object value)
        {
            return new SQLiteParameter(name, type)
            {
                Value = value
            };
        }
    }
}
