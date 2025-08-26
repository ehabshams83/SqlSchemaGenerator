using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TesterApp.Models.MTM
{
    public class Tag
    {
        public int Id { get; set; }
        public string Label { get; set; }
        public ICollection<Product> Products { get; set; }
    }

}
