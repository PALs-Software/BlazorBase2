namespace BlazorBase.Hosting.BackgroundJobs;

/// <summary>Bound from the <c>BackgroundJobs</c> configuration section.</summary>
public sealed class BackgroundJobOptions
{
    public const string SectionName = "BackgroundJobs";

    /// <summary>Whether background jobs run on this instance. Defaults to <see langword="true"/>.</summary>
    public bool Enabled { get; set; } = true;
}
