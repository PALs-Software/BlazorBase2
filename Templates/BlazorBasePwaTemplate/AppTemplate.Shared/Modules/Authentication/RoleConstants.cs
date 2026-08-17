namespace AppTemplate.Shared.Modules.Authentication;

/// <summary>
/// The role names used across client and server. Kept in the shared project so both
/// sides authorize against the same literals.
/// </summary>
public static class RoleConstants
{
    public const string Admin = "Admin";

    public const string User = "User";

    public static IReadOnlyList<string> All { get; } = [Admin, User];
}
