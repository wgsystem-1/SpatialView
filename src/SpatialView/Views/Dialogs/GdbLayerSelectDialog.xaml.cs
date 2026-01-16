using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SpatialView.Infrastructure.DataProviders;
using MessageBox = System.Windows.MessageBox;

namespace SpatialView.Views.Dialogs;

/// <summary>
/// FileGDB 레이어 선택 다이얼로그
/// </summary>
public partial class GdbLayerSelectDialog : Window
{
    private readonly List<GdbLayerInfo> _layers;
    private GridViewColumnHeader? _lastHeaderClicked = null;
    private ListSortDirection _lastDirection = ListSortDirection.Ascending;
    
    /// <summary>
    /// 선택된 레이어 인덱스 목록
    /// </summary>
    public int[] SelectedLayerIndices { get; private set; } = Array.Empty<int>();
    
    public GdbLayerSelectDialog(string gdbPath, List<GdbLayerInfo> layers)
    {
        InitializeComponent();
        
        _layers = layers;
        foreach (var layer in _layers)
        {
            layer.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(GdbLayerInfo.IsSelected))
                    UpdateSelectionCount();
            };
        }
        
        GdbPathText.Text = gdbPath;
        LayerListView.ItemsSource = _layers;
        
        UpdateSelectionCount();
    }
    
    /// <summary>
    /// 컬럼 헤더 클릭 시 정렬
    /// </summary>
    private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader headerClicked) return;
        if (headerClicked.Role == GridViewColumnHeaderRole.Padding) return;
        if (headerClicked.Tag is not string sortBy) return;
        
        ListSortDirection direction;
        
        if (headerClicked != _lastHeaderClicked)
        {
            direction = ListSortDirection.Ascending;
        }
        else
        {
            direction = _lastDirection == ListSortDirection.Ascending 
                ? ListSortDirection.Descending 
                : ListSortDirection.Ascending;
        }
        
        // 정렬 적용
        var dataView = CollectionViewSource.GetDefaultView(LayerListView.ItemsSource);
        dataView.SortDescriptions.Clear();
        dataView.SortDescriptions.Add(new SortDescription(sortBy, direction));
        dataView.Refresh();
        
        // 헤더 텍스트 업데이트 (정렬 방향 표시)
        UpdateHeaderText(headerClicked, direction);
        
        _lastHeaderClicked = headerClicked;
        _lastDirection = direction;
    }
    
    /// <summary>
    /// 헤더 텍스트에 정렬 방향 표시
    /// </summary>
    private void UpdateHeaderText(GridViewColumnHeader header, ListSortDirection direction)
    {
        // 모든 헤더의 화살표 초기화
        if (LayerListView.View is GridView gridView)
        {
            foreach (var column in gridView.Columns)
            {
                if (column.Header is GridViewColumnHeader colHeader && colHeader.Tag is string tag)
                {
                    var baseText = tag switch
                    {
                        "Name" => "레이어 이름",
                        "FeatureCount" => "피처 수",
                        "GeometryType" => "유형",
                        _ => tag
                    };
                    colHeader.Content = colHeader == header 
                        ? $"{baseText} {(direction == ListSortDirection.Ascending ? "▲" : "▼")}"
                        : $"{baseText} ↕";
                }
            }
        }
    }
    
    private void UpdateSelectionCount()
    {
        var selectedCount = _layers.Count(l => l.IsSelected);
        SelectionCountText.Text = $"{selectedCount}개 선택됨";
    }

    private void CheckBox_Click(object sender, RoutedEventArgs e)
    {
        UpdateSelectionCount();
    }
    
    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var layer in _layers)
        {
            layer.IsSelected = true;
        }
        LayerListView.Items.Refresh();
        UpdateSelectionCount();
    }
    
    private void DeselectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var layer in _layers)
        {
            layer.IsSelected = false;
        }
        LayerListView.Items.Refresh();
        UpdateSelectionCount();
    }
    
    private void OK_Click(object sender, RoutedEventArgs e)
    {
        var selectedLayers = _layers.Where(l => l.IsSelected).ToList();
        
        if (selectedLayers.Count == 0)
        {
            MessageBox.Show("레이어를 하나 이상 선택하세요.", "알림",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        SelectedLayerIndices = selectedLayers.Select(l => l.Index).ToArray();
        DialogResult = true;
        Close();
    }
    
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

