namespace BlazorBase.User.Server.Test.Infrastructure;

/// <summary>
/// A <see cref="TimeProvider"/> a test advances by hand, so refresh-token expiry and rotation can be
/// proven without a real wall-clock wait.
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan delta) => _utcNow += delta;
}
