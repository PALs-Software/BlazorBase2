using System.ComponentModel.DataAnnotations;
using BlazorBase.CRUD.Attributes;
using BlazorBase.CRUD.Core;

namespace BlazorBase.User.Models;

/// <summary>
/// The framework user CRUD model. Derives from <see cref="AuditModel"/> so the generic CRUD UI can
/// render audit/concurrency fields, and implements <see cref="IBaseUser"/> so components and the
/// user data provider operate against a stable contract. Apps may subclass this to add properties;
/// members are <c>virtual</c> for that reason. The wire shape (Id, DisplayName, Email, Role,
/// IsActive, CreatedAt, Password) is unchanged from the original DTO so existing consumers and the
/// user admin REST endpoints keep working.
/// </summary>
[BaseCrud("users")]
[CrudAccess("Admin", "RIMD")]
public class UserModel : AuditModel, IBaseUser
{
    public virtual string Id { get; set; } = string.Empty;

    /// <summary>
    /// The account's human-readable identity. Marked as the display key so a card header reads
    /// "Jane Doe" rather than the account's GUID, which is what the fallback to the primary key
    /// produced. A new account has no display name yet, and the header falls back to "add".
    /// </summary>
    [DisplayKey]
    [Required, MaxLength(100)]
    public virtual string DisplayName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public virtual string Email { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    [UserRoleInput]
    public virtual string Role { get; set; } = string.Empty;

    public virtual bool IsActive { get; set; } = true;

    public virtual DateTime CreatedAt { get; set; }

    [UserPasswordInput]
    public virtual string? Password { get; set; }
}
