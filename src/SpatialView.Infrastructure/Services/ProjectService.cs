using System.Text.Json;
using SpatialView.Core.Models;
using SpatialView.Core.Services.Interfaces;

namespace SpatialView.Infrastructure.Services;

/// <summary>
/// 프로젝트 파일 관리 서비스 구현
/// </summary>
public class ProjectService : IProjectService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    /// <summary>
    /// 현재 프로젝트 파일 경로
    /// </summary>
    public string? CurrentProjectPath { get; private set; }
    
    /// <summary>
    /// 프로젝트 수정 여부
    /// </summary>
    public bool IsModified { get; set; }
    
    /// <summary>
    /// 프로젝트 저장
    /// </summary>
    public async Task SaveProjectAsync(string filePath, ProjectFile project)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentException("파일 경로가 비어있습니다.", nameof(filePath));
        
        // 수정 시간 업데이트
        project.Modified = DateTime.Now;
        
        // 디렉토리 생성
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        // JSON 직렬화 및 저장
        var json = JsonSerializer.Serialize(project, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
        
        CurrentProjectPath = filePath;
        IsModified = false;
        
        System.Diagnostics.Debug.WriteLine($"프로젝트 저장 완료: {filePath}");
    }
    
    /// <summary>
    /// 프로젝트 불러오기
    /// </summary>
    public async Task<ProjectFile> LoadProjectAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentException("파일 경로가 비어있습니다.", nameof(filePath));
        
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"프로젝트 파일을 찾을 수 없습니다: {filePath}");
        
        var json = await File.ReadAllTextAsync(filePath);
        var project = JsonSerializer.Deserialize<ProjectFile>(json, JsonOptions);
        
        if (project == null)
            throw new InvalidDataException("프로젝트 파일을 파싱할 수 없습니다.");
        
        // 버전 호환성 체크
        if (!IsVersionCompatible(project.Version))
        {
            throw new NotSupportedException($"지원하지 않는 프로젝트 버전입니다: {project.Version}");
        }
        
        CurrentProjectPath = filePath;
        IsModified = false;
        
        System.Diagnostics.Debug.WriteLine($"프로젝트 로드 완료: {filePath}");
        
        return project;
    }
    
    /// <summary>
    /// 새 프로젝트 생성
    /// </summary>
    public ProjectFile CreateNewProject()
    {
        CurrentProjectPath = null;
        IsModified = false;
        
        return new ProjectFile
        {
            Name = "새 프로젝트",
            Created = DateTime.Now,
            Modified = DateTime.Now,
            MapSettings = new MapSettings
            {
                CenterX = 127.0,
                CenterY = 37.5,
                Zoom = 1000000,
                CRS = "EPSG:4326",
                BaseMapEnabled = false
            }
        };
    }
    
    /// <summary>
    /// 절대 경로를 상대 경로로 변환
    /// </summary>
    public string ToRelativePath(string absolutePath, string projectFilePath)
    {
        if (string.IsNullOrEmpty(absolutePath) || string.IsNullOrEmpty(projectFilePath))
            return absolutePath;
        
        try
        {
            var projectDir = Path.GetDirectoryName(projectFilePath);
            if (string.IsNullOrEmpty(projectDir))
                return absolutePath;
            
            var projectUri = new Uri(projectDir + Path.DirectorySeparatorChar);
            var fileUri = new Uri(absolutePath);
            
            var relativeUri = projectUri.MakeRelativeUri(fileUri);
            var relativePath = Uri.UnescapeDataString(relativeUri.ToString());
            
            // URI 경로 구분자를 OS 경로 구분자로 변환
            return relativePath.Replace('/', Path.DirectorySeparatorChar);
        }
        catch
        {
            return absolutePath;
        }
    }
    
    /// <summary>
    /// 상대 경로를 절대 경로로 변환
    /// </summary>
    public string ToAbsolutePath(string relativePath, string projectFilePath)
    {
        if (string.IsNullOrEmpty(relativePath) || string.IsNullOrEmpty(projectFilePath))
            return relativePath;
        
        // 이미 절대 경로인 경우
        if (Path.IsPathRooted(relativePath))
            return relativePath;
        
        try
        {
            var projectDir = Path.GetDirectoryName(projectFilePath);
            if (string.IsNullOrEmpty(projectDir))
                return relativePath;
            
            return Path.GetFullPath(Path.Combine(projectDir, relativePath));
        }
        catch
        {
            return relativePath;
        }
    }
    
    /// <summary>
    /// 버전 호환성 체크
    /// </summary>
    private bool IsVersionCompatible(string version)
    {
        // 현재는 1.x 버전만 지원
        if (string.IsNullOrEmpty(version))
            return false;
        
        return version.StartsWith("1.");
    }
}

