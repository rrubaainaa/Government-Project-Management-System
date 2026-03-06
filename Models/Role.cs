using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GPMS.Models;

[Index("RoleName", Name = "UQ__Roles__783254B1E5E34E85", IsUnique = true)]
public partial class Role
{
    [Key]
    [Column("role_id")]
    public int RoleId { get; set; }

    [Column("role_name")]
    [StringLength(50)]
    [Unicode(false)]
    public string RoleName { get; set; } = null!;

    [Column("role_description")]
    [StringLength(150)]
    [Unicode(false)]
    public string? RoleDescription { get; set; }

    [Column("parent_role_id")]
    public int? ParentRoleId { get; set; }

    [InverseProperty("ParentRole")]
    public virtual ICollection<Role> InverseParentRole { get; set; } = new List<Role>();

    [ForeignKey("ParentRoleId")]
    [InverseProperty("InverseParentRole")]
    public virtual Role? ParentRole { get; set; }

    [InverseProperty("Role")]
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
