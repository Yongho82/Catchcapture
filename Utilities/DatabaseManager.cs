using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using CatchCapture.Models;

namespace CatchCapture.Utilities
{
    public class DatabaseManager
    {
        private static DatabaseManager? _instance;
        public static DatabaseManager Instance => _instance ??= new DatabaseManager();

        private string DefaultDbPath => Path.Combine(Settings.Load().NoteStoragePath, "notedb", "catch_notes.db");
        private string DbPath => DefaultDbPath;
        public string DbFilePath => DbPath;

        private DatabaseManager()
        {
            InitializeDatabase();
            Settings.SettingsChanged += (s, e) => {
                InitializeDatabase();
            };
        }

        public void Reload() => InitializeDatabase();

        private void InitializeDatabase()
        {
            string dbDir = Path.GetDirectoryName(DbPath)!;
            string rootDir = Path.GetDirectoryName(dbDir)!; // One level up from notedb

            if (!Directory.Exists(dbDir)) Directory.CreateDirectory(dbDir);

            // Create img directory at root (outside notedb)
            string imgDir = Path.Combine(rootDir, "img");
            if (!Directory.Exists(imgDir)) Directory.CreateDirectory(imgDir);

            // Create attachments directory at root
            string attachDir = Path.Combine(rootDir, "attachments");
            if (!Directory.Exists(attachDir)) Directory.CreateDirectory(attachDir);

            using (var connection = new SqliteConnection($"Data Source={DbPath}"))
            {
                connection.Open();

                // Create Notes table
                string createNotesTable = @"
                    CREATE TABLE IF NOT EXISTS Notes (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Title TEXT,
                        Content TEXT,
                        SourceApp TEXT,
                        SourceUrl TEXT,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                        UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                        IsFavorite INTEGER DEFAULT 0,
                        Status INTEGER DEFAULT 0, -- 0: Active, 1: Trash
                        PasswordHash TEXT
                    );";

                // Create Images table (for multi-screenshot support)
                string createImagesTable = @"
                    CREATE TABLE IF NOT EXISTS NoteImages (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        NoteId INTEGER,
                        FilePath TEXT,
                        OrderIndex INTEGER,
                        FOREIGN KEY(NoteId) REFERENCES Notes(Id) ON DELETE CASCADE
                    );";

                // Create Tags table
                string createTagsTable = @"
                    CREATE TABLE IF NOT EXISTS Tags (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT UNIQUE
                    );";

                // Create NoteTags mapping table
                string createNoteTagsTable = @"
                    CREATE TABLE IF NOT EXISTS NoteTags (
                        NoteId INTEGER,
                        TagId INTEGER,
                        PRIMARY KEY(NoteId, TagId),
                        FOREIGN KEY(NoteId) REFERENCES Notes(Id) ON DELETE CASCADE,
                        FOREIGN KEY(TagId) REFERENCES Tags(Id) ON DELETE CASCADE
                    );";

                // Create Attachments table
                string createAttachmentsTable = @"
                    CREATE TABLE IF NOT EXISTS NoteAttachments (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        NoteId INTEGER,
                        FilePath TEXT,
                        FileType TEXT,
                        OriginalName TEXT,
                        FOREIGN KEY(NoteId) REFERENCES Notes(Id) ON DELETE CASCADE
                    );";

                // Create Categories table
                string createCategoriesTable = @"
                    CREATE TABLE IF NOT EXISTS Categories (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT UNIQUE NOT NULL,
                        Color TEXT
                    );";

                // Create Configuration table
                string createConfigTable = @"
                    CREATE TABLE IF NOT EXISTS Configuration (
                        Key TEXT PRIMARY KEY,
                        Value TEXT
                    );";

                using (var command = new SqliteCommand(createNotesTable, connection)) { command.ExecuteNonQuery(); }
                using (var command = new SqliteCommand(createImagesTable, connection)) { command.ExecuteNonQuery(); }
                using (var command = new SqliteCommand(createTagsTable, connection)) { command.ExecuteNonQuery(); }
                using (var command = new SqliteCommand(createNoteTagsTable, connection)) { command.ExecuteNonQuery(); }
                using (var command = new SqliteCommand(createAttachmentsTable, connection)) { command.ExecuteNonQuery(); }
                using (var command = new SqliteCommand(createCategoriesTable, connection)) { command.ExecuteNonQuery(); }
                using (var command = new SqliteCommand(createConfigTable, connection)) { command.ExecuteNonQuery(); }

                // Migration: Add CategoryId to Notes
                try
                {
                    using (var command = new SqliteCommand("ALTER TABLE Notes ADD COLUMN CategoryId INTEGER DEFAULT 1;", connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
                catch { /* Column might already exist */ }

                // Migration: Add ContentXaml to Notes
                try
                {
                    using (var command = new SqliteCommand("ALTER TABLE Notes ADD COLUMN ContentXaml TEXT;", connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
                catch { /* Column might already exist */ }

                // Migration: Add IsPinned to Notes
                try
                {
                    using (var command = new SqliteCommand("ALTER TABLE Notes ADD COLUMN IsPinned INTEGER DEFAULT 0;", connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
                catch { /* Column might already exist */ }

                // Migration: Add DeletedAt to Notes
                try
                {
                    using (var command = new SqliteCommand("ALTER TABLE Notes ADD COLUMN DeletedAt DATETIME;", connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
                catch { /* Column might already exist */ }

                // Migration: Add FileHash to NoteImages
                try
                {
                    using (var command = new SqliteCommand("ALTER TABLE NoteImages ADD COLUMN FileHash TEXT;", connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
                catch { /* Column might already exist */ }

                // Insert Default Categories if empty
                string checkCategories = "SELECT COUNT(*) FROM Categories;";
                using (var command = new SqliteCommand(checkCategories, connection))
                {
                    long count = (long)command.ExecuteScalar()!;
                    if (count == 0)
                    {
                        string insertDefaults = @"
                            INSERT INTO Categories (Name, Color) VALUES ('기본', '#8E2DE2');
                            INSERT INTO Categories (Name, Color) VALUES ('커뮤니티', '#3498DB');
                            INSERT INTO Categories (Name, Color) VALUES ('업무', '#E67E22');";
                        using (var insertCmd = new SqliteCommand(insertDefaults, connection))
                        {
                            insertCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        public string GetImageFolderPath()
        {
            string dbDir = Path.GetDirectoryName(DbPath)!;
            string rootDir = Path.GetDirectoryName(dbDir)!;
            return Path.Combine(rootDir, "img");
        }
        
        public string GetAttachmentsFolderPath()
        {
            string dbDir = Path.GetDirectoryName(DbPath)!;
            string rootDir = Path.GetDirectoryName(dbDir)!;
            return Path.Combine(rootDir, "attachments");
        }

        public string GetYearSubFolder()
        {
            return DateTime.Now.Year.ToString();
        }

        public string EnsureYearFolderExists(string rootPath)
        {
            string yearFolder = GetYearSubFolder();
            string fullPath = Path.Combine(rootPath, yearFolder);
            if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);
            return yearFolder;
        }

        public void Vacuum()
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DbPath}"))
                {
                    connection.Open();
                    using (var command = new SqliteCommand("VACUUM;", connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB VACUUM 오류: {ex.Message}");
            }
        }

        public void CleanupTrash(int daysLimit = 30)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={DbPath}"))
                {
                    connection.Open();
                    
                    // 1. Get notes to permanently delete
                    var notesToDelete = new List<long>();
                    string findSql = "SELECT Id FROM Notes WHERE Status = 1 AND DeletedAt <= datetime('now', 'localtime', '-' || $days || ' days')";
                    using (var cmd = new SqliteCommand(findSql, connection))
                    {
                        cmd.Parameters.AddWithValue("$days", daysLimit);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read()) notesToDelete.Add(reader.GetInt64(0));
                        }
                    }

                    if (notesToDelete.Count == 0) return;

                    // 2. For each note, perform hard delete (using existing logic or similar)
                    // Since NoteExplorerWindow has the file deletion logic, maybe we should move it here or centralize it.
                    // For now, let's implement file deletion here too for automation.
                    
                    string imgDir = GetImageFolderPath();
                    string attachDir = GetAttachmentsFolderPath();

                    foreach (var noteId in notesToDelete)
                    {
                        // Get images
                        using (var cmd = new SqliteCommand("SELECT FilePath FROM NoteImages WHERE NoteId = $id", connection))
                        {
                            cmd.Parameters.AddWithValue("$id", noteId);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string fileName = reader.GetString(0);
                                    // Delete only if no other note references this file
                                    if (!IsImageReferenced(fileName, noteId))
                                    {
                                        try { File.Delete(Path.Combine(imgDir, fileName)); } catch { }
                                    }
                                }
                            }
                        }

                        // Get attachments
                        using (var cmd = new SqliteCommand("SELECT FilePath FROM NoteAttachments WHERE NoteId = $id", connection))
                        {
                            cmd.Parameters.AddWithValue("$id", noteId);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    string fileName = reader.GetString(0);
                                    if (!IsAttachmentReferenced(fileName, noteId))
                                    {
                                        try { File.Delete(Path.Combine(attachDir, fileName)); } catch { }
                                    }
                                }
                            }
                        }

                        // Delete from DB
                        using (var cmd = new SqliteCommand("DELETE FROM Notes WHERE Id = $id", connection))
                        {
                            cmd.Parameters.AddWithValue("$id", noteId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Cleanup orphaned tags
                    using (var cmd = new SqliteCommand("DELETE FROM Tags WHERE Id NOT IN (SELECT DISTINCT TagId FROM NoteTags)", connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"휴지통 자동 비우기 오류: {ex.Message}");
            }
        }

        public void AddNoteImage(long noteId, string filePath, int orderIndex, string? fileHash = null)
        {
            using (var connection = new SqliteConnection($"Data Source={DbPath}"))
            {
                connection.Open();
                string sql = "INSERT INTO NoteImages (NoteId, FilePath, OrderIndex, FileHash) VALUES ($noteId, $filePath, $orderIndex, $fileHash);";
                using (var command = new SqliteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("$noteId", noteId);
                    command.Parameters.AddWithValue("$filePath", filePath);
                    command.Parameters.AddWithValue("$orderIndex", orderIndex);
                    command.Parameters.AddWithValue("$fileHash", (object?)fileHash ?? DBNull.Value);
                    command.ExecuteNonQuery();
                }
            }
        }

        public long InsertNote(string title, string content, string contentXaml, string tags, string fileName, string? sourceApp, string? sourceUrl, long categoryId = 1)
        {
            using (var connection = new SqliteConnection($"Data Source={DbPath}"))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Insert Note
                        string insertNoteSql = @"
                            INSERT INTO Notes (Title, Content, ContentXaml, SourceApp, SourceUrl, CreatedAt, UpdatedAt, CategoryId)
                            VALUES ($title, $content, $contentXaml, $sourceApp, $sourceUrl, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, $categoryId);
                            SELECT last_insert_rowid();";
                        
                        long noteId;
                        using (var command = new SqliteCommand(insertNoteSql, connection, transaction))
                        {
                            command.Parameters.AddWithValue("$title", title);
                            command.Parameters.AddWithValue("$content", content);
                            command.Parameters.AddWithValue("$contentXaml", contentXaml);
                            command.Parameters.AddWithValue("$sourceApp", (object?)sourceApp ?? DBNull.Value);
                            command.Parameters.AddWithValue("$sourceUrl", (object?)sourceUrl ?? DBNull.Value);
                            command.Parameters.AddWithValue("$categoryId", categoryId);
                            noteId = Convert.ToInt64(command.ExecuteScalar());
                        }
 
                        // 2. Insert Main Image (only if provided)
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            string? hash = null;
                            string imgDir = GetImageFolderPath();
                            string fullPath = Path.Combine(imgDir, fileName);
                            if (File.Exists(fullPath))
                            {
                                hash = ComputeHash(fullPath);
                                // Check if this hash already exists to potentially reuse file (Duplicate Prevention)
                                string? existingFile = GetExistingImageByHash(hash);
                                if (existingFile != null && existingFile != fileName)
                                {
                                    // Reuse existing file and delete the current temporary one if it's different
                                    try { File.Delete(fullPath); } catch { }
                                    fileName = existingFile;
                                }
                            }

                            string insertImageSql = @"
                                INSERT INTO NoteImages (NoteId, FilePath, OrderIndex, FileHash)
                                VALUES ($noteId, $filePath, 0, $hash);";
                            
                            using (var command = new SqliteCommand(insertImageSql, connection, transaction))
                            {
                                command.Parameters.AddWithValue("$noteId", noteId);
                                command.Parameters.AddWithValue("$filePath", fileName);
                                command.Parameters.AddWithValue("$hash", (object?)hash ?? DBNull.Value);
                                command.ExecuteNonQuery();
                            }
                        }

                        // 3. Handle Tags
                        if (!string.IsNullOrWhiteSpace(tags))
                        {
                            var tagList = tags.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var tagName in tagList)
                            {
                                string cleanTagName = tagName.Trim().ToLower();
                                if (string.IsNullOrEmpty(cleanTagName)) continue;

                                // Upsert Tag
                                string upsertTagSql = @"
                                    INSERT OR IGNORE INTO Tags (Name) VALUES ($name);
                                    SELECT Id FROM Tags WHERE Name = $name;";
                                
                                long tagId;
                                using (var command = new SqliteCommand(upsertTagSql, connection, transaction))
                                {
                                    command.Parameters.AddWithValue("$name", cleanTagName);
                                    tagId = (long)command.ExecuteScalar()!;
                                }

                                // Link Tag to Note
                                string linkTagSql = @"
                                    INSERT OR IGNORE INTO NoteTags (NoteId, TagId) VALUES ($noteId, $tagId);";
                                
                                using (var command = new SqliteCommand(linkTagSql, connection, transaction))
                                {
                                    command.Parameters.AddWithValue("$noteId", noteId);
                                    command.Parameters.AddWithValue("$tagId", tagId);
                                    command.ExecuteNonQuery();
                                }
                            }
                        }

                        transaction.Commit();
                        return noteId;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public void UpdateNote(long noteId, string title, string content, string contentXaml, string tags, long categoryId)
        {
            using (var connection = new SqliteConnection($"Data Source={DbPath}"))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        string updateSql = @"
                            UPDATE Notes 
                            SET Title = $title, Content = $content, ContentXaml = $contentXaml, CategoryId = $categoryId, UpdatedAt = CURRENT_TIMESTAMP
                            WHERE Id = $id";
                        
                        using (var command = new SqliteCommand(updateSql, connection, transaction))
                        {
                            command.Parameters.AddWithValue("$title", title);
                            command.Parameters.AddWithValue("$content", content);
                            command.Parameters.AddWithValue("$contentXaml", contentXaml);
                            command.Parameters.AddWithValue("$categoryId", categoryId);
                            command.Parameters.AddWithValue("$id", noteId);
                            command.ExecuteNonQuery();
                        }

                        string deleteTagsSql = "DELETE FROM NoteTags WHERE NoteId = $noteId";
                        using (var command = new SqliteCommand(deleteTagsSql, connection, transaction))
                        {
                            command.Parameters.AddWithValue("$noteId", noteId);
                            command.ExecuteNonQuery();
                        }

                        if (!string.IsNullOrWhiteSpace(tags))
                        {
                            var tagList = tags.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var tagName in tagList)
                            {
                                string cleanTagName = tagName.Trim().ToLower();
                                if (string.IsNullOrEmpty(cleanTagName)) continue;

                                string upsertTagSql = @"
                                    INSERT OR IGNORE INTO Tags (Name) VALUES ($name);
                                    SELECT Id FROM Tags WHERE Name = $name;";
                                
                                long tagId;
                                using (var command = new SqliteCommand(upsertTagSql, connection, transaction))
                                {
                                    command.Parameters.AddWithValue("$name", cleanTagName);
                                    tagId = (long)command.ExecuteScalar()!;
                                }

                                string linkTagSql = "INSERT OR IGNORE INTO NoteTags (NoteId, TagId) VALUES ($noteId, $tagId);";
                                using (var command = new SqliteCommand(linkTagSql, connection, transaction))
                                {
                                    command.Parameters.AddWithValue("$noteId", noteId);
                                    command.Parameters.AddWithValue("$tagId", tagId);
                                    command.ExecuteNonQuery();
                                }
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        public void InsertAttachment(long noteId, string filePath, string originalName)
        {
            using (var connection = new SqliteConnection($"Data Source={DbPath}"))
            {
                connection.Open();
                string sql = @"
                    INSERT INTO NoteAttachments (NoteId, FilePath, OriginalName, FileType)
                    VALUES ($noteId, $filePath, $originalName, $fileType);";
                
                using (var command = new SqliteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("$noteId", noteId);
                    command.Parameters.AddWithValue("$filePath", filePath);
                    command.Parameters.AddWithValue("$originalName", originalName);
                    command.Parameters.AddWithValue("$fileType", Path.GetExtension(originalName).ToLower());
                    command.ExecuteNonQuery();
                }
            }
        }
        public List<string> GetAllTags()
        {
            var tags = new List<string>();
            using (var connection = new SqliteConnection($"Data Source={DbPath}"))
            {
                connection.Open();
                string sql = "SELECT Name FROM Tags ORDER BY Name ASC";
                using (var command = new SqliteCommand(sql, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tags.Add(reader.GetString(0));
                    }
                }
            }
            return tags;
        }

        public List<Category> GetAllCategories()
        {
            var categories = new List<Category>();
            using (var connection = new SqliteConnection($"Data Source={DbPath}"))
            {
                connection.Open();
                string sql = "SELECT Id, Name, Color FROM Categories ORDER BY Id ASC";
                using (var command = new SqliteCommand(sql, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        categories.Add(new Category
                        {
                            Id = reader.GetInt64(0),
                            Name = reader.GetString(1),
                            Color = reader.IsDBNull(2) ? "#8E2DE2" : reader.GetString(2)
                        });
                    }
                }
            }
            return categories;
        }

        public void InsertCategory(string name, string color)
        {
            using (var connection = new SqliteConnection($"Data Source={DbPath}"))
            {
                connection.Open();
                string sql = "INSERT OR IGNORE INTO Categories (Name, Color) VALUES ($name, $color);";
                using (var command = new SqliteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("$name", name);
                    command.Parameters.AddWithValue("$color", color);
                    command.ExecuteNonQuery();
                }
            }
        }

        public void DeleteCategory(long id)
        {
            if (id == 1) return; // Prevent deleting default category

            using (var connection = new SqliteConnection($"Data Source={DbPath}"))
            {
                connection.Open();
                // Move notes to default category (Id=1) before deleting
                string updateSql = "UPDATE Notes SET CategoryId = 1 WHERE CategoryId = $id;";
                using (var command = new SqliteCommand(updateSql, connection))
                {
                    command.Parameters.AddWithValue("$id", id);
                    command.ExecuteNonQuery();
                }

                string deleteSql = "DELETE FROM Categories WHERE Id = $id;";
                using (var command = new SqliteCommand(deleteSql, connection))
                {
                    command.Parameters.AddWithValue("$id", id);
                    command.ExecuteNonQuery();
                }
            }
        }
        public Category? GetCategory(long id)
        {
            using (var connection = new SqliteConnection($"Data Source={DbPath}"))
            {
                connection.Open();
                string sql = "SELECT Id, Name, Color FROM Categories WHERE Id = $id";
                using (var command = new SqliteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("$id", id);
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new Category
                            {
                                Id = reader.GetInt64(0),
                                Name = reader.GetString(1),
                                Color = reader.IsDBNull(2) ? "#8E2DE2" : reader.GetString(2)
                            };
                        }
                    }
                }
            }
            return null;
        }
        public string? GetConfig(string key)
        {
            using (var connection = new SqliteConnection($"Data Source={DbPath}"))
            {
                connection.Open();
                using (var command = new SqliteCommand("SELECT Value FROM Configuration WHERE Key = @key", connection))
                {
                    command.Parameters.AddWithValue("@key", key);
                    var value = command.ExecuteScalar();
                    return value?.ToString();
                }
            }
        }

        public void SetConfig(string key, string? value)
        {
            using (var connection = new SqliteConnection($"Data Source={DbPath}"))
            {
                connection.Open();
                using (var command = new SqliteCommand(@"
                    INSERT INTO Configuration (Key, Value) VALUES (@key, @value)
                    ON CONFLICT(Key) DO UPDATE SET Value = @value", connection))
                {
                    command.Parameters.AddWithValue("@key", key);
                    command.Parameters.AddWithValue("@value", (object?)value ?? DBNull.Value);
                    command.ExecuteNonQuery();
                }
            }
        }

        public string? GetExistingImageByHash(string hash)
        {
            if (string.IsNullOrEmpty(hash)) return null;
            using (var connection = new SqliteConnection($"Data Source={DbPath}"))
            {
                connection.Open();
                string sql = "SELECT FilePath FROM NoteImages WHERE FileHash = $hash LIMIT 1";
                using (var command = new SqliteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("$hash", hash);
                    var result = command.ExecuteScalar();
                    if (result != null)
                    {
                        string filePath = result.ToString()!;
                        if (File.Exists(Path.Combine(GetImageFolderPath(), filePath)))
                        {
                            return filePath;
                        }
                    }
                }
            }
            return null;
        }

        public string ComputeHash(string filePath)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        public bool IsImageReferenced(string fileName, long excludeNoteId = -1)
        {
            using (var connection = new SqliteConnection($"Data Source={DbPath}"))
            {
                connection.Open();
                string sql = "SELECT COUNT(*) FROM NoteImages WHERE FilePath = $path AND NoteId != $excludeId";
                using (var cmd = new SqliteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("$path", fileName);
                    cmd.Parameters.AddWithValue("$excludeId", excludeNoteId);
                    return (long)cmd.ExecuteScalar()! > 0;
                }
            }
        }

        public bool IsAttachmentReferenced(string fileName, long excludeNoteId = -1)
        {
            using (var connection = new SqliteConnection($"Data Source={DbPath}"))
            {
                connection.Open();
                string sql = "SELECT COUNT(*) FROM NoteAttachments WHERE FilePath = $path AND NoteId != $excludeId";
                using (var cmd = new SqliteCommand(sql, connection))
                {
                    cmd.Parameters.AddWithValue("$path", fileName);
                    cmd.Parameters.AddWithValue("$excludeId", excludeNoteId);
                    return (long)cmd.ExecuteScalar()! > 0;
                }
            }
        }
    }
}
