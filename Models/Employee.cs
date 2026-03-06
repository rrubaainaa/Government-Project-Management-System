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

    [Column("employee_name")]
    [StringLength(100)]
    [Unicode(false)]
    public string EmployeeName { get; set; } = null!;

    [Column("email")]
    [StringLength(100)]
    [Unicode(false)]
    public string Email { get; set; } = null!;

    [Column("username")]
    [StringLength(50)]
    [Unicode(false)]
    public string Username { get; set; } = null!;

    [Column("epassword")]
    [StringLength(255)]
    [Unicode(false)]
    public string Epassword { get; set; } = null!;

    [Column("designation_id")]
    public int DesignationId { get; set; }

    [InverseProperty("Employee")]
    public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();

    [ForeignKey("DesignationId")]
    [InverseProperty("Employees")]
    public virtual Designation Designation { get; set; } = null!;
}
