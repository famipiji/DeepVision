using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace iVault.Api.Models
{
    [Table("records")]
    public class Record
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("record_definition_id")]
        public Guid RecordDefinitionId { get; set; }

        [Required]
        [Column("metadata", TypeName = "jsonb")]
        public string Metadata { get; set; } = "{}";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        [ForeignKey("RecordDefinitionId")]
        public RecordDefinition? Definition { get; set; }
    }
}