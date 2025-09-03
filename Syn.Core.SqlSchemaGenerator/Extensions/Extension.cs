using System.Reflection;

namespace Syn.Core.SqlSchemaGenerator.Extensions;

public static class Extension
{
    /// <summary>
    /// Filters types from the given assembly that match at least one of the provided filter types
    /// (interface or base class).
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="filterTypes">Interfaces or base classes to filter by.</param>
    /// <returns>List of matching types.</returns>
    public static List<Type> FilterTypesFromAssembly(this Assembly assembly, params Type[] filterTypes)
    {
        return assembly
            .GetTypes()
            .Where(t =>
                t.IsClass &&
                !t.IsAbstract &&
                (filterTypes == null || filterTypes.Length == 0 ||
                 filterTypes.Any(f => f.IsAssignableFrom(t))))
            .ToList();
    }


    /// <summary>
    /// Filters types from multiple assemblies that match at least one of the provided filter types
    /// (interface or base class).
    /// </summary>
    /// <param name="assemblies">Assemblies to scan.</param>
    /// <param name="filterTypes">Interfaces or base classes to filter by.</param>
    /// <returns>List of matching types from all assemblies.</returns>
    public static List<Type> FilterTypesFromAssemblies(this IEnumerable<Assembly> assemblies, params Type[] filterTypes)
    {
        var result = new List<Type>();

        foreach (var assembly in assemblies)
        {
            var types = FilterTypesFromAssembly(assembly, filterTypes);
            result.AddRange(types);
        }
        return result.Distinct().ToList();
    }
}
