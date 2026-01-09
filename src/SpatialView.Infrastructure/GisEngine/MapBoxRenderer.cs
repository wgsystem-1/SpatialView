using System.Windows.Input;
using SpatialView.Core.GisEngine;

namespace SpatialView.Infrastructure.GisEngine;

/// <summary>
/// 맵 렌더러 임시 구현 (SharpMap.Forms.MapBox 없이)
/// 실제 UI 컨트롤은 Phase 4에서 구현 예정
/// </summary>
public class MapBoxRenderer : IMapRenderer
{
    private IMapEngine? _mapEngine;
    private bool _isEnabled = true;
    private MapTool _activeTool = MapTool.None;
    
    public MapBoxRenderer()
    {
    }
    
    public IMapEngine? MapEngine
    {
        get => _mapEngine;
        set => _mapEngine = value;
    }
    
    public bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }
    
    public MapTool ActiveTool
    {
        get => _activeTool;
        set => _activeTool = value;
    }
    
    public void Render()
    {
        // 실제 렌더링은 Phase 4에서 구현
        MapRendered?.Invoke(this, EventArgs.Empty);
    }
    
    public void Refresh()
    {
        // 실제 리프레시는 Phase 4에서 구현
        MapRefreshed?.Invoke(this, EventArgs.Empty);
    }
    
// Events (미사용 경고 억제)
#pragma warning disable CS0067
public event EventHandler<MouseEventArgs>? MouseMove;
public event EventHandler<MouseButtonEventArgs>? MouseDown;
public event EventHandler<MouseButtonEventArgs>? MouseUp;
public event EventHandler<MouseWheelEventArgs>? MouseWheel;
public event EventHandler<MouseButtonEventArgs>? MouseDoubleClick;
public event EventHandler? MapRefreshed;
public event EventHandler? MapRendered;
#pragma warning restore CS0067
}