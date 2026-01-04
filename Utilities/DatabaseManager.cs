using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using CatchCapture.Models;

using System.Linq;
using System.Diagnostics;
using System.Windows.Media.Imaging;

namespace CatchCapture.Utilities
{
    public class SaveNoteRequest
    {
        public long? Id { get; set; }
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string ContentXaml { get; set; } = "";
        public string Tags { get; set; } = "";
        public long CategoryId { get; set; } = 1;
        public string? SourceApp { get; set; }
        public string? SourceUrl { get; set; }
        public List<BitmapSource> Images { get; set; } = new List<BitmapSource>();
        public List<NoteAttachmentRequest> Attachments { get; set; } = new List<NoteAttachmentRequest>();
    }

    public class NoteAttachmentRequest
    {
        public long? Id { get; set; }
        public string? FullPath { get; set; }
        public string DisplayName { get; set; } = "";
        public bool IsDeleted { get; set; }
        public bool IsExisting { get; set; }
    }

    public class DatabaseManager
    {
        private static DatabaseManager? _instance;
        public static DatabaseManager Instance => _instance ??= new DatabaseManager();

        private string _dbPath;
        private string DbPath => _dbPath;
        private static readonly object _dbLock = new object();
        private readonly object _lockObj = new object();
        private readonly string _currentIdentity = $"{Environment.MachineName}:{Environment.ProcessId}";
        private DateTime _lastTakeoverTime = DateTime.MinValue;
        private bool _isLosingOwnership = false;
        private bool _isTakeoverInProgress = false;
        private SqliteConnection? _activeConnection;
        private string? _noteLockingMachine;
        private string? _historyLockingMachine;
        private System.Timers.Timer? _heartbeatTimer;
        public event Action? OwnershipLost;

        // 디버그 로그 파일 경로
        private static readonly string _logFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "CatchCapture_LockDebug.txt");
        private static readonly object _logLock = new object();

        public string? NoteLockingMachine => _noteLockingMachine;
        public string? HistoryLockingMachine => _historyLockingMachine;
        public string HistoryDbFilePath => _historyDbPath;

        public string DbFilePath => _dbPath;

        /// <summary>
        /// 디버그 로그를 txt 파일로 저장
        /// </summary>
        private void LogToFile(string message)
        {
            try
            {
                lock (_logLock)
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    string logLine = $"[{timestamp}] [{_currentIdentity}] {message}";
                    File.AppendAllText(_logFilePath, logLine + Environment.NewLine);
                    Debug.WriteLine(logLine);
                }
            }
            catch { /* 로그 실패 무시 */ }
        }

        private DatabaseManager()
        {
            var settings = Settings.Load();
            string storagePath = settings.NoteStoragePath;
            if (string.IsNullOrEmpty(storagePath))
            {
                storagePath = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "CatchCapture"), "notedata");
            }
            else
            {
                storagePath = ResolveStoragePath(storagePath);
            }
            
            _dbPath = Path.Combine(storagePath, "notedb", "catch_notes.db");
            _historyDbPath = Path.Combine(settings.DefaultSaveFolder, "history", "history.db");

            // ★ 버전 확인용 시작 로그
            LogToFile("============================================");
            LogToFile("★★★ DatabaseManager 초기화 (v2 - PENDING 즉시감지) ★★★");
            LogToFile($"Note DB 경로: {_dbPath}");
            LogToFile($"History DB 경로: {_historyDbPath}");
            LogToFile("============================================");

            InitializeDatabase();
            InitializeHistoryDatabase(); // Separate DB initialization
            
            // Check for cross-computer locks (Cloud sync safety)
            CheckAndCreateLock();

            Settings.SettingsChanged += (s, e) => {
                Reinitialize();
            };
        }

        private string _historyDbPath;
        private string HistoryDbPath => _historyDbPath;

        public void CloseConnection()
        {
            lock (_dbLock)
            {
                if (_activeConnection != null)
                {
                    _activeConnection.Close();
                    SqliteConnection.ClearAllPools(); // IMPORTANT: Release all file handles
                    _activeConnection = null;
                }
            }
        }

        public void Reinitialize()
        {
            CloseConnection();
            var settings = Settings.Load();
            string storagePath = settings.NoteStoragePath;
            if (string.IsNullOrEmpty(storagePath))
            {
                storagePath = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "CatchCapture"), "notedata");
            }
            else
            {
                storagePath = ResolveStoragePath(storagePath);
            }
            
            _dbPath = Path.Combine(storagePath, "notedb", "catch_notes.db");
            _historyDbPath = Path.Combine(settings.DefaultSaveFolder, "history", "history.db");
            InitializeDatabase();
            InitializeHistoryDatabase();
            CheckAndCreateLock();
            StartHeartbeat();
        }


        private void StartHeartbeat()
        {
            lock (_lockObj)
            {
                if (_heartbeatTimer == null)
                {
                    _heartbeatTimer = new System.Timers.Timer(2000); // 2 seconds for faster takeover detection
                    _heartbeatTimer.Elapsed += (s, e) => UpdateLocks(false);
                    _heartbeatTimer.AutoReset = true;
                    _heartbeatTimer.Start();
                }
                else if (!_heartbeatTimer.Enabled)
                {
                    _heartbeatTimer.Start();
                }
                _isLosingOwnership = false;
            }
        }

        public enum TakeoverStatus
        {
            None,
            Pending,
            Released,
            Stale
        }

        public void RequestTakeover()
        {
            lock (_lockObj)
            {
                LogToFile("========== RequestTakeover 시작 ==========");
                LogToFile($"현재상태: _isLosingOwnership={_isLosingOwnership}, _isTakeoverInProgress={_isTakeoverInProgress}");
                
                // CRITICAL: Stop heartbeat to prevent overwriting our PENDING status
                _isTakeoverInProgress = true;
                _heartbeatTimer?.Stop();
                LogToFile("Heartbeat 타이머 중지됨");
                
                string ticks = DateTime.Now.Ticks.ToString();
                string pendingContent = $"{_currentIdentity}|{ticks}|PENDING";
                
                // Mark as PENDING to notify the other computer to close DB
                SafeWriteFile(_dbPath + ".lock", pendingContent);
                SafeWriteFile(_historyDbPath + ".lock", pendingContent);
                
                LogToFile($"PENDING 파일 작성 완료: {pendingContent}");
            }
        }
        
        public void CompleteTakeover()
        {
            lock (_lockObj)
            {
                _isTakeoverInProgress = false;
                LogToFile("CompleteTakeover: _isTakeoverInProgress = false");
            }
        }

        public TakeoverStatus CheckTakeoverStatus()
        {
            try
            {
                string lockPath = _dbPath + ".lock";
                if (!File.Exists(lockPath)) return TakeoverStatus.Released;

                string content = SafeReadFile(lockPath);
                if (string.IsNullOrEmpty(content)) return TakeoverStatus.None;

                string[] parts = content.Split('|');
                if (parts.Length < 3) return TakeoverStatus.Pending;

                string status = parts[2];
                if (status == "RELEASED") return TakeoverStatus.Released;
                
                return TakeoverStatus.Pending;
            }
            catch { return TakeoverStatus.None; }
        }

        private void UpdateLocks(bool forceWrite)
        {
            lock (_lockObj)
            {
                try
                {
                    string noteLockPath = _dbPath + ".lock";
                    string historyLockPath = _historyDbPath + ".lock";

                    // 1. Calculate grace period FIRST
                    double secondsSinceTakeover = (DateTime.Now - _lastTakeoverTime).TotalSeconds;
                    bool isWithinTakeoverGracePeriod = secondsSinceTakeover < 60;

                    // 2. Skip if already losing ownership (prevent duplicate triggers)
                    if (_isLosingOwnership)
                    {
                        // 가끔 로그 (5초에 한번)
                        if (DateTime.Now.Second % 5 == 0)
                            LogToFile($"[SKIP] 이미 점유권 상실 상태 (_isLosingOwnership=true)");
                        return;
                    }

                    // 3. Skip if current instance is already requesting a takeover
                    if (_isTakeoverInProgress)
                    {
                        LogToFile($"[SKIP] Takeover 진행 중 (_isTakeoverInProgress=true)");
                        return;
                    }

                    // 4. CRITICAL: Grace Period 내에서도 PENDING만큼은 반드시 체크!
                    // ACTIVE는 클라우드 지연 때문에 무시하지만, PENDING은 즉시 반응해야 함
                    if (isWithinTakeoverGracePeriod && !forceWrite)
                    {
                        // Grace period 로그 (10초에 한번)
                        if (DateTime.Now.Second % 10 == 0)
                            LogToFile($"[GRACE PERIOD] {secondsSinceTakeover:F1}초 경과 (60초까지 보호, 단 PENDING은 체크)");
                        
                        // ★★★ PENDING 요청만큼은 Grace Period 내에서도 반드시 확인! ★★★
                        if (File.Exists(noteLockPath))
                        {
                            try
                            {
                                string content = SafeReadFile(noteLockPath);
                                if (!string.IsNullOrEmpty(content))
                                {
                                    string[] parts = content.Split('|');
                                    string lockingIdentity = parts[0];
                                    string status = parts.Length >= 3 ? parts[2] : "ACTIVE";
                                    bool isMe = string.Equals(lockingIdentity, _currentIdentity, StringComparison.OrdinalIgnoreCase);
                                    
                                    // 다른 컴퓨터가 PENDING 요청을 보냈다면 즉시 양도!
                                    if (!isMe && status == "PENDING")
                                    {
                                        LogToFile($"▶▶▶ [GRACE PERIOD 중 PENDING 감지!] {lockingIdentity}가 점유권 요청. 즉시 양도!");
                                        ReleaseForTakeover(lockingIdentity);
                                        return;
                                    }
                                    // ACTIVE는 Grace Period 내에서 무시 (클라우드 지연 때문)
                                }
                            }
                            catch (Exception ex)
                            {
                                LogToFile($"[GRACE PERIOD] Lock 파일 읽기 실패: {ex.Message}");
                            }
                        }
                        
                        // 3.5초 이상 경과했으면 heartbeat 쓰기
                        if (secondsSinceTakeover >= 3.5)
                        {
                            string ticks = DateTime.Now.Ticks.ToString();
                            string contentToWrite = $"{_currentIdentity}|{ticks}|ACTIVE";
                            SafeWriteFile(noteLockPath, contentToWrite);
                            SafeWriteFile(historyLockPath, contentToWrite);
                        }
                        return;
                    }

                    // 5. Check current lock file status (ONLY after grace period)
                    if (!forceWrite && File.Exists(noteLockPath))
                    {
                        try
                        {
                            string content = SafeReadFile(noteLockPath);
                            if (!string.IsNullOrEmpty(content))
                            {
                                string[] parts = content.Split('|');
                                string lockingIdentity = parts[0];
                                string status = parts.Length >= 3 ? parts[2] : "ACTIVE";
                                string ticksStr = parts.Length >= 2 ? parts[1] : "0";

                                bool isMe = string.Equals(lockingIdentity, _currentIdentity, StringComparison.OrdinalIgnoreCase);

                                // 상세 로그 (10초에 한번)
                                if (DateTime.Now.Second % 10 == 0)
                                {
                                    LogToFile($"[READ] Lock파일: identity={lockingIdentity}, status={status}, isMe={isMe}");
                                }

                                if (!isMe)
                                {
                                    // A. Someone else wants takeover (PENDING) -> Hand over immediately
                                    if (status == "PENDING")
                                    {
                                        LogToFile($"▶▶▶ [PENDING 감지] {lockingIdentity}가 점유권 요청함. 양도 시작...");
                                        ReleaseForTakeover(lockingIdentity);
                                        return;
                                    }

                                    // B. Someone else is ACTIVE (And we are beyond the grace period)
                                    if (status == "ACTIVE")
                                    {
                                        LogToFile($"▶▶▶ [ACTIVE 감지] 다른 컴퓨터({lockingIdentity})가 ACTIVE 상태!");
                                        LogToFile($"     Grace Period 경과: {secondsSinceTakeover:F1}초 (60초 이후에만 반응)");
                                        
                                        // 이미 grace period 체크를 위에서 했으므로 여기서는 바로 잠금 처리
                                        _isLosingOwnership = true;
                                        _heartbeatTimer?.Stop();
                                        LogToFile($"▶▶▶ [셀프 잠금 발동] OwnershipLost 이벤트 호출!");
                                        OwnershipLost?.Invoke();
                                        return;
                                    }
                                }
                                else
                                {
                                    // It's my lock. Reset the losing flag if it was set somehow.
                                    if (_isLosingOwnership)
                                    {
                                        LogToFile($"[자기 락 확인] 내 락 파일이므로 _isLosingOwnership 리셋");
                                    }
                                    _isLosingOwnership = false;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogToFile($"[ERROR] Lock 파일 읽기 실패: {ex.Message}");
                        }
                    }

                    // 6. Normal heartbeat update: Every 3.5+ seconds
                    if (!forceWrite && secondsSinceTakeover < 3.5) return;

                    // ★★★ CRITICAL: heartbeat 쓰기 전 마지막으로 PENDING 체크! ★★★
                    // 클라우드 동기화로 인해 위의 체크 이후 파일이 변경됐을 수 있음
                    if (File.Exists(noteLockPath))
                    {
                        try
                        {
                            string checkContent = SafeReadFile(noteLockPath);
                            if (!string.IsNullOrEmpty(checkContent))
                            {
                                string[] checkParts = checkContent.Split('|');
                                string checkIdentity = checkParts[0];
                                string checkStatus = checkParts.Length >= 3 ? checkParts[2] : "ACTIVE";
                                bool checkIsMe = string.Equals(checkIdentity, _currentIdentity, StringComparison.OrdinalIgnoreCase);
                                
                                if (!checkIsMe && checkStatus == "PENDING")
                                {
                                    LogToFile($"▶▶▶ [HEARTBEAT 전 PENDING 발견!] {checkIdentity}가 점유권 요청. 즉시 양도!");
                                    ReleaseForTakeover(checkIdentity);
                                    return;
                                }
                            }
                        }
                        catch { /* 읽기 실패 시 무시하고 heartbeat 진행 */ }
                    }

                    string ticksNow = DateTime.Now.Ticks.ToString();
                    string heartbeatContent = $"{_currentIdentity}|{ticksNow}|ACTIVE";
                    
                    // 매 heartbeat마다 로그 (디버깅용)
                    LogToFile($"[HEARTBEAT] 파일 쓰기: {heartbeatContent.Substring(0, Math.Min(50, heartbeatContent.Length))}...");
                    
                    // Re-assert ACTIVE status
                    SafeWriteFile(noteLockPath, heartbeatContent);
                    SafeWriteFile(historyLockPath, heartbeatContent);
                    
                    if (forceWrite)
                    {
                        _lastTakeoverTime = DateTime.Now;
                        LogToFile($"[FORCE WRITE] Heartbeat 강제 갱신, _lastTakeoverTime 리셋");
                    }
                    _isLosingOwnership = false;
                }
                catch (Exception ex)
                {
                    LogToFile($"[CRITICAL ERROR] UpdateLocks 예외: {ex.Message}");
                    if (forceWrite) throw;
                }
            }
        }

        private void ReleaseForTakeover(string newOwnerIdentity)
        {
            try
            {
                LogToFile($"========== ReleaseForTakeover 시작 ==========");
                LogToFile($"새 소유자: {newOwnerIdentity}");
                
                // 1. Close DB Connection
                CloseConnection();
                LogToFile("DB 연결 종료됨");
                
                // 2. Mark as RELEASED
                string ticks = DateTime.Now.Ticks.ToString();
                string releasedContent = $"{newOwnerIdentity}|{ticks}|RELEASED";
                SafeWriteFile(_dbPath + ".lock", releasedContent);
                LogToFile($"RELEASED 파일 작성: {releasedContent}");
                
                // 3. Trigger local lock UI
                _isLosingOwnership = true;
                _heartbeatTimer?.Stop();
                LogToFile("Heartbeat 중지, OwnershipLost 이벤트 호출");
                OwnershipLost?.Invoke();
            }
            catch (Exception ex)
            {
                LogToFile($"[ERROR] ReleaseForTakeover 예외: {ex.Message}");
            }
        }

        private string SafeReadFile(string path)
        {
            // Use minimal buffer and no buffering to bypass file system cache
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1, FileOptions.SequentialScan))
            using (var reader = new StreamReader(fs))
            {
                return reader.ReadToEnd();
            }
        }

        private void SafeWriteFile(string path, string content)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            using (var writer = new StreamWriter(fs))
            {
                writer.Write(content);
                writer.Flush();
            }
        }

        public void Reload() 
        {
            CloseConnection();
            InitializeDatabase();
            // [IMPORTANT] Don't check locks here if we just acquired ownership
            // The _lastTakeoverTime check in CheckAndCreateLock handles this
            CheckAndCreateLock();
        }

        private void CheckAndCreateLock()
        {
            try
            {
                // If we just took ownership (within 60 seconds), don't check for other locks
                // This prevents reading stale cloud-synced data
                if ((DateTime.Now - _lastTakeoverTime).TotalSeconds < 60)
                {
                    _noteLockingMachine = null;
                    _historyLockingMachine = null;
                    return;
                }
                _noteLockingMachine = CheckLock(_dbPath);
                _historyLockingMachine = CheckLock(_historyDbPath);
            }
            catch { }
        }

        public void ClearLockState()
        {
            lock (_lockObj)
            {
                _noteLockingMachine = null;
                _historyLockingMachine = null;
                _isLosingOwnership = false;
            }
        }

        public bool TakeOwnership()
        {
            lock (_lockObj)
            {
                try
                {
                    LogToFile("========== TakeOwnership 시작 ==========");
                    LogToFile($"이전상태: _isLosingOwnership={_isLosingOwnership}, _isTakeoverInProgress={_isTakeoverInProgress}");
                    LogToFile($"이전 _lastTakeoverTime: {_lastTakeoverTime}");
                    
                    // ★★★ CRITICAL: Set grace period FIRST before ANY file operations ★★★
                    // This prevents the heartbeat timer from triggering self-lock
                    _lastTakeoverTime = DateTime.Now;
                    _isLosingOwnership = false;
                    _isTakeoverInProgress = false;
                    _noteLockingMachine = null;
                    _historyLockingMachine = null;
                    
                    LogToFile($"상태 초기화 완료: _lastTakeoverTime={_lastTakeoverTime}, 이제 60초간 보호됨");
                    
                    // [IMPORTANT] Delete old locks to ensure a clean state
                    string noteLockPath = _dbPath + ".lock";
                    string historyLockPath = _historyDbPath + ".lock";
                    
                    try { if (File.Exists(noteLockPath)) File.Delete(noteLockPath); } catch { }
                    try { if (File.Exists(historyLockPath)) File.Delete(historyLockPath); } catch { }
                    LogToFile("기존 락 파일 삭제 시도 완료");

                    string ticks = DateTime.Now.Ticks.ToString();
                    string content = $"{_currentIdentity}|{ticks}|ACTIVE";
                    
                    SafeWriteFile(noteLockPath, content);
                    SafeWriteFile(historyLockPath, content);
                    LogToFile($"새 락 파일 작성: {content}");
                    
                    StartHeartbeat();
                    LogToFile("Heartbeat 시작됨. TakeOwnership 성공!");
                    LogToFile("===========================================");
                    return true;
                }
                catch (Exception ex)
                {
                    LogToFile($"[CRITICAL ERROR] TakeOwnership 실패: {ex.Message}");
                    return false;
                }
            }
        }

        private string? CheckLock(string dbFilePath)
        {
            try
            {
                string lockPath = dbFilePath + ".lock";
                if (File.Exists(lockPath))
                {
                    string content = SafeReadFile(lockPath);
                    string[] parts = content.Split('|');
                    if (parts.Length > 0)
                    {
                        string lockingIdentity = parts[0];
                        string currentIdentity = $"{Environment.MachineName}:{Environment.ProcessId}";
                        if (!string.Equals(lockingIdentity, _currentIdentity, StringComparison.OrdinalIgnoreCase))
                        {
                            // If same machine but different PID, or different machine entirely
                            string lockingMachine = lockingIdentity.Split(':')[0];
                            // Check if the lock is recent (heartbeat within 2 mins)
                            if (parts.Length > 1 && long.TryParse(parts[1], out long ticks))
                            {
                                DateTime lockTime = new DateTime(ticks);
                                // If updated within 2 minutes, it's definitely active.
                                if (DateTime.Now - lockTime < TimeSpan.FromMinutes(2))
                                {
                                    return lockingMachine;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public void RemoveLock()
        {
            try
            {
                _heartbeatTimer?.Stop();
                _heartbeatTimer?.Dispose();
                _heartbeatTimer = null;

                RemoveSingleLock(_dbPath);
                RemoveSingleLock(_historyDbPath);
            }
            catch { }
        }

        private void RemoveSingleLock(string dbFilePath)
        {
            try
            {
                string lockPath = dbFilePath + ".lock";
                if (File.Exists(lockPath))
                {
                    string content = File.ReadAllText(lockPath);
                    string currentIdentity = $"{Environment.MachineName}:{Environment.ProcessId}";
                    if (content.StartsWith(currentIdentity))
                    {
                        File.Delete(lockPath);
                    }
                }
            }
            catch { }
        }

        public static string ResolveStoragePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;

            try
            {
                // 1. 이미 'notedata' 폴더를 가리키고 있으면 그대로 사용
                if (Path.GetFileName(path).Equals("notedata", StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }

                // 2. 지정된 폴더 직하에 이미 3개 필수 폴더가 있는 경우 그대로 사용
                if (Directory.Exists(Path.Combine(path, "notedb")) &&
                    Directory.Exists(Path.Combine(path, "img")) &&
                    Directory.Exists(Path.Combine(path, "attachments")))
                {
                    return path;
                }

                // 3. 하위에 'notedata' 폴더가 존재하는지 확인
                string subPath = Path.Combine(path, "notedata");
                if (Directory.Exists(subPath))
                {
                    return subPath;
                }

                // 4. 그 외의 경우 'notedata'를 붙여서 관리 (InitializeDatabase에서 생성됨)
                return subPath;
            }
            catch
            {
                return path;
            }
        }

        private void InitializeDatabase()
        {
            string dbDir = Path.GetDirectoryName(_dbPath)!;
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

                // Create Captures (History) table


                using (var command = new SqliteCommand(createNotesTable, connection)) { command.ExecuteNonQuery(); }
                using (var command = new SqliteCommand(createImagesTable, connection)) { command.ExecuteNonQuery(); }
                using (var command = new SqliteCommand(createTagsTable, connection)) { command.ExecuteNonQuery(); }
                using (var command = new SqliteCommand(createNoteTagsTable, connection)) { command.ExecuteNonQuery(); }
                using (var command = new SqliteCommand(createAttachmentsTable, connection)) { command.ExecuteNonQuery(); }
                using (var command = new SqliteCommand(createCategoriesTable, connection)) { command.ExecuteNonQuery(); }
                using (var command = new SqliteCommand(createConfigTable, connection)) { command.ExecuteNonQuery(); }
                // Captures table is now handled in InitializeHistoryDatabase

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

        private void InitializeHistoryDatabase()
        {
            string dbDir = Path.GetDirectoryName(_historyDbPath)!;
            if (!Directory.Exists(dbDir)) Directory.CreateDirectory(dbDir);

            using (var connection = new SqliteConnection($"Data Source={HistoryDbPath}"))
            {
                connection.Open();

                // Create Captures (History) table
                string createCapturesTable = @"
                    CREATE TABLE IF NOT EXISTS Captures (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        FileName TEXT,
                        FilePath TEXT,
                        SourceApp TEXT,
                        SourceTitle TEXT,
                        OriginalFilePath TEXT,
                        IsFavorite INTEGER DEFAULT 0,
                        IsPinned INTEGER DEFAULT 0,
                        Status INTEGER DEFAULT 0, -- 0: Active, 1: Trash
                        FileSize INTEGER,
                        Resolution TEXT,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                        Memo TEXT DEFAULT ''
                    );";

                using (var command = new SqliteCommand(createCapturesTable, connection)) { command.ExecuteNonQuery(); }

                // Migration: Add OriginalFilePath if it doesn't exist
                try
                {
                    using (var command = new SqliteCommand("ALTER TABLE Captures ADD COLUMN OriginalFilePath TEXT;", connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
                catch { /* Column might already exist */ }

                try
                {
                    using (var command = new SqliteCommand("ALTER TABLE Captures ADD COLUMN IsPinned INTEGER DEFAULT 0;", connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
                catch { /* Column might already exist */ }

                try
                {
                    using (var command = new SqliteCommand("ALTER TABLE Captures ADD COLUMN Memo TEXT DEFAULT '';", connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
                catch { /* Column might already exist */ }
            }
        }

        public void BackupDatabase(string destinationPath)
        {
            try
            {
                string? dir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                // If destination exists, Delete it first (VACUUM INTO requires the target to NOT exist)
                if (File.Exists(destinationPath)) File.Delete(destinationPath);

                using (var connection = new SqliteConnection($"Data Source={DbPath}"))
                {
                    connection.Open();
                    // 'VACUUM INTO' is the safest way to backup a live SQLite database
                    using (var command = new SqliteCommand($"VACUUM INTO '{destinationPath.Replace("'", "''")}';", connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB 백업 중 오류: {ex.Message}");
                // Fallback: if VACUUM INTO fails, try standard copy with sharing
                try
                {
                    using (var sourceStream = new FileStream(DbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var destStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
                    {
                        sourceStream.CopyTo(destStream);
                    }
                }
                catch { throw; } // Re-throw if both fail
            }
        }

        public void BackupHistoryDatabase(string destinationPath)
        {
            try
            {
                string? dir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                if (File.Exists(destinationPath)) File.Delete(destinationPath);

                using (var connection = new SqliteConnection($"Data Source={HistoryDbPath}"))
                {
                    connection.Open();
                    using (var command = new SqliteCommand($"VACUUM INTO '{destinationPath.Replace("'", "''")}';", connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"History DB 백업 중 오류: {ex.Message}");
                try
                {
                    using (var sourceStream = new FileStream(HistoryDbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var destStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
                    {
                        sourceStream.CopyTo(destStream);
                    }
                }
                catch { throw; }
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

        public void VacuumHistory()
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={HistoryDbPath}"))
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
                System.Diagnostics.Debug.WriteLine($"History DB VACUUM 오류: {ex.Message}");
                throw;
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

        public void CleanupHistory(int retentionDays, int trashRetentionDays)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={HistoryDbPath}"))
                {
                    connection.Open();

                    // 1. Move old items to trash (Status 0 -> 1)
                    if (retentionDays > 0)
                    {
                        string toTrashSql = @"
                            UPDATE Captures 
                            SET Status = 1 
                            WHERE Status = 0 AND IsPinned = 0 AND CreatedAt <= datetime('now', 'localtime', '-' || $days || ' days')";
                        
                        using (var cmd = new SqliteCommand(toTrashSql, connection))
                        {
                            cmd.Parameters.AddWithValue("$days", retentionDays);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // 2. Permanently delete items from trash
                    if (trashRetentionDays > 0)
                    {
                        var filesToDelete = new List<string>();
                        string findSql = "SELECT FilePath FROM Captures WHERE Status = 1 AND CreatedAt <= datetime('now', 'localtime', '-' || $days || ' days')";
                        
                        using (var cmd = new SqliteCommand(findSql, connection))
                        {
                            cmd.Parameters.AddWithValue("$days", trashRetentionDays);
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read()) filesToDelete.Add(reader.GetString(0));
                            }
                        }

                        if (filesToDelete.Count > 0)
                        {
                            foreach (var filePath in filesToDelete)
                            {
                                try { if (File.Exists(filePath)) File.Delete(filePath); } catch { }
                            }

                            string deleteSql = "DELETE FROM Captures WHERE Status = 1 AND CreatedAt <= datetime('now', 'localtime', '-' || $days || ' days')";
                            using (var cmd = new SqliteCommand(deleteSql, connection))
                            {
                                cmd.Parameters.AddWithValue("$days", trashRetentionDays);
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"히스토리 자동 정리 오류: {ex.Message}");
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
        public async Task<long> SaveNoteAsync(SaveNoteRequest request)
        {
            return await Task.Run(() =>
            {
                using (var connection = new SqliteConnection($"Data Source={DbPath}"))
                {
                    connection.Open();
                    using (var transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            long noteId;
                            bool isEdit = request.Id.HasValue;

                            if (isEdit)
                            {
                                noteId = request.Id!.Value;
                                string updateSql = @"
                                    UPDATE Notes 
                                    SET Title = $title, Content = $content, ContentXaml = $contentXaml, CategoryId = $categoryId, UpdatedAt = CURRENT_TIMESTAMP
                                    WHERE Id = $id";
                                using (var cmd = new SqliteCommand(updateSql, connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("$title", request.Title);
                                    cmd.Parameters.AddWithValue("$content", request.Content);
                                    cmd.Parameters.AddWithValue("$contentXaml", request.ContentXaml);
                                    cmd.Parameters.AddWithValue("$categoryId", request.CategoryId);
                                    cmd.Parameters.AddWithValue("$id", noteId);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            else
                            {
                                string insertSql = @"
                                    INSERT INTO Notes (Title, Content, ContentXaml, SourceApp, SourceUrl, UpdatedAt, CategoryId)
                                    VALUES ($title, $content, $contentXaml, $sourceApp, $sourceUrl, CURRENT_TIMESTAMP, $categoryId);
                                    SELECT last_insert_rowid();";
                                using (var cmd = new SqliteCommand(insertSql, connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("$title", request.Title);
                                    cmd.Parameters.AddWithValue("$content", request.Content);
                                    cmd.Parameters.AddWithValue("$contentXaml", request.ContentXaml);
                                    cmd.Parameters.AddWithValue("$sourceApp", (object?)request.SourceApp ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("$sourceUrl", (object?)request.SourceUrl ?? DBNull.Value);
                                    cmd.Parameters.AddWithValue("$categoryId", request.CategoryId);
                                    noteId = Convert.ToInt64(cmd.ExecuteScalar());
                                }
                            }

                            // 1. Manage Images
                            string imgDir = GetImageFolderPath();
                            var savedImages = new List<(string FileName, string Hash)>();
                            var settings = Settings.Load();

                            foreach (var imgSource in request.Images)
                            {
                                string? hash = null;
                                string? fileName = null;

                                if (imgSource is BitmapImage bi && bi.UriSource != null && bi.UriSource.IsFile)
                                {
                                    string localPath = bi.UriSource.LocalPath;
                                    if (localPath.StartsWith(imgDir, StringComparison.OrdinalIgnoreCase))
                                    {
                                        fileName = localPath.Substring(imgDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                                        hash = ComputeHash(localPath);
                                        savedImages.Add((fileName, hash));
                                        continue;
                                    }
                                }

                                // New image processing
                                bool isRemote = (imgSource is BitmapImage bir && bir.UriSource != null && !bir.UriSource.IsFile);
                                string format = isRemote ? "JPG" : (settings.NoteSaveFormat ?? "PNG");
                                int quality = isRemote ? 80 : settings.NoteImageQuality;

                                using (var ms = new MemoryStream())
                                {
                                    ScreenCaptureUtility.SaveImageToStream(imgSource, ms, format, quality);
                                    ms.Position = 0;
                                    using (var md5 = System.Security.Cryptography.MD5.Create())
                                    {
                                        hash = BitConverter.ToString(md5.ComputeHash(ms)).Replace("-", "").ToLowerInvariant();
                                    }

                                    string? existing = GetExistingImageByHash(hash);
                                    if (existing != null)
                                    {
                                        savedImages.Add((existing, hash));
                                        continue;
                                    }

                                    string subFolder = "";
                                    string grpMode = settings.NoteFolderGroupingMode ?? "None";
                                    DateTime now = DateTime.Now;
                                    if (grpMode == "Yearly") subFolder = now.ToString("yyyy");
                                    else if (grpMode == "Monthly") subFolder = now.ToString("yyyy-MM");
                                    else if (grpMode == "Quarterly") subFolder = $"{now.Year}_{(now.Month + 2) / 3}Q";

                                    string targetDir = string.IsNullOrEmpty(subFolder) ? imgDir : Path.Combine(imgDir, subFolder);
                                    if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                                    string fName = settings.NoteFileNameTemplate ?? "Catch_$yyyy-MM-dd_HH-mm-ss$";
                                    foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(fName, @"\$(.*?)\$"))
                                    {
                                        string k = match.Groups[1].Value;
                                        if (k.Equals("App", StringComparison.OrdinalIgnoreCase)) fName = fName.Replace(match.Value, request.SourceApp ?? "CatchC");
                                        else if (k.Equals("Title", StringComparison.OrdinalIgnoreCase))
                                        {
                                            string st = request.Title;
                                            if (st.Length > 20) st = st.Substring(0, 20);
                                            fName = fName.Replace(match.Value, st);
                                        }
                                        else try { fName = fName.Replace(match.Value, now.ToString(k)); } catch { }
                                    }
                                    foreach (char c in Path.GetInvalidFileNameChars()) fName = fName.Replace(c, '_');
                                    
                                    string fileNameOnly = fName + "." + format.ToLower();
                                    string fullPath = Path.Combine(targetDir, fileNameOnly);
                                    int counter = 1;
                                    while (File.Exists(fullPath))
                                    {
                                        fileNameOnly = $"{fName}_{counter++}.{format.ToLower()}";
                                        fullPath = Path.Combine(targetDir, fileNameOnly);
                                    }

                                    fileName = string.IsNullOrEmpty(subFolder) ? fileNameOnly : Path.Combine(subFolder, fileNameOnly);
                                    ms.Position = 0;
                                    using (var fs = new FileStream(fullPath, FileMode.Create)) { ms.CopyTo(fs); }
                                    savedImages.Add((fileName, hash));
                                }
                            }

                            // Update NoteImages in DB
                            using (var cmd = new SqliteCommand("DELETE FROM NoteImages WHERE NoteId = $id", connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("$id", noteId);
                                cmd.ExecuteNonQuery();
                            }
                            for (int i = 0; i < savedImages.Count; i++)
                            {
                                using (var cmd = new SqliteCommand("INSERT INTO NoteImages (NoteId, FilePath, OrderIndex, FileHash) VALUES ($nid, $path, $idx, $hash)", connection, transaction))
                                {
                                    cmd.Parameters.AddWithValue("$nid", noteId);
                                    cmd.Parameters.AddWithValue("$path", savedImages[i].FileName);
                                    cmd.Parameters.AddWithValue("$idx", i);
                                    cmd.Parameters.AddWithValue("$hash", savedImages[i].Hash);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            // 2. Manage Tags
                            using (var cmd = new SqliteCommand("DELETE FROM NoteTags WHERE NoteId = $id", connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("$id", noteId);
                                cmd.ExecuteNonQuery();
                            }

                            if (!string.IsNullOrWhiteSpace(request.Tags))
                            {
                                foreach (var tag in request.Tags.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim().ToLower()).Distinct())
                                {
                                    if (string.IsNullOrEmpty(tag)) continue;
                                    using (var cmd = new SqliteCommand("INSERT OR IGNORE INTO Tags (Name) VALUES ($name); SELECT Id FROM Tags WHERE Name = $name;", connection, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("$name", tag);
                                        long tagId = (long)cmd.ExecuteScalar()!;
                                        using (var linkCmd = new SqliteCommand("INSERT OR IGNORE INTO NoteTags (NoteId, TagId) VALUES ($nid, $tid)", connection, transaction))
                                        {
                                            linkCmd.Parameters.AddWithValue("$nid", noteId);
                                            linkCmd.Parameters.AddWithValue("$tid", tagId);
                                            linkCmd.ExecuteNonQuery();
                                        }
                                    }
                                }
                            }

                            // 3. Manage Attachments
                            string attachDir = GetAttachmentsFolderPath();
                            foreach (var att in request.Attachments)
                            {
                                if (att.IsDeleted && att.Id.HasValue)
                                {
                                    string? path = null;
                                    using (var cmd = new SqliteCommand("SELECT FilePath FROM NoteAttachments WHERE Id = $id", connection, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("$id", att.Id.Value);
                                        path = cmd.ExecuteScalar()?.ToString();
                                    }
                                    using (var cmd = new SqliteCommand("DELETE FROM NoteAttachments WHERE Id = $id", connection, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("$id", att.Id.Value);
                                        cmd.ExecuteNonQuery();
                                    }
                                    if (!string.IsNullOrEmpty(path))
                                    {
                                        try { File.Delete(Path.Combine(attachDir, path)); } catch { }
                                    }
                                }
                                else if (!att.IsExisting && att.FullPath != null && File.Exists(att.FullPath))
                                {
                                    string yearSub = EnsureYearFolderExists(attachDir);
                                    string newFileNameOnly = $"{Guid.NewGuid()}_{att.DisplayName}";
                                    string relPath = Path.Combine(yearSub, newFileNameOnly);
                                    string fullPath = Path.Combine(attachDir, relPath);
                                    File.Copy(att.FullPath, fullPath);

                                    using (var cmd = new SqliteCommand("INSERT INTO NoteAttachments (NoteId, FilePath, OriginalName, FileType) VALUES ($nid, $path, $name, $type)", connection, transaction))
                                    {
                                        cmd.Parameters.AddWithValue("$nid", noteId);
                                        cmd.Parameters.AddWithValue("$path", relPath);
                                        cmd.Parameters.AddWithValue("$name", att.DisplayName);
                                        cmd.Parameters.AddWithValue("$type", Path.GetExtension(att.DisplayName).ToLower());
                                        cmd.ExecuteNonQuery();
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
            });
        }

        public void MergeNotesFromBackup(string sourceDbPath, string sourceImgDir, string sourceAttachDir, Action<string>? onProgress = null)
        {
            using (var targetConn = new SqliteConnection($"Data Source={DbPath}"))
            {
                targetConn.Open();
                using (var transaction = targetConn.BeginTransaction())
                {
                    try
                    {
                        using (var sourceConn = new SqliteConnection($"Data Source={sourceDbPath}"))
                        {
                            sourceConn.Open();

                            // 1. 카테고리 매핑 (이름 기준)
                            var categoryMap = new Dictionary<long, long>(); // SourceId -> TargetId
                            string sourceCatSql = "SELECT Id, Name, Color FROM Categories";
                            using (var cmd = new SqliteCommand(sourceCatSql, sourceConn))
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    long sId = reader.GetInt64(0);
                                    string name = reader.GetString(1);
                                    string? color = reader.IsDBNull(2) ? null : reader.GetString(2);

                                    // 현재 DB에서 동일 이름 찾기
                                    long tId = 1; // 기본값
                                    using (var checkCmd = new SqliteCommand("SELECT Id FROM Categories WHERE Name = $name", targetConn, transaction))
                                    {
                                        checkCmd.Parameters.AddWithValue("$name", name);
                                        var result = checkCmd.ExecuteScalar();
                                        if (result != null)
                                        {
                                            tId = Convert.ToInt64(result);
                                        }
                                        else
                                        {
                                            // 없으면 생성
                                            using (var insertCmd = new SqliteCommand("INSERT INTO Categories (Name, Color) VALUES ($name, $color); SELECT last_insert_rowid();", targetConn, transaction))
                                            {
                                                insertCmd.Parameters.AddWithValue("$name", name);
                                                insertCmd.Parameters.AddWithValue("$color", (object?)color ?? DBNull.Value);
                                                tId = Convert.ToInt64(insertCmd.ExecuteScalar());
                                            }
                                        }
                                    }
                                    categoryMap[sId] = tId;
                                }
                            }

                            // 2. 태그 매핑
                            var tagMap = new Dictionary<long, long>(); // SourceId -> TargetId
                            using (var cmd = new SqliteCommand("SELECT Id, Name FROM Tags", sourceConn))
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    long sId = reader.GetInt64(0);
                                    string name = reader.GetString(1);
                                    
                                    long tId;
                                    using (var checkCmd = new SqliteCommand("INSERT OR IGNORE INTO Tags (Name) VALUES ($name); SELECT Id FROM Tags WHERE Name = $name;", targetConn, transaction))
                                    {
                                        checkCmd.Parameters.AddWithValue("$name", name);
                                        tId = Convert.ToInt64(checkCmd.ExecuteScalar());
                                    }
                                    tagMap[sId] = tId;
                                }
                            }

                            // 3. 노트 병합 (날짜 순 정렬)
                            string noteSql = "SELECT * FROM Notes ORDER BY CreatedAt ASC";
                            var sourceNotes = new List<Dictionary<string, object>>();
                            using (var cmd = new SqliteCommand(noteSql, sourceConn))
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    var row = new Dictionary<string, object>();
                                    for (int i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.GetValue(i);
                                    sourceNotes.Add(row);
                                }
                            }

                            int count = 0;
                            string targetImgDir = GetImageFolderPath();
                            string targetAttachDir = GetAttachmentsFolderPath();

                            foreach (var note in sourceNotes)
                            {
                                long sourceNoteId = (long)note["Id"];
                                onProgress?.Invoke($"Merging: {note["Title"] ?? "Untitled"} ({++count}/{sourceNotes.Count})");

                                // A. 노트 삽입
                                string insertNoteSql = @"
                                    INSERT INTO Notes (Title, Content, ContentXaml, SourceApp, SourceUrl, CreatedAt, UpdatedAt, IsFavorite, Status, PasswordHash, CategoryId, IsPinned, DeletedAt)
                                    VALUES ($title, $content, $contentXaml, $sourceApp, $sourceUrl, $createdAt, $updatedAt, $isFav, $status, $pHash, $catId, $isPinned, $delAt);
                                    SELECT last_insert_rowid();";
                                
                                long newNoteId;
                                using (var cmd = new SqliteCommand(insertNoteSql, targetConn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("$title", note["Title"]);
                                    cmd.Parameters.AddWithValue("$content", note["Content"]);
                                    cmd.Parameters.AddWithValue("$contentXaml", note["ContentXaml"]);
                                    cmd.Parameters.AddWithValue("$sourceApp", note["SourceApp"]);
                                    cmd.Parameters.AddWithValue("$sourceUrl", note["SourceUrl"]);
                                    cmd.Parameters.AddWithValue("$createdAt", note["CreatedAt"]);
                                    cmd.Parameters.AddWithValue("$updatedAt", note["UpdatedAt"]);
                                    cmd.Parameters.AddWithValue("$isFav", note["IsFavorite"]);
                                    cmd.Parameters.AddWithValue("$status", note["Status"]);
                                    cmd.Parameters.AddWithValue("$pHash", note["PasswordHash"]);
                                    cmd.Parameters.AddWithValue("$catId", categoryMap.ContainsKey(Convert.ToInt64(note["CategoryId"])) ? categoryMap[Convert.ToInt64(note["CategoryId"])] : 1);
                                    cmd.Parameters.AddWithValue("$isPinned", note.ContainsKey("IsPinned") ? note["IsPinned"] : 0);
                                    cmd.Parameters.AddWithValue("$delAt", note.ContainsKey("DeletedAt") ? note["DeletedAt"] : DBNull.Value);
                                    newNoteId = Convert.ToInt64(cmd.ExecuteScalar());
                                }

                                // B. 이미지 복사 및 삽입
                                using (var imgCmd = new SqliteCommand("SELECT FilePath, OrderIndex, FileHash FROM NoteImages WHERE NoteId = $nid", sourceConn))
                                {
                                    imgCmd.Parameters.AddWithValue("$nid", sourceNoteId);
                                    using (var reader = imgCmd.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            string relPath = reader.GetString(0);
                                            string sourceFullPath = Path.Combine(sourceImgDir, relPath);
                                            
                                            if (File.Exists(sourceFullPath))
                                            {
                                                string hash = reader.IsDBNull(2) ? ComputeHash(sourceFullPath) : reader.GetString(2);
                                                
                                                // 동일 Hash가 이미 있으면 재사용
                                                string? existing = GetExistingImageByHashInTransaction(hash, targetConn, transaction);
                                                string finalRelPath;

                                                if (existing != null)
                                                {
                                                    finalRelPath = existing;
                                                }
                                                else
                                                {
                                                    string destRelPath = GetUniqueRelativePath(targetImgDir, relPath);
                                                    string destFullPath = Path.Combine(targetImgDir, destRelPath);
                                                    
                                                    string? destDir = Path.GetDirectoryName(destFullPath);
                                                    if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                                                    
                                                    File.Copy(sourceFullPath, destFullPath, true);
                                                    finalRelPath = destRelPath;
                                                }

                                                using (var insImg = new SqliteCommand("INSERT INTO NoteImages (NoteId, FilePath, OrderIndex, FileHash) VALUES ($nid, $path, $idx, $hash)", targetConn, transaction))
                                                {
                                                    insImg.Parameters.AddWithValue("$nid", newNoteId);
                                                    insImg.Parameters.AddWithValue("$path", finalRelPath);
                                                    insImg.Parameters.AddWithValue("$idx", reader.GetInt32(1));
                                                    insImg.Parameters.AddWithValue("$hash", hash);
                                                    insImg.ExecuteNonQuery();
                                                }
                                            }
                                        }
                                    }
                                }

                                // C. 태그 연결
                                using (var tagCmd = new SqliteCommand("SELECT TagId FROM NoteTags WHERE NoteId = $nid", sourceConn))
                                {
                                    tagCmd.Parameters.AddWithValue("$nid", sourceNoteId);
                                    using (var reader = tagCmd.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            long sTagId = reader.GetInt64(0);
                                            if (tagMap.TryGetValue(sTagId, out long tTagId))
                                            {
                                                using (var insTag = new SqliteCommand("INSERT OR IGNORE INTO NoteTags (NoteId, TagId) VALUES ($nid, $tid)", targetConn, transaction))
                                                {
                                                    insTag.Parameters.AddWithValue("$nid", newNoteId);
                                                    insTag.Parameters.AddWithValue("$tid", tTagId);
                                                    insTag.ExecuteNonQuery();
                                                }
                                            }
                                        }
                                    }
                                }

                                // D. 첨부파일 복사 및 삽입
                                using (var attCmd = new SqliteCommand("SELECT FilePath, OriginalName, FileType FROM NoteAttachments WHERE NoteId = $nid", sourceConn))
                                {
                                    attCmd.Parameters.AddWithValue("$nid", sourceNoteId);
                                    using (var reader = attCmd.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            string relPath = reader.GetString(0);
                                            string sourceFullPath = Path.Combine(sourceAttachDir, relPath);

                                            if (File.Exists(sourceFullPath))
                                            {
                                                string destRelPath = GetUniqueRelativePath(targetAttachDir, relPath);
                                                string destFullPath = Path.Combine(targetAttachDir, destRelPath);

                                                string? destDir = Path.GetDirectoryName(destFullPath);
                                                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

                                                File.Copy(sourceFullPath, destFullPath, true);

                                                using (var insAtt = new SqliteCommand("INSERT INTO NoteAttachments (NoteId, FilePath, OriginalName, FileType) VALUES ($nid, $path, $oname, $type)", targetConn, transaction))
                                                {
                                                    insAtt.Parameters.AddWithValue("$nid", newNoteId);
                                                    insAtt.Parameters.AddWithValue("$path", destRelPath);
                                                    insAtt.Parameters.AddWithValue("$oname", reader.GetString(1));
                                                    insAtt.Parameters.AddWithValue("$type", reader.GetString(2));
                                                    insAtt.ExecuteNonQuery();
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private string? GetExistingImageByHashInTransaction(string hash, SqliteConnection conn, SqliteTransaction trans)
        {
            if (string.IsNullOrEmpty(hash)) return null;
            string sql = "SELECT FilePath FROM NoteImages WHERE FileHash = $hash LIMIT 1";
            using (var command = new SqliteCommand(sql, conn, trans))
            {
                command.Parameters.AddWithValue("$hash", hash);
                var result = command.ExecuteScalar();
                if (result != null)
                {
                    string filePath = result.ToString()!;
                    if (File.Exists(Path.Combine(GetImageFolderPath(), filePath))) return filePath;
                }
            }
            return null;
        }

        private string GetUniqueRelativePath(string baseFolder, string relativePath)
        {
            string fullPath = Path.Combine(baseFolder, relativePath);
            if (!File.Exists(fullPath)) return relativePath;

            string directory = Path.GetDirectoryName(relativePath) ?? "";
            string fileName = Path.GetFileNameWithoutExtension(relativePath);
            string extension = Path.GetExtension(relativePath);
            
            int counter = 1;
            string newRelativePath;
            do
            {
                string newFileName = $"{fileName}_{counter++}{extension}";
                newRelativePath = Path.Combine(directory, newFileName);
            } while (File.Exists(Path.Combine(baseFolder, newRelativePath)));

            return newRelativePath;
        }

        public void CleanupTempFiles()
        {
            Task.Run(() =>
            {
                try
                {
                    string tempDir = Path.GetTempPath();
                    var files = Directory.GetFiles(tempDir, "catchcapture_temp_*");
                    foreach (var file in files)
                    {
                        try
                        {
                            if (File.GetCreationTime(file) < DateTime.Now.AddHours(-1)) File.Delete(file);
                        }
                        catch { }
                    }
                }
                catch { }
            });
        }

        #region History (Captures) Management

        public long InsertCapture(HistoryItem item)
        {
            using (var connection = new SqliteConnection($"Data Source={HistoryDbPath}"))
            {
                connection.Open();
                string sql = @"
                    INSERT INTO Captures (FileName, FilePath, SourceApp, SourceTitle, OriginalFilePath, IsFavorite, Status, FileSize, Resolution)
                    VALUES ($fileName, $filePath, $sourceApp, $sourceTitle, $originalPath, $isFavorite, $status, $fileSize, $resolution);
                    SELECT last_insert_rowid();";
                
                using (var command = new SqliteCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("$fileName", item.FileName);
                    command.Parameters.AddWithValue("$filePath", item.FilePath);
                    command.Parameters.AddWithValue("$sourceApp", (object?)item.SourceApp ?? DBNull.Value);
                    command.Parameters.AddWithValue("$sourceTitle", (object?)item.SourceTitle ?? DBNull.Value);
                    command.Parameters.AddWithValue("$originalPath", (object?)item.OriginalFilePath ?? DBNull.Value);
                    command.Parameters.AddWithValue("$isFavorite", item.IsFavorite ? 1 : 0);
                    command.Parameters.AddWithValue("$status", item.Status);
                    command.Parameters.AddWithValue("$fileSize", item.FileSize);
                    command.Parameters.AddWithValue("$resolution", item.Resolution);
                    return Convert.ToInt64(command.ExecuteScalar());
                }
            }
        }

        public List<HistoryItem> GetHistory(string filter = "All", string search = "", DateTime? dateFrom = null, DateTime? dateTo = null, string? fileType = null, int limit = 0, int offset = 0)
        {
            var items = new List<HistoryItem>();
            using (var connection = new SqliteConnection($"Data Source={HistoryDbPath}"))
            {
                connection.Open();
                
                List<string> wheres = new List<string>();
                if (filter == "Trash") wheres.Add("Status = 1");
                else
                {
                    wheres.Add("Status = 0");
                    if (filter == "Favorite") wheres.Add("IsFavorite = 1");
                    else if (filter == "Recent7") wheres.Add("CreatedAt >= date('now', 'localtime', '-7 days')");
                    else if (filter == "Recent30") wheres.Add("CreatedAt >= date('now', 'localtime', '-30 days')");
                    else if (filter == "Recent3Months") wheres.Add("CreatedAt >= date('now', 'localtime', '-3 months')");
                    else if (filter == "Recent6Months") wheres.Add("CreatedAt >= date('now', 'localtime', '-6 months')");
                }

                if (!string.IsNullOrEmpty(search))
                {
                    wheres.Add("(FileName LIKE $search OR SourceApp LIKE $search OR SourceTitle LIKE $search OR Memo LIKE $search)");
                }

                if (dateFrom.HasValue)
                {
                    wheres.Add("CreatedAt >= $dateFrom");
                }
                if (dateTo.HasValue)
                {
                    wheres.Add("CreatedAt <= $dateTo");
                }
                if (!string.IsNullOrEmpty(fileType) && fileType != "전체")
                {
                    if (fileType.Contains("이미지"))
                        wheres.Add("(FileName LIKE '%.png' OR FileName LIKE '%.jpg' OR FileName LIKE '%.jpeg' OR FileName LIKE '%.webp' OR FileName LIKE '%.bmp' OR FileName LIKE '%.gif')");
                    else if (fileType.Contains("미디어"))
                        wheres.Add("(FileName LIKE '%.mp4' OR FileName LIKE '%.mp3')");
                }

                string whereClause = wheres.Count > 0 ? " WHERE " + string.Join(" AND ", wheres) : "";
                string pagination = limit > 0 ? $" LIMIT {limit} OFFSET {offset}" : "";
                string sql = $"SELECT Id, FileName, FilePath, SourceApp, SourceTitle, IsFavorite, Status, FileSize, Resolution, CreatedAt, OriginalFilePath, IsPinned, Memo FROM Captures {whereClause} ORDER BY IsPinned DESC, CreatedAt DESC{pagination}";

                using (var command = new SqliteCommand(sql, connection))
                {
                    if (!string.IsNullOrEmpty(search)) command.Parameters.AddWithValue("$search", $"%{search}%");
                    if (dateFrom.HasValue) command.Parameters.AddWithValue("$dateFrom", dateFrom.Value.ToString("yyyy-MM-dd 00:00:00"));
                    if (dateTo.HasValue) command.Parameters.AddWithValue("$dateTo", dateTo.Value.ToString("yyyy-MM-dd 23:59:59"));
                    
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new HistoryItem
                            {
                                Id = reader.GetInt64(0),
                                FileName = reader.GetString(1),
                                FilePath = reader.GetString(2),
                                SourceApp = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                SourceTitle = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                IsFavorite = reader.GetInt32(5) == 1,
                                Status = reader.GetInt32(6),
                                FileSize = reader.GetInt64(7),
                                Resolution = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                CreatedAt = reader.GetDateTime(9),
                                OriginalFilePath = reader.IsDBNull(10) ? "" : reader.GetString(10),
                                IsPinned = reader.GetInt32(11) == 1,
                                Memo = reader.IsDBNull(12) ? "" : reader.GetString(12)
                            });
                        }
                    }
                }
            }
            return items;
        }

        public int GetHistoryCount(string filter = "All", string search = "", DateTime? dateFrom = null, DateTime? dateTo = null, string? fileType = null)
        {
            using (var connection = new SqliteConnection($"Data Source={HistoryDbPath}"))
            {
                connection.Open();
                List<string> wheres = new List<string>();
                if (filter == "Trash") wheres.Add("Status = 1");
                else
                {
                    wheres.Add("Status = 0");
                    if (filter == "Favorite") wheres.Add("IsFavorite = 1");
                    else if (filter == "Recent7") wheres.Add("CreatedAt >= date('now', 'localtime', '-7 days')");
                    else if (filter == "Recent30") wheres.Add("CreatedAt >= date('now', 'localtime', '-30 days')");
                    else if (filter == "Recent3Months") wheres.Add("CreatedAt >= date('now', 'localtime', '-3 months')");
                    else if (filter == "Recent6Months") wheres.Add("CreatedAt >= date('now', 'localtime', '-6 months')");
                }

                if (!string.IsNullOrEmpty(search)) wheres.Add("(FileName LIKE $search OR SourceApp LIKE $search OR SourceTitle LIKE $search)");
                if (dateFrom.HasValue) wheres.Add("CreatedAt >= $dateFrom");
                if (dateTo.HasValue) wheres.Add("CreatedAt <= $dateTo");
                if (!string.IsNullOrEmpty(fileType) && fileType != "전체")
                {
                    if (fileType.Contains("이미지"))
                        wheres.Add("(FileName LIKE '%.png' OR FileName LIKE '%.jpg' OR FileName LIKE '%.jpeg' OR FileName LIKE '%.webp' OR FileName LIKE '%.bmp' OR FileName LIKE '%.gif')");
                    else if (fileType.Contains("미디어"))
                        wheres.Add("(FileName LIKE '%.mp4' OR FileName LIKE '%.mp3')");
                }

                string whereClause = wheres.Count > 0 ? " WHERE " + string.Join(" AND ", wheres) : "";
                string sql = $"SELECT COUNT(*) FROM Captures {whereClause}";
                using (var command = new SqliteCommand(sql, connection))
                {
                    if (!string.IsNullOrEmpty(search)) command.Parameters.AddWithValue("$search", $"%{search}%");
                    if (dateFrom.HasValue) command.Parameters.AddWithValue("$dateFrom", dateFrom.Value.ToString("yyyy-MM-dd 00:00:00"));
                    if (dateTo.HasValue) command.Parameters.AddWithValue("$dateTo", dateTo.Value.ToString("yyyy-MM-dd 23:59:59"));
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        public void DeleteCapture(long id, bool permanent = false)
        {
            using (var connection = new SqliteConnection($"Data Source={HistoryDbPath}"))
            {
                connection.Open();
                if (permanent)
                {
                    // Get file paths first to delete files
                    string? path = null;
                    string? originalPath = null;
                    using (var cmd = new SqliteCommand("SELECT FilePath, OriginalFilePath FROM Captures WHERE Id = $id", connection))
                    {
                        cmd.Parameters.AddWithValue("$id", id);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                path = reader.IsDBNull(0) ? null : reader.GetString(0);
                                originalPath = reader.IsDBNull(1) ? null : reader.GetString(1);
                            }
                        }
                    }

                    // Delete managed copy
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try 
                { 
                    File.Delete(path); 
                    // [추가] 프리뷰용 썸네일 파일도 함께 삭제
                    string thumbPath = path + ".preview.png";
                    if (File.Exists(thumbPath)) File.Delete(thumbPath);
                } 
                catch { }
            }

            // Delete user's original copy
            if (!string.IsNullOrEmpty(originalPath) && originalPath != path && File.Exists(originalPath))
            {
                try 
                { 
                    File.Delete(originalPath);
                    // [추가] 원본 경로 기준 프리뷰 파일도 존재한다면 삭제
                    string thumbPath = originalPath + ".preview.png";
                    if (File.Exists(thumbPath)) File.Delete(thumbPath);
                } 
                catch { }
            }

            using (var cmd = new SqliteCommand("DELETE FROM Captures WHERE Id = $id", connection))
            {
                cmd.Parameters.AddWithValue("$id", id);
                cmd.ExecuteNonQuery();
            }
        }
        else
                {
                    using (var cmd = new SqliteCommand("UPDATE Captures SET Status = 1 WHERE Id = $id", connection))
                    {
                        cmd.Parameters.AddWithValue("$id", id);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public void RestoreCapture(long id)
        {
            using (var connection = new SqliteConnection($"Data Source={HistoryDbPath}"))
            {
                connection.Open();
                using (var cmd = new SqliteCommand("UPDATE Captures SET Status = 0 WHERE Id = $id", connection))
                {
                    cmd.Parameters.AddWithValue("$id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void ToggleFavoriteCapture(long id)
        {
            using (var connection = new SqliteConnection($"Data Source={HistoryDbPath}"))
            {
                connection.Open();
                using (var cmd = new SqliteCommand("UPDATE Captures SET IsFavorite = 1 - IsFavorite WHERE Id = $id", connection))
                {
                    cmd.Parameters.AddWithValue("$id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        #endregion
        public void EmptyTrash()
        {
            using (var connection = new SqliteConnection($"Data Source={HistoryDbPath}"))
            {
                connection.Open();
                var trashIds = new List<long>();
                using (var cmd = new SqliteCommand("SELECT Id FROM Captures WHERE Status = 1", connection))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            trashIds.Add(reader.GetInt64(0));
                        }
                    }
                }

                foreach (var id in trashIds)
                {
                    DeleteCapture(id, true);
                }
            }
        }
        public void TogglePinCapture(long id)
        {
            using (var connection = new SqliteConnection($"Data Source={HistoryDbPath}"))
            {
                connection.Open();
                using (var cmd = new SqliteCommand("UPDATE Captures SET IsPinned = CASE WHEN IsPinned = 1 THEN 0 ELSE 1 END WHERE Id = $id", connection))
                {
                    cmd.Parameters.AddWithValue("$id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void UpdateCaptureMemo(long id, string memo)
        {
            using (var connection = new SqliteConnection($"Data Source={HistoryDbPath}"))
            {
                connection.Open();
                using (var cmd = new SqliteCommand("UPDATE Captures SET Memo = $memo WHERE Id = $id", connection))
                {
                    cmd.Parameters.AddWithValue("$memo", memo);
                    cmd.Parameters.AddWithValue("$id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
