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
using System.Globalization;
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
    private List<(string Header, string PropertyName, bool IsCheckBox, Type DataType)> _columnDefinitions = [];
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
        Focusable = false;
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

        // Obtener el tipo de dato de la columna
        var columnDataType = _columnDefinitions
            .Where(col => col.PropertyName == sortProperty)
            .Select(col => col.DataType)
            .FirstOrDefault();

        var sorted = rows.ToList();
        sorted.Sort((left, right) =>
        {
            var result = CompareSortableValues(
                GetSortableValue(left, sortProperty), 
                GetSortableValue(right, sortProperty),
                columnDataType);
            return ascending ? result : -result;
        });

        rows.Clear();
        foreach (var item in sorted)
            rows.Add(item);

        Dispatcher.UIThread.Post(ApplyRowVisualState);
    }

    private static object? GetSortableValue(object item, string propertyName)
    {
        if (item is IDictionary<string, object?> dictionary &&
            dictionary.TryGetValue(propertyName, out var value))
        {
            return value;
        }

        var property = item.GetType().GetProperty(propertyName);
        return property?.GetValue(item);
    }

    private static int CompareSortableValues(object? left, object? right, Type? columnDataType = null)
    {
        if (ReferenceEquals(left, right))
            return 0;

        if (left is null)
            return -1;

        if (right is null)
            return 1;

        // Si conocemos el tipo de la columna, parsear según ese tipo
        if (columnDataType != null)
        {
            if (columnDataType == typeof(DateTime) || columnDataType == typeof(DateTime?))
            {
                if (TryToDateTime(left, out var leftDate) && TryToDateTime(right, out var rightDate))
                    return leftDate.CompareTo(rightDate);
            }
            else if (columnDataType == typeof(decimal) || columnDataType == typeof(decimal?))
            {
                if (TryToDecimal(left, out var leftDecimal) && TryToDecimal(right, out var rightDecimal))
                    return leftDecimal.CompareTo(rightDecimal);
            }
            else if (columnDataType == typeof(int) || columnDataType == typeof(int?) ||
                     columnDataType == typeof(double) || columnDataType == typeof(double?) ||
                     columnDataType == typeof(float) || columnDataType == typeof(float?))
            {
                if (TryToDecimal(left, out var leftNum) && TryToDecimal(right, out var rightNum))
                    return leftNum.CompareTo(rightNum);
            }
            else if (columnDataType == typeof(bool) || columnDataType == typeof(bool?))
            {
                if (TryToBoolean(left, out var leftBool) && TryToBoolean(right, out var rightBool))
                    return leftBool.CompareTo(rightBool);
            }
        }

        // Fallback: intentar por tipo de dato actual
        if (left.GetType() == right.GetType() && left is IComparable comparable)
            return comparable.CompareTo(right);

        if (TryToDecimal(left, out var leftDecimal2) && TryToDecimal(right, out var rightDecimal2))
            return leftDecimal2.CompareTo(rightDecimal2);

        if (TryToDateTime(left, out var leftDate2) && TryToDateTime(right, out var rightDate2))
            return leftDate2.CompareTo(rightDate2);

        if (TryToBoolean(left, out var leftBool2) && TryToBoolean(right, out var rightBool2))
            return leftBool2.CompareTo(rightBool2);

        return string.Compare(left.ToString(), right.ToString(), StringComparison.CurrentCultureIgnoreCase);
    }

    private static bool TryToDecimal(object value, out decimal result)
    {
        if (value is decimal d)
        {
            result = d;
            return true;
        }

        var text = value.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            result = 0m;
            return false;
        }

        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out result)
            || decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out result);
    }

    private static bool TryToDateTime(object value, out DateTime result)
    {
        if (value is DateTime date)
        {
            result = date;
            return true;
        }

        return DateTime.TryParse(value.ToString(), CultureInfo.CurrentCulture, DateTimeStyles.None, out result)
            || DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }

    private static bool TryToBoolean(object value, out bool result)
    {
        if (value is bool boolean)
        {
            result = boolean;
            return true;
        }

        return bool.TryParse(value.ToString(), out result);
    }

    public void LoadData(IList<object> data, List<(string Header, string PropertyName, bool IsCheckBox, Type DataType)> columns)
    {
        _data = data;
        _columnDefinitions = columns;

        // Initialize visibility (all visible by default, keep previous state if possible)
        var oldVisibility = new Dictionary<string, bool>(_columnVisibility);
        _columnVisibility.Clear();
        foreach (var (header, _, _, _) in _columnDefinitions)
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
                ("Selección", "", false, typeof(bool)),
                ("ID", "Id", false, typeof(int)),
                ("Nombre", "Nombre", false, typeof(string)),
                ("Email", "Email", false, typeof(string)),
                ("Saldo", "Saldo", false, typeof(decimal)),
                ("Activo", "Activo", true, typeof(bool))
            ]);
            return;
        }

        if (string.Equals(tableTitle, "Articulos", StringComparison.OrdinalIgnoreCase))
        {
            LoadData(BuildArticulosRows(),
            [
                ("Selección", "", false, typeof(bool)),
                ("ID", "Id", false, typeof(int)),
                ("Codigo", "Codigo", false, typeof(string)),
                ("Descripcion", "Descripcion", false, typeof(string)),
                ("Precio", "Precio", false, typeof(decimal)),
                ("Stock", "Stock", false, typeof(int)),
                ("Activo", "Activo", true, typeof(bool))
            ]);
            return;
        }

        LoadData(BuildGenericRows(tableTitle),
        [
            ("Selección", "", false, typeof(bool)),
            ("ID", "Id", false, typeof(int)),
            ("Tabla", "Tabla", false, typeof(string)),
            ("Descripcion", "Descripcion", false, typeof(string)),
            ("Valor", "Valor", false, typeof(decimal)),
            ("Activo", "Activo", true, typeof(bool))
        ]);
    }

    private void RebuildColumns()
    {
        Columns.Clear();

        foreach (var (header, propertyName, isCheckBox, dataType) in _columnDefinitions)
        {
            if (!_columnVisibility.GetValueOrDefault(header, true))
                continue;

            DataGridColumn col;
            if (header == "Selección")
                col = BuildSelectionColumn();
            else if (isCheckBox)
                col = BuildCheckBoxColumn(header, propertyName);
            else
                col = BuildTextColumn(header, propertyName, dataType);

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

        if (row is IDictionary<string, object?> dictionary &&
            dictionary.TryGetValue(propertyName, out var dictValue))
        {
            if (dictValue is bool boolValue)
            {
                value = boolValue;
                return true;
            }

            if (dictValue is not null && bool.TryParse(dictValue.ToString(), out var parsed))
            {
                value = parsed;
                return true;
            }

            return false;
        }

        var property = row.GetType().GetProperty(propertyName);
        if (property?.PropertyType != typeof(bool))
            return false;

        value = (bool)(property.GetValue(row) ?? false);
        return true;
    }

    private static DataGridColumn BuildTextColumn(string header, string propertyName, Type dataType)
    {
        var bindingPath = $"[{propertyName}]";
        var nonNullableType = Nullable.GetUnderlyingType(dataType) ?? dataType;
        var isDecimalType = nonNullableType == typeof(decimal) || nonNullableType == typeof(double) || nonNullableType == typeof(float);
        var isIntegerType = nonNullableType == typeof(byte) || nonNullableType == typeof(sbyte) ||
                            nonNullableType == typeof(short) || nonNullableType == typeof(ushort) ||
                            nonNullableType == typeof(int) || nonNullableType == typeof(uint) ||
                            nonNullableType == typeof(long) || nonNullableType == typeof(ulong);
        var isDateType = nonNullableType == typeof(DateTime);

        // Si es una columna numérica, usar DataGridTemplateColumn con alineación a la derecha
        if (isDecimalType || isIntegerType)
        {
            return new DataGridTemplateColumn
            {
                Header = header,
                CanUserSort = true,
                SortMemberPath = propertyName,
                CellTemplate = new FuncDataTemplate<object>((_, _) =>
                {
                    var textBlock = new TextBlock
                    {
                        HorizontalAlignment = isDecimalType ? HorizontalAlignment.Right : HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Avalonia.Thickness(5, 0, 5, 0)
                    };
                    var binding = new Avalonia.Data.Binding(bindingPath);
                    if (isDecimalType)
                        binding.StringFormat = "{0:N2}";

                    textBlock.Bind(TextBlock.TextProperty, binding);
                    return textBlock;
                })
            };
        }

        if (isDateType)
        {
            return new DataGridTextColumn
            {
                Header = header,
                IsReadOnly = true,
                CanUserSort = true,
                SortMemberPath = propertyName,
                Binding = new Avalonia.Data.Binding(bindingPath)
                {
                    StringFormat = "{0:dd/MM/yyyy}"
                }
            };
        }

        // Para columnas de texto, usar DataGridTextColumn estándar
        return new DataGridTextColumn
        {
            Header = header,
            IsReadOnly = true,
            CanUserSort = true,
            SortMemberPath = propertyName,
            Binding = new Avalonia.Data.Binding(bindingPath)
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
            Binding = new Avalonia.Data.Binding($"[{propertyName}]")
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
            new ArticuloRow { Id = 1, Codigo = "ART-001", Descripcion = "Teclado", Precio = 350.90m, Stock = 12, Activo = true },
            new ArticuloRow { Id = 2, Codigo = "ART-002", Descripcion = "Monitor", Precio = 189.00m, Stock = 7, Activo = true },
            new ArticuloRow { Id = 3, Codigo = "ART-003", Descripcion = "Raton", Precio = 189.50m, Stock = 25, Activo = true },
            new ArticuloRow { Id = 4, Codigo = "ART-004", Descripcion = "Impresora", Precio = 729.99m, Stock = 3, Activo = false }
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
            var (header, _, _, _) = _columnDefinitions[i];
            var isVisible = _columnVisibility.TryGetValue(header, out var visible) ? visible : true;
            configs.Add(new ColumnConfig(header, header, i, isVisible));
        }
        return configs;
    }

    public Type? GetColumnDataType(string propertyName)
    {
        foreach (var definition in _columnDefinitions)
        {
            if (string.Equals(definition.PropertyName, propertyName, StringComparison.Ordinal))
                return definition.DataType;
        }

        return null;
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
        var reorderedDefinitions = new List<(string Header, string PropertyName, bool IsCheckBox, Type DataType)>();
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
