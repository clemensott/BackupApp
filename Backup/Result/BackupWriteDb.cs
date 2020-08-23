using BackupApp.Helper;
using StdOttStandard.Linq.DataStructures;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Threading;
using System.Threading.Tasks;

namespace BackupApp.Backup.Result
{
    public class BackupWriteDb : IDisposable
    {
        private readonly SQLiteConnection connection;
        private readonly SemaphoreSlim writeSem;
        private long lastFolderIndex, lastFileIndex;
        private readonly IDictionary<string, long> fileIds;
        private readonly LockQueue<DbFolder> folders;
        private readonly LockQueue<DbFile> files;
        private readonly LockQueue<DbFolderFile> foldersFiles;
        private readonly LockQueue<long> flushedFolderIds, flushedFileIds;

        public bool Disposed { get; private set; }

        public string Path { get; }

        public Task FlushTask { get; private set; }

        private BackupWriteDb(SQLiteConnection connection, string path)
        {
            this.connection = connection;
            writeSem = new SemaphoreSlim(1);
            Path = path;

            lastFolderIndex = lastFileIndex = 0;
            fileIds = new Dictionary<string, long>();
            folders = new LockQueue<DbFolder>();
            files = new LockQueue<DbFile>();
            foldersFiles = new LockQueue<DbFolderFile>();
            flushedFolderIds = new LockQueue<long>();
            flushedFileIds = new LockQueue<long>();
        }

        public static async Task<BackupWriteDb> Create(string filePath)
        {
            SQLiteConnection connection = new SQLiteConnection($"Data Source={filePath};Version=3;New=True;");
            await connection.OpenAsync();
            await CreateTables(connection);

            BackupWriteDb db = new BackupWriteDb(connection, filePath);
            db.FlushTask = db.Flush();
            return db;
        }

        private static Task CreateTables(SQLiteConnection connection)
        {
            const string sql = @"
                CREATE TABLE folders
                (
                    id        INTEGER PRIMARY KEY,
                    name      TEXT    NOT NULL,
                    parent_id INTEGER,
                    FOREIGN KEY (parent_id) REFERENCES folders (id)
                );

                CREATE TABLE files
                (
                    id        INTEGER PRIMARY KEY,
                    hash      TEXT NOT NULL UNIQUE,
                    file_name TEXT NOT NULL UNIQUE
                );

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

            return connection.ExecuteNonQueryAsync(sql);
        }

        private Task AddedIndexies()
        {
            const string sql = @"
                CREATE INDEX folders_parent_id_idx ON folders (parent_id);
                CREATE INDEX folders_files_folder_id_idx ON folders_files (folder_id);
            ";

            return connection.ExecuteNonQueryAsync(sql);
        }

        private async Task Flush()
        {
            await Task.WhenAll(Task.Run(FlushFolders), Task.Run(FlushFiles), Task.Run(FlushFoldersFiles));

            if (!Disposed) await AddedIndexies();

            Dispose();
        }

        public long AddFolder(string name, long? parentId)
        {
            folders.Enqueue(new DbFolder(++lastFolderIndex, parentId, name));
            return lastFolderIndex;
        }

        public void AddFile(string name, string hash, string backupFileName, long folderId)
        {
            long fileId;
            lock (fileIds)
            {
                if (!fileIds.TryGetValue(hash, out fileId))
                {
                    fileId = ++lastFileIndex;
                    fileIds.Add(hash, fileId);

                    files.Enqueue(new DbFile(fileId, hash, backupFileName));
                }
            }

            foldersFiles.Enqueue(new DbFolderFile(folderId, fileId, name));
        }

        private async Task FlushFolders()
        {
            long lastId = 0;
            SqlStatementBuilder builder = new SqlStatementBuilder("INSERT INTO folders (id, name, parent_id) VALUES ");

            while (true)
            {
                DbFolder item;
                if (!folders.TryDequeue(out item))
                {
                    if (builder.DataCount > 0) await Execute();
                    break;
                }
                if (Disposed) break;

                string sql = $",(@id{builder.DataCount}, @name{builder.DataCount}, @parentId{builder.DataCount})";
                if (builder.DataCount == 0) sql = sql.TrimStart(',');

                builder.Add(sql, DbHelper.GetParam("@id" + builder.DataCount, item.ID),
                    DbHelper.GetParam("@name" + builder.DataCount, item.Name),
                    DbHelper.GetParam("@parentId" + builder.DataCount, item.ParentID));

                lastId = item.ID;

                if (builder.DataCount > 1000 || (builder.DataCount > 500 && folders.Count == 0)) await Execute();
            }

            flushedFolderIds.End();

            async Task Execute()
            {
                await ExecuteNonQueryAsync(builder);
                flushedFolderIds.Enqueue(lastId);
            }
        }

        private async Task FlushFiles()
        {
            long lastId = 0;
            SqlStatementBuilder builder = new SqlStatementBuilder("INSERT INTO files (id, hash, file_name) VALUES ");

            while (true)
            {
                DbFile item;
                if (!files.TryDequeue(out item))
                {
                    if (builder.DataCount > 0) await Execute();
                    break;
                }
                if (Disposed) break;

                string sql = $",(@id{builder.DataCount}, @hash{builder.DataCount}, @fileName{builder.DataCount})";
                if (builder.DataCount == 0) sql = sql.TrimStart(',');

                builder.Add(sql, DbHelper.GetParam("@id" + builder.DataCount, item.ID),
                    DbHelper.GetParam("@hash" + builder.DataCount, item.Hash),
                    DbHelper.GetParam("@fileName" + builder.DataCount, item.FileName));

                lastId = item.ID;

                if (builder.DataCount > 1000 || (builder.DataCount > 500 && files.Count == 0)) await Execute();
            }

            flushedFileIds.End();

            async Task Execute()
            {
                await ExecuteNonQueryAsync(builder);
                flushedFileIds.Enqueue(lastId);
            }
        }

        private async Task FlushFoldersFiles()
        {
            long lastFlushedFolderId = -1, lastAddedFolderId = -1, lastFlushedFileId = -1, lastAddedFileId = -1;
            SqlStatementBuilder builder = new SqlStatementBuilder("INSERT INTO folders_files (file_name, folder_id, file_id) VALUES ");

            while (true)
            {
                DbFolderFile item;
                if (!foldersFiles.TryDequeue(out item))
                {
                    if (builder.DataCount > 0) await Execute();
                    return;
                }
                if (Disposed) break;

                string sql = $",(@fileName{builder.DataCount}, @folderId{builder.DataCount}, @fileId{builder.DataCount})";
                if (builder.DataCount == 0) sql = sql.TrimStart(',');

                builder.Add(sql, DbHelper.GetParam("@fileName" + builder.DataCount, item.FileName),
                    DbHelper.GetParam("@folderId" + builder.DataCount, item.FolderID),
                    DbHelper.GetParam("@fileId" + builder.DataCount, item.FileID));

                lastAddedFolderId = item.FolderID;
                lastAddedFileId = item.FileID;

                if (builder.DataCount > 2000 || (builder.DataCount > 1000 && foldersFiles.Count == 0)) await Execute();
            }

            async Task Execute()
            {
                while (lastAddedFolderId > lastFlushedFolderId)
                {
                    (bool isEnd, long id) = flushedFolderIds.Dequeue();
                    if (isEnd) return;
                    else lastFlushedFolderId = id;
                }
                while (lastAddedFileId > lastFlushedFileId)
                {
                    (bool isEnd, long id) = flushedFileIds.Dequeue();
                    if (isEnd) return;
                    else lastFlushedFileId = id;
                }

                if (Disposed) return;
                await ExecuteNonQueryAsync(builder);
            }
        }

        private async Task ExecuteNonQueryAsync(SqlStatementBuilder builder)
        {
            (string sql, SQLiteParameter[] parameters) = builder.Reset();
            try
            {
                await writeSem.WaitAsync();
                if (!Disposed) await connection.ExecuteNonQueryAsync(sql, parameters);
            }
            catch
            {
                Dispose();
            }
            finally
            {
                writeSem.Release();
            }
        }

        public void Finish()
        {
            folders.End();
            files.End();
            foldersFiles.End();
        }

        public void Dispose()
        {
            if (Disposed) return;

            Finish();
            Disposed = true;
            connection.Dispose();
        }
    }
}
