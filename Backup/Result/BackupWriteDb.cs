using BackupApp.Helper;
using System;
using System.Collections.Generic;
using System.Data.SQLite;

namespace BackupApp.Backup.Result
{
    public class BackupWriteDb : IDisposable
    {
        private long lastFolderIndex, lastFileIndex;
        private readonly IDictionary<string, long> fileIds;
        private readonly SQLiteConnection connection;
        private readonly SQLiteTransaction transaction;
        private readonly SQLiteCommand insertFoldersCmd, insertFilesCmd, insertFoldersFilesCmd;

        public string Path { get; }

        public BackupWriteDb(string path)
        {
            connection = new SQLiteConnection($"Data Source={path};Version=3;New=True;").OpenAndReturn();
            Path = path;

            lastFolderIndex = lastFileIndex = 0;

            transaction = connection.BeginTransaction();
            CreateTablesCmd();

            insertFoldersCmd = new SQLiteCommand(
                "INSERT INTO folders (id, name, parent_id) VALUES (@id, @name, @parentId);",
                connection,
                transaction);

            insertFilesCmd = new SQLiteCommand(
                "INSERT INTO files (id, hash, file_name) VALUES (@id, @hash, @fileName);",
                connection,
                transaction);

            insertFoldersFilesCmd = new SQLiteCommand(
                "INSERT INTO folders_files (file_name, folder_id, file_id) VALUES (@fileName, @folderId, @fileId);",
                connection,
                transaction);

            fileIds = new Dictionary<string, long>();
        }

        private void CreateTablesCmd()
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

            SQLiteCommand cmd = new SQLiteCommand(sql, connection, transaction);
            cmd.ExecuteNonQuery();
        }

        private void AddedIndexiesCmd()
        {
            const string sql = @"
                CREATE INDEX folders_parent_id_idx ON folders (parent_id);
                CREATE INDEX folders_files_folder_id_idx ON folders_files (folder_id);
            ";

            SQLiteCommand cmd = new SQLiteCommand(sql, connection, transaction);
            cmd.ExecuteNonQuery();
        }

        public void Commit()
        {
            try
            {
                AddedIndexiesCmd();
                transaction.Commit();
            }
            finally
            {
                Dispose();
            }
        }

        public long AddFolder(string name, long? parentId)
        {
            lock (insertFoldersCmd)
            {
                insertFoldersCmd.Parameters.Add(DbHelper.GetParam("@id", ++lastFolderIndex));
                insertFoldersCmd.Parameters.Add(DbHelper.GetParam("@name", name));
                insertFoldersCmd.Parameters.Add(DbHelper.GetParam("@parentId", parentId));
                insertFoldersCmd.ExecuteNonQuery();

                return lastFolderIndex;
            }
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

                    insertFilesCmd.Parameters.Add(DbHelper.GetParam("@id", fileId));
                    insertFilesCmd.Parameters.Add(DbHelper.GetParam("@hash", hash));
                    insertFilesCmd.Parameters.Add(DbHelper.GetParam("@fileName", backupFileName));
                    insertFilesCmd.ExecuteNonQuery();
                }

                insertFoldersFilesCmd.Parameters.Add(DbHelper.GetParam("@fileName", name));
                insertFoldersFilesCmd.Parameters.Add(DbHelper.GetParam("@folderId", folderId));
                insertFoldersFilesCmd.Parameters.Add(DbHelper.GetParam("@fileId", fileId));
                insertFoldersFilesCmd.ExecuteNonQuery();
            }
        }

        public void Dispose()
        {
            fileIds.Clear();
            transaction?.Dispose();

            insertFoldersCmd?.Dispose();
            insertFilesCmd?.Dispose();
            insertFoldersFilesCmd?.Dispose();

            connection?.Close();

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}
