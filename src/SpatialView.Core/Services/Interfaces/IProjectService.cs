using SpatialView.Core.Models;

namespace SpatialView.Core.Services.Interfaces;

/// <summary>
/// 프로젝트 파일 관리 서비스 인터페이스
/// </summary>
public interface IProjectService
{
    /// <summary>
    /// 현재 프로젝트 파일 경로
    /// </summary>
    string? CurrentProjectPath { get; }
    
    /// <summary>
    /// 프로젝트가 수정되었는지 여부
    /// </summary>
    bool IsModified { get; set; }
    
    /// <summary>
    /// 프로젝트 저장
    /// </summary>
    /// <param name="filePath">저장할 파일 경로</param>
    /// <param name="project">프로젝트 데이터</param>
    Task SaveProjectAsync(string filePath, ProjectFile project);
    
    /// <summary>
    /// 프로젝트 불러오기
    /// </summary>
    /// <param name="filePath">불러올 파일 경로</param>
    /// <returns>프로젝트 데이터</returns>
    Task<ProjectFile> LoadProjectAsync(string filePath);
    
    /// <summary>
    /// 새 프로젝트 생성
    /// </summary>
    /// <returns>빈 프로젝트</returns>
    ProjectFile CreateNewProject();
    
    /// <summary>
    /// 절대 경로를 프로젝트 기준 상대 경로로 변환
    /// </summary>
    string ToRelativePath(string absolutePath, string projectFilePath);
    
    /// <summary>
    /// 프로젝트 기준 상대 경로를 절대 경로로 변환
    /// </summary>
    string ToAbsolutePath(string relativePath, string projectFilePath);
}

