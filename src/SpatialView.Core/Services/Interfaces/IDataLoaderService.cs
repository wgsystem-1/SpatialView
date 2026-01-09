using SpatialView.Core.Models;

namespace SpatialView.Core.Services.Interfaces;

/// <summary>
/// 로딩 진행 상황 정보
/// </summary>
public class LoadingProgress
{
    public int Current { get; set; }
    public int Total { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
    public double Percentage => Total > 0 ? (double)Current / Total * 100 : 0;
}

/// <summary>
/// 데이터 로딩 서비스 인터페이스
/// 파일 확장자에 따라 적절한 DataProvider를 선택하여 로드합니다.
/// </summary>
public interface IDataLoaderService
{
    /// <summary>
    /// 지원되는 모든 파일 확장자 목록
    /// </summary>
    string[] SupportedExtensions { get; }
    
    /// <summary>
    /// 파일 열기 다이얼로그 필터 문자열
    /// </summary>
    string FileDialogFilter { get; }
    
    /// <summary>
    /// 해당 파일을 로드할 수 있는지 확인
    /// </summary>
    bool CanLoad(string filePath);
    
    /// <summary>
    /// 파일을 비동기로 로드
    /// </summary>
    Task<LayerInfo> LoadFileAsync(string filePath);
    
    /// <summary>
    /// 파일을 비동기로 로드 (취소 지원)
    /// </summary>
    Task<LayerInfo> LoadFileAsync(string filePath, CancellationToken cancellationToken);
    
    /// <summary>
    /// 여러 파일을 비동기로 로드
    /// </summary>
    Task<IEnumerable<LayerInfo>> LoadFilesAsync(IEnumerable<string> filePaths);
    
    /// <summary>
    /// 여러 파일을 비동기로 로드 (취소 및 진행 상황 지원)
    /// </summary>
    Task<IEnumerable<LayerInfo>> LoadFilesAsync(
        IEnumerable<string> filePaths, 
        CancellationToken cancellationToken,
        IProgress<LoadingProgress>? progress = null);
}

