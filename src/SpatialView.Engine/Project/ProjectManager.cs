using SpatialView.Engine.Data.Layers;
using SpatialView.Engine.Data.Sources;
using SpatialView.Engine.Events;
using SpatialView.Engine.Map;
using SpatialView.Engine.Plugins;
using SpatialView.Engine.Styling;

namespace SpatialView.Engine.Project;

/// <summary>
/// 프로젝트 관리자
/// </summary>
public class ProjectManager : IDisposable
{
    private readonly IEventBus _eventBus;
    private readonly IPluginManager? _pluginManager;
    private GisProject? _currentProject;
    private bool _disposed;

    /// <summary>
    /// 현재 프로젝트
    /// </summary>
    public GisProject? CurrentProject => _currentProject;

    /// <summary>
    /// 프로젝트 열림 이벤트
    /// </summary>
    public event EventHandler<ProjectEventArgs>? ProjectOpened;

    /// <summary>
    /// 프로젝트 닫힘 이벤트
    /// </summary>
    public event EventHandler<ProjectEventArgs>? ProjectClosed;

    /// <summary>
    /// 프로젝트 저장 이벤트
    /// </summary>
    public event EventHandler<ProjectEventArgs>? ProjectSaved;

    /// <summary>
    /// 프로젝트 변경 이벤트
    /// </summary>
    public event EventHandler<ProjectEventArgs>? ProjectChanged;

    public ProjectManager(IEventBus eventBus, IPluginManager? pluginManager = null)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _pluginManager = pluginManager;
    }

    /// <summary>
    /// 새 프로젝트 생성
    /// </summary>
    public GisProject CreateNewProject(string name)
    {
        // 기존 프로젝트 닫기
        CloseProject();

        _currentProject = new GisProject
        {
            Name = name,
            Author = Environment.UserName,
            CreatedDate = DateTime.Now,
            ModifiedDate = DateTime.Now
        };

        OnProjectOpened();
        return _currentProject;
    }

    /// <summary>
    /// 프로젝트 열기
    /// </summary>
    public async Task<GisProject?> OpenProjectAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        // 기존 프로젝트 닫기
        CloseProject();

        try
        {
            // 확장자에 따라 로드
            var extension = Path.GetExtension(filePath).ToLower();
            _currentProject = extension switch
            {
                ".json" => GisProject.LoadFromJson(filePath),
                ".xml" => GisProject.LoadFromXml(filePath),
                _ => null
            };

            if (_currentProject != null)
            {
                _currentProject.FilePath = filePath;
                await LoadProjectResourcesAsync();
                OnProjectOpened();
            }

            return _currentProject;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"프로젝트 열기 실패: {ex.Message}");
            _currentProject = null;
            return null;
        }
    }

    /// <summary>
    /// 프로젝트 저장
    /// </summary>
    public void SaveProject()
    {
        if (_currentProject?.FilePath == null)
            return;

        SaveProjectAs(_currentProject.FilePath);
    }

    /// <summary>
    /// 프로젝트 다른 이름으로 저장
    /// </summary>
    public void SaveProjectAs(string filePath)
    {
        if (_currentProject == null)
            return;

        try
        {
            var extension = Path.GetExtension(filePath).ToLower();
            switch (extension)
            {
                case ".json":
                    _currentProject.SaveAsJson(filePath);
                    break;
                case ".xml":
                    _currentProject.SaveAsXml(filePath);
                    break;
                default:
                    throw new NotSupportedException($"지원하지 않는 파일 형식: {extension}");
            }

            OnProjectSaved();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"프로젝트 저장 실패: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 프로젝트 닫기
    /// </summary>
    public void CloseProject()
    {
        if (_currentProject == null)
            return;

        // 변경 사항이 있으면 저장 확인
        if (_currentProject.IsDirty)
        {
            // 실제 구현에서는 사용자에게 저장 여부 확인
        }

        var project = _currentProject;
        _currentProject = null;

        OnProjectClosed(project);
    }

    /// <summary>
    /// 맵 캔버스에 프로젝트 적용
    /// </summary>
    public async Task ApplyToMapCanvasAsync(IMapCanvas mapCanvas)
    {
        if (_currentProject == null || mapCanvas == null)
            return;

        // 맵 설정 적용
        ApplyMapSettings(mapCanvas);

        // 레이어 로드
        await LoadLayersAsync(mapCanvas);

        // 초기 뷰 설정
        if (_currentProject.InitialExtent != null)
        {
            // TODO: IMapCanvas 인터페이스가 정의되면 수정 필요
            // mapCanvas.ZoomToExtent(_currentProject.InitialExtent);
        }

        // 플러그인 로드
        await LoadPluginsAsync();
    }

    /// <summary>
    /// 현재 맵 상태를 프로젝트에 저장
    /// </summary>
    public void SaveMapStateToProject(IMapCanvas mapCanvas)
    {
        if (_currentProject == null || mapCanvas == null)
            return;

        // 현재 뷰 범위 저장
        var viewExtent = mapCanvas.ViewExtent;
        _currentProject.InitialExtent = new Geometry.Envelope(
            viewExtent.MinX, viewExtent.MinY, viewExtent.MaxX, viewExtent.MaxY);
        _currentProject.InitialZoom = mapCanvas.ZoomLevel;

        // 레이어 구성 저장
        SaveLayerConfiguration(mapCanvas.Layers);

        _currentProject.IsDirty = true;
        OnProjectChanged();
    }

    /// <summary>
    /// 레이어 구성 추가
    /// </summary>
    public void AddLayerConfiguration(LayerConfiguration layerConfig)
    {
        if (_currentProject == null)
            return;

        _currentProject.Layers.Add(layerConfig);
        _currentProject.IsDirty = true;
        OnProjectChanged();
    }

    /// <summary>
    /// 레이어 구성 제거
    /// </summary>
    public void RemoveLayerConfiguration(string layerId)
    {
        if (_currentProject == null)
            return;

        _currentProject.Layers.RemoveAll(l => l.Id == layerId);
        _currentProject.IsDirty = true;
        OnProjectChanged();
    }

    #region Private Methods

    private async Task LoadProjectResourcesAsync()
    {
        if (_currentProject == null)
            return;

        // 상대 경로를 절대 경로로 변환
        if (!string.IsNullOrEmpty(_currentProject.FilePath))
        {
            var projectDir = Path.GetDirectoryName(_currentProject.FilePath);
            if (projectDir != null)
            {
                foreach (var layer in _currentProject.Layers)
                {
                    if (!Path.IsPathRooted(layer.DataSource))
                    {
                        layer.DataSource = Path.GetFullPath(Path.Combine(projectDir, layer.DataSource));
                    }
                }
            }
        }
    }

    private void ApplyMapSettings(IMapCanvas mapCanvas)
    {
        if (_currentProject?.MapSettings == null)
            return;

        // 실제 구현에서는 맵 캔버스에 설정 적용
        // 예: 배경색, 선택 색상, 안티앨리어싱 등
    }

    private async Task LoadLayersAsync(IMapCanvas mapCanvas)
    {
        if (_currentProject == null)
            return;

        mapCanvas.Layers.Clear();

        foreach (var layerConfig in _currentProject.Layers)
        {
            try
            {
                var layer = await CreateLayerFromConfigAsync(layerConfig);
                if (layer != null)
                {
                    mapCanvas.Layers.Add(layer);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"레이어 로드 실패 ({layerConfig.Name}): {ex.Message}");
            }
        }
    }

    private async Task<ILayer?> CreateLayerFromConfigAsync(LayerConfiguration config)
    {
        IDataSource? dataSource = null;

        // 데이터 소스 생성
        switch (config.DataSourceType)
        {
            case DataSourceType.Shapefile:
                dataSource = new ShapefileDataSource(config.DataSource);
                break;
            
            case DataSourceType.GeoJSON:
                dataSource = new MemoryDataSource(config.Name + "_GeoJson");
                break;
            
            case DataSourceType.GeoPackage:
                dataSource = new GeoPackageDataSource(config.DataSource, config.Name);
                break;
            
            case DataSourceType.PostGIS:
                // 연결 문자열과 테이블 이름 분리
                var parts = config.DataSource.Split(';');
                if (parts.Length >= 2)
                {
                    // parts[0]: connectionString, parts[1]: tableName
                    dataSource = new PostGisDataSource(parts[0], parts[1]);
                }
                else
                {
                    // 기본 테이블명 사용
                    dataSource = new PostGisDataSource(config.DataSource, config.Name);
                }
                break;
            
            case DataSourceType.Memory:
                dataSource = new MemoryDataSource(config.Name);
                break;
        }

        if (dataSource == null)
            return null;

        // 레이어 생성
        ILayer? layer = config.Type switch
        {
            LayerType.Vector => new VectorLayer()
            {
                Name = config.Name,
                DataSource = dataSource,
                Visible = config.IsVisible,
                Selectable = config.IsSelectable,
                Editable = config.IsEditable,
                MinimumZoom = config.MinScale,
                MaximumZoom = config.MaxScale
            },
            LayerType.Raster => new RasterLayer(config.Name, config.DataSource)
            {
                Visible = config.IsVisible,
                MinScale = config.MinScale,
                MaxScale = config.MaxScale,
                Opacity = config.Opacity
            },
            _ => null
        };

        if (layer == null)
            return null;

        // 스타일 적용
        if (config.Style != null && layer is VectorLayer vectorLayer)
        {
            ApplyStyleToLayer(vectorLayer, config.Style);
        }

        // 기본 레이어 인터페이스로 반환
        return layer;
    }

    private void ApplyStyleToLayer(VectorLayer layer, StyleConfiguration styleConfig)
    {
        // TODO: SimpleVectorStyle, SolidBrush, Pen 클래스가 구현되면 활성화
        /*
        var style = new SimpleVectorStyle
        {
            Fill = styleConfig.FillColor != null ? new SolidBrush { Color = styleConfig.FillColor } : null,
            Outline = styleConfig.StrokeColor != null ? new Pen { Color = styleConfig.StrokeColor, Width = (float)styleConfig.StrokeWidth } : null
        };

        layer.Style = style;
        */
    }

    private async Task LoadPluginsAsync()
    {
        if (_currentProject == null || _pluginManager == null)
            return;

        foreach (var pluginConfig in _currentProject.Plugins.Where(p => p.IsEnabled))
        {
            try
            {
                var plugin = _pluginManager.GetPlugin(pluginConfig.Id);
                if (plugin == null)
                {
                    // 플러그인이 로드되지 않았다면 로드 시도
                    continue;
                }

                // 플러그인 설정 적용
                if (pluginConfig.Settings != null && plugin.GetSettings() != null)
                {
                    var settings = plugin.GetSettings()!;
                    var json = System.Text.Json.JsonSerializer.Serialize(pluginConfig.Settings);
                    settings.FromJson(json);
                    plugin.ApplySettings(settings);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"플러그인 로드 실패 ({pluginConfig.Name}): {ex.Message}");
            }
        }
    }

    private void SaveLayerConfiguration(ILayerCollection layers)
    {
        if (_currentProject == null)
            return;

        _currentProject.Layers.Clear();

        foreach (var layer in layers)
        {
            if (layer is ILayer engineLayer)
            {
                var config = CreateLayerConfiguration(engineLayer);
                if (config != null)
                {
                    _currentProject.Layers.Add(config);
                }
            }
        }
    }

    private LayerConfiguration? CreateLayerConfiguration(ILayer layer)
    {
        var config = new LayerConfiguration
        {
            Id = layer.Id,
            Name = layer.Name,
            IsVisible = layer.Visible,
            IsSelectable = layer.Selectable,
            IsEditable = layer.Editable,
            MinScale = layer.MinScale,
            MaxScale = layer.MaxScale
        };

        // 데이터 소스 정보
        if (layer.DataSource != null)
        {
            config.DataSource = GetDataSourceString(layer.DataSource);
            config.DataSourceType = DetectDataSourceType(layer.DataSource);
        }

        // 레이어 타입
        config.Type = layer switch
        {
            VectorLayer => LayerType.Vector,
            RasterLayer => LayerType.Raster,
            _ => LayerType.Vector
        };

        // 스타일 정보
        if (layer is VectorLayer vectorLayer && vectorLayer.Style != null)
        {
            config.Style = ExtractStyleConfiguration(vectorLayer.Style);
        }

        return config;
    }

    private string GetDataSourceString(IDataSource dataSource)
    {
        // 실제 구현에서는 각 데이터 소스 타입별로 연결 문자열 생성
        return dataSource.Name;
    }

    private DataSourceType DetectDataSourceType(IDataSource dataSource)
    {
        return dataSource switch
        {
            ShapefileDataSource => DataSourceType.Shapefile,
            // GeoJsonDataSource => DataSourceType.GeoJSON, // TODO: GeoJsonDataSource 클래스 구현 필요
            GeoPackageDataSource => DataSourceType.GeoPackage,
            PostGisDataSource => DataSourceType.PostGIS,
            MemoryDataSource => DataSourceType.Memory,
            _ => DataSourceType.Memory
        };
    }

    private StyleConfiguration ExtractStyleConfiguration(IStyle style)
    {
        var config = new StyleConfiguration();
        
        // TODO: SimpleVectorStyle 클래스가 구현되면 활성화
        /*
        if (style is SimpleVectorStyle simpleStyle)
        {
            config.Type = StyleType.Simple;
            config.FillColor = simpleStyle.Fill?.Color;
            config.StrokeColor = simpleStyle.Outline?.Color;
            config.StrokeWidth = simpleStyle.Outline?.Width ?? 1.0;
        }
        */

        return config;
    }

    #endregion

    #region Event Handlers

    private void OnProjectOpened()
    {
        ProjectOpened?.Invoke(this, new ProjectEventArgs(_currentProject!));
        _eventBus.Publish(new ProjectOpenedEvent(this, _currentProject!));
    }

    private void OnProjectClosed(GisProject project)
    {
        ProjectClosed?.Invoke(this, new ProjectEventArgs(project));
        _eventBus.Publish(new ProjectClosedEvent(this, project));
    }

    private void OnProjectSaved()
    {
        ProjectSaved?.Invoke(this, new ProjectEventArgs(_currentProject!));
        _eventBus.Publish(new ProjectSavedEvent(this, _currentProject!));
    }

    private void OnProjectChanged()
    {
        ProjectChanged?.Invoke(this, new ProjectEventArgs(_currentProject!));
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;

        CloseProject();
        _disposed = true;
    }
}

/// <summary>
/// 프로젝트 이벤트 인수
/// </summary>
public class ProjectEventArgs : EventArgs
{
    public GisProject Project { get; }

    public ProjectEventArgs(GisProject project)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
    }
}

/// <summary>
/// 프로젝트 열림 이벤트
/// </summary>
public class ProjectOpenedEvent : EventBase
{
    public GisProject Project { get; }

    public ProjectOpenedEvent(object source, GisProject project) : base(source)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
    }
}

/// <summary>
/// 프로젝트 닫힘 이벤트
/// </summary>
public class ProjectClosedEvent : EventBase
{
    public GisProject Project { get; }

    public ProjectClosedEvent(object source, GisProject project) : base(source)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
    }
}

/// <summary>
/// 프로젝트 저장 이벤트
/// </summary>
public class ProjectSavedEvent : EventBase
{
    public GisProject Project { get; }

    public ProjectSavedEvent(object source, GisProject project) : base(source)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
    }
}