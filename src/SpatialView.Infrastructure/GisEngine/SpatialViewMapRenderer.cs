using System.Windows.Controls;
using System.Windows.Input;
using SpatialView.Core.GisEngine;
using SpatialView.Engine.Rendering;

namespace SpatialView.Infrastructure.GisEngine;

/// <summary>
/// SpatialView 엔진의 렌더러를 IMapRenderer로 어댑터
/// </summary>
public class SpatialViewMapRenderer : IMapRenderer, IDisposable
{
    private SpatialViewMapEngine? _mapEngine;
    private WpfMapRenderer? _renderer;
    private Canvas? _canvas;
    private MapTool _activeTool = MapTool.Pan;
    private bool _isEnabled = true;
    private bool _disposed;
    
    public IMapEngine? MapEngine
    {
        get => _mapEngine;
        set
        {
            if (_mapEngine != value)
            {
                // 기존 렌더러 정리
                DisposeRenderer();
                
                _mapEngine = value as SpatialViewMapEngine;
                
                // 새 렌더러 생성
                if (_mapEngine != null && _canvas != null)
                {
                    CreateRenderer();
                }
            }
        }
    }
    
    public MapTool ActiveTool
    {
        get => _activeTool;
        set => _activeTool = value;
    }
    
    public bool IsEnabled
    {
        get => _isEnabled;
        set => _isEnabled = value;
    }
    
    /// <summary>
    /// 렌더링할 Canvas 설정
    /// </summary>
    public void SetCanvas(Canvas canvas)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        
        if (_mapEngine != null)
        {
            CreateRenderer();
        }
        
        // 마우스 이벤트 연결
        _canvas.MouseMove += OnMouseMove;
        _canvas.MouseDown += OnMouseDown;
        _canvas.MouseUp += OnMouseUp;
        _canvas.MouseWheel += OnMouseWheel;
        _canvas.MouseLeftButtonDown += OnMouseClick;
    }
    
    public void Render()
    {
        _renderer?.Render();
        MapRendered?.Invoke(this, EventArgs.Empty);
    }
    
    public void Refresh()
    {
        Render();
        MapRefreshed?.Invoke(this, EventArgs.Empty);
    }
    
    private void CreateRenderer()
    {
        if (_mapEngine != null && _canvas != null)
        {
            _renderer = new WpfMapRenderer(_mapEngine.InternalMap, _canvas);
            _renderer.Render();
        }
    }
    
    private void DisposeRenderer()
    {
        _renderer?.Dispose();
        _renderer = null;
    }
    
    #region Mouse Events
    
    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_canvas == null) return;
        
        var pos = e.GetPosition(_canvas);
        MouseMove?.Invoke(this, e);
    }
    
    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_canvas == null) return;
        
        var pos = e.GetPosition(_canvas);
        MouseDown?.Invoke(this, e);
        
        // 캔버스 포커스
        _canvas.Focus();
    }
    
    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_canvas == null) return;
        
        var pos = e.GetPosition(_canvas);
        MouseUp?.Invoke(this, e);
    }
    
    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_canvas == null || _mapEngine == null) return;
        
        var pos = e.GetPosition(_canvas);
        MouseWheel?.Invoke(this, e);
        
        // 기본 줌 동작
        if (ActiveTool == MapTool.Pan || ActiveTool == MapTool.ZoomIn || ActiveTool == MapTool.ZoomOut)
        {
            var factor = e.Delta > 0 ? 0.8 : 1.25;
            _mapEngine.Zoom *= factor;
        }
    }
    
    private void OnMouseClick(object sender, MouseButtonEventArgs e)
    {
        if (_canvas == null) return;
        
        if (e.ClickCount == 2)
        {
            var pos = e.GetPosition(_canvas);
            MouseDoubleClick?.Invoke(this, e);
        }
    }
    
    private MapMouseButton GetMouseButton(MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            return MapMouseButton.Left;
        if (e.RightButton == MouseButtonState.Pressed)
            return MapMouseButton.Right;
        if (e.MiddleButton == MouseButtonState.Pressed)
            return MapMouseButton.Middle;
        return MapMouseButton.None;
    }
    
    private MapMouseButton GetMouseButton(MouseButtonEventArgs e)
    {
        switch (e.ChangedButton)
        {
            case MouseButton.Left: return MapMouseButton.Left;
            case MouseButton.Right: return MapMouseButton.Right;
            case MouseButton.Middle: return MapMouseButton.Middle;
            default: return MapMouseButton.None;
        }
    }
    
    #endregion
    
    #region Events
    
    public event EventHandler<System.Windows.Input.MouseEventArgs>? MouseMove;
    public event EventHandler<MouseButtonEventArgs>? MouseDown;
    public event EventHandler<MouseButtonEventArgs>? MouseUp;
    public event EventHandler<MouseWheelEventArgs>? MouseWheel;
    public event EventHandler<MouseButtonEventArgs>? MouseDoubleClick;
    public event EventHandler? MapRefreshed;
    public event EventHandler? MapRendered;
    
    #endregion
    
    #region IDisposable
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        
        if (disposing)
        {
            DisposeRenderer();
            
            if (_canvas != null)
            {
                _canvas.MouseMove -= OnMouseMove;
                _canvas.MouseDown -= OnMouseDown;
                _canvas.MouseUp -= OnMouseUp;
                _canvas.MouseWheel -= OnMouseWheel;
                _canvas.MouseLeftButtonDown -= OnMouseClick;
            }
        }
        
        _disposed = true;
    }
    
    #endregion
}