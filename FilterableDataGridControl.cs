using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using CoreIA;

namespace Predator;

public sealed class FilterableDataGridControl : Border
{
    private sealed class StringFilterRule
    {
        public HashSet<string> EqualsValues { get; } = new(StringComparer.CurrentCultureIgnoreCase);
        public string ContainsValue { get; set; } = string.Empty;
    }

    private sealed class DateFilterRule
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    private readonly Sesion _sesion;
    private readonly Dictionary<string, StringFilterRule> _activeStringFilters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateFilterRule> _activeDateFilters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextFilterComboBox> _headerFilterCombos = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateRangeFilterControl> _headerDateFilterControls = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _unfilteredTextCatalog = new(StringComparer.OrdinalIgnoreCase);
    private Task? _catalogLoadTask;
    private CancellationTokenSource? _filterReloadCts;
    private List<object> _allRows = [];
    private string _lastFilterSummaryText = "Filtro: (sin filtros)";
    private Label? _filterSummaryLabel;

    public CoreIA.Entidades.Entidad? Entidad { get; set; }
    public bool AutoLoadOnLoaded { get; set; } = true;
    public DataGridPredator Grid { get; }

    public FilterableDataGridControl(Sesion sesion, CoreIA.Entidades.Entidad? entidad = null)
    {
        _sesion = sesion;
        Entidad = entidad;
        Padding = new Avalonia.Thickness(0);

        Grid = new DataGridPredator();
        Child = CreateGridWithFooter(Grid);

        Loaded += async (_, _) =>
        {
            if (AutoLoadOnLoaded)
                await LoadGridDataAsync();

            _ = EnsureUnfilteredTextCatalogLoadedAsync();
        };
    }

    public async Task ReloadAsync()
    {
        await LoadGridDataAsync(BuildSqlFilterFromActiveFilters());
    }

    public void LoadData(IList<object> data, List<(string Header, string PropertyName, bool IsCheckBox, Type DataType)> columns)
    {
        _allRows = data.ToList();
        Grid.LoadData(_allRows, columns);
    }

    private Control CreateGridWithFooter(DataGridPredator grid)
    {
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

        var filterSummaryLabel = new Label
        {
            Content = "Filtro: (sin filtros)",
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Avalonia.Thickness(6, 0),
            Background = Brushes.White
        };
        _filterSummaryLabel = filterSummaryLabel;

        var filterSummaryViewport = new Border
        {
            Height = 24,
            Background = Brushes.White,
            ClipToBounds = true,
            Child = filterSummaryLabel
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
                _headerFilterCombos.Clear();

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

                    Control headerCell;
                    
                    if (nonNullableType == typeof(string))
                    {
                        headerCell = new TextFilterComboBox
                        {
                            Background = Brushes.White,
                            BorderThickness = new Avalonia.Thickness(0.5),
                            BorderBrush = Brushes.LightGray,
                            Padding = new Avalonia.Thickness(3, 7, 0, 7),
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            VerticalAlignment = VerticalAlignment.Stretch,
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                            VerticalContentAlignment = VerticalAlignment.Center
                        };
                    }
                    else if (nonNullableType == typeof(DateTime))
                    {
                        headerCell = new DateRangeFilterControl();
                    }
                    else
                    {
                        headerCell = new Label
                        {
                            Content = string.Empty,
                            Background = Brushes.White,
                            BorderThickness = new Avalonia.Thickness(0.5),
                            BorderBrush = Brushes.LightGray,
                            Padding = new Avalonia.Thickness(6, 0),
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                            VerticalContentAlignment = VerticalAlignment.Center
                        };
                    }

                    if (headerCell is TextFilterComboBox comboBox && !string.IsNullOrWhiteSpace(sortMemberPath))
                    {
                        comboBox.ItemsSource = GetFilterComboValues(sortMemberPath).ToList();

                        var propertyName = sortMemberPath;
                        _headerFilterCombos[propertyName] = comboBox;
                        comboBox.ItemSelected += (_, args) => UpdateStringFilterSelections(propertyName, args.SelectedValues);
                        comboBox.TypedTextChanged += (_, args) => UpdateStringContainsFilter(propertyName, args.Text);
                    }
                    else if (headerCell is DateRangeFilterControl dateRangeControl && !string.IsNullOrWhiteSpace(sortMemberPath))
                    {
                        var propertyName = sortMemberPath;
                        _headerDateFilterControls[propertyName] = dateRangeControl;
                        dateRangeControl.DateRangeChanged += (_, args) => UpdateDateRangeFilter(propertyName, args.StartDate, args.EndDate);
                    }

                    Avalonia.Controls.Grid.SetColumn(headerCell, i);
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

                    Avalonia.Controls.Grid.SetColumn(label, i);
                    if (mergeFirstTwoColumns && i == 0)
                        Avalonia.Controls.Grid.SetColumnSpan(label, 2);

                    footerGrid.Children.Add(label);
                    footerLabels.Add(label);
                }
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

            // Synchronize footer widths to match header
            for (var i = 0; i < grid.Columns.Count && i < headerGrid.ColumnDefinitions.Count; i++)
            {
                if (i < footerGrid.ColumnDefinitions.Count)
                {
                    footerGrid.ColumnDefinitions[i].Width = headerGrid.ColumnDefinitions[i].Width;
                }
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
        DockPanel.SetDock(filterSummaryViewport, Dock.Bottom);
        DockPanel.SetDock(footerViewport, Dock.Bottom);
        panel.Children.Add(headerViewport);
        panel.Children.Add(filterSummaryViewport);
        panel.Children.Add(footerViewport);
        panel.Children.Add(grid);
        return panel;
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

    private IEnumerable<string> GetFilterComboValues(string propertyName)
    {
        if (_unfilteredTextCatalog.TryGetValue(propertyName, out var catalogValues))
            return catalogValues;

        return GetDistinctTextValues(Grid.ItemsSource, propertyName);
    }

    private async Task EnsureUnfilteredTextCatalogLoadedAsync()
    {
        if (_catalogLoadTask is not null)
        {
            await _catalogLoadTask;
            return;
        }

        _catalogLoadTask = LoadUnfilteredTextCatalogAsync();
        await _catalogLoadTask;
    }

    private async Task LoadUnfilteredTextCatalogAsync()
    {
        if (Entidad is null)
            return;

        try
        {
            var cmd = Datos.ComandoSql(null, Entidad, string.Empty, string.Empty, _sesion);
            var unfilteredData = await Datos.TablaListaAsync(cmd, _sesion.ConexionCrud);
            if (unfilteredData is null)
                return;

            var catalog = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Data.DataColumn column in unfilteredData.Columns)
            {
                if (column.DataType != typeof(string))
                    continue;

                var values = unfilteredData.Rows
                    .Cast<System.Data.DataRow>()
                    .Select(row => row[column.ColumnName]?.ToString())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!.Trim())
                    .Distinct(StringComparer.CurrentCultureIgnoreCase)
                    .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                catalog[column.ColumnName] = values;
            }

            _unfilteredTextCatalog.Clear();
            foreach (var pair in catalog)
                _unfilteredTextCatalog[pair.Key] = pair.Value;

            Avalonia.Threading.Dispatcher.UIThread.Post(UpdateHeaderComboItemsFromCatalog, Avalonia.Threading.DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading unfiltered filter catalog: {ex.Message}");
        }
    }

    private void UpdateHeaderComboItemsFromCatalog()
    {
        foreach (var (propertyName, combo) in _headerFilterCombos)
            combo.ItemsSource = GetFilterComboValues(propertyName).ToList();
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

    private void UpdateStringFilterSelections(string propertyName, IReadOnlyList<string> selectedValues)
    {
        var normalizedValues = selectedValues
            .Select(value => value?.Trim() ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (normalizedValues.Count == 0)
        {
            _activeStringFilters.Remove(propertyName);
        }
        else
        {
            if (!_activeStringFilters.TryGetValue(propertyName, out var rule))
            {
                rule = new StringFilterRule();
                _activeStringFilters[propertyName] = rule;
            }

            rule.EqualsValues.Clear();
            foreach (var value in normalizedValues)
                rule.EqualsValues.Add(value);

            rule.ContainsValue = string.Empty;
        }

        QueueFilterSummaryUpdate();
        QueueFilteredSqlReload();
    }

    private void UpdateStringContainsFilter(string propertyName, string? filterText)
    {
        var normalized = filterText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            _activeStringFilters.Remove(propertyName);
        }
        else
        {
            if (!_activeStringFilters.TryGetValue(propertyName, out var rule))
            {
                rule = new StringFilterRule();
                _activeStringFilters[propertyName] = rule;
            }

            rule.EqualsValues.Clear();
            rule.ContainsValue = normalized;
        }

        QueueFilterSummaryUpdate();
        QueueFilteredSqlReload();
    }

    private void UpdateDateRangeFilter(string propertyName, DateTime? startDate, DateTime? endDate)
    {
        if (!startDate.HasValue && !endDate.HasValue)
        {
            _activeDateFilters.Remove(propertyName);
        }
        else
        {
            if (!_activeDateFilters.TryGetValue(propertyName, out var rule))
            {
                rule = new DateFilterRule();
                _activeDateFilters[propertyName] = rule;
            }

            rule.StartDate = startDate;
            rule.EndDate = endDate;
        }

        QueueFilterSummaryUpdate();
        QueueFilteredSqlReload();
    }

    private void QueueFilteredSqlReload()
    {
        _filterReloadCts?.Cancel();
        _filterReloadCts?.Dispose();

        var cts = new CancellationTokenSource();
        _filterReloadCts = cts;

        _ = ReloadWithDelayAsync(cts.Token);
    }

    private async Task ReloadWithDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(250, cancellationToken);
            var filtroSql = BuildSqlFilterFromActiveFilters();
            await LoadGridDataAsync(filtroSql, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Ignore: a newer filter input superseded this request.
        }
    }

    private void QueueFilterSummaryUpdate()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(UpdateFilterSummaryLabel, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void UpdateFilterSummaryLabel()
    {
        if (_filterSummaryLabel is null)
            return;

        var summaryText = "Filtro: (sin filtros)";
        var allRules = new List<string>();

        if (_activeStringFilters.Count > 0)
        {
            var rules = _activeStringFilters
                .Select(pair =>
                {
                    var field = pair.Key;
                    var rule = pair.Value;

                    if (rule.EqualsValues.Count > 1)
                        return $"{field} en ({string.Join(", ", rule.EqualsValues.Select(v => $"'{v}'"))})";

                    if (rule.EqualsValues.Count == 1)
                        return $"{field}='{rule.EqualsValues.First()}'";

                    return $"{field} Contenga '{rule.ContainsValue}'";
                })
                .ToList();

            allRules.AddRange(rules);
        }

        if (_activeDateFilters.Count > 0)
        {
            var dateRules = _activeDateFilters
                .Select(pair =>
                {
                    var field = pair.Key;
                    var rule = pair.Value;

                    if (rule.StartDate.HasValue && rule.EndDate.HasValue)
                        return $"{field} entre {rule.StartDate.Value:dd/MM/yyyy} y {rule.EndDate.Value:dd/MM/yyyy}";

                    if (rule.StartDate.HasValue)
                        return $"{field} desde {rule.StartDate.Value:dd/MM/yyyy}";

                    if (rule.EndDate.HasValue)
                        return $"{field} hasta {rule.EndDate.Value:dd/MM/yyyy}";

                    return string.Empty;
                })
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();

            allRules.AddRange(dateRules);
        }

        if (allRules.Count > 0)
            summaryText = $"Filtro: {string.Join(" y ", allRules)}";

        if (string.Equals(_lastFilterSummaryText, summaryText, StringComparison.Ordinal))
            return;

        _lastFilterSummaryText = summaryText;
        _filterSummaryLabel.Content = summaryText;
    }

    private static string EscapeSqlLiteral(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private string BuildSqlFilterFromActiveFilters()
    {
        if (_activeStringFilters.Count == 0 && _activeDateFilters.Count == 0)
            return string.Empty;

        var clauses = new List<string>();
        
        foreach (var (propertyName, rule) in _activeStringFilters)
        {
            if (rule.EqualsValues.Count > 0)
            {
                if (rule.EqualsValues.Count == 1)
                {
                    var onlyValue = EscapeSqlLiteral(rule.EqualsValues.First());
                    clauses.Add($"{propertyName} = '{onlyValue}'");
                }
                else
                {
                    var inValues = string.Join(", ", rule.EqualsValues
                        .Select(value => $"'{EscapeSqlLiteral(value)}'"));
                    clauses.Add($"{propertyName} IN ({inValues})");
                }
            }

            if (!string.IsNullOrWhiteSpace(rule.ContainsValue))
            {
                var escaped = EscapeSqlLiteral(rule.ContainsValue);
                clauses.Add($"{propertyName} LIKE '%{escaped}%'");
            }
        }

        foreach (var (propertyName, rule) in _activeDateFilters)
        {
            if (rule.StartDate.HasValue)
            {
                var startDateStr = rule.StartDate.Value.ToString("yyyy-MM-dd HH:mm:ss");
                clauses.Add($"{propertyName} >= '{startDateStr}'");
            }

            if (rule.EndDate.HasValue)
            {
                var endDateStr = rule.EndDate.Value.ToString("yyyy-MM-dd HH:mm:ss");
                clauses.Add($"{propertyName} <= '{endDateStr}'");
            }
        }

        return string.Join(" AND ", clauses);
    }

    private async Task LoadGridDataAsync(string filtroSql = "", CancellationToken cancellationToken = default)
    {
        if (Entidad is null)
            return;

        try
        {
            var cmd = Datos.ComandoSql(null, Entidad, filtroSql, "", _sesion);
            var data = await Datos.TablaListaAsync(cmd, _sesion.ConexionCrud);

            if (cancellationToken.IsCancellationRequested)
                return;

            if (data is null)
                return;

            var dataList = new List<object>();
            foreach (System.Data.DataRow row in data.Rows)
                dataList.Add(ConvertDataRow(row));

            var columns = GetColumnsFromDataTable(data);
            LoadData(dataList, columns);

            _ = EnsureUnfilteredTextCatalogLoadedAsync();
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation caused by rapid typing/filter changes.
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
        var columns = new List<(string Header, string PropertyName, bool IsCheckBox, Type DataType)>
        {
            ("Selección", "", false, typeof(bool))
        };

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

            var isCheckBox = column.DataType == typeof(bool);
            columns.Add((column.ColumnName, column.ColumnName, isCheckBox, column.DataType));
        }

        return columns;
    }
}
