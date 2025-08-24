using Syn.Core.SqlSchemaGenerator.Interfaces;

using System.Reflection;

namespace Syn.Core.SqlSchemaGenerator.Core
{
    /// <summary>
    /// Scans an assembly for types that implement IDbEntity and are valid for schema generation.
    /// </summary>
    public class DbEntityScanner
    {
        /// <summary>
        /// Returns all non-abstract types in the assembly that implement IDbEntity.
        /// </summary>
        public IEnumerable<Type> Scan(Assembly assembly) =>
            assembly.GetTypes().Where(t => typeof(IDbEntity).IsAssignableFrom(t) && !t.IsAbstract);
    }
}