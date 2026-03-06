using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GPMS.Models;

[Table("Task")]
public partial class Task
{
    [Key]
    [Column("task_id")]
    public int TaskId { get; set; }

    [Column("module_id")]
    public int ModuleId { get; set; }

    [Column("task_name")]
    [StringLength(100)]
    [Unicode(false)]
    public string TaskName { get; set; } = null!;

    [Column("task_description")]
    [StringLength(255)]
    [Unicode(false)]
    public string? TaskDescription { get; set; }

    [Column("task_status")]
    [StringLength(50)]
    [Unicode(false)]
    public string? TaskStatus { get; set; }

    [Column("task_start_date")]
    public DateOnly? TaskStartDate { get; set; }

    [Column("task_end_date")]
    public DateOnly? TaskEndDate { get; set; }

    [InverseProperty("Task")]
    public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();

    [ForeignKey("ModuleId")]
    [InverseProperty("Tasks")]
    public virtual Module Module { get; set; } = null!;
}
