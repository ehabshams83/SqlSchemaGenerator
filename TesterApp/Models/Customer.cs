using Syn.Core.SqlSchemaGenerator.Attributes;

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TesterApp.Models;


/// <summary>
/// يمثل عميل في النظام.
/// </summary>
[Table("Customers", Schema = "sales")]
public class Customer
{
    [Key]
    public int CustomerId { get; set; }

    [Required]
    [StringLength(150)] // ← تعديل فعلي: من 100 إلى 120
    public string FullName { get; set; }

    [Required]
    [StringLength(100)]
    public string Email { get; set; }

    [MaxLength(15)]
    public string PhoneNumber { get; set; }

    [MaxLength(200)]
    public string Address { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; }
}