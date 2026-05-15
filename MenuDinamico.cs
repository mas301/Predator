using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Predator;

/// <summary>
/// Representa un nodo de menú de forma dinámica, almacenando los datos sin esquema fijo
/// </summary>
public sealed class NodoMenu
{
    private readonly Dictionary<string, object?> _datos;

    public int Id { get; }
    public int? IdPadre { get; }
    public string Nombre { get; }
    public string? NombreEntidad { get; }
    public string? Codigo { get; }
    public List<NodoMenu> Hijos { get; } = [];

    /// <summary>
    /// Obtiene el valor de una columna de forma dinámica
    /// </summary>
    public object? ObtenerValor(string nombreColumna)
    {
        if (_datos.TryGetValue(nombreColumna, out var valor))
            return valor;

        var coincidencia = _datos.FirstOrDefault(kv =>
            kv.Key.Equals(nombreColumna, StringComparison.OrdinalIgnoreCase));

        return string.IsNullOrEmpty(coincidencia.Key) ? null : coincidencia.Value;
    }

    /// <summary>
    /// Obtiene todas las columnas disponibles
    /// </summary>
    public IEnumerable<KeyValuePair<string, object?>> ObtenerTodosDatos()
    {
        return _datos.AsEnumerable();
    }

    public NodoMenu(DataRow fila)
    {
        _datos = [];

        // Almacenar todos los datos de la fila de forma dinámica
        foreach (DataColumn columna in fila.Table.Columns)
        {
            var valor = fila[columna.ColumnName];
            _datos[columna.ColumnName] = valor == DBNull.Value ? null : valor;
        }

        // Identificar campos estándar (flexible para cambios de nombres)
        Id = ObtenerEnteroODefault(_datos, ["MenuId", "Id"], 0);
        IdPadre = ObtenerEnteroONull(_datos, ["MenuPadreId", "PapId", "ParentId", "IdPadre"]);
        Nombre = ObtenerTextODefault(_datos, ["Menu", "Nombre", "Descripcion", "Nombre Menu"], "");
        NombreEntidad = ObtenerTextODefault(_datos, ["NombreEntidad", "Entidad", "EntityName"], null);
        Codigo = ObtenerTextODefault(_datos, ["Codigo", "CodigoMenu", "Codigo Menu", "Codigo_Menu", "Code", "MenuCodigo"], null);
    }

    private static int ObtenerEnteroODefault(Dictionary<string, object?> datos, string[] posiblesColumnas, int valorPorDefecto)
    {
        foreach (var columna in posiblesColumnas)
        {
            if (datos.TryGetValue(columna, out var valor) && valor != null)
            {
                if (int.TryParse(valor.ToString(), out var resultado))
                    return resultado;
            }
        }
        return valorPorDefecto;
    }

    private static int? ObtenerEnteroONull(Dictionary<string, object?> datos, string[] posiblesColumnas)
    {
        foreach (var columna in posiblesColumnas)
        {
            if (datos.TryGetValue(columna, out var valor) && valor != null)
            {
                if (int.TryParse(valor.ToString(), out var resultado))
                    return resultado;
            }
        }
        return null;
    }

    private static string ObtenerTextODefault(Dictionary<string, object?> datos, string[] posiblesColumnas, string? valorPorDefecto)
    {
        foreach (var columna in posiblesColumnas)
        {
            if (datos.TryGetValue(columna, out var valor) && valor != null)
                return valor.ToString() ?? (valorPorDefecto ?? "");
        }
        return valorPorDefecto ?? "";
    }
}

/// <summary>
/// Constructor dinámico de árbol de menús desde un DataSet
/// </summary>
public sealed class ConstructorArbolMenu
{
    /// <summary>
    /// Construye un árbol jerárquico de menús desde un DataSet
    /// </summary>
    public static List<NodoMenu> ConstruirArbol(DataSet? dataSet)
    {
        if (dataSet?.Tables.Count == 0)
            return [];

        var tabla = dataSet!.Tables[0];
        if (tabla.Rows.Count == 0)
            return [];

        // Crear todos los nodos desde las filas
        var nodos = tabla.Rows
            .Cast<DataRow>()
            .Select(fila => new NodoMenu(fila))
            .ToList();

        // Establecer relaciones padre-hijo
        var nodosDict = nodos.ToDictionary(n => n.Id);

        foreach (var nodo in nodos)
        {
            if (nodo.IdPadre.HasValue && nodosDict.TryGetValue(nodo.IdPadre.Value, out var nodoPadre))
            {
                nodoPadre.Hijos.Add(nodo);
            }
        }

        // Ordenar recursivamente por código (original)
        var raices = nodos
            .Where(n => !n.IdPadre.HasValue)
            .OrderBy(n => n.Codigo ?? "")
            .ToList();

        OrdenarHijos(raices);

        return raices;
    }

    private static void OrdenarHijos(List<NodoMenu> nodos)
    {
        foreach (var nodo in nodos)
        {
            if (nodo.Hijos.Count > 0)
            {
                nodo.Hijos.Sort((a, b) => (a.Codigo ?? "").CompareTo(b.Codigo ?? ""));
                OrdenarHijos(nodo.Hijos);
            }
        }
    }

    /// <summary>
    /// Obtiene una lista plana de todos los menús en orden de profundidad
    /// </summary>
    public static List<(NodoMenu Nodo, int Nivel)> ObtenerListaPlana(List<NodoMenu> raices)
    {
        var resultado = new List<(NodoMenu, int)>();
        
        void RecorrerArbol(List<NodoMenu> nodos, int nivel)
        {
            foreach (var nodo in nodos)
            {
                resultado.Add((nodo, nivel));
                if (nodo.Hijos.Count > 0)
                    RecorrerArbol(nodo.Hijos, nivel + 1);
            }
        }

        RecorrerArbol(raices, 0);
        return resultado;
    }
}
