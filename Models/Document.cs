using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GPMS.Models;

[Table("Document")]
public partial class Document
{
    [Key]
    [Column("document_id")]
    public int DocumentId { get; set; }

    [Column("assignment_id")]
    public int? AssignmentId { get; set; }

    [Column("document_name")]
    [StringLength(100)]
    [Unicode(false)]
    public string DocumentName { get; set; } = null!;

    [ForeignKey("AssignmentId")]
    [InverseProperty("Documents")]
    public virtual Assignment? Assignment { get; set; }
}
