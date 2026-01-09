using System.Windows;
using System.Windows.Input;

namespace SpatialView.Core.GisEngine;

/// <summary>
/// 지도 렌더러 추상 인터페이스
/// UI 렌더링과 사용자 상호작용을 담당
/// </summary>
public interface IMapRenderer
{
    /// <summary>
    /// 연결된 지도 엔진
    /// </summary>
    IMapEngine? MapEngine { get; set; }
    
    /// <summary>
    /// 렌더러 활성화 여부
    /// </summary>
    bool IsEnabled { get; set; }
    
    /// <summary>
    /// 현재 지도 도구
    /// </summary>
    MapTool ActiveTool { get; set; }
    
    /// <summary>
    /// 지도 렌더링
    /// </summary>
    void Render();
    
    /// <summary>
    /// 렌더링 새로고침
    /// </summary>
    void Refresh();
    
    /// <summary>
    /// 마우스 이동 이벤트
    /// </summary>
    event EventHandler<System.Windows.Input.MouseEventArgs>? MouseMove;
    
    /// <summary>
    /// 마우스 다운 이벤트
    /// </summary>
    event EventHandler<MouseButtonEventArgs>? MouseDown;
    
    /// <summary>
    /// 마우스 업 이벤트
    /// </summary>
    event EventHandler<MouseButtonEventArgs>? MouseUp;
    
    /// <summary>
    /// 마우스 휠 이벤트
    /// </summary>
    event EventHandler<MouseWheelEventArgs>? MouseWheel;
    
    /// <summary>
    /// 마우스 더블클릭 이벤트
    /// </summary>
    event EventHandler<MouseButtonEventArgs>? MouseDoubleClick;
    
    /// <summary>
    /// 지도 새로고침 완료 이벤트
    /// </summary>
    event EventHandler? MapRefreshed;
    
    /// <summary>
    /// 지도 렌더링 완료 이벤트
    /// </summary>
    event EventHandler? MapRendered;
}

