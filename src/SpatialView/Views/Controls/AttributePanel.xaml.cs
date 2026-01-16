using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SpatialView.ViewModels;
using SpatialView.Views.Dialogs;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace SpatialView.Views.Controls;

/// <summary>
/// 속성 테이블 패널
/// </summary>
public partial class AttributePanel : UserControl
{
    private AttributePanelViewModel? _viewModel;
    
        /// <summary>
        /// 특정 FID 행 선택 및 스크롤
        /// </summary>
        public bool SelectRowByFid(uint fid)
        {
            if (_viewModel == null) return false;

            var found = _viewModel.TrySelectFid(fid);
            if (found)
            {
                // UI 스크롤
                AttributeDataGrid.UpdateLayout();
                AttributeDataGrid.ScrollIntoView(AttributeDataGrid.SelectedItem);
            }

            return found;
        }

    public AttributePanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }
    
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AttributePanelViewModel viewModel)
        {
            _viewModel = viewModel;
            _viewModel.AddFieldRequested += OnAddFieldRequested;
            _viewModel.DeleteFieldRequested += OnDeleteFieldRequested;
            _viewModel.FieldCalculatorRequested += OnFieldCalculatorRequested;
            _viewModel.SaveTableRequested += OnSaveTableRequested;
            _viewModel.ExportTableRequested += OnExportTableRequested;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.AddFieldRequested -= OnAddFieldRequested;
            _viewModel.DeleteFieldRequested -= OnDeleteFieldRequested;
            _viewModel.FieldCalculatorRequested -= OnFieldCalculatorRequested;
            _viewModel.SaveTableRequested -= OnSaveTableRequested;
            _viewModel.ExportTableRequested -= OnExportTableRequested;
            _viewModel = null;
        }
    }
    
    /// <summary>
    /// 필드 삭제 버튼 클릭 - 선택할 필드 목록 표시
    /// </summary>
    private void DeleteField_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.AttributeTable == null)
        {
            MessageBox.Show("속성 테이블이 로드되지 않았습니다.", "알림",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        // 삭제 가능한 필드 목록 (FID 제외)
        var fields = new List<string>();
        foreach (DataColumn col in _viewModel.AttributeTable.Columns)
        {
            if (col.ColumnName != "FID")
            {
                fields.Add(col.ColumnName);
            }
        }
        
        if (fields.Count == 0)
        {
            MessageBox.Show("삭제할 수 있는 필드가 없습니다.", "알림",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        // 필드 선택 다이얼로그
        var dialog = new SelectFieldDialog(fields, "삭제할 필드 선택")
        {
            Owner = Window.GetWindow(this)
        };
        
        if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedField))
        {
            OnDeleteFieldRequested(dialog.SelectedField);
        }
    }
    
    /// <summary>
    /// 열 헤더 컨텍스트 메뉴 - 필드 삭제
    /// </summary>
    private void DeleteColumn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem menuItem)
        {
            // 헤더에서 열 이름 가져오기
            var contextMenu = menuItem.Parent as System.Windows.Controls.ContextMenu;
            if (contextMenu?.PlacementTarget is DataGridColumnHeader header)
            {
                var columnName = header.Column?.Header?.ToString();
                if (!string.IsNullOrEmpty(columnName))
                {
                    OnDeleteFieldRequested(columnName);
                }
            }
        }
    }
    
    /// <summary>
    /// 열 자동 생성 시 - 열 헤더에 컨텍스트 메뉴 바인딩
    /// </summary>
    private void DataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        // FID 열은 삭제 불가능하므로 컨텍스트 메뉴 비활성화는 스타일에서 처리
    }
    
    /// <summary>
    /// 필드 추가 다이얼로그 표시
    /// </summary>
    private void OnAddFieldRequested()
    {
        var dialog = new AddFieldDialog
        {
            Owner = Window.GetWindow(this)
        };
        
        if (dialog.ShowDialog() == true)
        {
            object? defaultValue = null;
            if (!string.IsNullOrEmpty(dialog.DefaultValue))
            {
                try
                {
                    defaultValue = Convert.ChangeType(dialog.DefaultValue, dialog.FieldType);
                }
                catch
                {
                    defaultValue = null;
                }
            }
            
            _viewModel?.ExecuteAddField(dialog.FieldName, dialog.FieldType, defaultValue);
        }
    }
    
    /// <summary>
    /// 필드 삭제 확인
    /// </summary>
    private void OnDeleteFieldRequested(string fieldName)
    {
        var result = MessageBox.Show(
            $"필드 '{fieldName}'을(를) 삭제하시겠습니까?\n\n이 작업은 되돌릴 수 없습니다.",
            "필드 삭제",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        
        if (result == MessageBoxResult.Yes)
        {
            _viewModel?.ExecuteDeleteField(fieldName);
        }
    }
    
    /// <summary>
    /// 필드 계산기 다이얼로그 표시
    /// </summary>
    private void OnFieldCalculatorRequested()
    {
        if (_viewModel?.AttributeTable == null)
        {
            MessageBox.Show("속성 테이블이 로드되지 않았습니다.", "알림",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        var dialog = new FieldCalculatorDialog(_viewModel.AttributeTable)
        {
            Owner = Window.GetWindow(this)
        };
        
        if (dialog.ShowDialog() == true && dialog.TargetField != null)
        {
            _viewModel.ExecuteFieldCalculation(
                dialog.TargetField,
                dialog.Expression,
                dialog.IsNewField,
                dialog.NewFieldName,
                dialog.NewFieldType);
        }
    }
    
    /// <summary>
    /// DataGrid 더블클릭 시 해당 피처로 줌
    /// </summary>
    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is AttributePanelViewModel viewModel)
        {
            viewModel.ZoomToSelectedCommand.Execute(null);
        }
    }

    /// <summary>
    /// 데이터 소스에 저장
    /// </summary>
    private async void OnSaveTableRequested()
    {
        if (_viewModel?.AttributeTable == null)
        {
            MessageBox.Show("속성 테이블이 로드되지 않았습니다.", "알림",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!_viewModel.HasUnsavedChanges)
        {
            MessageBox.Show("저장할 변경사항이 없습니다.", "알림",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            "변경된 속성을 원본 파일에 저장하시겠습니까?\n\n이 작업은 되돌릴 수 없습니다.",
            "속성 저장 확인",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        var (success, message) = await _viewModel.SaveToDataSourceAsync();

        if (success)
        {
            MessageBox.Show(message, "저장 완료",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show(message, "저장 실패",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 테이블 내보내기 다이얼로그 표시
    /// </summary>
    private void OnExportTableRequested()
    {
        if (_viewModel?.AttributeTable == null)
        {
            MessageBox.Show("속성 테이블이 로드되지 않았습니다.", "알림",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV 파일 (*.csv)|*.csv|탭 구분 파일 (*.txt)|*.txt|Excel 호환 (*.tsv)|*.tsv",
            DefaultExt = ".csv",
            FileName = $"{_viewModel.SelectedLayer?.Name ?? "attributes"}_{DateTime.Now:yyyyMMdd}"
        };

        if (saveDialog.ShowDialog() == true)
        {
            bool success;
            var ext = System.IO.Path.GetExtension(saveDialog.FileName).ToLowerInvariant();

            if (ext == ".tsv" || ext == ".txt")
            {
                success = _viewModel.ExportTableToTsv(saveDialog.FileName);
            }
            else
            {
                success = _viewModel.ExportTableToCsv(saveDialog.FileName);
            }

            if (success)
            {
                MessageBox.Show($"속성 테이블이 내보내졌습니다.\n{saveDialog.FileName}", "내보내기 완료",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}

