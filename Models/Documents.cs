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

        // ✅ OPTIONAL (no longer required for upload)
        [Column("assignment_id")]
        public int? AssignmentId { get; set; }

        [Column("document_name")]
        [StringLength(100)]
        [Unicode(false)]
        public string? DocumentName { get; set; }

        [Column("file_path")]
        [StringLength(255)]
        [Unicode(false)]
        public string? FilePath { get; set; }

        [Column("uploaded_at")]
        public DateTime UploadedAt { get; set; } = DateTime.Now;

        [Column("uploaded_by")]
        public int? UploadedBy { get; set; }

        // =========================
        // 🔹 HIERARCHY SUPPORT
        // =========================

        // ✅ These are now PRIMARY references (used instead of assignment)

        [Column("project_id")]
        public int? ProjectId { get; set; }

        [Column("module_id")]
        public int? ModuleId { get; set; }

        [Column("task_id")]
        public int? TaskId { get; set; }

        // =========================
        // 🔹 NAVIGATION PROPERTIES
        // =========================

        [ForeignKey(nameof(AssignmentId))]
        public virtual Assignment? Assignment { get; set; }

        [ForeignKey(nameof(UploadedBy))]
        public virtual Employee? UploadedByEmployee { get; set; }

        [ForeignKey(nameof(ProjectId))]
        public virtual Project? Project { get; set; }

        [ForeignKey(nameof(ModuleId))]
        public virtual Module? Module { get; set; }

        // ✅ FIX: Explicit type to avoid Task conflict
        [ForeignKey(nameof(TaskId))]
        public virtual GPMS.Models.Task? Task { get; set; }
    }
}