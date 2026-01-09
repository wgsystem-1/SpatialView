using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;

namespace SpatialView.Views;

/// <summary>
/// 클립보드에 저장되는 속성 데이터 구조
/// </summary>
public class ClipboardAttributeData
{
    public string LayerName { get; set; } = string.Empty;
    public Dictionary<string, string> Attributes { get; set; } = new();
}

/// <summary>
/// 피처 속성 팝업의 ViewModel
/// </summary>
public partial class FeaturePopupViewModel : ObservableObject
{
    [ObservableProperty]
    private string _layerName = string.Empty;
    
    [ObservableProperty]
    private uint _featureId;
    
    [ObservableProperty]
    private ObservableCollection<AttributeItem> _attributes = new();
    
    [ObservableProperty]
    private bool _selectAll = false;
    
    [ObservableProperty]
    private string _statusMessage = string.Empty;
    
    /// <summary>
    /// 피처로 줌 요청 이벤트
    /// </summary>
    public event Action<uint>? ZoomToFeatureRequested;
    
    /// <summary>
    /// 속성 붙여넣기 요청 이벤트
    /// </summary>
    public event Action<Dictionary<string, string>>? PasteAttributesRequested;
    
    [RelayCommand]
    private void ZoomToFeature()
    {
        ZoomToFeatureRequested?.Invoke(FeatureId);
    }
    
    /// <summary>
    /// 전체 선택/해제 토글
    /// </summary>
    partial void OnSelectAllChanged(bool value)
    {
        foreach (var attr in Attributes)
        {
            attr.IsSelected = value;
        }
    }
    
    /// <summary>
    /// 선택된 속성 복사
    /// </summary>
    [RelayCommand]
    private void CopyAttributes()
    {
        var selectedAttrs = Attributes.Where(a => a.IsSelected).ToList();
        
        if (selectedAttrs.Count == 0)
        {
            // 선택된 것이 없으면 모두 복사
            selectedAttrs = Attributes.ToList();
        }
        
        // 레이어명과 속성을 함께 저장
        var clipboardData = new ClipboardAttributeData
        {
            LayerName = LayerName,
            Attributes = selectedAttrs.ToDictionary(a => a.Key, a => a.Value)
        };
        
        var json = JsonSerializer.Serialize(clipboardData, new JsonSerializerOptions 
        { 
            WriteIndented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        
        try
        {
            Clipboard.SetText(json);
            MessageBox.Show($"{selectedAttrs.Count}개 필드가 복사되었습니다.", "복사 완료",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"클립보드 복사 오류: {ex.Message}");
            MessageBox.Show($"복사 실패: {ex.Message}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    /// <summary>
    /// 속성 붙여넣기
    /// </summary>
    [RelayCommand]
    private void PasteAttributes()
    {
        try
        {
            if (!Clipboard.ContainsText())
            {
                MessageBox.Show("붙여넣을 수 있는 속성 데이터가 없습니다.", "알림",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            var text = Clipboard.GetText();
            
            // ClipboardAttributeData 형식 파싱 시도
            try
            {
                var clipboardData = JsonSerializer.Deserialize<ClipboardAttributeData>(text);
                if (clipboardData != null && clipboardData.Attributes.Count > 0)
                {
                    // 레이어명 확인
                    if (!string.Equals(clipboardData.LayerName, LayerName, StringComparison.OrdinalIgnoreCase))
                    {
                        var result = MessageBox.Show(
                            $"복사한 속성의 레이어({clipboardData.LayerName})와\n" +
                            $"현재 레이어({LayerName})가 다릅니다.\n\n" +
                            $"동일한 레이어에서만 붙여넣기가 가능합니다.",
                            "레이어 불일치",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }
                    
                    PasteAttributesRequested?.Invoke(clipboardData.Attributes);
                    return;
                }
            }
            catch
            {
                // ClipboardAttributeData 형식이 아님
            }
            
            // 기존 Dictionary<string, string> 형식 시도 (하위 호환)
            try
            {
                var attrDict = JsonSerializer.Deserialize<Dictionary<string, string>>(text);
                if (attrDict != null && attrDict.Count > 0)
                {
                    // 레이어 정보가 없으므로 경고
                    var result = MessageBox.Show(
                        "레이어 정보가 없는 속성 데이터입니다.\n" +
                        "붙여넣기를 계속하시겠습니까?",
                        "경고",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        PasteAttributesRequested?.Invoke(attrDict);
                    }
                    return;
                }
            }
            catch
            {
                // JSON이 아님
            }
            
            MessageBox.Show("붙여넣을 수 있는 속성 데이터가 없습니다.", "알림",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"클립보드 붙여넣기 오류: {ex.Message}");
            StatusMessage = "붙여넣기 실패";
        }
    }
    
    /// <summary>
    /// 속성 업데이트 (기존 팝업 내용만 갱신)
    /// </summary>
    public void UpdateAttributes(string layerName, uint featureId, IEnumerable<AttributeItem> attributes)
    {
        LayerName = layerName;
        FeatureId = featureId;
        
        Attributes.Clear();
        foreach (var attr in attributes)
        {
            Attributes.Add(attr);
        }
        
        SelectAll = false;
        StatusMessage = string.Empty;
    }
}

/// <summary>
/// 속성 항목 (Key-Value 쌍)
/// </summary>
public partial class AttributeItem : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected = false;
    
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    
    public AttributeItem() { }
    
    public AttributeItem(string key, object? value)
    {
        Key = key;
        Value = value?.ToString() ?? "(null)";
    }
}

