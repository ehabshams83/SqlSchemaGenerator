using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Syn.Core.SqlSchemaGenerator.Models
{
    public class SqlIndexInfo
    {
        public string Name { get; set; }
        public List<string> Columns { get; set; } = new();
        public bool IsUnique { get; set; }
    }

}
