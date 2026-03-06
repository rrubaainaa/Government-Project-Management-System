using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GPMS.Models;

[Table("Permission")]
[Index("PermsName", Name = "UQ__Permissi__B8100928EC646E2C", IsUnique = true)]
public partial class Permission
{
    [Key]
    [Column("perms_id")]
    public int PermsId { get; set; }

    [Column("perms_name")]
    [StringLength(100)]
    [Unicode(false)]
    public string PermsName { get; set; } = null!;

    [Column("description")]
    [StringLength(200)]
    [Unicode(false)]
    public string? Description { get; set; }

    [InverseProperty("Permission")]
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
