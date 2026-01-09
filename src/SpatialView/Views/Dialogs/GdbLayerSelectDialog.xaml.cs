using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SpatialView.Infrastructure.DataProviders;
using MessageBox = System.Windows.MessageBox;

namespace SpatialView.Views.Dialogs;

/// <summary>
/// FileGDB 레이어 선택 다이얼로그
/// </summary>
public partial class GdbLayerSelectDialog : Window
{
    private readonly List<GdbLayerInfo> _layers;
    
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

