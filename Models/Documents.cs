using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GPMS.Models
{
    [Table("Document")]
    public class Document
    {
        [Key]
        [Column("document_id")]
        public int DocumentId { get; set; }

        // ✅ Keep Assignment (important for permissions)
        [Column("assignment_id")]
        public int? AssignmentId { get; set; }

        // ✅ Allow flexibility (avoid crash)
        [Column("document_name")]
        [StringLength(100)]
        [Unicode(false)]
        public string? DocumentName { get; set; }

        [Column("file_path")]
        [StringLength(255)]
        [Unicode(false)]
        public string? FilePath { get; set; }

        // ✅ Default value to avoid null issues
        [Column("uploaded_at")]
        public DateTime UploadedAt { get; set; } = DateTime.Now;

        [Column("uploaded_by")]
        public int? UploadedBy { get; set; }

        // =========================
        // 🔹 NEW HIERARCHY SUPPORT
        // =========================

        [Column("project_id")]
        public int? ProjectId { get; set; }

        [Column("module_id")]
        public int? ModuleId { get; set; }

        [Column("task_id")]
        public int? TaskId { get; set; }

        // =========================
        // 🔹 NAVIGATION PROPERTIES
        // =========================

        [ForeignKey("AssignmentId")]
        public virtual Assignment? Assignment { get; set; }

        [ForeignKey("UploadedBy")]
        public virtual Employee? UploadedByEmployee { get; set; }

        [ForeignKey("ProjectId")]
        public virtual Project? Project { get; set; }

        [ForeignKey("ModuleId")]
        public virtual Module? Module { get; set; }

        // ⚠️ FIX: Avoid conflict with System.Threading.Tasks.Task
        [ForeignKey("TaskId")]
        public virtual GPMS.Models.Task? Task { get; set; }
    }
}