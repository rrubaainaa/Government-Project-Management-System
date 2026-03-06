using GPMS.Models;
using Microsoft.EntityFrameworkCore;

namespace GPMS.Data
{
    public partial class AppDbContext : DbContext
    {
        public AppDbContext()
        {
        }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Assignment> Assignments { get; set; }
        public virtual DbSet<Designation> Designations { get; set; }
        public virtual DbSet<Document> Documents { get; set; }
        public virtual DbSet<Employee> Employees { get; set; }
        public virtual DbSet<Module> Modules { get; set; }
        public virtual DbSet<Permission> Permissions { get; set; }
        public virtual DbSet<Project> Projects { get; set; }
        public virtual DbSet<Role> Roles { get; set; }
        public virtual DbSet<RolePermission> RolePermissions { get; set; }

        // Keep fully qualified name to avoid Task conflict
        public virtual DbSet<GPMS.Models.Task> Tasks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Assignment relationships
            modelBuilder.Entity<Assignment>()
                .HasOne(a => a.Employee)
                .WithMany(e => e.Assignments)
                .HasForeignKey(a => a.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            modelBuilder.Entity<Assignment>()
                .HasOne(a => a.Project)
                .WithMany(p => p.Assignments)
                .HasForeignKey(a => a.ProjectId);

            modelBuilder.Entity<Assignment>()
                .HasOne(a => a.Module)
                .WithMany(m => m.Assignments)
                .HasForeignKey(a => a.ModuleId);

            modelBuilder.Entity<Assignment>()
                .HasOne(a => a.Task)
                .WithMany(t => t.Assignments)
                .HasForeignKey(a => a.TaskId);


            // Employee → Designation
            modelBuilder.Entity<Employee>()
                .HasOne(e => e.Designation)
                .WithMany(d => d.Employees)
                .HasForeignKey(e => e.DesignationId)
                .OnDelete(DeleteBehavior.ClientSetNull);


            // Module → Project
            modelBuilder.Entity<Module>()
                .HasOne(m => m.Project)
                .WithMany(p => p.Modules)
                .HasForeignKey(m => m.ProjectId);


            // Task → Module
            modelBuilder.Entity<GPMS.Models.Task>()
                .HasOne(t => t.Module)
                .WithMany(m => m.Tasks)
                .HasForeignKey(t => t.ModuleId);


            // Document → Assignment
            modelBuilder.Entity<Document>()
                .HasOne(d => d.Assignment)
                .WithMany(a => a.Documents)
                .HasForeignKey(d => d.AssignmentId);


            // Role hierarchy
            modelBuilder.Entity<Role>()
                .HasOne(r => r.ParentRole)
                .WithMany(r => r.InverseParentRole)
                .HasForeignKey(r => r.ParentRoleId);


            // RolePermission relations
            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId);

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId);
        }
    }
}