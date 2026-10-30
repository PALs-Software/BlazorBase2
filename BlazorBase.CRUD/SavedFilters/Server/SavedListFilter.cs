using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.CRUD.SavedFilters.Server;

/// <summary>
/// A filter a user saved for a list. One row per user, list and name; at most one row per user and list is the default.
/// </summary>
/// <remarks>
/// The user is stored as the authenticated identifier string. The non-generic
/// <see cref="SavedListFilterEntityConfiguration.ApplySavedListFilterConfiguration"/> adds no foreign key, so the entity works
/// with any user table; the generic overload adds a cascading foreign key to the host's user entity.
/// </remarks>
[Table("SavedListFilters")]
[Index(nameof(UserId), nameof(ListKey), nameof(Name), IsUnique = true)]
public class SavedListFilter
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = String.Empty;

    [Required]
    [MaxLength(ListFilterValidator.MaxListKeyLength)]
    public string ListKey { get; set; } = String.Empty;

    [Required]
    [MaxLength(ListFilterValidator.MaxNameLength)]
    public string Name { get; set; } = String.Empty;

    /// <summary>
    /// The filter tree, serialized by <see cref="ListFilterSerializer"/>.
    /// </summary>
    [Required]
    public string FilterJson { get; set; } = String.Empty;

    public bool IsDefault { get; set; }

    public DateTime CreatedOn { get; set; }

    public DateTime ModifiedOn { get; set; }
}
