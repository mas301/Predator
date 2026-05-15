using Avalonia.Controls;
using Avalonia.Layout;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CoreIA;

namespace Predator;

public interface IWorkspaceTabDefinition
{
    bool Matches(string title);
    TabItem Create(string title, Sesion sesionActual, CoreIA.Entidades.Entidad? entidad = null);
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

    public TabItem Create(string title, Sesion sesionActual, CoreIA.Entidades.Entidad? entidad = null)
    {
        var listaPage = new ListaPage(title, sesionActual, entidad);
        return WorkspaceTabFactory.CreateTabItem(title, "edicion-tab", listaPage);
    }
}

public sealed class EditorTabDefinition : IWorkspaceTabDefinition
{
    public bool Matches(string title) =>
        title.StartsWith("Tablas", StringComparison.OrdinalIgnoreCase);

    public TabItem Create(string title, Sesion sesionActual, CoreIA.Entidades.Entidad? entidad = null)
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

    public TabItem Create(string title, Sesion sesionActual, CoreIA.Entidades.Entidad? entidad = null)
    {
        return WorkspaceTabFactory.CreateTabItem(title, "propiedades-tab");
    }
}

public sealed class LoginTabDefinition : IWorkspaceTabDefinition
{
    public bool Matches(string title) =>
        title.Equals("Login", StringComparison.OrdinalIgnoreCase);

    public TabItem Create(string title, Sesion sesionActual, CoreIA.Entidades.Entidad? entidad = null)
    {
        var loginPage = new LoginPage
        {
            SesionActual = sesionActual
        };
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

    public static bool TryCreate(string title, Sesion sesionActual, out TabItem? tab, CoreIA.Entidades.Entidad? entidad = null)
    {
        foreach (var definition in Definitions)
        {
            if (definition.Matches(title))
            {
                tab = definition.Create(title, sesionActual, entidad);
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
