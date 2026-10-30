namespace BlazorBase.Hosting.BackgroundJobs;

/// <summary>
/// Tells hosts and hosted services whether this instance should run background jobs. A staging slot or
/// a test site turns jobs off with <c>BackgroundJobs:Enabled=false</c> while it still serves requests.
/// </summary>
public interface IBackgroundJobGate
{
    /// <summary><see langword="true"/> when background jobs may run; <see langword="true"/> unless configured otherwise.</summary>
    bool IsEnabled { get; }
}
