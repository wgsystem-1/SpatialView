using SpatialView.Core.Models;
using SpatialView.Core.Services.Interfaces;
using SpatialView.Infrastructure.DataProviders;

namespace SpatialView.Infrastructure.Services;

/// <summary>
/// 데이터 로딩 서비스 구현
/// 등록된 DataProvider들을 관리하고 파일 확장자에 따라 적절한 Provider를 선택합니다.
/// </summary>
public class DataLoaderService : IDataLoaderService
{
    private readonly List<IDataProvider> _providers;
    
    /// <summary>
    /// 마지막 로드 작업에서 발생한 오류 목록
    /// </summary>
    public List<string> LastErrors { get; } = new();
    
    public DataLoaderService()
    {
        // 지원되는 DataProvider 등록
        _providers = new List<IDataProvider>
        {
            new ShapefileDataProvider(),
            new GeoJsonDataProvider(),
            new FileGdbDataProvider()  // GDAL 기반 FileGDB 지원
        };
        
        System.Diagnostics.Debug.WriteLine($"DataLoaderService 초기화: {_providers.Count}개 Provider 등록됨");
    }
    
    /// <summary>
    /// 지원되는 모든 파일 확장자 목록
    /// </summary>
    public string[] SupportedExtensions => _providers
        .SelectMany(p => p.SupportedExtensions)
        .Distinct()
        .ToArray();
    
    /// <summary>
    /// 파일 기반 Provider만 필터링 (폴더 기반 제외)
    /// </summary>
    private IEnumerable<IDataProvider> FileBasedProviders => _providers.Where(p => !p.IsFolderBased);
    
    /// <summary>
    /// 파일 열기 다이얼로그용 필터 문자열 생성
    /// 폴더 기반 데이터소스 (FileGDB 등)는 제외됩니다.
    /// </summary>
    public string FileDialogFilter
    {
        get
        {
            var filters = new List<string>();
            var fileProviders = FileBasedProviders.ToList();
            
            // 모든 지원 파일 (파일 기반만)
            var allExtensions = string.Join(";", fileProviders.SelectMany(p => p.SupportedExtensions).Select(e => $"*{e}"));
            filters.Add($"모든 공간 데이터|{allExtensions}");
            
            // 개별 포맷별 필터 (파일 기반만)
            foreach (var provider in fileProviders)
            {
                var extensions = string.Join(";", provider.SupportedExtensions.Select(e => $"*{e}"));
                filters.Add($"{provider.ProviderName} 파일|{extensions}");
            }
            
            // 모든 파일
            filters.Add("모든 파일|*.*");
            
            return string.Join("|", filters);
        }
    }
    
    /// <summary>
    /// 해당 파일을 로드할 수 있는지 확인
    /// </summary>
    public bool CanLoad(string filePath)
    {
        return _providers.Any(p => p.CanLoad(filePath));
    }
    
    /// <summary>
    /// 파일을 비동기로 로드
    /// </summary>
    public async Task<LayerInfo> LoadFileAsync(string filePath)
    {
        return await LoadFileAsync(filePath, CancellationToken.None);
    }
    
    /// <summary>
    /// 파일을 비동기로 로드 (취소 지원)
    /// </summary>
    public async Task<LayerInfo> LoadFileAsync(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        System.Diagnostics.Debug.WriteLine($"LoadFileAsync: {filePath}");
        
        var provider = _providers.FirstOrDefault(p => p.CanLoad(filePath));
        
        if (provider == null)
        {
            var extension = Path.GetExtension(filePath);
            throw new NotSupportedException($"지원하지 않는 파일 형식입니다: {extension}");
        }
        
        System.Diagnostics.Debug.WriteLine($"Provider 선택됨: {provider.ProviderName}");
        
        cancellationToken.ThrowIfCancellationRequested();
        
        return await provider.LoadAsync(filePath);
    }
    
    /// <summary>
    /// 여러 파일을 비동기로 로드
    /// </summary>
    public async Task<IEnumerable<LayerInfo>> LoadFilesAsync(IEnumerable<string> filePaths)
    {
        return await LoadFilesAsync(filePaths, CancellationToken.None, null);
    }
    
    /// <summary>
    /// 여러 파일을 비동기로 로드 (취소 및 진행 상황 지원)
    /// </summary>
    public async Task<IEnumerable<LayerInfo>> LoadFilesAsync(
        IEnumerable<string> filePaths, 
        CancellationToken cancellationToken,
        IProgress<LoadingProgress>? progress = null)
    {
        LastErrors.Clear();
        var results = new List<LayerInfo>();
        var fileList = filePaths.ToList();
        var total = fileList.Count;
        var current = 0;
        
        foreach (var filePath in fileList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // 진행 상황 보고
            progress?.Report(new LoadingProgress
            {
                Current = current,
                Total = total,
                CurrentFile = Path.GetFileName(filePath)
            });
            
            try
            {
                System.Diagnostics.Debug.WriteLine($"파일 로드 시작: {filePath}");
                var layerInfo = await LoadFileAsync(filePath, cancellationToken);
                results.Add(layerInfo);
                System.Diagnostics.Debug.WriteLine($"파일 로드 성공: {filePath}, 피처 수: {layerInfo.FeatureCount}");
            }
            catch (OperationCanceledException)
            {
                throw; // 취소는 그대로 전파
            }
            catch (Exception ex)
            {
                var errorMsg = $"{Path.GetFileName(filePath)}: {ex.Message}";
                LastErrors.Add(errorMsg);
                System.Diagnostics.Debug.WriteLine($"파일 로드 실패: {filePath}");
                System.Diagnostics.Debug.WriteLine($"  예외 타입: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"  메시지: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"  스택: {ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"  내부 예외: {ex.InnerException.Message}");
                }
            }
            
            current++;
        }
        
        // 최종 진행 상황 보고
        progress?.Report(new LoadingProgress
        {
            Current = total,
            Total = total,
            CurrentFile = "완료"
        });
        
        // 오류가 있으면 예외 발생
        if (LastErrors.Count > 0 && results.Count == 0)
        {
            throw new AggregateException(
                $"모든 파일 로드 실패:\n{string.Join("\n", LastErrors)}",
                LastErrors.Select(e => new Exception(e)));
        }
        
        return results;
    }
    
    /// <summary>
    /// DataProvider 등록
    /// </summary>
    public void RegisterProvider(IDataProvider provider)
    {
        if (!_providers.Contains(provider))
        {
            _providers.Add(provider);
        }
    }
}
