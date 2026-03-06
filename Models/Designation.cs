using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GPMS.Models;

[Table("Designation")]
[Index("DesignationName", Name = "UQ__Designat__108F431B97E69E20", IsUnique = true)]
public partial class Designation
{
    [Key]
    [Column("designation_id")]
    public int DesignationId { get; set; }

    [Column("designation_name")]
    [StringLength(100)]
    [Unicode(false)]
    public string DesignationName { get; set; } = null!;

    [Column("designation_description")]
    [StringLength(200)]
    [Unicode(false)]
    public string? DesignationDescription { get; set; }

    [InverseProperty("Designation")]
    public virtual ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
