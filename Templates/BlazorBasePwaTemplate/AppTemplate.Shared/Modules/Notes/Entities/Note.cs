using System.ComponentModel.DataAnnotations;
using BlazorBase.CRUD.Attributes;
using BlazorBase.CRUD.Core;

namespace AppTemplate.Shared.Modules.Notes.Entities;

/// <summary>
/// Minimal demo entity showing the generic BaseList/BaseCard CRUD pattern end to end —
/// the pattern to copy when adding the first real module to a project built from this template.
/// </summary>
[BaseCrud("notes")]
public class Note : AuditModel
{
    [Key]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [DisplayKey]
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Content { get; set; }
}
