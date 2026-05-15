
using System;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Predator;

public sealed class EdicionPage : Border
{
    public object? RegistroSeleccionado { get; init; }
    public string? TituloOrigen { get; init; }
    public CoreIA.Entidades.Entidad? Entidad { get; init; }

    public EdicionPage(object row, string sourceTitle, CoreIA.Entidades.Entidad? entidad = null)
    {
        RegistroSeleccionado = row;
        TituloOrigen = sourceTitle;
        Entidad = entidad;
        Padding = new Avalonia.Thickness(0);

        var actionMenu = new Menu
        {
            Padding = new Avalonia.Thickness(0),
            Margin = new Avalonia.Thickness(0),
            Background = new SolidColorBrush(Colors.Gainsboro),
            Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51))
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

        if (row is System.Collections.Generic.IDictionary<string, object?> dictionary)
        {
            foreach (var pair in dictionary)
                fieldsPanel.Children.Add(CreateDynamicFieldEditor(pair.Key, pair.Value));
        }
        else if (properties.Count > 0)
        {
            // Handle regular properties
            foreach (var property in properties)
                fieldsPanel.Children.Add(CreateFieldEditor(property, row));
        }

        if (properties.Count == 0 && row is not System.Collections.Generic.IDictionary<string, object?>)
        {
            fieldsPanel.Children.Add(new TextBlock
            {
                Text = "No hay datos para editar.",
                Foreground = Brushes.Gray
            });
        }

        var root = new DockPanel
        {
            Background = Brushes.White
        };

        DockPanel.SetDock(actionMenu, Dock.Top);
        root.Children.Add(actionMenu);
        root.Children.Add(new ScrollViewer
        {
            Content = fieldsPanel,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        });

        Child = root;
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
            return Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.CurrentCulture);

        if (propertyType == typeof(byte) || propertyType == typeof(sbyte) ||
            propertyType == typeof(short) || propertyType == typeof(ushort) ||
            propertyType == typeof(int) || propertyType == typeof(uint) ||
            propertyType == typeof(long) || propertyType == typeof(ulong))
            return Convert.ToString(value, System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty;

        return value.ToString() ?? string.Empty;
    }

    private static Control CreateDynamicFieldEditor(string fieldName, object? value)
    {
        var fieldPanel = new StackPanel
        {
            Width = 300,
            Spacing = 6,
            Margin = new Avalonia.Thickness(0, 0, 16, 12)
        };

        var label = new TextBlock
        {
            Text = fieldName,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = Avalonia.Media.FontWeight.Medium
        };

        // Determine the type based on the value
        var fieldType = value?.GetType() ?? typeof(string);
        var editor = CreateEditorControl(fieldType, value);
        editor.Width = 300;
        editor.HorizontalAlignment = HorizontalAlignment.Left;

        fieldPanel.Children.Add(label);
        fieldPanel.Children.Add(editor);

        return fieldPanel;
    }
}
