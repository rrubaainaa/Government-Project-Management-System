using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GPMS.Models;

[Table("Employee")]
[Index("Email", Name = "UQ__Employee__AB6E61640D4E57BA", IsUnique = true)]
[Index("Username", Name = "UQ__Employee__F3DBC572E1224198", IsUnique = true)]
public partial class Employee
{
    [Key]
    [Column("employee_id")]
    public int EmployeeId { get; set; }

    public bool IsAdmin { get; set; } = false;

    [Column("employee_name")]
    [StringLength(100)]
    [Unicode(false)]
    [Required(ErrorMessage = "Name is required")]
    public string EmployeeName { get; set; } = null!;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Enter valid email")]
    [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        ErrorMessage = "Enter proper email like abc@gmail.com")]
    [Column("email")]
    [StringLength(100)]
    [Unicode(false)]
    public string Email { get; set; } = null!;

    [Column("username")]
    [StringLength(50)]
    [Unicode(false)]
    [Required(ErrorMessage = "Username is required")]
    public string Username { get; set; } = null!;

    [Column("epassword")]
    [StringLength(255)]
    [Unicode(false)]
    public string? Epassword { get; set; }

    [Column("designation_id")]
    public int? DesignationId { get; set; }

    [Required]
    [Column("system_role")]
    [StringLength(20)]
    [Unicode(false)]
    public string SystemRole { get; set; } = "Employee";

    [Column("IsFirstLogin")]
    public bool IsFirstLogin { get; set; } = true;

    [Column("PasswordChangedAt", TypeName = "datetime")]
    public DateTime? PasswordChangedAt { get; set; }

    [Column("ResetToken")]
    [StringLength(200)]
    [Unicode(false)]
    public string? ResetToken { get; set; }

    [Column("ResetTokenExpiry")]
    public DateTime? ResetTokenExpiry { get; set; }

    // =========================================
    // NAVIGATION PROPERTIES
    // =========================================

    [InverseProperty("Employee")]
    public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();

    [ForeignKey("DesignationId")]
    [InverseProperty("Employees")]
    public Designation? Designation { get; set; }

    public ICollection<Message> SentMessages { get; set; }
    public ICollection<MessageReceiver> ReceivedMessages { get; set; }
}