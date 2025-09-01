using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace TesterApp.Models;


/// <summary>
/// يمثل طلب شراء في النظام.
/// </summary>
[Table("Orders", Schema = "sales")]
public class Order
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int OrderId { get; set; }

    [Required]
    [MaxLength(20)]
    public string OrderNumber { get; set; }

    [Required]
    public DateTime OrderDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Range(0, double.MaxValue)]
    public decimal TotalAmount { get; set; }

    public string Notes { get; set; }
    [Required]
    public int CustomerId { get; set; }

    // Navigation Properties
    public Customer Customer { get; set; }
}