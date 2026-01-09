namespace SpatialView.Engine.Data.Sources;

/// <summary>
/// 컬럼 스키마 정보
/// </summary>
public class ColumnSchema
{
    /// <summary>
    /// 컬럼 이름
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 데이터 타입
    /// </summary>
    public Type DataType { get; set; } = typeof(string);
    
    /// <summary>
    /// NULL 허용 여부
    /// </summary>
    public bool AllowNull { get; set; } = true;
    
    /// <summary>
    /// 기본값
    /// </summary>
    public object? DefaultValue { get; set; }
    
    /// <summary>
    /// 최대 길이 (문자열 타입)
    /// </summary>
    public int MaxLength { get; set; } = -1;
    
    /// <summary>
    /// 정밀도 (숫자 타입)
    /// </summary>
    public int Precision { get; set; } = -1;
    
    /// <summary>
    /// 스케일 (숫자 타입)
    /// </summary>
    public int Scale { get; set; } = -1;
    
    /// <summary>
    /// 기본 키 여부
    /// </summary>
    public bool IsPrimaryKey { get; set; }
    
    /// <summary>
    /// 유니크 여부
    /// </summary>
    public bool IsUnique { get; set; }
    
    /// <summary>
    /// 인덱스 여부
    /// </summary>
    public bool IsIndexed { get; set; }
    
    /// <summary>
    /// 컬럼 설명
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    public override string ToString()
    {
        return $"{Name} ({DataType.Name}{(AllowNull ? ", NULL" : ", NOT NULL")})";
    }
}