using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Syn.Core.SqlSchemaGenerator.Attributes
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class CompositeIndexAttribute : Attribute
    {
        public string Name { get; }
        public bool IsUnique { get; }

        public CompositeIndexAttribute(string name, bool isUnique = false)
        {
            Name = name;
            IsUnique = isUnique;
        }
    }

}
