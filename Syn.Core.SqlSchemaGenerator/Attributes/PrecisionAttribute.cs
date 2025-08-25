using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Syn.Core.SqlSchemaGenerator.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class PrecisionAttribute : Attribute
    {
        public int Precision { get; }
        public int Scale { get; }
        public PrecisionAttribute(int precision, int scale)
        {
            Precision = precision;
            Scale = scale;
        }
    }


}
