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
        public virtual DbSet<Employee> Employees { get; set; }
        public virtual DbSet<Module> Modules { get; set; }
        public virtual DbSet<Permission> Permissions { get; set; }
        public virtual DbSet<Project> Projects { get; set; }
        public virtual DbSet<Role> Roles { get; set; }
        public virtual DbSet<RolePermission> RolePermissions { get; set; }

        public virtual DbSet<Document> Documents { get; set; }

        // Avoid Task naming conflict
        public virtual DbSet<GPMS.Models.Task> Tasks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // =========================
            // 🔹 ASSIGNMENT RELATIONSHIPS
            // =========================

            modelBuilder.Entity<Assignment>()
                .HasOne(a => a.Employee)
                .WithMany(e => e.Assignments)
                .HasForeignKey(a => a.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Assignment>()
                .HasOne(a => a.Project)
                .WithMany(p => p.Assignments)
                .HasForeignKey(a => a.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Assignment>()
                .HasOne(a => a.Module)
                .WithMany(m => m.Assignments)
                .HasForeignKey(a => a.ModuleId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Assignment>()
                .HasOne(a => a.Task)
                .WithMany(t => t.Assignments)
                .HasForeignKey(a => a.TaskId)
                .OnDelete(DeleteBehavior.Restrict);

            // ✅ NEW: Assignment → Role (VERY IMPORTANT)
            modelBuilder.Entity<Assignment>()
                .HasOne(a => a.Role)
                .WithMany() // no back navigation needed
                .HasForeignKey(a => a.RoleId)
                .OnDelete(DeleteBehavior.Restrict);


            // =========================
            // 🔹 EMPLOYEE → DESIGNATION
            // =========================

            modelBuilder.Entity<Employee>()
                .HasOne(e => e.Designation)
                .WithMany(d => d.Employees)
                .HasForeignKey(e => e.DesignationId)
                .OnDelete(DeleteBehavior.Restrict);


            // =========================
            // 🔹 MODULE → PROJECT
            // =========================

            modelBuilder.Entity<Module>()
                .HasOne(m => m.Project)
                .WithMany(p => p.Modules)
                .HasForeignKey(m => m.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);


            // =========================
            // 🔹 TASK → MODULE
            // =========================

            modelBuilder.Entity<GPMS.Models.Task>()
                .HasOne(t => t.Module)
                .WithMany(m => m.Tasks)
                .HasForeignKey(t => t.ModuleId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // =========================
            // 🔹 ROLE HIERARCHY
            // =========================

            modelBuilder.Entity<Role>()
                .HasOne(r => r.ParentRole)
                .WithMany(r => r.InverseParentRole)
                .HasForeignKey(r => r.ParentRoleId)
                .OnDelete(DeleteBehavior.Restrict);


            // =========================
            // 🔹 ROLE ↔ PERMISSION
            // =========================

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);


            // =========================
            // 🔹 OPTIONAL: UNIQUE CONSTRAINT (VERY GOOD)
            // =========================

            modelBuilder.Entity<Assignment>()
                .HasIndex(a => new
                {
                    a.EmployeeId,
                    a.ProjectId,
                    a.ModuleId,
                    a.TaskId
                })
                .IsUnique(); // 🔥 prevents duplicate assignment
        }
    }
}