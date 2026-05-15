using CoreIA;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Data;
using System.Linq;

namespace Predator;

/// <summary>
/// Gestor dinámico del menú de aplicación
/// </summary>
internal sealed class GestorMenuAplicacion
{
    private List<NodoMenu> _todosLosMenus = [];
    private List<NodoMenu> _menusSinMenuGrupal = [];
    private Dictionary<int, List<NodoMenu>> _menusPorMenuGrupal = [];

    /// <summary>
    /// Carga el menú desde la base de datos de forma dinámica
    /// </summary>
    public void CargarMenu(Sesion sesion)
    {
        try
        {
            // Obtener datos del menú desde CoreIA
            var dataSet = Datos.LeeMenu(sesion);

            var tablaMenu = dataSet?.Tables.Count > 0 ? dataSet.Tables[0] : null;
            _todosLosMenus = [];
            _menusSinMenuGrupal = [];
            _menusPorMenuGrupal.Clear();

            if (tablaMenu is not null)
            {
                var columnaMenuGrupal = EncontrarColumnaMenuGrupal(tablaMenu);
                foreach (DataRow fila in tablaMenu.Rows)
                {
                    var nodo = new NodoMenu(fila);
                    _todosLosMenus.Add(nodo);

                    var menuGrupalId = ObtenerMenuGrupalIdDesdeFila(fila, columnaMenuGrupal);
                    if (!menuGrupalId.HasValue)
                    {
                        _menusSinMenuGrupal.Add(nodo);
                        continue;
                    }

                    if (!_menusPorMenuGrupal.TryGetValue(menuGrupalId.Value, out var lista))
                    {
                        lista = [];
                        _menusPorMenuGrupal[menuGrupalId.Value] = lista;
                    }

                    lista.Add(nodo);
                }
            }

        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error al cargar el menú de la aplicación", ex);
        }
    }

    /// <summary>
    /// Obtiene los menús que no tienen menú grupal asignado.
    /// </summary>
    public List<NodoMenu> ObtenerMenusSinMenuGrupal()
    {
        return _menusSinMenuGrupal
            .OrderBy(n => n.Codigo ?? string.Empty)
            .ThenBy(n => n.Nombre)
            .ToList();
    }

    /// <summary>
    /// Obtiene los menús agrupados bajo un menú principal (MenuGrupalId == menuId).
    /// </summary>
    public List<NodoMenu> ObtenerMenusPorMenuGrupal(int menuId)
    {
        if (!_menusPorMenuGrupal.TryGetValue(menuId, out var lista))
            return [];

        return lista
            .OrderBy(n => n.Codigo ?? string.Empty)
            .ThenBy(n => n.Nombre)
            .ToList();
    }


    private static string? EncontrarColumnaMenuGrupal(DataTable tabla)
    {
        foreach (DataColumn columna in tabla.Columns)
        {
            if (EsColumnaMenuGrupal(columna.ColumnName))
                return columna.ColumnName;
        }

        return null;
    }

    private static int? ObtenerMenuGrupalIdDesdeFila(DataRow fila, string? columnaMenuGrupal)
    {
        if (string.IsNullOrWhiteSpace(columnaMenuGrupal) || !fila.Table.Columns.Contains(columnaMenuGrupal))
            return null;

        var valor = fila[columnaMenuGrupal!];
        if (valor == DBNull.Value || valor is null)
            return null;

        var texto = valor.ToString();
        if (string.IsNullOrWhiteSpace(texto) || string.Equals(texto, "0", StringComparison.OrdinalIgnoreCase))
            return null;

        if (int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            return id;

        if (int.TryParse(texto, NumberStyles.Integer, CultureInfo.CurrentCulture, out id))
            return id;

        if (decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var decInv))
            return Convert.ToInt32(decInv);

        if (decimal.TryParse(texto, NumberStyles.Number, CultureInfo.CurrentCulture, out var decCur))
            return Convert.ToInt32(decCur);

        return null;
    }

    private static bool EsColumnaMenuGrupal(string nombreColumna)
    {
        if (string.IsNullOrWhiteSpace(nombreColumna))
            return false;

        var normalizado = new string(nombreColumna
            .Where(char.IsLetterOrDigit)
            .ToArray())
            .ToLowerInvariant();

        return normalizado.Contains("menugrupal")
            || normalizado.Contains("grupomenu")
            || normalizado == "grlid";
    }
}
