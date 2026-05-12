using Avalonia.Controls;
using Avalonia.Layout;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Predator;

public interface IWorkspaceTabDefinition
{
    bool Matches(string title);
    TabItem Create(string title);
}

public sealed class WorkspaceTabHeader
{
    public WorkspaceTabHeader(string title, bool isCloseButtonEnabled = true)
    {
        Title = title;
        IsCloseButtonEnabled = isCloseButtonEnabled;
    }

    public string Title { get; }

    public bool IsCloseButtonEnabled { get; }

    public override string ToString() => Title;
}

public sealed class ListaTabDefinition : IWorkspaceTabDefinition
{
    public bool Matches(string title) =>
        !title.Equals("Login", StringComparison.OrdinalIgnoreCase) &&
        !title.StartsWith("Tablas", StringComparison.OrdinalIgnoreCase) &&
        !title.StartsWith("Propiedades", StringComparison.OrdinalIgnoreCase);

    public TabItem Create(string title)
    {
        var editorPage = WorkspaceTabFactory.CreateEditorPage(title);
        return WorkspaceTabFactory.CreateTabItem(title, "edicion-tab", editorPage);
    }
}

public sealed class EditorTabDefinition : IWorkspaceTabDefinition
{
    public bool Matches(string title) =>
        title.StartsWith("Tablas", StringComparison.OrdinalIgnoreCase);

    public TabItem Create(string title)
    {
        var grid = WorkspaceTabFactory.CreateGenericGrid(g =>
        {
            g.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
            g.RowBackground = Avalonia.Media.Brushes.White;
        });

        return WorkspaceTabFactory.CreateTabItem(title, "tablas-tab", grid);
    }
}

public sealed class PropiedadesTabDefinition : IWorkspaceTabDefinition
{
    public bool Matches(string title) =>
        title.StartsWith("Propiedades", StringComparison.OrdinalIgnoreCase);

    public TabItem Create(string title)
    {
        return WorkspaceTabFactory.CreateTabItem(title, "propiedades-tab");
    }
}

public sealed class LoginTabDefinition : IWorkspaceTabDefinition
{
    public bool Matches(string title) =>
        title.Equals("Login", StringComparison.OrdinalIgnoreCase);

    public TabItem Create(string title)
    {
        var loginPage = new LoginPage();
        return WorkspaceTabFactory.CreateTabItem(title, "login-tab", loginPage, isCloseButtonEnabled: false);
    }
}

public static class WorkspaceTabFactory
{
    private static readonly IReadOnlyList<IWorkspaceTabDefinition> Definitions =
    [
        new ListaTabDefinition(),
        new EditorTabDefinition(),
        new PropiedadesTabDefinition(),
        new LoginTabDefinition()
    ];

    public static bool TryCreate(string title, out TabItem? tab)
    {
        foreach (var definition in Definitions)
        {
            if (definition.Matches(title))
            {
                tab = definition.Create(title);
                return true;
            }
        }

        tab = null;
        return false;
    }

    internal static Control CreateGenericGrid(
        Action<DataGridPredator>? configure = null)
    {
        var grid = new DataGridPredator();
        configure?.Invoke(grid);

        grid.LoadData("General");
        return grid;
    }

    internal static Control CreateEditorPage(string title)
    {
        DataGridPredator? editionGrid = null;

        var menu = new Menu
        {
            Padding = new Avalonia.Thickness(0),
            Margin = new Avalonia.Thickness(0),
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.Gainsboro),
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(51, 51, 51))
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

        var grid = CreateTitleGrid(title, g => editionGrid = g);
        var gridWithTotals = CreateGridWithTotals(grid, title);

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

            var nextEditIndex = tabItems
                .OfType<TabItem>()
                .Select(t => t.Header)
                .OfType<WorkspaceTabHeader>()
                .Count(h => h.Title.StartsWith($"{title} - Edicion", StringComparison.OrdinalIgnoreCase)) + 1;

            var dynamicEditContent = BuildDynamicEditContent(selectedRow, title);

            var editTab = CreateTabItem($"{title} - Edicion {nextEditIndex}", "edicion-tab", dynamicEditContent);
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
        dockPanel.Children.Add(gridWithTotals);

        return new Border
        {
            Padding = new Avalonia.Thickness(0),
            Child = dockPanel
        };
    }

    private static MenuItem CreateEditorMenuItem(string title)
    {
        var darkGrayBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(51, 51, 51));
        var whitesmokeBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.WhiteSmoke);
        
        var textBlock = new TextBlock 
        { 
            Text = title,
            Foreground = darkGrayBrush,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(4, 0)
        };
        
        var menuItem = new MenuItem 
        { 
            Header = textBlock
        };
        
        // Agregar efecto de hover con whitesmoke
        menuItem.PointerEntered += (_, _) =>
        {
            menuItem.Background = whitesmokeBrush;
        };
        
        menuItem.PointerExited += (_, _) =>
        {
            menuItem.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.Transparent);
        };
        
        return menuItem;
    }

    private static DataGridPredator CreateTitleGrid(string title, Action<DataGridPredator>? configure = null)
    {
        var grid = new DataGridPredator();
        configure?.Invoke(grid);

        grid.LoadData(title);
        return grid;
    }

    private static Control CreateGridWithTotals(DataGridPredator grid, string title)
    {
        var totalsGrid = new Grid
        {
            ColumnSpacing = 0,
            Height = 30,
            Background = Avalonia.Media.Brushes.WhiteSmoke
        };
        var footerCells = new List<Label>();

        void SyncTotalsWithColumns()
        {
            var mergeFirstTwoColumns = grid.Columns.Count >= 2;
            var expectedFooterCells = mergeFirstTwoColumns ? grid.Columns.Count - 1 : grid.Columns.Count;
            var needsRebuild = totalsGrid.ColumnDefinitions.Count != grid.Columns.Count ||
                               totalsGrid.Children.Count != expectedFooterCells;

            if (needsRebuild)
            {
                totalsGrid.ColumnDefinitions.Clear();
                totalsGrid.Children.Clear();
                footerCells.Clear();

                for (var i = 0; i < grid.Columns.Count; i++)
                {
                    totalsGrid.ColumnDefinitions.Add(new ColumnDefinition(0, GridUnitType.Pixel));

                    if (mergeFirstTwoColumns && i == 1)
                        continue;

                    var footerCell = new Label
                    {
                        Content = string.Empty,
                        Tag = i,
                        FontWeight = Avalonia.Media.FontWeight.Bold,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Padding = new Avalonia.Thickness(6, 0),
                        BorderThickness = new Avalonia.Thickness(0.5),
                        BorderBrush = Avalonia.Media.Brushes.LightGray,
                        Background = Avalonia.Media.Brushes.WhiteSmoke
                    };

                    Grid.SetColumn(footerCell, i);
                    if (mergeFirstTwoColumns && i == 0)
                        Grid.SetColumnSpan(footerCell, 2);

                    totalsGrid.Children.Add(footerCell);
                    footerCells.Add(footerCell);
                }
            }

            for (var i = 0; i < grid.Columns.Count; i++)
            {
                var width = Math.Max(0, grid.Columns[i].ActualWidth);
                totalsGrid.ColumnDefinitions[i].Width = new GridLength(width, GridUnitType.Pixel);
            }

            foreach (var footerCell in footerCells)
            {
                if (footerCell.Tag is not int sourceColumnIndex)
                    continue;

                footerCell.Content = GetFooterText(grid, sourceColumnIndex);
                footerCell.HorizontalContentAlignment = GetFooterHorizontalAlignment(grid, sourceColumnIndex);
            }
        }

        grid.LayoutUpdated += (_, _) => SyncTotalsWithColumns();
        SyncTotalsWithColumns();

        var panel = new DockPanel();
        DockPanel.SetDock(totalsGrid, Dock.Bottom);
        panel.Children.Add(totalsGrid);
        panel.Children.Add(grid);
        return panel;
    }

    private static string GetFooterText(DataGridPredator grid, int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= grid.Columns.Count)
            return string.Empty;

        var column = grid.Columns[columnIndex];
        var rows = grid.ItemsSource?.Cast<object>().ToList() ?? [];

        if (rows.Count == 0)
        {
            if (columnIndex == 0)
                return "Totales";

            if (columnIndex == 2)
                return "0";

            return string.Empty;
        }

        // First two footer columns are always merged to show the Totales title.
        if (columnIndex == 0)
            return "Totales";

        if (column.Header is CheckBox)
            return string.Empty;

        // Third footer column always shows record count, regardless of table type.
        if (columnIndex == 2)
            return rows.Count.ToString(CultureInfo.InvariantCulture);

        if (string.IsNullOrWhiteSpace(column.SortMemberPath))
            return string.Empty;

        var property = rows[0].GetType().GetProperty(column.SortMemberPath);
        if (property is null)
            return string.Empty;

        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        // Sum only non-integer numeric columns with N2 format.
        if (propertyType == typeof(decimal) || propertyType == typeof(double) || propertyType == typeof(float))
        {
            var sum = rows.Sum(r => Convert.ToDecimal(property.GetValue(r) ?? 0m, CultureInfo.InvariantCulture));
            return sum.ToString("N2", CultureInfo.CurrentCulture);
        }

        return string.Empty;
    }

    private static HorizontalAlignment GetFooterHorizontalAlignment(DataGridPredator grid, int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= grid.Columns.Count)
            return HorizontalAlignment.Left;

        if (columnIndex == 2)
            return HorizontalAlignment.Center;

        var column = grid.Columns[columnIndex];
        var rows = grid.ItemsSource?.Cast<object>().ToList() ?? [];
        if (rows.Count == 0 || string.IsNullOrWhiteSpace(column.SortMemberPath))
            return columnIndex == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Center;

        var property = rows[0].GetType().GetProperty(column.SortMemberPath);
        if (property is null)
            return HorizontalAlignment.Left;

        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (propertyType == typeof(decimal) || propertyType == typeof(double) || propertyType == typeof(float))
            return HorizontalAlignment.Right;

        if (propertyType == typeof(byte) || propertyType == typeof(sbyte) ||
            propertyType == typeof(short) || propertyType == typeof(ushort) ||
            propertyType == typeof(int) || propertyType == typeof(uint) ||
            propertyType == typeof(long) || propertyType == typeof(ulong))
            return HorizontalAlignment.Center;

        return HorizontalAlignment.Left;
    }

    private static Control BuildDynamicEditContent(object row, string sourceTitle)
    {
        var actionMenu = new Menu
        {
            Padding = new Avalonia.Thickness(0),
            Margin = new Avalonia.Thickness(0),
            Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.Gainsboro),
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(51, 51, 51))
        };
        var grabarItem = CreateEditorMenuItem("Grabar");
        var aplicarItem = CreateEditorMenuItem("Aplicar");
        var cancelarItem = CreateEditorMenuItem("Cancelar");

        grabarItem.Click += (_, _) => CloseCurrentTabAndSelectPrevious(actionMenu);
        cancelarItem.Click += (_, _) => CloseCurrentTabAndSelectPrevious(actionMenu);

        actionMenu.Items.Add(grabarItem);
        actionMenu.Items.Add(aplicarItem);
        actionMenu.Items.Add(cancelarItem);

        var fieldsPanel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Avalonia.Thickness(16),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var properties = row.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToList();

        foreach (var property in properties)
            fieldsPanel.Children.Add(CreateFieldEditor(property, row));

        if (properties.Count == 0)
        {
            fieldsPanel.Children.Add(new TextBlock
            {
                Text = "No hay datos para editar.",
                Foreground = Avalonia.Media.Brushes.Gray
            });
        }

        var root = new DockPanel
        {
            Background = Avalonia.Media.Brushes.White
        };

        DockPanel.SetDock(actionMenu, Dock.Top);
        root.Children.Add(actionMenu);
        root.Children.Add(new ScrollViewer
        {
            Content = fieldsPanel,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        });

        return root;
    }

    private static Control CreateFieldEditor(PropertyInfo property, object row)
    {
        var value = property.GetValue(row);
        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        var fieldPanel = new StackPanel
        {
            Width = 300,
            Spacing = 6,
            Margin = new Avalonia.Thickness(0, 0, 16, 12)
        };

        var label = new TextBlock
        {
            Text = property.Name,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = Avalonia.Media.FontWeight.Medium
        };

        var editor = CreateEditorControl(propertyType, value);
        editor.Width = 300;
        editor.HorizontalAlignment = HorizontalAlignment.Left;

        fieldPanel.Children.Add(label);
        fieldPanel.Children.Add(editor);

        return fieldPanel;
    }

    private static Control CreateEditorControl(Type propertyType, object? value)
    {
        if (propertyType == typeof(bool))
        {
            return new CheckBox
            {
                IsChecked = value is bool booleanValue && booleanValue,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        if (propertyType == typeof(DateTime))
        {
            return new DatePicker
            {
                SelectedDate = value is DateTime date ? date.Date : null,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
        }

        if (propertyType.IsEnum)
        {
            return new ComboBox
            {
                ItemsSource = Enum.GetValues(propertyType).Cast<object>().ToList(),
                SelectedItem = value,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
        }

        return new TextBox
        {
            Text = FormatEditorValue(value, propertyType),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
    }

    private static string FormatEditorValue(object? value, Type propertyType)
    {
        if (value is null)
            return string.Empty;

        if (propertyType == typeof(decimal) || propertyType == typeof(double) || propertyType == typeof(float))
            return Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString(CultureInfo.CurrentCulture);

        if (propertyType == typeof(byte) || propertyType == typeof(sbyte) ||
            propertyType == typeof(short) || propertyType == typeof(ushort) ||
            propertyType == typeof(int) || propertyType == typeof(uint) ||
            propertyType == typeof(long) || propertyType == typeof(ulong))
            return Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;

        return value.ToString() ?? string.Empty;
    }

    private static void CloseCurrentTabAndSelectPrevious(Control sourceControl)
    {
        var topLevel = TopLevel.GetTopLevel(sourceControl);
        if (topLevel is not Window window)
            return;

        var workspaceTabs = window.FindControl<TabControl>("WorkspaceTabs");
        if (workspaceTabs?.Items is not System.Collections.IList tabItems)
            return;

        var currentTab = workspaceTabs.SelectedItem;
        if (currentTab is null)
            return;

        var currentIndex = tabItems.IndexOf(currentTab);
        if (currentIndex < 0)
            return;

        var previousIndex = currentIndex > 0 ? currentIndex - 1 : 0;
        tabItems.RemoveAt(currentIndex);

        if (tabItems.Count == 0)
            return;

        if (previousIndex >= tabItems.Count)
            previousIndex = tabItems.Count - 1;

        workspaceTabs.SelectedItem = tabItems[previousIndex];
    }

    internal static TabItem CreateTabItem(
        string title,
        string styleClass,
        object? content = null,
        bool isCloseButtonEnabled = true)
    {
        var tab = new TabItem
        {
            Header = new WorkspaceTabHeader(title, isCloseButtonEnabled),
            Content = content ?? title
        };

        tab.Classes.Add(styleClass);
        return tab;
    }
}
