namespace BlazorBase.User.Models;

/// <summary>
/// Generic contract for a user record that the BlazorBase CRUD UI (<c>BaseList</c>/<c>BaseCard</c>)
/// and the user-management data provider operate on. Implemented by <see cref="UserModel"/> and any
/// app-specific subclass. The members mirror the wire shape exposed by the user admin REST endpoints
/// so the same model travels unchanged between client and server.
/// </summary>
public interface IBaseUser
{
    /// <summary>The identity primary key of the user.</summary>
    string Id { get; set; }

    /// <summary>The display name shown in the UI.</summary>
    string DisplayName { get; set; }

    /// <summary>The login email address.</summary>
    string Email { get; set; }

    /// <summary>The single role name assigned to the user.</summary>
    string Role { get; set; }

    /// <summary>Whether the account is active.</summary>
    bool IsActive { get; set; }

    /// <summary>
    /// The write-only password channel: left blank keeps the current password, a non-blank value
    /// sets or resets it. Never populated when reading a user.
    /// </summary>
    string? Password { get; set; }
}
