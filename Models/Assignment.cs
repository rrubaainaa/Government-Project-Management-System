using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GPMS.Models;

[Table("Assignment")]
public partial class Assignment
{
    [Key]
    [Column("assignment_id")]
    public int AssignmentId { get; set; }

    [Column("project_id")]
    public int? ProjectId { get; set; }

    [Column("module_id")]
    public int? ModuleId { get; set; }

    [Column("task_id")]
    public int? TaskId { get; set; }

    [Column("employee_id")]
    public int EmployeeId { get; set; }

    [Column("assigned_date")]
    public DateOnly AssignedDate { get; set; }

    [InverseProperty("Assignment")]
    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

    [ForeignKey("EmployeeId")]
    [InverseProperty("Assignments")]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey("ModuleId")]
    [InverseProperty("Assignments")]
    public virtual Module? Module { get; set; }

    [ForeignKey("ProjectId")]
    [InverseProperty("Assignments")]
    public virtual Project? Project { get; set; }

    [ForeignKey("TaskId")]
    [InverseProperty("Assignments")]
    public virtual Task? Task { get; set; }
}
