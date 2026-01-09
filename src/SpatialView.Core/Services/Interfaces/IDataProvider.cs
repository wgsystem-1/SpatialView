using SpatialView.Core.Models;

namespace SpatialView.Core.Services.Interfaces;

/// <summary>
/// GIS 데이터 파일 로드를 위한 Provider 인터페이스
/// 각 파일 포맷별로 구현됩니다.
/// </summary>
public interface IDataProvider
{
    /// <summary>
    /// 지원하는 파일 확장자 목록
    /// </summary>
    string[] SupportedExtensions { get; }
    
    /// <summary>
    /// Provider 이름
    /// </summary>
    string ProviderName { get; }
    
    /// <summary>
    /// 폴더 기반 데이터소스 여부
    /// FileGDB 등 폴더를 선택해야 하는 경우 true
    /// </summary>
    bool IsFolderBased { get; }
    
    /// <summary>
    /// 해당 파일을 로드할 수 있는지 확인
    /// </summary>
    /// <param name="filePath">파일 경로</param>
    /// <returns>로드 가능 여부</returns>
    bool CanLoad(string filePath);
    
    /// <summary>
    /// 파일을 비동기로 로드
    /// </summary>
    /// <param name="filePath">파일 경로</param>
    /// <returns>로드된 레이어 정보</returns>
    Task<LayerInfo> LoadAsync(string filePath);
}

