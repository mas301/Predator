using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using CoreIA;
using System.Collections.ObjectModel;

namespace Predator;

public sealed class ListaPage : Border
{
    public CoreIA.Entidades.Entidad? Entidad { get; set; }
    
    private readonly Sesion _sesion;
    private readonly string _title;
    private DataGridPredator? _grid;
    private DataGridPredator? _editionGrid;

    public ListaPage(string title, Sesion? sesion = null, CoreIA.Entidades.Entidad? entidad = null)
    {
        Entidad = entidad;
        _title = title;
        _sesion = sesion ?? new Sesion();
        Padding = new Avalonia.Thickness(0);

        DataGridPredator? editionGrid = null;

        var menu = new Menu
        {
            Padding = new Avalonia.Thickness(0),
            Margin = new Avalonia.Thickness(0),
            Background = new SolidColorBrush(Colors.Gainsboro),
            Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51))
        };

        menu.Items.Add(CreateEditorMenuItem("Nuevo"));
        var modificarItem = CreateEditorMenuItem("Modificar");
        modificarItem.IsVisible = false;
        menu.Items.Add(modificarItem);

        var consultarItem = CreateEditorMenuItem("Consultar");
        consultarItem.IsVisible = false;
        menu.Items.Add(consultarItem);

        var eliminarItem = CreateEditorMenuItem("Eliminar");
        eliminarItem.IsVisible = false;
        menu.Items.Add(eliminarItem);

        var anularItem = CreateEditorMenuItem("Anular");
        anularItem.IsVisible = false;
        menu.Items.Add(anularItem);

        var reactivarItem = CreateEditorMenuItem("Reactivar");
        reactivarItem.IsVisible = false;
        menu.Items.Add(reactivarItem);

        menu.Items.Add(CreateEditorMenuItem("Importar"));
        var exportarItem = CreateEditorMenuItem("Exportar");
        exportarItem.IsVisible = false;
        menu.Items.Add(exportarItem);

        var imprimirItem = CreateEditorMenuItem("Imprimir");
        imprimirItem.IsVisible = false;
        menu.Items.Add(imprimirItem);

        var diseñoItem = CreateEditorMenuItem("Diseño");
        diseñoItem.Click += async (_, _) =>
        {
            if (editionGrid is null)
                return;

            var configs = editionGrid.GetColumnConfigs();
            var designWindow = new ColumnDesignWindow(configs);
            var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(editionGrid);
            if (topLevel is Window window)
            {
                var result = await designWindow.ShowDialog<bool?>(window);
                if (result == true)
                {
                    editionGrid.ApplyColumnConfigs(designWindow.ColumnConfigs);
                }
            }
        };
        menu.Items.Add(diseñoItem);

        menu.Items.Add(CreateEditorMenuItem("Propiedades"));

        var grid = CreateTitleGrid(title, g => 
        {
            editionGrid = g;
            _editionGrid = g;
            _grid = g;
        });
        var gridWithFooter = CreateGridWithFooter(grid);

        modificarItem.Click += (_, _) =>
        {
            if (editionGrid is null || editionGrid.SelectedItems.Count != 1)
                return;

            var selectedRow = editionGrid.SelectedItems.Cast<object>().FirstOrDefault();
            if (selectedRow is null)
                return;

            var topLevel = TopLevel.GetTopLevel(editionGrid);
            if (topLevel is not Window window)
                return;

            var workspaceTabs = window.FindControl<TabControl>("WorkspaceTabs");
            if (workspaceTabs?.Items is not System.Collections.IList tabItems)
                return;

            var singularTitle = ResolveEntidadNombreSingular(Entidad, title);

            var editPage = new EdicionPage(selectedRow, singularTitle, Entidad);
            var editTab = WorkspaceTabFactory.CreateTabItem(singularTitle, "edicion-tab", editPage);
            tabItems.Add(editTab);
            workspaceTabs.SelectedItem = editTab;
        };

        void RefreshEditionMenuState()
        {
            var selectedRows = editionGrid?.SelectedItems.Cast<object>().ToList() ?? [];
            var selectedCount = selectedRows.Count;

            static bool TryGetBooleanProperty(object row, string propertyName, out bool value)
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

            static bool IsInactiveOrCancelled(object row)
            {
                var hasAnulada = TryGetBooleanProperty(row, "Anulada", out var anulada);
                if (hasAnulada && anulada)
                    return true;

                var hasActivo = TryGetBooleanProperty(row, "Activo", out var activo);
                return hasActivo && !activo;
            }

            var canModify = selectedCount == 1 && !IsInactiveOrCancelled(selectedRows[0]);
            var canConsult = selectedCount == 1;
            var hasRows = editionGrid?.TotalRows >= 1;
            var allSelectedNotCancelled = selectedCount > 0 && selectedRows.All(row => !IsInactiveOrCancelled(row));
            var allSelectedCancelled = selectedCount > 0 && selectedRows.All(IsInactiveOrCancelled);

            modificarItem.IsVisible = canModify;
            modificarItem.IsEnabled = canModify;
            modificarItem.IsHitTestVisible = canModify;

            consultarItem.IsVisible = canConsult;
            consultarItem.IsEnabled = canConsult;
            consultarItem.IsHitTestVisible = canConsult;

            eliminarItem.IsVisible = allSelectedCancelled;
            eliminarItem.IsEnabled = allSelectedCancelled;
            eliminarItem.IsHitTestVisible = allSelectedCancelled;

            anularItem.IsVisible = allSelectedNotCancelled;
            anularItem.IsEnabled = allSelectedNotCancelled;
            anularItem.IsHitTestVisible = allSelectedNotCancelled;

            reactivarItem.IsVisible = allSelectedCancelled;
            reactivarItem.IsEnabled = allSelectedCancelled;
            reactivarItem.IsHitTestVisible = allSelectedCancelled;

            exportarItem.IsVisible = hasRows;
            exportarItem.IsEnabled = hasRows;
            exportarItem.IsHitTestVisible = hasRows;

            imprimirItem.IsVisible = hasRows;
            imprimirItem.IsEnabled = hasRows;
            imprimirItem.IsHitTestVisible = hasRows;
        }

        if (editionGrid is not null)
        {
            editionGrid.PointerReleased += (_, _) => RefreshEditionMenuState();
            editionGrid.SelectionChanged += (_, _) => RefreshEditionMenuState();
        }

        RefreshEditionMenuState();

        var dockPanel = new DockPanel
        {
            Margin = new Avalonia.Thickness(0)
        };
        DockPanel.SetDock(menu, Dock.Top);
        dockPanel.Children.Add(menu);
        dockPanel.Children.Add(gridWithFooter);

        Child = dockPanel;
        
        // Load data asynchronously when the control is loaded
        Loaded += async (_, _) => await LoadGridDataAsync();
    }

    private static MenuItem CreateEditorMenuItem(string title)
    {
        var darkGrayBrush = new SolidColorBrush(Color.FromRgb(51, 51, 51));
        var whitesmokeBrush = new SolidColorBrush(Colors.WhiteSmoke);

        var textBlock = new TextBlock
        {
            Text = title,
            Foreground = darkGrayBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(4, 0)
        };

        var menuItem = new MenuItem
        {
            Header = textBlock
        };

        menuItem.PointerEntered += (_, _) =>
        {
            menuItem.Background = whitesmokeBrush;
        };

        menuItem.PointerExited += (_, _) =>
        {
            menuItem.Background = new SolidColorBrush(Colors.Transparent);
        };

        return menuItem;
    }

    private static DataGridPredator CreateTitleGrid(string title, Action<DataGridPredator>? configure = null)
    {
        var grid = new DataGridPredator();
        configure?.Invoke(grid);

        return grid;
    }

    private static Control CreateGridWithFooter(DataGridPredator grid)
    {
        // Header Grid Setup
        var headerGrid = new Grid
        {
            ColumnSpacing = 0,
            Height = 30,
            Background = Brushes.White
        };

        var headerViewport = new Border
        {
            Height = 30,
            Background = Brushes.White,
            ClipToBounds = true,
            Child = headerGrid
        };

        var headerCells = new List<Control>();
        var headerTranslate = new TranslateTransform();
        headerGrid.RenderTransform = headerTranslate;

        // Footer Grid Setup
        var footerGrid = new Grid
        {
            ColumnSpacing = 0,
            Height = 30,
            Background = Brushes.WhiteSmoke
        };

        var footerViewport = new Border
        {
            Height = 30,
            Background = Brushes.WhiteSmoke,
            ClipToBounds = true,
            Child = footerGrid
        };

        var footerLabels = new List<Label>();
        var footerTranslate = new TranslateTransform();
        footerGrid.RenderTransform = footerTranslate;

        ScrollViewer? wiredScrollViewer = null;
        EventHandler<Avalonia.AvaloniaPropertyChangedEventArgs>? scrollViewerPropertyChangedHandler = null;
        Avalonia.Controls.Primitives.ScrollBar? wiredHorizontalScrollBar = null;
        EventHandler<Avalonia.AvaloniaPropertyChangedEventArgs>? horizontalScrollBarPropertyChangedHandler = null;

        void SyncHeader()
        {
            var expectedColumns = grid.Columns.Count;
            var needsRebuild = headerGrid.ColumnDefinitions.Count != expectedColumns ||
                               headerCells.Count != expectedColumns;

            if (needsRebuild)
            {
                headerGrid.ColumnDefinitions.Clear();
                headerGrid.Children.Clear();
                headerCells.Clear();

                for (var i = 0; i < grid.Columns.Count; i++)
                {
                    headerGrid.ColumnDefinitions.Add(new ColumnDefinition(0, GridUnitType.Pixel));

                    var sortMemberPath = grid.Columns[i].SortMemberPath;
                    var columnDataType = !string.IsNullOrWhiteSpace(sortMemberPath)
                        ? grid.GetColumnDataType(sortMemberPath)
                        : null;
                    var nonNullableType = columnDataType is null
                        ? null
                        : Nullable.GetUnderlyingType(columnDataType) ?? columnDataType;

                    Control headerCell = nonNullableType == typeof(string)
                        ? new TextFilterComboBox
                        {
                            Background = Brushes.White,
                            BorderThickness = new Avalonia.Thickness(0.5),
                            BorderBrush = Brushes.LightGray,
                            Padding = new Avalonia.Thickness(4, 7),
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            VerticalAlignment = VerticalAlignment.Stretch,
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                            VerticalContentAlignment = VerticalAlignment.Center
                        }
                        : new Label
                        {
                            Content = string.Empty,
                            Background = Brushes.White,
                            BorderThickness = new Avalonia.Thickness(0.5),
                            BorderBrush = Brushes.LightGray,
                            Padding = new Avalonia.Thickness(6, 0),
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                            VerticalContentAlignment = VerticalAlignment.Center
                        };

                    if (headerCell is TextFilterComboBox comboBox && !string.IsNullOrWhiteSpace(sortMemberPath))
                        comboBox.ItemsSource = GetDistinctTextValues(grid.ItemsSource, sortMemberPath).ToList();

                    Grid.SetColumn(headerCell, i);
                    headerGrid.Children.Add(headerCell);
                    headerCells.Add(headerCell);
                }
            }

            for (var i = 0; i < grid.Columns.Count; i++)
            {
                var width = Math.Max(0, grid.Columns[i].ActualWidth);
                var currentWidth = headerGrid.ColumnDefinitions[i].Width;
                if (Math.Abs(currentWidth.Value - width) > 0.5 || currentWidth.GridUnitType != GridUnitType.Pixel)
                    headerGrid.ColumnDefinitions[i].Width = new GridLength(width, GridUnitType.Pixel);
            }
        }

        void SyncFooter()
        {
            var mergeFirstTwoColumns = grid.Columns.Count >= 2;
            var expectedColumns = grid.Columns.Count;
            var expectedCells = mergeFirstTwoColumns ? grid.Columns.Count - 1 : grid.Columns.Count;
            var needsRebuild = footerGrid.ColumnDefinitions.Count != expectedColumns ||
                               footerLabels.Count != expectedCells;

            if (needsRebuild)
            {
                footerGrid.ColumnDefinitions.Clear();
                footerGrid.Children.Clear();
                footerLabels.Clear();

                for (var i = 0; i < grid.Columns.Count; i++)
                {
                    footerGrid.ColumnDefinitions.Add(new ColumnDefinition(0, GridUnitType.Pixel));

                    if (mergeFirstTwoColumns && i == 1)
                        continue;

                    var label = new Label
                    {
                        Content = string.Empty,
                        Tag = i,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                        Padding = new Avalonia.Thickness(6, 0),
                        BorderThickness = new Avalonia.Thickness(0.5),
                        BorderBrush = Brushes.LightGray,
                        Background = Brushes.WhiteSmoke
                    };

                    Grid.SetColumn(label, i);
                    if (mergeFirstTwoColumns && i == 0)
                        Grid.SetColumnSpan(label, 2);

                    footerGrid.Children.Add(label);
                    footerLabels.Add(label);
                }
            }

            for (var i = 0; i < grid.Columns.Count; i++)
            {
                var width = Math.Max(0, grid.Columns[i].ActualWidth);
                var currentWidth = footerGrid.ColumnDefinitions[i].Width;
                if (Math.Abs(currentWidth.Value - width) > 0.5 || currentWidth.GridUnitType != GridUnitType.Pixel)
                    footerGrid.ColumnDefinitions[i].Width = new GridLength(width, GridUnitType.Pixel);
            }

            var totalRows = 0;
            if (grid.ItemsSource is System.Collections.IEnumerable rows)
            {
                foreach (var _ in rows)
                    totalRows++;
            }

            foreach (var label in footerLabels)
            {
                if (label.Tag is not int sourceColumnIndex)
                    continue;

                object content = string.Empty;
                var alignment = HorizontalAlignment.Left;

                if (sourceColumnIndex == 0)
                {
                    content = "Totales";
                    alignment = HorizontalAlignment.Left;
                }
                else if (sourceColumnIndex == 2)
                {
                    content = totalRows.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    alignment = HorizontalAlignment.Center;
                }
                else if (sourceColumnIndex >= 3 && sourceColumnIndex < grid.Columns.Count)
                {
                    var sortMemberPath = grid.Columns[sourceColumnIndex].SortMemberPath;
                    if (!string.IsNullOrWhiteSpace(sortMemberPath) &&
                        TrySumDecimalColumn(grid.ItemsSource, sortMemberPath, out var sum))
                    {
                        content = sum.ToString("N2", System.Globalization.CultureInfo.CurrentCulture);
                        alignment = HorizontalAlignment.Right;
                    }
                }

                if (!Equals(label.Content, content))
                    label.Content = content;

                if (label.HorizontalContentAlignment != alignment)
                    label.HorizontalContentAlignment = alignment;
            }
        }

        void TryWireHorizontalSync()
        {
            if (wiredScrollViewer is null)
            {
                var gridScrollViewer = grid
                    .GetVisualDescendants()
                    .OfType<ScrollViewer>()
                    .FirstOrDefault();

                if (gridScrollViewer is not null)
                {
                    headerTranslate.X = -gridScrollViewer.Offset.X;
                    footerTranslate.X = -gridScrollViewer.Offset.X;

                    scrollViewerPropertyChangedHandler = (_, args) =>
                    {
                        if (args.Property != ScrollViewer.OffsetProperty)
                            return;

                        if (args.NewValue is not Avalonia.Vector offset)
                            return;

                        headerTranslate.X = -offset.X;
                        footerTranslate.X = -offset.X;
                    };

                    gridScrollViewer.PropertyChanged += scrollViewerPropertyChangedHandler;
                    wiredScrollViewer = gridScrollViewer;
                }
            }

            if (wiredHorizontalScrollBar is null)
            {
                var horizontalScrollBar = grid
                    .GetVisualDescendants()
                    .OfType<Avalonia.Controls.Primitives.ScrollBar>()
                    .FirstOrDefault(sb => sb.Orientation == Orientation.Horizontal);

                if (horizontalScrollBar is not null)
                {
                    headerTranslate.X = -horizontalScrollBar.Value;
                    footerTranslate.X = -horizontalScrollBar.Value;

                    horizontalScrollBarPropertyChangedHandler = (_, args) =>
                    {
                        if (args.Property != Avalonia.Controls.Primitives.RangeBase.ValueProperty)
                            return;

                        if (args.NewValue is not double value)
                            return;

                        headerTranslate.X = -value;
                        footerTranslate.X = -value;
                    };

                    horizontalScrollBar.PropertyChanged += horizontalScrollBarPropertyChangedHandler;
                    wiredHorizontalScrollBar = horizontalScrollBar;
                }
            }
        }

        grid.LayoutUpdated += (_, _) =>
        {
            SyncHeader();
            SyncFooter();
            TryWireHorizontalSync();
        };

        grid.DetachedFromVisualTree += (_, _) =>
        {
            if (wiredScrollViewer is not null && scrollViewerPropertyChangedHandler is not null)
                wiredScrollViewer.PropertyChanged -= scrollViewerPropertyChangedHandler;

            if (wiredHorizontalScrollBar is not null && horizontalScrollBarPropertyChangedHandler is not null)
                wiredHorizontalScrollBar.PropertyChanged -= horizontalScrollBarPropertyChangedHandler;

            wiredScrollViewer = null;
            scrollViewerPropertyChangedHandler = null;
            wiredHorizontalScrollBar = null;
            horizontalScrollBarPropertyChangedHandler = null;
        };

        SyncHeader();
        SyncFooter();

        var panel = new DockPanel();
        DockPanel.SetDock(headerViewport, Dock.Top);
        DockPanel.SetDock(footerViewport, Dock.Bottom);
        panel.Children.Add(headerViewport);
        panel.Children.Add(footerViewport);
        panel.Children.Add(grid);
        return panel;
    }

    private static string ResolveEntidadNombreSingular(CoreIA.Entidades.Entidad? entidad, string fallback)
    {
        if (entidad is null)
            return fallback;

        static string? Normalize(object? value)
        {
            var text = value?.ToString()?.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        static string? TryResolveFromObject(object source)
        {
            var candidateNames = new[] { "NombreSingular", "Nombre Singular", "Nombre_Singular", "nombreSingular", "nombre_singular" };
            var type = source.GetType();

            foreach (var name in candidateNames)
            {
                var property = type.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                var propertyValue = Normalize(property?.GetValue(source));
                if (!string.IsNullOrWhiteSpace(propertyValue))
                    return propertyValue;

                var field = type.GetField(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                var fieldValue = Normalize(field?.GetValue(source));
                if (!string.IsNullOrWhiteSpace(fieldValue))
                    return fieldValue;
            }

            return null;
        }

        var resolved = TryResolveFromObject(entidad);
        if (!string.IsNullOrWhiteSpace(resolved))
            return resolved;

        if (entidad is System.Collections.IDictionary dictionary)
        {
            foreach (var key in dictionary.Keys)
            {
                if (key is not object keyObject)
                    continue;

                var keyText = keyObject.ToString();
                if (string.IsNullOrWhiteSpace(keyText))
                    continue;

                var normalizedKey = keyText.Replace("_", string.Empty).Replace(" ", string.Empty);
                if (!normalizedKey.Equals("NombreSingular", StringComparison.OrdinalIgnoreCase))
                    continue;

                var dictValue = Normalize(dictionary[keyObject]);
                if (!string.IsNullOrWhiteSpace(dictValue))
                    return dictValue;
            }
        }

        return fallback;
    }

    private static bool TrySumDecimalColumn(System.Collections.IEnumerable? itemsSource, string propertyName, out decimal sum)
    {
        sum = 0m;
        if (itemsSource is null)
            return false;

        var foundDecimalValue = false;
        foreach (var item in itemsSource)
        {
            var value = GetRowValue(item, propertyName);
            if (!TryConvertDecimalLike(value, out var decimalValue))
                continue;

            sum += decimalValue;
            foundDecimalValue = true;
        }

        return foundDecimalValue;
    }

    private static object? GetRowValue(object? row, string propertyName)
    {
        if (row is null)
            return null;

        if (row is IDictionary<string, object?> dictionary &&
            dictionary.TryGetValue(propertyName, out var dictionaryValue))
        {
            return dictionaryValue;
        }

        var property = row.GetType().GetProperty(propertyName);
        return property?.GetValue(row);
    }

    private static IEnumerable<string> GetDistinctTextValues(System.Collections.IEnumerable? itemsSource, string propertyName)
    {
        if (itemsSource is null)
            return Enumerable.Empty<string>();

        return itemsSource
            .Cast<object>()
            .Select(item => GetRowValue(item, propertyName)?.ToString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static bool TryConvertDecimalLike(object? value, out decimal decimalValue)
    {
        decimalValue = 0m;
        if (value is decimal decimalNumber)
        {
            decimalValue = decimalNumber;
            return true;
        }

        if (value is double doubleNumber)
        {
            decimalValue = Convert.ToDecimal(doubleNumber, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        if (value is float floatNumber)
        {
            decimalValue = Convert.ToDecimal(floatNumber, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        return false;
    }

    private async System.Threading.Tasks.Task LoadGridDataAsync()
    {
        if (_grid is null || Entidad is null)
            return;

        try
        {
            // Build SQL command: Datos.ComandoSql(null, Entidad, "", "", SesionActual)
            var cmd = Datos.ComandoSql(null, Entidad, "", "", _sesion);
            
            // Load data: Data = await Datos.TablaListaAsync(cmd, SesionActual.ConexionCrud)
            var data = await Datos.TablaListaAsync(cmd, _sesion.ConexionCrud);
            
            // Load the data into the grid with columns mapping
            if (data != null)
            {
                var dataList = new List<object>();
                
                // Convert DataRow objects to dynamic objects that expose columns as properties
                foreach (System.Data.DataRow row in data.Rows)
                {
                    dataList.Add(ConvertDataRow(row));
                }
                
                var columns = GetColumnsFromDataTable(data);
                _grid.LoadData(dataList, columns);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading grid data: {ex.Message}");
        }
    }

    private static Dictionary<string, object?> ConvertDataRow(System.Data.DataRow row)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Data.DataColumn column in row.Table.Columns)
        {
            var rawValue = row[column.ColumnName];
            values[column.ColumnName] = NormalizeValue(rawValue, column.DataType);
        }

        return values;
    }

    private static object? NormalizeValue(object rawValue, Type targetType)
    {
        if (rawValue == DBNull.Value)
            return null;

        var nonNullableType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        try
        {
            if (nonNullableType == typeof(decimal))
                return Convert.ToDecimal(rawValue, System.Globalization.CultureInfo.InvariantCulture);

            if (nonNullableType == typeof(double))
                return Convert.ToDouble(rawValue, System.Globalization.CultureInfo.InvariantCulture);

            if (nonNullableType == typeof(float))
                return Convert.ToSingle(rawValue, System.Globalization.CultureInfo.InvariantCulture);

            if (nonNullableType == typeof(byte))
                return Convert.ToByte(rawValue, System.Globalization.CultureInfo.InvariantCulture);

            if (nonNullableType == typeof(sbyte))
                return Convert.ToSByte(rawValue, System.Globalization.CultureInfo.InvariantCulture);

            if (nonNullableType == typeof(short))
                return Convert.ToInt16(rawValue, System.Globalization.CultureInfo.InvariantCulture);

            if (nonNullableType == typeof(ushort))
                return Convert.ToUInt16(rawValue, System.Globalization.CultureInfo.InvariantCulture);

            if (nonNullableType == typeof(int))
                return Convert.ToInt32(rawValue, System.Globalization.CultureInfo.InvariantCulture);

            if (nonNullableType == typeof(uint))
                return Convert.ToUInt32(rawValue, System.Globalization.CultureInfo.InvariantCulture);

            if (nonNullableType == typeof(long))
                return Convert.ToInt64(rawValue, System.Globalization.CultureInfo.InvariantCulture);

            if (nonNullableType == typeof(ulong))
                return Convert.ToUInt64(rawValue, System.Globalization.CultureInfo.InvariantCulture);

            if (nonNullableType == typeof(bool))
                return Convert.ToBoolean(rawValue, System.Globalization.CultureInfo.InvariantCulture);

            if (nonNullableType == typeof(DateTime))
                return Convert.ToDateTime(rawValue, System.Globalization.CultureInfo.InvariantCulture);

            return rawValue;
        }
        catch
        {
            return rawValue;
        }
    }


    private static List<(string Header, string PropertyName, bool IsCheckBox, Type DataType)> GetColumnsFromDataTable(System.Data.DataTable dataTable)
    {
        var columns = new List<(string Header, string PropertyName, bool IsCheckBox, Type DataType)>();
        
        // Add checkbox column for selection
        columns.Add(("Selección", "", false, typeof(bool)));
        
        // Add columns for each data column
        foreach (System.Data.DataColumn column in dataTable.Columns)
        {
            var isIntegerType = column.DataType == typeof(byte) ||
                                column.DataType == typeof(sbyte) ||
                                column.DataType == typeof(short) ||
                                column.DataType == typeof(ushort) ||
                                column.DataType == typeof(int) ||
                                column.DataType == typeof(uint) ||
                                column.DataType == typeof(long) ||
                                column.DataType == typeof(ulong);

            var endsWithId = column.ColumnName.EndsWith("Id", StringComparison.OrdinalIgnoreCase);
            if (isIntegerType && endsWithId)
                continue;

            // Determine if this column is a boolean (checkbox)
            var isCheckBox = column.DataType == typeof(bool);
            columns.Add((column.ColumnName, column.ColumnName, isCheckBox, column.DataType));
        }
        
        return columns;
    }

}
