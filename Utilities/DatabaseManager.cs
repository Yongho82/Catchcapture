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

        // 1. 경로 관리
        private string _cloudDbPath = default!;        // 클라우드 원본
        private string _cloudHistoryDbPath = default!; // 클라우드 히스토리
        private string _localDbPath = default!;        // 로컬 작업용 (AppData)
        private string _localHistoryDbPath = default!; // 로컬 히스토리 (AppData)
        private string _localSyncMetaPath = default!;  // [추가] 마지막 동기화 경로 기록용 (오프라인 방지)

        // 외부에서는 로컬 경로를 보게 함 (호환성을 위해 기존 이름 DbPath 사용)
        public string DbPath => _localDbPath;
        public string HistoryDbPath => _localHistoryDbPath;

        // Alias for compatibility
        public string DbFilePath => _localDbPath;
        public string HistoryDbFilePath => _localHistoryDbPath;

        // 원본 경로는 필요할 때만 접근
        public string CloudDbFilePath => _cloudDbPath;
        public string CloudHistoryDbFilePath => _cloudHistoryDbPath;

        private static readonly object _dbLock = new object();
        private readonly object _lockObj = new object();
        private readonly string _currentIdentity = $"{Environment.MachineName}:{Environment.ProcessId}";
        
        private SqliteConnection? _activeConnection;
        
        // 권한 관리 상태
        public bool IsReadOnly { get; private set; } = true; // 기본은 읽기 전용으로 시작
        public bool IsOfflineMode { get; private set; } = false; // [추가] 오프라인 모드 상태 플래그
        
        // _syncTimer 제거됨

        // 디버그 로그 파일 경로
        private static readonly string _logFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CatchCapture",
            "CatchCapture_LockDebug.txt");
        private static readonly object _logLock = new object();

        // 락킹 상태 정보
        public string? NoteLockingMachine => CheckLock(_cloudDbPath);
        public string? HistoryLockingMachine => CheckLock(_cloudHistoryDbPath);
        
        // 데이터베이스 초기화 상태
        public bool IsHistoryDatabaseReady { get; private set; } = false;

        /// <summary>
        /// 디버그 로그를 txt 파일로 저장
        /// </summary>
        public void LogToFile(string message)
        {
            try
            {
                lock (_logLock)
                {
                    string? dir = Path.GetDirectoryName(_logFilePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

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
            InitializePaths();

            // ★ 버전 확인용 시작 로그
            LogToFile("============================================");
            LogToFile("★★★ DatabaseManager 초기화 (Local-First 구조) ★★★");
            LogToFile($"Cloud DB: {_cloudDbPath}");
            LogToFile($"Local DB: {_localDbPath}");
            LogToFile("============================================");

            // 3. 파일 동기화 (Cloud -> Local)
            SyncFromCloudToLocal();

            // 4. DB 초기화 (Local 파일 기준)
            InitializeDatabase();
            InitializeHistoryDatabase(); 
            
            // 5. 초기 권한 체크 (Lock이 있으면 ReadOnly)
            CheckInitialLockStatus();

            Settings.SettingsChanged += (s, e) => {
                Reinitialize();
            };
        }

        public event EventHandler<string>? OfflineModeDetected;
        public event EventHandler<string>? CloudSaveFailed;

        private void InitializePaths()
        {
            var settings = Settings.Load();
            
            // 1. 클라우드(원본) 경로 설정 (노트)
            string cloudStoragePath = settings.NoteStoragePath;
            if (string.IsNullOrEmpty(cloudStoragePath))
            {
                cloudStoragePath = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "CatchCapture"), "notedata");
            }
            else
            {
                // [추가] 사용자가 지정한 폴더 하위에 CatchCapture 폴더 보장
                cloudStoragePath = EnsureCatchCaptureSubFolder(cloudStoragePath);
                cloudStoragePath = ResolveStoragePath(cloudStoragePath);
            }
            
            _cloudDbPath = Path.Combine(cloudStoragePath, "notedb", "catch_notes.db");

            // 2. 히스토리 클라우드 경로 설정
            string saveFolder = settings.DefaultSaveFolder;
            if (string.IsNullOrEmpty(saveFolder))
            {
                saveFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "CatchCapture");
            }
            else
            {
                // [추가] 히스토리도 지정 폴더 하위에 CatchCapture 폴더 보장
                saveFolder = EnsureCatchCaptureSubFolder(saveFolder);
            }
            _cloudHistoryDbPath = Path.Combine(saveFolder, "history", "history.db");

            // 2. 로컬(작업용) 경로 설정 (AppData/Local/CatchCapture)
            string localAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CatchCapture");
            if (!Directory.Exists(localAppData)) Directory.CreateDirectory(localAppData);

            _localDbPath = Path.Combine(localAppData, "catch_notes_local.db");
            _localHistoryDbPath = Path.Combine(localAppData, "history_local.db");
            _localSyncMetaPath = Path.Combine(localAppData, "catch_sync_meta.txt");
        }

        private void CheckInitialLockStatus()
        {
            string? lockingMachine = NoteLockingMachine;
            if (string.IsNullOrEmpty(lockingMachine) || lockingMachine == _currentIdentity)
            {
                // 아무도 안 쓰고 있으면 (또는 내가 쓰던거면) -> 일단은 ReadOnly로 시작하되, 
                // 사용자가 편집 시도할 때 Lock 잡도록 유도 (혹은 자동 획득 가능)
                // 정책: 시작은 조용하게 ReadOnly로. 사용자가 쓰려고 할 때 TakeOwnership 호출.
                IsReadOnly = true; 
            }
            else
            {
                // 남이 쓰고 있으면 -> 당연히 ReadOnly
                IsReadOnly = true;
            }
            LogToFile($"초기 모드: {(IsReadOnly ? "읽기 전용" : "편집 가능")}");
        }

        private void OpenConnection()
        {
            lock (_dbLock)
            {
                if (_activeConnection == null)
                {
                    _activeConnection = new SqliteConnection($"Data Source={_localDbPath}");
                }
                
                if (_activeConnection.State != System.Data.ConnectionState.Open)
                {
                    _activeConnection.Open();
                }
            }
        }

        public void CloseConnection()
        {
            lock (_dbLock)
            {
                if (_activeConnection != null)
                {
                    _activeConnection.Close();
                    SqliteConnection.ClearAllPools(); // 중요: 파일 핸들 해제
                    _activeConnection = null;
                }
            }
        }

        public void Reinitialize()
        {
            try
            {
                LogToFile("경로 설정 변경 감지. 재초기화 시작...");
                CloseConnection();
                InitializePaths();
                
                // 새로운 경로에서 데이터 가져오기 시도
                SyncFromCloudToLocal();
                
                InitializeDatabase();
                InitializeHistoryDatabase();
                CheckInitialLockStatus();
                LogToFile("재초기화 완료.");
            }
            catch (Exception ex)
            {
                LogToFile($"[ERROR] Reinitialize 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 클라우드(원본)에서 로컬(작업본)로 파일을 복사합니다.
        /// </summary>
        public void SyncFromCloudToLocal()
        {
            try
            {
                IsOfflineMode = false; // 기본적으로 초기화

                // [중요] 내 Lock이 걸려있다면? -> 비정상 종료 등으로 로컬 데이터가 아직 백업 안 된 상태일 수 있음.
                if (IsMyLock())
                {
                    LogToFile("[SKIP] 내 Lock 파일이 존재하므로 로컬 데이터를 보존합니다. (동기화 건너뜀)");
                    return;
                }

                LogToFile("Cloud -> Local 동기화 시작...");

                // 1. Note DB 처리
                if (File.Exists(_cloudDbPath))
                {
                    // 클라우드 존재 -> 로컬로 복사 (Source of Truth)
                    File.Copy(_cloudDbPath, _localDbPath, true);
                    LogToFile($"Note DB 강제 동기화 완료: {_cloudDbPath} -> {_localDbPath}");
                    
                    // 성공했으므로 메타 업데이트
                    SetLastSyncedPath("NotePath", _cloudDbPath);
                }
                else
                {
                    // 클라우드 없음 -> 오프라인인지, 경로 변경인지 판단
                    string lastPath = GetLastSyncedPath("NotePath");
                    if (lastPath == _cloudDbPath)
                    {
                         // 경로는 같은데 파일이 없다? -> 일시적 연결 실패(오프라인)로 간주
                         // 로컬 데이터 보존!
                         // 로컬 데이터 보존!
                         // 로컬 데이터 보존!
                         IsOfflineMode = true;
                         LogToFile($"[OFFLINE PROTECT] Note DB 클라우드 파일 없음. 오프라인 모드로 로컬 데이터 보존함. ({_cloudDbPath})");
                         OfflineModeDetected?.Invoke(this, "클라우드 노트 저장소 연결에 실패하여 오프라인(로컬) 모드로 시작합니다.\n인터넷 연결을 확인하세요.");
                    }
                    else
                    {
                        // 경로가 달라짐 -> 사용자가 진짜로 경로를 바꿈 (또는 첫 설치)
                        // 이 경우엔 기존 로컬 데이터가 쓸모 없거나 꼬일 수 있으므로 삭제 (기존 로직 유지)
                        if (File.Exists(_localDbPath)) File.Delete(_localDbPath);
                        LogToFile($"Note DB 클라우드 없음 & 경로 변경됨. 로컬 초기화. (Old: {lastPath} -> New: {_cloudDbPath})");
                        
                        // 새 경로는 아직 파일이 없지만, 이 상태를 '동기화된 상태'로 볼 것인가?
                        // 보통 새 폴더 지정시 빈 DB 생성은 InitializeDatabase에서 함. 
                        // 다음 실행시 오프라인 보호를 위해 미리 메타 기록
                        SetLastSyncedPath("NotePath", _cloudDbPath);
                    }
                }

                // 2. History DB 처리
                if (File.Exists(_cloudHistoryDbPath))
                {
                    File.Copy(_cloudHistoryDbPath, _localHistoryDbPath, true);
                    LogToFile("History DB 강제 동기화 완료");
                    SetLastSyncedPath("HistoryPath", _cloudHistoryDbPath);
                }
                else
                {
                    // History도 동일 로직
                    string lastPath = GetLastSyncedPath("HistoryPath");
                    if (lastPath == _cloudHistoryDbPath)
                    {
                         IsOfflineMode = true;
                         LogToFile($"[OFFLINE PROTECT] History DB 클라우드 파일 없음. 로컬 데이터 보존함.");
                    }
                    else
                    {
                        if (File.Exists(_localHistoryDbPath)) File.Delete(_localHistoryDbPath);
                        LogToFile("History DB 클라우드 없음 & 경로 변경됨. 로컬 초기화.");
                        SetLastSyncedPath("HistoryPath", _cloudHistoryDbPath);
                    }
                }
                
                LogToFile("Cloud -> Local 동기화 완료");
            }
            catch (Exception ex)
            {
                LogToFile($"[ERROR] 동기화 실패: {ex.Message}");
            }
        }

        private string GetLastSyncedPath(string key)
        {
            try
            {
                if (!File.Exists(_localSyncMetaPath)) return string.Empty;
                var lines = File.ReadAllLines(_localSyncMetaPath);
                foreach (var line in lines)
                {
                    var parts = line.Split(new[]{'|'}, 2);
                    if (parts.Length == 2 && parts[0] == key) return parts[1];
                }
            }
            catch { }
            return string.Empty;
        }

        private void SetLastSyncedPath(string key, string value)
        {
            try
            {
                var dict = new Dictionary<string, string>();
                if (File.Exists(_localSyncMetaPath))
                {
                    var lines = File.ReadAllLines(_localSyncMetaPath);
                    foreach (var line in lines)
                    {
                        var parts = line.Split(new[]{'|'}, 2);
                        if (parts.Length == 2) dict[parts[0]] = parts[1];
                    }
                }
                
                dict[key] = value;
                
                var newLines = dict.Select(kv => $"{kv.Key}|{kv.Value}").ToArray();
                File.WriteAllLines(_localSyncMetaPath, newLines);
            }
            catch { }
        }

        private bool IsMyLock()
        {
            try
            {
                string lockPath = _cloudDbPath + ".lock";
                if (!File.Exists(lockPath)) return false;
                
                // SafeReadFile은 아래에 정의됨 (382라인 근처)
                string content = SafeReadFile(lockPath); 
                string[] parts = content.Split('|');
                return parts.Length > 0 && parts[0] == _currentIdentity;
            }
            catch { return false; }
        }

        private void CopyIfNewer(string source, string dest)
        {
            // 원본이 없으면 복사할 게 없음
            if (!File.Exists(source)) return;

            string destDir = Path.GetDirectoryName(dest)!;
            if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

            // 로컬 파일이 존재하면 날짜 비교
            if (File.Exists(dest))
            {
                DateTime sourceTime = File.GetLastWriteTimeUtc(source);
                DateTime destTime = File.GetLastWriteTimeUtc(dest);

                // 로컬이 클라우드보다 최신이거나 같으면 덮어쓰지 않음 (데이터 유실 절대 방지)
                if (destTime >= sourceTime)
                {
                    LogToFile($"[SKIP] 로컬 파일이 최신이거나 같음. 동기화 건너뜀. (Local: {destTime} >= Cloud: {sourceTime})");
                    return;
                }
            }

            File.Copy(source, dest, true);
            LogToFile($"파일 복사됨: {Path.GetFileName(source)} -> Local");
        }

        public void SyncToCloud(bool isLive = false)
        {
            if (IsOfflineMode)
            {
                LogToFile("[SKIP] 오프라인 모드이므로 클라우드 저장(백업)을 건너뜀.");
                return;
            }

            // IsReadOnly 상태라도 데이터 보호를 위해 백업 시도
            if (IsReadOnly)
            {
                if (IsLocked(_cloudDbPath) || IsLocked(_cloudHistoryDbPath))
                {
                    LogToFile("[SKIP] 읽기 전용 모드이며 다른 사용자가 있어 백업 건너뜀.");
                    return;
                }
            }

            try
            {
                LogToFile($"Local -> Cloud 저장(백업) 시작... (Live: {isLive})");

                // 클라우드 디렉토리 존재 확인 및 생성
                string cloudDbDir = Path.GetDirectoryName(_cloudDbPath)!;
                string cloudNoteDataRoot = Path.GetDirectoryName(cloudDbDir)!; // notedata 폴더
                
                if (!Directory.Exists(cloudDbDir)) Directory.CreateDirectory(cloudDbDir);
                
                // [추가] notedata 하위에 img, attachments 폴더 생성 보장
                string cloudImgDir = Path.Combine(cloudNoteDataRoot, "img");
                string cloudAttachDir = Path.Combine(cloudNoteDataRoot, "attachments");
                if (!Directory.Exists(cloudImgDir)) Directory.CreateDirectory(cloudImgDir);
                if (!Directory.Exists(cloudAttachDir)) Directory.CreateDirectory(cloudAttachDir);
                
                string cloudHistoryDir = Path.GetDirectoryName(_cloudHistoryDbPath)!;
                if (!Directory.Exists(cloudHistoryDir)) Directory.CreateDirectory(cloudHistoryDir);

                if (isLive)
                {
                    // [실행 중 백업] 연결 유지 + SQLite Backup API 사용
                    // 주의: _activeConnection이 없으면 일시적으로 열어서 처리
                    bool needClose = false;
                    if (_activeConnection == null || _activeConnection.State != System.Data.ConnectionState.Open)
                    {
                        OpenConnection();
                        needClose = true;
                    }

                    using (var cloudConnection = new SqliteConnection($"Data Source={_cloudDbPath}"))
                    {
                        cloudConnection.Open();
                        _activeConnection!.BackupDatabase(cloudConnection);
                        LogToFile("Note DB Live 백업 완료 (API)");
                    }
                    
                    if (needClose) CloseConnection();
                }
                else
                {
                    // [종료 시 백업] 연결 확실히 종료 + Atomic Copy (파일 깨짐 방지)
                    CloseConnection();

                    string tempPath = _cloudDbPath + ".tmp";
                    File.Copy(_localDbPath, tempPath, true);
                    
                    // 복사 성공 시 원본 교체
                    if (File.Exists(_cloudDbPath)) File.Delete(_cloudDbPath);
                    File.Move(tempPath, _cloudDbPath);
                    
                    LogToFile($"Note DB 복사 완료(Atomic): {_localDbPath} -> {_cloudDbPath}");
                }

                // History DB 백업 (단순 복사, 실패해도 무방)
                try
                {
                    File.Copy(_localHistoryDbPath, _cloudHistoryDbPath, true);
                    LogToFile("History DB 복사 완료");
                }
                catch { }

                LogToFile("Local -> Cloud 저장 완료");
            }
            catch (Exception ex)
            {
                LogToFile($"[ERROR] 클라우드 저장 실패: {ex.Message}");
                CloudSaveFailed?.Invoke(this, $"클라우드 저장 실패: {ex.Message}");
            }
        }


        private void StartHeartbeat()
        {
             // 삭제된 기능
        }

        public enum TakeoverStatus
        {
            None,      // Lock 없음 (사용 가능)
            Locked     // 남이 사용 중
        }

        public TakeoverStatus CheckTakeoverStatus()
        {
            try
            {
                // Note DB Lock 체크
                if (IsLocked(_cloudDbPath)) return TakeoverStatus.Locked;
                return TakeoverStatus.None;
            }
            catch { return TakeoverStatus.None; }
        }

        private bool IsLocked(string dbPath)
        {
            string lockPath = dbPath + ".lock";
            if (!File.Exists(lockPath)) return false;
            
            // 내 Lock이면 Locked가 아님
            try {
                string content = File.ReadAllText(lockPath);
                string[] parts = content.Split('|');
                if (parts.Length > 0 && parts[0] == _currentIdentity) return false;
            } catch {}

            return true;
        }

        // Heartbeat 제거됨 (UpdateLocks 삭제)

        /// <summary>
        /// 편집 권한 반납 (저장 후 Lock 해제)
        /// </summary>
        public void ReleaseOwnership()
        {
            try
            {
                LogToFile("========== ReleaseOwnership 시작 ==========");
                
                // 1. Cloud로 데이터 백업 (동기화)
                // SyncToCloud();
                
                // 2. Lock 파일 삭제
                RemoveSingleLock(_cloudDbPath);
                RemoveSingleLock(_cloudHistoryDbPath);
                
                // 3. 읽기 전용으로 전환
                IsReadOnly = true;
                LogToFile("Lock 해제 및 ReadOnly 모드 전환 완료");
            }
            catch (Exception ex)
            {
                LogToFile($"[ERROR] ReleaseOwnership 예외: {ex.Message}");
            }
        }

        private void ReleaseForTakeover(string newOwnerIdentity)
        {
            // 삭제된 기능
        }

        private string SafeReadFile(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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
            // 사용하지 않음 (CheckInitialLockStatus로 대체됨)
        }

        public void ClearLockState()
        {
            // 사용하지 않음 (호환성 유지용 빈 메서드)
        }

        public bool TakeOwnership(bool forceReload = false)
        {
            lock (_lockObj)
            {
                try
                {
                    LogToFile("========== TakeOwnership (권한 획득) 시작 ==========");

                    // 1. 이미 권한이 있으면 패스
                    if (!IsReadOnly && !forceReload)
                    {
                        LogToFile("[SKIP] 이미 권한 보유 중");
                        return true;
                    }

                    // 2. 다른 사람이 쓰고 있는지 체크 (Lock 파일 확인)
                    if (IsLocked(_cloudDbPath))
                    {
                        LogToFile("[FAIL] 다른 사용자가 이미 점유 중입니다.");
                        return false;
                    }
                    
                    // 3. 최신 데이터 가져오기 (Cloud -> Local)
                    // 중요: 권한을 얻는 시점에 클라우드의 최신 데이터를 로컬로 가져와야 함
                    CloseConnection(); // 연결 끊고
                    SyncFromCloudToLocal(); // 복사
                    
                    // 4. Lock 파일 생성 (내가 점유함)
                    string ticks = DateTime.Now.Ticks.ToString();
                    string content = $"{_currentIdentity}|{ticks}|ACTIVE";
                    
                    SafeWriteFile(_cloudDbPath + ".lock", content);
                    SafeWriteFile(_cloudHistoryDbPath + ".lock", content);
                    LogToFile($"Lock 파일 생성: {content}");
                    
                    // 5. 권한 부여
                    IsReadOnly = false;
                    LogToFile("TakeOwnership 성공! (이제 쓰기 가능)");
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
            // 새 로직: 단순 Lock 파일 체크
             try
            {
                string lockPath = dbFilePath + ".lock";
                if (File.Exists(lockPath))
                {
                    string content = SafeReadFile(lockPath);
                    string[] parts = content.Split('|');
                    if (parts.Length > 0)
                    {
                        return parts[0].Split(':')[0]; // Machine Name
                    }
                }
            }
            catch { }
            return null;
        }

        public void RemoveLock()
        {
            // 프로그램 종료 시 호출됨
            // 변경 사항 저장하고 Lock 해제
             ReleaseOwnership();
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
                // 1. 'CatchCapture' 폴더가 경로에 없으면 먼저 추가
                path = EnsureCatchCaptureSubFolder(path);

                // 2. 최종 경로가 'notedata'인지 확인
                if (Path.GetFileName(path).Equals("notedata", StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }

                // 3. 무조건 'notedata'를 붙여서 관리 (InitializeDatabase에서 생성됨)
                return Path.Combine(path, "notedata");
            }
            catch
            {
                return path;
            }
        }

        public static string EnsureCatchCaptureSubFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;

            try
            {
                // 이미 CatchCapture 폴더이거나 경로에 포함되어 있으면 그대로 반환
                if (path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Any(p => p.Equals("CatchCapture", StringComparison.OrdinalIgnoreCase)))
                {
                    return path;
                }

                // 경로에 없으면 하위에 CatchCapture 폴더 추가
                return Path.Combine(path, "CatchCapture");
            }
            catch { return path; }
        }

        private void InitializeDatabase()
        {
            try
            {
                InitializeDatabaseInternal();
            }
            catch (Exception ex)
            {
                LogToFile($"[CRITICAL] DB 초기화 실패 (손상 의심): {ex.Message}");
                try
                {
                    // 손상된 DB 처리
                    CloseConnection();
                    string corruptPath = DbPath + $".corrupt_{DateTime.Now.Ticks}";
                    if (File.Exists(DbPath))
                    {
                        File.Move(DbPath, corruptPath);
                        LogToFile($"손상된 DB 백업됨: {corruptPath}");
                    }
                    InitializeDatabaseInternal();
                    LogToFile("DB 자동 복구 완료");
                }
                catch (Exception retryEx)
                {
                    LogToFile($"[FATAL] DB 복구 실패: {retryEx.Message}");
                    throw;
                }
            }
        }

        private void InitializeDatabaseInternal()
        {
            string dbDir = Path.GetDirectoryName(DbPath)!;
            string rootDir = Path.GetDirectoryName(dbDir)!; // One level up from notedb

            if (!Directory.Exists(dbDir)) Directory.CreateDirectory(dbDir);

            // Create img directory at root (outside notedb)
            string imgDir = Path.Combine(rootDir, "img");
            // 로컬 구조에서도 동일하게 생성
            if (!Directory.Exists(imgDir)) Directory.CreateDirectory(imgDir);

            string attachDir = Path.Combine(rootDir, "attachments");
            if (!Directory.Exists(attachDir)) Directory.CreateDirectory(attachDir);

            using (var connection = new SqliteConnection($"Data Source={DbPath}"))
            {
                connection.Open();

                // 무결성 검사 (손상 감지용)
                using (var cmd = new SqliteCommand("PRAGMA quick_check;", connection))
                {
                    try { cmd.ExecuteNonQuery(); } catch { throw new Exception("DB Integrity Check Failed"); }
                }

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
            try
            {
                string dbDir = Path.GetDirectoryName(HistoryDbPath)!;
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
                            CreatedAt DATETIME DEFAULT (datetime('now', 'localtime')),
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
                
                // 초기화 성공
                IsHistoryDatabaseReady = true;
                LogToFile("히스토리 데이터베이스 초기화 완료");
            }
            catch (Exception ex)
            {
                IsHistoryDatabaseReady = false;
                LogToFile($"[ERROR] 히스토리 데이터베이스 초기화 실패: {ex.Message}");
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

                using (var connection = new SqliteConnection($"Data Source={DbFilePath}"))
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
                    using (var sourceStream = new FileStream(DbFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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

                using (var connection = new SqliteConnection($"Data Source={HistoryDbFilePath}"))
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
                    using (var sourceStream = new FileStream(HistoryDbFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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
            // 이미지는 클라우드 경로를 사용 (공유 목적)
            string dbDir = Path.GetDirectoryName(CloudDbFilePath)!;
            string rootDir = Path.GetDirectoryName(dbDir)!;
            return Path.Combine(rootDir, "img");
        }

        public string GetAttachmentsFolderPath()
        {
            // 첨부파일도 클라우드 경로 사용
            string dbDir = Path.GetDirectoryName(CloudDbFilePath)!;
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
                using (var connection = new SqliteConnection($"Data Source={DbFilePath}"))
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
                using (var connection = new SqliteConnection($"Data Source={HistoryDbFilePath}"))
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
                using (var connection = new SqliteConnection($"Data Source={DbFilePath}"))
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
                                    
                                    // [Modified] Check if original file name exists
                                    string? originalName = null;
                                    if (imgSource is BitmapImage biOrig && biOrig.UriSource != null && biOrig.UriSource.IsFile)
                                    {
                                        originalName = Path.GetFileNameWithoutExtension(biOrig.UriSource.LocalPath);
                                    }

                                    if (!string.IsNullOrEmpty(originalName))
                                    {
                                        // Use original filename
                                        fName = originalName;
                                    }
                                    else
                                    {
                                        // Use Template
                                        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(fName, @"\$(.*?)\$"))
                                        {
                                            string k = match.Groups[1].Value;
                                            if (k.Equals("App", StringComparison.OrdinalIgnoreCase)) fName = fName.Replace(match.Value, request.SourceApp ?? "CatchC");
                                            else if (k.Equals("Title", StringComparison.OrdinalIgnoreCase))
                                            {
                                                string st = request.Title;
                                                if (st.Length > 20) st = st.Substring(0, 20);
                                                foreach (char c in Path.GetInvalidFileNameChars()) st = st.Replace(c, '_'); // sanitize title
                                                fName = fName.Replace(match.Value, st);
                                            }
                                            else try { fName = fName.Replace(match.Value, now.ToString(k)); } catch { }
                                        }
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
                                    
                                    // [Modified] Use Template for Attachment Filename
                                    string ext = Path.GetExtension(att.DisplayName).ToLower();
                                    string fName = settings.NoteFileNameTemplate ?? "Catch_$yyyy-MM-dd_HH-mm-ss$";
                                    DateTime now = DateTime.Now;
                                    
                                    foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(fName, @"\$(.*?)\$"))
                                    {
                                        string k = match.Groups[1].Value;
                                        if (k.Equals("App", StringComparison.OrdinalIgnoreCase)) fName = fName.Replace(match.Value, request.SourceApp ?? "CatchC");
                                        else if (k.Equals("Title", StringComparison.OrdinalIgnoreCase))
                                        {
                                            string st = request.Title;
                                            if (st.Length > 20) st = st.Substring(0, 20);
                                            // sanitize title
                                            foreach (char c in Path.GetInvalidFileNameChars()) st = st.Replace(c, '_');
                                            fName = fName.Replace(match.Value, st);
                                        }
                                        else try { fName = fName.Replace(match.Value, now.ToString(k)); } catch { }
                                    }
                                    
                                    // Sanitize final filename
                                    foreach (char c in Path.GetInvalidFileNameChars()) fName = fName.Replace(c, '_');
                                    
                                    string fileNameOnly = fName + ext;
                                    string targetDir = Path.Combine(attachDir, yearSub);
                                    string fullPath = Path.Combine(targetDir, fileNameOnly);
                                    
                                    int counter = 1;
                                    while (File.Exists(fullPath))
                                    {
                                        fileNameOnly = $"{fName}_{counter++}{ext}";
                                        fullPath = Path.Combine(targetDir, fileNameOnly);
                                    }

                                    string relPath = Path.Combine(yearSub, fileNameOnly);
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
            // 데이터베이스 초기화 상태 확인
            if (!IsHistoryDatabaseReady)
            {
                throw new InvalidOperationException("히스토리 데이터베이스가 아직 초기화되지 않았습니다. 클라우드 드라이브가 로드될 때까지 잠시 기다려주세요.");
            }
            
            using (var connection = new SqliteConnection($"Data Source={HistoryDbPath}"))
            {
                connection.Open();
                string sql = @"
                    INSERT INTO Captures (FileName, FilePath, SourceApp, SourceTitle, OriginalFilePath, IsFavorite, Status, FileSize, Resolution, CreatedAt)
                    VALUES ($fileName, $filePath, $sourceApp, $sourceTitle, $originalPath, $isFavorite, $status, $fileSize, $resolution, $createdAt);
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
                    command.Parameters.AddWithValue("$createdAt", item.CreatedAt == default ? DateTime.Now : item.CreatedAt);
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
                    else if (filter == "Pinned") wheres.Add("IsPinned = 1");
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
                    else if (filter == "Pinned") wheres.Add("IsPinned = 1");
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

        public Dictionary<string, int> GetHistorySummaryCounts()
        {
            var counts = new Dictionary<string, int>();
            using (var connection = new SqliteConnection($"Data Source={HistoryDbPath}"))
            {
                connection.Open();
                string sql = @"
                    SELECT 
                        COUNT(CASE WHEN Status = 0 THEN 1 END) as AllCount,
                        COUNT(CASE WHEN Status = 0 AND IsPinned = 1 THEN 1 END) as PinnedCount,
                        COUNT(CASE WHEN Status = 0 AND IsFavorite = 1 THEN 1 END) as FavoriteCount,
                        COUNT(CASE WHEN Status = 1 THEN 1 END) as TrashCount,
                        COUNT(CASE WHEN Status = 0 AND CreatedAt >= date('now', 'localtime', '-7 days') THEN 1 END) as Recent7,
                        COUNT(CASE WHEN Status = 0 AND CreatedAt >= date('now', 'localtime', '-30 days') THEN 1 END) as Recent30,
                        COUNT(CASE WHEN Status = 0 AND CreatedAt >= date('now', 'localtime', '-3 months') THEN 1 END) as Recent3Months,
                        COUNT(CASE WHEN Status = 0 AND CreatedAt >= date('now', 'localtime', '-6 months') THEN 1 END) as Recent6Months
                    FROM Captures";

                using (var command = new SqliteCommand(sql, connection))
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        counts["All"] = reader.GetInt32(0);
                        counts["Pinned"] = reader.GetInt32(1);
                        counts["Favorite"] = reader.GetInt32(2);
                        counts["Trash"] = reader.GetInt32(3);
                        counts["Recent7"] = reader.GetInt32(4);
                        counts["Recent30"] = reader.GetInt32(5);
                        counts["Recent3Months"] = reader.GetInt32(6);
                        counts["Recent6Months"] = reader.GetInt32(7);
                    }
                }
            }
            return counts;
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
