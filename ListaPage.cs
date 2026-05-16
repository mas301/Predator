using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using CoreIA;

namespace Predator;

public sealed class ListaPage : Border
{
    private CoreIA.Entidades.Entidad? _entidad;

    public CoreIA.Entidades.Entidad? Entidad
    {
        get => _entidad;
        set
        {
            _entidad = value;
            if (_listControl is not null)
                _listControl.Entidad = value;
        }
    }

    private readonly Sesion _sesion;
    private readonly string _title;
    private readonly FilterableDataGridControl _listControl;

    public ListaPage(string title, Sesion? sesion = null, CoreIA.Entidades.Entidad? entidad = null)
    {
        Entidad = entidad;
        _title = title;
        _sesion = sesion ?? new Sesion();
        Padding = new Avalonia.Thickness(0);

        _listControl = new FilterableDataGridControl(_sesion, Entidad);
        var editionGrid = _listControl.Grid;

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

        modificarItem.Click += (_, _) =>
        {
            if (editionGrid.SelectedItems.Count != 1)
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

        editionGrid.PointerReleased += (_, _) => RefreshEditionMenuState();
        editionGrid.SelectionChanged += (_, _) => RefreshEditionMenuState();

        RefreshEditionMenuState();

        var dockPanel = new DockPanel
        {
            Margin = new Avalonia.Thickness(0)
        };
        DockPanel.SetDock(menu, Dock.Top);
        dockPanel.Children.Add(menu);
        dockPanel.Children.Add(_listControl);

        Child = dockPanel;
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

}
