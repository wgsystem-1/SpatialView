using System.Text.Json;
using SpatialView.Engine.Events;
using SpatialView.Engine.Project;
using SpatialView.Engine.Editing;

namespace SpatialView.Engine.Session;

/// <summary>
/// 세션 관리자
/// </summary>
public class SessionManager : IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly ProjectManager _projectManager;
    private readonly string _sessionDirectory;
    private readonly Timer _autoSaveTimer;
    private readonly List<SessionAction> _actionHistory = new();
    private int _currentActionIndex = -1;
    private bool _isRecording = true;
    private bool _disposed;

    /// <summary>
    /// 현재 세션
    /// </summary>
    public WorkSession? CurrentSession { get; private set; }

    /// <summary>
    /// 자동 저장 간격 (분)
    /// </summary>
    public int AutoSaveInterval { get; set; } = 5;

    /// <summary>
    /// 자동 저장 활성화
    /// </summary>
    public bool EnableAutoSave { get; set; } = true;

    /// <summary>
    /// 최대 히스토리 크기
    /// </summary>
    public int MaxHistorySize { get; set; } = 1000;

    /// <summary>
    /// 세션 시작 이벤트
    /// </summary>
    public event EventHandler<SessionEventArgs>? SessionStarted;

    /// <summary>
    /// 세션 종료 이벤트
    /// </summary>
    public event EventHandler<SessionEventArgs>? SessionEnded;

    /// <summary>
    /// 작업 기록 이벤트
    /// </summary>
    public event EventHandler<SessionActionEventArgs>? ActionRecorded;

    public SessionManager(IEventBus eventBus, ProjectManager projectManager, string? sessionDirectory = null)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _projectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));
        
        _sessionDirectory = sessionDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpatialView", "Sessions");
        
        Directory.CreateDirectory(_sessionDirectory);

        // 자동 저장 타이머 설정
        _autoSaveTimer = new Timer(AutoSaveCallback, null, Timeout.Infinite, Timeout.Infinite);

        // 이벤트 구독
        SubscribeToEvents();
    }

    /// <summary>
    /// 새 세션 시작
    /// </summary>
    public WorkSession StartNewSession(string? name = null)
    {
        // 기존 세션 종료
        EndCurrentSession();

        CurrentSession = new WorkSession
        {
            Id = Guid.NewGuid().ToString(),
            Name = name ?? $"세션_{DateTime.Now:yyyyMMdd_HHmmss}",
            StartTime = DateTime.Now,
            ProjectId = _projectManager.CurrentProject?.Id
        };

        // 자동 저장 시작
        if (EnableAutoSave)
        {
            _autoSaveTimer.Change(
                TimeSpan.FromMinutes(AutoSaveInterval),
                TimeSpan.FromMinutes(AutoSaveInterval));
        }

        OnSessionStarted();
        return CurrentSession;
    }

    /// <summary>
    /// 세션 재개
    /// </summary>
    public async Task<WorkSession?> ResumeSessionAsync(string sessionId)
    {
        var sessionPath = GetSessionFilePath(sessionId);
        if (!File.Exists(sessionPath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(sessionPath);
            var session = JsonSerializer.Deserialize<WorkSession>(json);
            
            if (session != null)
            {
                // 기존 세션 종료
                EndCurrentSession();
                
                CurrentSession = session;
                
                // 프로젝트 로드
                if (!string.IsNullOrEmpty(session.LastProjectPath))
                {
                    await _projectManager.OpenProjectAsync(session.LastProjectPath);
                }

                // 작업 히스토리 복원
                _actionHistory.Clear();
                _actionHistory.AddRange(session.Actions);
                _currentActionIndex = _actionHistory.Count - 1;

                // 자동 저장 시작
                if (EnableAutoSave)
                {
                    _autoSaveTimer.Change(
                        TimeSpan.FromMinutes(AutoSaveInterval),
                        TimeSpan.FromMinutes(AutoSaveInterval));
                }

                OnSessionStarted();
            }

            return session;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"세션 재개 실패: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 현재 세션 종료
    /// </summary>
    public void EndCurrentSession()
    {
        if (CurrentSession == null)
            return;

        // 자동 저장 중지
        _autoSaveTimer.Change(Timeout.Infinite, Timeout.Infinite);

        // 세션 저장
        SaveCurrentSession();

        var session = CurrentSession;
        CurrentSession = null;
        
        OnSessionEnded(session);
    }

    /// <summary>
    /// 현재 세션 저장
    /// </summary>
    public void SaveCurrentSession()
    {
        if (CurrentSession == null)
            return;

        try
        {
            // 세션 정보 업데이트
            CurrentSession.LastSaveTime = DateTime.Now;
            CurrentSession.LastProjectPath = _projectManager.CurrentProject?.FilePath;
            CurrentSession.Actions = _actionHistory.Take(_currentActionIndex + 1).ToList();

            // JSON으로 저장
            var sessionPath = GetSessionFilePath(CurrentSession.Id);
            var json = JsonSerializer.Serialize(CurrentSession, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            File.WriteAllText(sessionPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"세션 저장 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 저장된 세션 목록 가져오기
    /// </summary>
    public List<SessionInfo> GetSavedSessions()
    {
        var sessions = new List<SessionInfo>();

        try
        {
            var files = Directory.GetFiles(_sessionDirectory, "*.session");
            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var session = JsonSerializer.Deserialize<WorkSession>(json);
                    if (session != null)
                    {
                        sessions.Add(new SessionInfo
                        {
                            Id = session.Id,
                            Name = session.Name,
                            StartTime = session.StartTime,
                            LastSaveTime = session.LastSaveTime,
                            FilePath = file,
                            ActionCount = session.Actions.Count
                        });
                    }
                }
                catch
                {
                    // 개별 세션 로드 실패 무시
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"세션 목록 로드 실패: {ex.Message}");
        }

        return sessions.OrderByDescending(s => s.LastSaveTime).ToList();
    }

    /// <summary>
    /// 세션 삭제
    /// </summary>
    public bool DeleteSession(string sessionId)
    {
        try
        {
            var sessionPath = GetSessionFilePath(sessionId);
            if (File.Exists(sessionPath))
            {
                File.Delete(sessionPath);
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"세션 삭제 실패: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// 작업 기록
    /// </summary>
    public void RecordAction(SessionAction action)
    {
        if (!_isRecording || CurrentSession == null)
            return;

        // 현재 위치 이후의 작업들 제거 (새로운 분기 생성)
        if (_currentActionIndex < _actionHistory.Count - 1)
        {
            _actionHistory.RemoveRange(_currentActionIndex + 1, _actionHistory.Count - _currentActionIndex - 1);
        }

        // 새 작업 추가
        _actionHistory.Add(action);
        _currentActionIndex++;

        // 히스토리 크기 제한
        if (_actionHistory.Count > MaxHistorySize)
        {
            var removeCount = _actionHistory.Count - MaxHistorySize;
            _actionHistory.RemoveRange(0, removeCount);
            _currentActionIndex -= removeCount;
        }

        OnActionRecorded(action);
    }

    /// <summary>
    /// 작업 히스토리 탐색
    /// </summary>
    public SessionAction? NavigateToAction(int index)
    {
        if (index < 0 || index >= _actionHistory.Count)
            return null;

        _currentActionIndex = index;
        return _actionHistory[index];
    }

    /// <summary>
    /// 이전 작업으로
    /// </summary>
    public SessionAction? NavigatePrevious()
    {
        if (_currentActionIndex > 0)
        {
            _currentActionIndex--;
            return _actionHistory[_currentActionIndex];
        }
        return null;
    }

    /// <summary>
    /// 다음 작업으로
    /// </summary>
    public SessionAction? NavigateNext()
    {
        if (_currentActionIndex < _actionHistory.Count - 1)
        {
            _currentActionIndex++;
            return _actionHistory[_currentActionIndex];
        }
        return null;
    }

    /// <summary>
    /// 작업 히스토리 가져오기
    /// </summary>
    public IReadOnlyList<SessionAction> GetActionHistory()
    {
        return _actionHistory.AsReadOnly();
    }

    /// <summary>
    /// 기록 일시 중지/재개
    /// </summary>
    public void SetRecording(bool enable)
    {
        _isRecording = enable;
    }

    #region Private Methods

    private void SubscribeToEvents()
    {
        // 프로젝트 이벤트
        _projectManager.ProjectOpened += OnProjectOpened;
        _projectManager.ProjectClosed += OnProjectClosed;
        _projectManager.ProjectSaved += OnProjectSaved;

        // 편집 이벤트
        _eventBus.Subscribe<AfterEditEvent>(OnAfterEdit);

        // 뷰 이벤트
        _eventBus.Subscribe<ViewChangedEvent>(OnViewChanged);

        // 레이어 이벤트
        _eventBus.Subscribe<LayerAddedEvent>(OnLayerAdded);
        _eventBus.Subscribe<LayerRemovedEvent>(OnLayerRemoved);

        // 피처 이벤트
        _eventBus.Subscribe<FeatureSelectedEvent>(OnFeatureSelected);
    }

    private void OnProjectOpened(object? sender, ProjectEventArgs e)
    {
        RecordAction(new SessionAction
        {
            Type = ActionType.ProjectOpened,
            Description = $"프로젝트 열기: {e.Project.Name}",
            Data = new Dictionary<string, object>
            {
                ["ProjectId"] = e.Project.Id,
                ["ProjectName"] = e.Project.Name,
                ["ProjectPath"] = e.Project.FilePath ?? ""
            }
        });
    }

    private void OnProjectClosed(object? sender, ProjectEventArgs e)
    {
        RecordAction(new SessionAction
        {
            Type = ActionType.ProjectClosed,
            Description = $"프로젝트 닫기: {e.Project.Name}",
            Data = new Dictionary<string, object>
            {
                ["ProjectId"] = e.Project.Id,
                ["ProjectName"] = e.Project.Name
            }
        });
    }

    private void OnProjectSaved(object? sender, ProjectEventArgs e)
    {
        RecordAction(new SessionAction
        {
            Type = ActionType.ProjectSaved,
            Description = $"프로젝트 저장: {e.Project.Name}",
            Data = new Dictionary<string, object>
            {
                ["ProjectId"] = e.Project.Id,
                ["ProjectPath"] = e.Project.FilePath ?? ""
            }
        });
    }

    private void OnAfterEdit(AfterEditEvent e)
    {
        if (!e.Success)
            return;

        RecordAction(new SessionAction
        {
            Type = ActionType.Edit,
            Description = $"{e.Operation} - {e.Layer.Name}",
            Data = new Dictionary<string, object>
            {
                ["Operation"] = e.Operation.ToString(),
                ["LayerId"] = e.Layer.Id,
                ["LayerName"] = e.Layer.Name,
                ["FeatureId"] = e.Feature.Id
            }
        });
    }

    private void OnViewChanged(ViewChangedEvent e)
    {
        RecordAction(new SessionAction
        {
            Type = ActionType.ViewChanged,
            Description = "뷰 변경",
            Data = new Dictionary<string, object>
            {
                ["OldExtent"] = $"{e.OldExtent.MinX},{e.OldExtent.MinY},{e.OldExtent.MaxX},{e.OldExtent.MaxY}",
                ["NewExtent"] = $"{e.NewExtent.MinX},{e.NewExtent.MinY},{e.NewExtent.MaxX},{e.NewExtent.MaxY}",
                ["OldZoom"] = e.OldZoom,
                ["NewZoom"] = e.NewZoom
            }
        });
    }

    private void OnLayerAdded(LayerAddedEvent e)
    {
        RecordAction(new SessionAction
        {
            Type = ActionType.LayerAdded,
            Description = $"레이어 추가: {e.Layer.Name}",
            Data = new Dictionary<string, object>
            {
                ["LayerId"] = e.Layer.Id,
                ["LayerName"] = e.Layer.Name,
                ["Index"] = e.Index
            }
        });
    }

    private void OnLayerRemoved(LayerRemovedEvent e)
    {
        RecordAction(new SessionAction
        {
            Type = ActionType.LayerRemoved,
            Description = $"레이어 제거: {e.Layer.Name}",
            Data = new Dictionary<string, object>
            {
                ["LayerId"] = e.Layer.Id,
                ["LayerName"] = e.Layer.Name
            }
        });
    }

    private void OnFeatureSelected(FeatureSelectedEvent e)
    {
        var description = e.Feature != null ? 
            $"피처 선택: {e.Feature.Id}" : 
            "선택 해제";

        RecordAction(new SessionAction
        {
            Type = ActionType.FeatureSelected,
            Description = description,
            Data = new Dictionary<string, object>
            {
                ["LayerId"] = e.Layer?.Id ?? "",
                ["LayerName"] = e.Layer?.Name ?? "",
                ["FeatureId"] = e.Feature?.Id ?? "",
                ["IsMultiSelect"] = e.IsMultiSelect
            }
        });
    }

    private void AutoSaveCallback(object? state)
    {
        if (CurrentSession != null)
        {
            SaveCurrentSession();
        }
    }

    private string GetSessionFilePath(string sessionId)
    {
        return Path.Combine(_sessionDirectory, $"{sessionId}.session");
    }

    private void OnSessionStarted()
    {
        SessionStarted?.Invoke(this, new SessionEventArgs(CurrentSession!));
    }

    private void OnSessionEnded(WorkSession session)
    {
        SessionEnded?.Invoke(this, new SessionEventArgs(session));
    }

    private void OnActionRecorded(SessionAction action)
    {
        ActionRecorded?.Invoke(this, new SessionActionEventArgs(action));
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;

        EndCurrentSession();
        
        _autoSaveTimer?.Dispose();
        _actionHistory.Clear();
        
        _disposed = true;
    }
}

/// <summary>
/// 작업 세션
/// </summary>
public class WorkSession
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public DateTime LastSaveTime { get; set; }
    public string? ProjectId { get; set; }
    public string? LastProjectPath { get; set; }
    public List<SessionAction> Actions { get; set; } = new();
    public Dictionary<string, object> CustomData { get; set; } = new();
}

/// <summary>
/// 세션 작업
/// </summary>
public class SessionAction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public ActionType Type { get; set; }
    public string Description { get; set; } = "";
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// 작업 타입
/// </summary>
public enum ActionType
{
    Unknown,
    ProjectOpened,
    ProjectClosed,
    ProjectSaved,
    ViewChanged,
    LayerAdded,
    LayerRemoved,
    LayerModified,
    FeatureSelected,
    Edit,
    ToolActivated,
    PluginAction,
    Custom
}

/// <summary>
/// 세션 정보
/// </summary>
public class SessionInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime LastSaveTime { get; set; }
    public string FilePath { get; set; } = "";
    public int ActionCount { get; set; }
}

/// <summary>
/// 세션 이벤트 인수
/// </summary>
public class SessionEventArgs : EventArgs
{
    public WorkSession Session { get; }

    public SessionEventArgs(WorkSession session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
    }
}

/// <summary>
/// 세션 작업 이벤트 인수
/// </summary>
public class SessionActionEventArgs : EventArgs
{
    public SessionAction Action { get; }

    public SessionActionEventArgs(SessionAction action)
    {
        Action = action ?? throw new ArgumentNullException(nameof(action));
    }
}