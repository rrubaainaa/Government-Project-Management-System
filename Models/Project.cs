using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GPMS.Models;

[Table("Project")]
public partial class Project
{
    [Key]
    [Column("project_id")]
    public int ProjectId { get; set; }

    [Column("project_name")]
    [StringLength(100)]
    [Unicode(false)]
    public string ProjectName { get; set; } = null!;

    [Column("project_details")]
    [StringLength(255)]
    [Unicode(false)]
    public string? ProjectDetails { get; set; }

    [Column("project_status")]
    [StringLength(50)]
    [Unicode(false)]
    public string? ProjectStatus { get; set; }

    [Column("project_start_date")]
    public DateOnly ProjectStartDate { get; set; }

    [Column("project_end_date")]
    public DateOnly? ProjectEndDate { get; set; }

    [InverseProperty("Project")]
    public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();

    [InverseProperty("Project")]
    public virtual ICollection<Module> Modules { get; set; } = new List<Module>();
}
