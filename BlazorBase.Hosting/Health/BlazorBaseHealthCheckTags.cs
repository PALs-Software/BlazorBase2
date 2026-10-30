namespace BlazorBase.Hosting.Health;

/// <summary>
/// The tags that decide which endpoint a health check is part of. Register an additional readiness
/// check - for example a blob container probe of a storage provider - with <see cref="Ready"/>.
/// </summary>
public static class BlazorBaseHealthCheckTags
{
    /// <summary>Process-only checks: answer "is the process able to respond", never touch a dependency.</summary>
    public const string Live = "blazorbase-live";

    /// <summary>Dependency checks: answer "can this instance serve traffic" (database, storage, ...).</summary>
    public const string Ready = "blazorbase-ready";
}
