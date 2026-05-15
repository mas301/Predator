using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using System;
using System.Linq;
using CoreIA;

namespace Predator;

public partial class MainWindow : Window
{
    public Sesion SesionActual;
    private GestorMenuAplicacion? _gestorMenu;
    private int? _menuGrupalSeleccionadoId;

    public MainWindow()
    {
        SesionActual = new Sesion();
        InitializeComponent();
        AddTab("Login");
    }

    private void BuildMainMenu()
    {
        MainMenu.Items.Clear();

        try
        {
            // Cargar menú desde la base de datos
            _gestorMenu = new GestorMenuAplicacion();
            _gestorMenu.CargarMenu(SesionActual);
            RenderMainMenuPorModuloSeleccionado();
        }
        catch (Exception ex)
        {
            var mensaje = $"Error al cargar el menú dinámico: {ex.Message}\n{ex.StackTrace}";
            System.Diagnostics.Debug.WriteLine(mensaje);
            MessageWindow.ShowMessage(mensaje);
        }
    }

    private void RenderMainMenuPorModuloSeleccionado()
    {
        if (_gestorMenu is null)
            return;

        MainMenu.Items.Clear();

        var submenusModulos = _gestorMenu.ObtenerMenusSinMenuGrupal();
        if (submenusModulos.Count == 0)
            return;

        if (!_menuGrupalSeleccionadoId.HasValue || !submenusModulos.Any(m => m.Id == _menuGrupalSeleccionadoId.Value))
        {
            var submenuVentas = submenusModulos.FirstOrDefault(m =>
                string.Equals(m.Nombre, "Ventas", StringComparison.OrdinalIgnoreCase));

            _menuGrupalSeleccionadoId = (submenuVentas ?? submenusModulos[0]).Id;
        }

        var moduloSeleccionado = submenusModulos
            .FirstOrDefault(m => m.Id == _menuGrupalSeleccionadoId.Value);

        var modulosMenu = new MenuItem
        {
            Header = moduloSeleccionado?.Nombre ?? "Modulos"
        };

        foreach (var submenu in submenusModulos)
        {
            var submenuId = submenu.Id;
            var submenuItem = new MenuItem { Header = submenu.Nombre };
            submenuItem.Click += (_, _) => OnMenuGrupalSeleccionado(submenuId);
            modulosMenu.Items.Add(submenuItem);
        }

        MainMenu.Items.Add(modulosMenu);

        foreach (var modulo in _gestorMenu.ObtenerMenusPorMenuGrupal(_menuGrupalSeleccionadoId.Value))
            MainMenu.Items.Add(CrearMenuItemDesdeNodo(modulo));
    }

    private void OnMenuGrupalSeleccionado(int menuGrupalId)
    {
        _menuGrupalSeleccionadoId = menuGrupalId;
        RenderMainMenuPorModuloSeleccionado();
    }

    private MenuItem CrearMenuItemDesdeNodo(NodoMenu nodo)
    {
        var item = new MenuItem { Header = nodo.Nombre };


        foreach (var hijo in nodo.Hijos)
            item.Items.Add(CrearMenuItemDesdeNodo(hijo));

        if (!string.IsNullOrWhiteSpace(nodo.NombreEntidad))
            item.Click += (_, _) => AbrirPestañaDesdeNodo(nodo);

        return item;
    }

    private void AbrirPestañaDesdeNodo(NodoMenu nodo)
    {
        if (string.IsNullOrWhiteSpace(nodo.NombreEntidad))
            return;

        var entidad = Globales.CreaEntidad(nodo.NombreEntidad);
        var listTitle = ResolveEntidadNombrePlural(entidad, nodo.NombreEntidad);

        AddTab(listTitle, entidad);
    }

    private static string ResolveEntidadNombrePlural(CoreIA.Entidades.Entidad? entidad, string fallback)
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
            var candidateNames = new[] { "NombrePlural", "Nombre Plural", "Nombre_Plural", "nombrePlural", "nombre_plural" };
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
                if (!normalizedKey.Equals("NombrePlural", StringComparison.OrdinalIgnoreCase))
                    continue;

                var dictValue = Normalize(dictionary[keyObject]);
                if (!string.IsNullOrWhiteSpace(dictValue))
                    return dictValue;
            }
        }

        return fallback;
    }

    private void AddTab(string title, CoreIA.Entidades.Entidad? entidad = null)
    {
        var existingTab = WorkspaceTabs.Items
            .OfType<TabItem>()
            .FirstOrDefault(t => string.Equals(t.Header?.ToString(), title, StringComparison.Ordinal));

        if (existingTab is not null)
        {
            if (existingTab.Content is ListaPage listaPage)
                listaPage.Entidad = entidad;

            WorkspaceTabs.SelectedItem = existingTab;
            return;
        }

        if (!WorkspaceTabFactory.TryCreate(title, SesionActual, out var tab, entidad) || tab is null)
            return;

        // Si es la pestaña de login, suscribirse a los eventos
        if (title.Equals("Login", StringComparison.OrdinalIgnoreCase) && tab.Content is LoginPage loginPage)
        {
            loginPage.LoginContinued += OnLoginContinued;
            loginPage.LogoutRequested += OnLogoutRequested;
        }

        WorkspaceTabs.Items.Add(tab);
        WorkspaceTabs.SelectedItem = tab;
    }

    private void OnLoginContinued(object? sender, EventArgs e)
    {
        BuildMainMenu();
    }

    private void OnLogoutRequested(object? sender, EventArgs e)
    {
        MainMenu.Items.Clear();
        _menuGrupalSeleccionadoId = null;

        var tabsToRemove = WorkspaceTabs.Items
            .OfType<TabItem>()
            .Where(t => !string.Equals(t.Header?.ToString(), "Login", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var tab in tabsToRemove)
            WorkspaceTabs.Items.Remove(tab);

        AddTab("Login");
    }

    private void OnTabCloseButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        // Buscar el TabItem padre navegando hacia arriba en el árbol visual
        var tabItem = button.GetVisualAncestors()
            .OfType<TabItem>()
            .FirstOrDefault();

        if (tabItem is null || WorkspaceTabs.Items is not System.Collections.IList tabItems)
            return;

        var wasSelected = ReferenceEquals(WorkspaceTabs.SelectedItem, tabItem);
        var currentIndex = tabItems.IndexOf(tabItem);
        if (currentIndex < 0)
            return;

        object? previousSelection = null;
        if (wasSelected && tabItems.Count > 1)
        {
            var previousIndex = currentIndex > 0 ? currentIndex - 1 : 0;
            if (previousIndex == currentIndex && currentIndex + 1 < tabItems.Count)
                previousIndex = currentIndex + 1;

            previousSelection = tabItems[previousIndex];
        }

        tabItems.RemoveAt(currentIndex);

        if (previousSelection is not null)
            WorkspaceTabs.SelectedItem = previousSelection;
    }

}