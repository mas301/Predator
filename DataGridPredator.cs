using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Predator;


public sealed class DataGridPredator : DataGrid
{
    private sealed class ClienteRow
    {
        public int Id { get; init; }
        public string Nombre { get; init; } = "";
        public string Email { get; init; } = "";
        public decimal Saldo { get; init; }
        public bool Activo { get; init; }
    }

    private sealed class ArticuloRow
    {
        public int Id { get; init; }
        public string Codigo { get; init; } = "";
        public string Descripcion { get; init; } = "";
        public decimal Precio { get; init; }
        public int Stock { get; init; }
        public bool Activo { get; init; }
    }

    private sealed class GenericRow
    {
        public int Id { get; init; }
        public string Tabla { get; init; } = "";
        public string Descripcion { get; init; } = "";
        public decimal Valor { get; init; }
        public bool Activo { get; init; }
    }

    protected override Type StyleKeyOverride => typeof(DataGrid);

    private IList<object>? _data;
    private List<(string Header, string PropertyName, bool IsCheckBox)> _columnDefinitions = [];
    private Dictionary<string, bool> _columnVisibility = [];
    private string? _lastSortProperty;
    private bool _lastSortAscending = true;
    private CheckBox? _headerSelectionCheckBox;
    private bool _isSyncingHeaderSelection;

    public DataGridPredator()
    {
        AutoGenerateColumns = false;
        SelectionMode = DataGridSelectionMode.Extended;
        CanUserSortColumns = true;
        CanUserReorderColumns = true;
        CanUserResizeColumns = true;
        IsReadOnly = true;
        GridLinesVisibility = DataGridGridLinesVisibility.All;
        BorderThickness = new Avalonia.Thickness(1);
        BorderBrush = Avalonia.Media.Brushes.LightGray;

        SelectionChanged += (_, _) =>
        {
            ApplyRowVisualState();
            UpdateHeaderSelectionCheckState();
        };
        LayoutUpdated += (_, _) => ApplyRowVisualState();
        Sorting += OnSorting;
    }

    private void OnSorting(object? sender, DataGridColumnEventArgs e)
    {
        if (ItemsSource is not System.Collections.ObjectModel.ObservableCollection<object> rows)
            return;

        var sortProperty = e.Column.SortMemberPath;
        if (string.IsNullOrWhiteSpace(sortProperty))
            return;

        var ascending = _lastSortProperty == sortProperty ? !_lastSortAscending : true;
        _lastSortProperty = sortProperty;
        _lastSortAscending = ascending;

        var sorted = ascending
            ? rows.OrderBy(item => GetSortableValue(item, sortProperty)).ToList()
            : rows.OrderByDescending(item => GetSortableValue(item, sortProperty)).ToList();

        rows.Clear();
        foreach (var item in sorted)
            rows.Add(item);

        Dispatcher.UIThread.Post(ApplyRowVisualState);
    }

    private static object? GetSortableValue(object item, string propertyName)
    {
        var property = item.GetType().GetProperty(propertyName);
        return property?.GetValue(item);
    }

    public void LoadData(IList<object> data, List<(string Header, string PropertyName, bool IsCheckBox)> columns)
    {
        _data = data;
        _columnDefinitions = columns;

        // Initialize visibility (all visible by default, keep previous state if possible)
        var oldVisibility = new Dictionary<string, bool>(_columnVisibility);
        _columnVisibility.Clear();
        foreach (var (header, _, _) in _columnDefinitions)
        {
            _columnVisibility[header] = oldVisibility.TryGetValue(header, out var vis) ? vis : true;
        }

        // Rebuild columns
        RebuildColumns();

        // Convertir a ObservableCollection para que soporte ordenamiento
        var observableData = new System.Collections.ObjectModel.ObservableCollection<object>(data);
        ItemsSource = observableData;
        Dispatcher.UIThread.Post(ApplyRowVisualState);
        Dispatcher.UIThread.Post(UpdateHeaderSelectionCheckState);
    }

    public void LoadData(string tableTitle)
    {
        if (string.Equals(tableTitle, "Clientes", StringComparison.OrdinalIgnoreCase))
        {
            LoadData(BuildClientesRows(),
            [
                ("Selección", "", false),
                ("ID", "Id", false),
                ("Nombre", "Nombre", false),
                ("Email", "Email", false),
                ("Saldo", "Saldo", false),
                ("Activo", "Activo", true)
            ]);
            return;
        }

        if (string.Equals(tableTitle, "Articulos", StringComparison.OrdinalIgnoreCase))
        {
            LoadData(BuildArticulosRows(),
            [
                ("Selección", "", false),
                ("ID", "Id", false),
                ("Codigo", "Codigo", false),
                ("Descripcion", "Descripcion", false),
                ("Precio", "Precio", false),
                ("Stock", "Stock", false),
                ("Activo", "Activo", true)
            ]);
            return;
        }

        LoadData(BuildGenericRows(tableTitle),
        [
            ("Selección", "", false),
            ("ID", "Id", false),
            ("Tabla", "Tabla", false),
            ("Descripcion", "Descripcion", false),
            ("Valor", "Valor", false),
            ("Activo", "Activo", true)
        ]);
    }

    private void RebuildColumns()
    {
        Columns.Clear();

        foreach (var (header, propertyName, isCheckBox) in _columnDefinitions)
        {
            if (!_columnVisibility.GetValueOrDefault(header, true))
                continue;

            DataGridColumn col;
            if (header == "Selección")
                col = BuildSelectionColumn();
            else if (isCheckBox)
                col = BuildCheckBoxColumn(header, propertyName);
            else
                col = BuildTextColumn(header, propertyName);

            Columns.Add(col);
        }
    }


    public IEnumerable<object> SelectedRows =>
        SelectedItems.Cast<object>();

    public int TotalRows => _data?.Count ?? 0;

    private DataGridTemplateColumn BuildSelectionColumn()
    {
        var headerCheckBox = new CheckBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            IsThreeState = true
        };

        _headerSelectionCheckBox = headerCheckBox;

        headerCheckBox.PropertyChanged += (_, args) =>
        {
            if (_isSyncingHeaderSelection)
                return;

            if (args.Property != ToggleButton.IsCheckedProperty)
                return;

            var isChecked = args.NewValue as bool?;
            if (isChecked == true)
            {
                SelectAllRows();
                return;
            }

            if (isChecked == false)
                SelectedItems.Clear();
        };

        Dispatcher.UIThread.Post(UpdateHeaderSelectionCheckState);

        return new DataGridTemplateColumn
        {
            Header = headerCheckBox,
            Width = new DataGridLength(40),
            CanUserSort = false,
            CanUserReorder = true,
            CanUserResize = true,
            CellTemplate = new FuncDataTemplate<object>((_, _) =>
            {
                var container = new Border();
                var checkBox = new CheckBox
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsThreeState = false
                };

                void SyncCheckState()
                {
                    var currentItem = checkBox.DataContext;
                    var shouldBeChecked = currentItem is not null && SelectedItems.Contains(currentItem);
                    if (checkBox.IsChecked != shouldBeChecked)
                        checkBox.IsChecked = shouldBeChecked;
                }

                EventHandler<SelectionChangedEventArgs>? selectionChangedHandler = (_, _) => SyncCheckState();
                SelectionChanged += selectionChangedHandler;
                checkBox.DetachedFromVisualTree += (_, _) =>
                {
                    SelectionChanged -= selectionChangedHandler;
                };
                SyncCheckState();

                // Keep row-selection behavior stable and explicit when clicking the checkbox.
                checkBox.Click += (_, e) =>
                {
                    var currentItem = checkBox.DataContext;
                    if (currentItem is null)
                    {
                        e.Handled = true;
                        return;
                    }

                    var isChecked = checkBox.IsChecked == true;

                    if (isChecked)
                    {
                        if (!SelectedItems.Contains(currentItem))
                            SelectedItems.Add(currentItem);
                    }
                    else if (SelectedItems.Contains(currentItem))
                    {
                        SelectedItems.Remove(currentItem);
                    }

                    e.Handled = true;
                };

                container.Child = checkBox;
                return container;
            }),
            CellEditingTemplate = new FuncDataTemplate<object>((item, _) => new Border())
        };
    }

    private void UpdateHeaderSelectionCheckState()
    {
        if (_headerSelectionCheckBox is null)
            return;

        var totalCount = _data?.Count ?? 0;
        var selectedCount = SelectedItems.Count;

        bool? newState = selectedCount switch
        {
            0 => false,
            _ when totalCount > 0 && selectedCount >= totalCount => true,
            _ => null
        };

        _isSyncingHeaderSelection = true;
        _headerSelectionCheckBox.IsChecked = newState;
        _isSyncingHeaderSelection = false;
    }

    private void SelectAllRows()
    {
        if (_data is null)
            return;

        SelectedItems.Clear();
        foreach (var row in _data)
            SelectedItems.Add(row);
    }

    private void ApplyRowVisualState()
    {
        foreach (var row in this.GetVisualDescendants().OfType<DataGridRow>())
        {
            if (row.DataContext is not object item)
                continue;

            // Row containers can be recycled between tabs; force selection checkbox to mirror row state.
            var selectionCheckBox = row.GetVisualDescendants().OfType<CheckBox>().FirstOrDefault();
            if (selectionCheckBox is not null && selectionCheckBox.IsChecked != row.IsSelected)
                selectionCheckBox.IsChecked = row.IsSelected;

            // Selected rows must keep selected color, even if they are inactive/anuladas.
            if (row.IsSelected)
            {
                row.ClearValue(DataGridRow.BackgroundProperty);
                continue;
            }

            var inactive = TryGetBooleanProperty(item, "Activo", out var activo) && !activo;
            if (inactive)
            {
                row.Background = Brushes.WhiteSmoke;
            }
            else
            {
                row.ClearValue(DataGridRow.BackgroundProperty);
            }
        }
    }

    private static bool TryGetBooleanProperty(object row, string propertyName, out bool value)
    {
        value = false;
        var property = row.GetType().GetProperty(propertyName);
        if (property?.PropertyType != typeof(bool))
            return false;

        value = (bool)(property.GetValue(row) ?? false);
        return true;
    }

    private static DataGridTextColumn BuildTextColumn(string header, string propertyName)
    {
        return new DataGridTextColumn
        {
            Header = header,
            IsReadOnly = true,
            CanUserSort = true,
            SortMemberPath = propertyName,
            Binding = new Avalonia.Data.Binding(propertyName)
        };
    }

    private static DataGridCheckBoxColumn BuildCheckBoxColumn(string header, string propertyName)
    {
        return new DataGridCheckBoxColumn
        {
            Header = header,
            IsReadOnly = true,
            CanUserSort = true,
            SortMemberPath = propertyName,
            Binding = new Avalonia.Data.Binding(propertyName)
        };
    }

    private static List<object> BuildClientesRows()
    {
        return new List<object>
        {
            new ClienteRow { Id = 1, Nombre = "Ana Garcia", Email = "ana@demo.com", Saldo = 120.50m, Activo = true },
            new ClienteRow { Id = 2, Nombre = "Luis Perez", Email = "luis@demo.com", Saldo = 98.20m, Activo = true },
            new ClienteRow { Id = 3, Nombre = "Marta Diaz", Email = "marta@demo.com", Saldo = 0m, Activo = false },
            new ClienteRow { Id = 4, Nombre = "Carlos Ruiz", Email = "carlos@demo.com", Saldo = 310.75m, Activo = true }
        };
    }

    private static List<object> BuildArticulosRows()
    {
        return new List<object>
        {
            new ArticuloRow { Id = 1, Codigo = "ART-001", Descripcion = "Teclado", Precio = 35.90m, Stock = 12, Activo = true },
            new ArticuloRow { Id = 2, Codigo = "ART-002", Descripcion = "Monitor", Precio = 189.00m, Stock = 7, Activo = true },
            new ArticuloRow { Id = 3, Codigo = "ART-003", Descripcion = "Raton", Precio = 18.50m, Stock = 25, Activo = true },
            new ArticuloRow { Id = 4, Codigo = "ART-004", Descripcion = "Impresora", Precio = 129.99m, Stock = 3, Activo = false }
        };
    }

    private static List<object> BuildGenericRows(string tableTitle)
    {
        return Enumerable.Range(1, 4)
            .Select(index => new GenericRow
            {
                Id = index,
                Tabla = tableTitle,
                Descripcion = $"Registro {index} de {tableTitle}",
                Valor = 50 + (index * 10),
                Activo = index % 2 == 0
            })
            .Cast<object>()
            .ToList();
    }

    public System.Collections.ObjectModel.ObservableCollection<ColumnConfig> GetColumnConfigs()
    {
        var configs = new System.Collections.ObjectModel.ObservableCollection<ColumnConfig>();
        // Always use the original definition order for DisplayOrder
        for (int i = 0; i < _columnDefinitions.Count; i++)
        {
            var (header, _, _) = _columnDefinitions[i];
            var isVisible = _columnVisibility.TryGetValue(header, out var visible) ? visible : true;
            configs.Add(new ColumnConfig(header, header, i, isVisible));
        }
        return configs;
    }

    public void ApplyColumnConfigs(System.Collections.ObjectModel.ObservableCollection<ColumnConfig> configs)
    {
        // Update visibility state
        _columnVisibility.Clear();
        foreach (var config in configs)
        {
            _columnVisibility[config.Header] = config.IsVisible;
        }

        // Reorder _columnDefinitions according to DisplayOrder of ALL configs (not just visibles)
        var orderedConfigs = configs.OrderBy(c => c.DisplayOrder).ToList();
        var reorderedDefinitions = new List<(string Header, string PropertyName, bool IsCheckBox)>();
        foreach (var config in orderedConfigs)
        {
            var definition = _columnDefinitions.FirstOrDefault(d => d.Header == config.Header);
            if (definition != default)
            {
                reorderedDefinitions.Add(definition);
            }
        }
        _columnDefinitions = reorderedDefinitions;

        // Rebuild the DataGrid columns
        RebuildColumns();
    }
}
