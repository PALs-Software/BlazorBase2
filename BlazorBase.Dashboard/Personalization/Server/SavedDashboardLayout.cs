using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.Dashboard.Server;

/// <summary>
/// The layout a user arranged for a dashboard. One row per user and dashboard key.
/// </summary>
/// <remarks>
/// The user is stored as the authenticated identifier string. The non-generic
/// <see cref="SavedDashboardLayoutEntityConfiguration.ApplySavedDashboardLayoutConfiguration"/> adds no foreign key, so the entity
/// works with any user table; the generic overload adds a cascading foreign key to the host's user entity.
/// </remarks>
[Table("SavedDashboardLayouts")]
[Index(nameof(UserId), nameof(DashboardKey), IsUnique = true)]
public class SavedDashboardLayout
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = String.Empty;

    [Required]
    [MaxLength(DashboardLayoutValidator.MaxKeyLength)]
    public string DashboardKey { get; set; } = String.Empty;

    /// <summary>
    /// The part preferences, serialized by <see cref="DashboardLayoutSerializer"/>.
    /// </summary>
    [Required]
    public string LayoutJson { get; set; } = String.Empty;

    public DateTime CreatedOn { get; set; }

    public DateTime ModifiedOn { get; set; }
}
