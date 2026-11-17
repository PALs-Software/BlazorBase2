using BlazorBase.User.Models;
using Microsoft.AspNetCore.Components;

namespace BlazorBase.User.Components.Invitations;

/// <summary>The configuration <see cref="InviteUserDialog"/> is opened with.</summary>
public sealed class InviteUserDialogContent
{
    /// <summary>The roles to choose from, usually those of the application's <c>IUserRoleProvider</c>.</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>The role selected when the dialog opens; the first of <see cref="Roles"/> when empty.</summary>
    public string? DefaultRole { get; init; }

    /// <summary>Sent as <see cref="CreateUserInvitationRequest.Language"/> with every invitation.</summary>
    public string? Language { get; init; }

    /// <summary>
    /// Optional application fields rendered below the standard ones; they write
    /// <see cref="CreateUserInvitationRequest.Context"/> of the request they receive.
    /// </summary>
    public RenderFragment<CreateUserInvitationRequest>? ContextContent { get; init; }
}
