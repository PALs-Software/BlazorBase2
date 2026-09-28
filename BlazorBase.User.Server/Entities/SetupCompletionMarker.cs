namespace BlazorBase.User.Server.Entities;

/// <summary>
/// A single row with a fixed primary key, inserted the moment the first admin account is created.
/// </summary>
/// <remarks>
/// <see cref="Controllers.AuthControllerBase{TUser}.Setup"/> cannot rely on
/// <c>UserManager.Users.AnyAsync()</c> alone to decide whether the system is already set up: two
/// concurrent requests can both observe no users and both proceed to create one. Inserting this row
/// inside the same transaction turns that race into a primary-key violation - exactly one request's
/// insert can succeed, and the constraint is enforced by the database regardless of how many
/// application instances are running.
/// </remarks>
public class SetupCompletionMarker
{
    /// <summary>The only value ever inserted; its uniqueness is the whole point of the row.</summary>
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
}
