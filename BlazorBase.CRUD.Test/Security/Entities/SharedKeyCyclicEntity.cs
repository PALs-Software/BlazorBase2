namespace BlazorBase.CRUD.Test.Security.Entities;

/// <summary>
/// Simulates the exact mechanism behind the team-lead/own-team stack overflow: an EF Core
/// lazy-loading proxy reached through an <c>AsNoTracking()</c> root query, where every hop around a
/// navigation cycle materializes a brand new, non-identity-resolved CLR instance for the same
/// database row. <see cref="Related"/> reproduces that by handing back a freshly allocated instance
/// with the same <see cref="Id"/> on every access, up to <see cref="MaxFreshInstances"/> (a safety
/// bound so a regression - the cycle guard failing to stop the walk - fails the assertion instead of
/// overflowing the test process's own stack).
/// </summary>
public class SharedKeyCyclicEntity
{
    public const int MaxFreshInstances = 200;

    public static int RelatedAccessCount;

    public int Id { get; set; }

    private int Depth { get; set; }

    public SharedKeyCyclicEntity? Related
    {
        get
        {
            RelatedAccessCount++;
            return Depth >= MaxFreshInstances ? null : new SharedKeyCyclicEntity { Id = Id, Depth = Depth + 1 };
        }
    }
}
