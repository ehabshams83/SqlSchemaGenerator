using Azure;

using Syn.Core.SqlSchemaGenerator.Attributes;
using Syn.Core.SqlSchemaGenerator.Interfaces;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TesterApp.Models.MTM
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public ICollection<Tag> Tags { get; set; }
    }


    //[Description("Stores product information")]
    //public class Product : IDbEntity
    //{
    //    [Description("Primary key")]
    //    [Required]
    //    [Key]
    //    public int Id { get; set; }

    //    [MaxLength(100)]
    //    [Required]
    //    [Description("Product name")]
    //    public string Name { get; set; }

    //    [DefaultValue(0)]
    //    [Description("Stock quantity available")]
    //    public int Stock { get; set; }

    //    [Unique(ConstraintName = "UQ_Product_Code")]
    //    [MaxLength(50)]
    //    [Description("Unique product code")]
    //    public string Code { get; set; }
    //}

}
