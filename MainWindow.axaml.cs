using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using System;
using System.Linq;

namespace Predator;

public partial class MainWindow : Window
{
    private LoginPage? _loginPage;
    
    private static readonly string[] WorkspaceMenuTitles =
    [
        "Clientes",
        "Articulos",
        "Tablas",
        "Tablas 1",
        "Propiedades",
        "Propiedades 1"
    ];

    public MainWindow()
    {
        InitializeComponent();
        AddTab("Login");
    }

    private void BuildMainMenu()
    {
        MainMenu.Items.Clear();

        MainMenu.Items.Add(CreateArchivoMenu());

        foreach (var title in WorkspaceMenuTitles)
            MainMenu.Items.Add(CreateWorkspaceMenuItem(title));
    }

    private MenuItem CreateArchivoMenu()
    {
        var archivoMenu = new MenuItem { Header = "Archivo" };
        archivoMenu.Items.Add(new MenuItem { Header = "Abrir" });
        archivoMenu.Items.Add(new MenuItem { Header = "Cerrar" });
        archivoMenu.Items.Add(new MenuItem { Header = "Grabar" });
        var loginItem = new MenuItem { Header = "Login" };
        loginItem.Click += OnLoginMenuItemClick;
        archivoMenu.Items.Add(loginItem);
        return archivoMenu;
    }

    private MenuItem CreateWorkspaceMenuItem(string title)
    {
        var menuItem = new MenuItem { Header = title };
        menuItem.PointerPressed += OnWorkspaceMenuItemPointerPressed;
        return menuItem;
    }

    private void OnWorkspaceMenuItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not MenuItem { Header: string title })
            return;

        AddTab(title);
        e.Handled = true;
    }

    private void AddTab(string title)
    {
        var existingTab = WorkspaceTabs.Items
            .OfType<TabItem>()
            .FirstOrDefault(t => string.Equals(t.Header?.ToString(), title, StringComparison.Ordinal));

        if (existingTab is not null)
        {
            WorkspaceTabs.SelectedItem = existingTab;
            return;
        }

        if (!WorkspaceTabFactory.TryCreate(title, out var tab) || tab is null)
            return;

        // Si es la pestaña de login, suscribirse a los eventos
        if (title.Equals("Login", StringComparison.OrdinalIgnoreCase) && tab.Content is LoginPage loginPage)
        {
            _loginPage = loginPage;
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

        if (tabItem is not null)
        {
            WorkspaceTabs.Items.Remove(tabItem);
        }
    }

    private void OnLoginMenuItemClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AddTab("Login");
    }
}