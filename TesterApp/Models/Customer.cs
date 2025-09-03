using Microsoft.EntityFrameworkCore;

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TesterApp.Models;


/// <summary>
/// يمثل عميل في النظام.
/// </summary>
[Table("Customers", Schema = "sales")]
[Index(nameof(Email), nameof(FullName), IsUnique = true)]
public class Customer
{
    [Key]
    public int CustomerId { get; set; }

    [Required]
    [StringLength(600)] // ← تعديل فعلي: من 150 إلى 600
    public string FullName { get; set; }

    [Required]
    [StringLength(300)]
    public string Email { get; set; }

    [MaxLength(15)]
    public string PhoneNumber { get; set; }

    [MaxLength(200)]
    public string Address { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; }
}