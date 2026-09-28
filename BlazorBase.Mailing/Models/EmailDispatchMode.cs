namespace BlazorBase.Mailing.Models;

/// <summary>
/// Selects how <see cref="IEmailDispatcher"/> hands a message off to <see cref="IEmailSender"/>.
/// </summary>
public enum EmailDispatchMode
{
    /// <summary>
    /// The send happens on a background worker, off the calling request. A failed send never
    /// surfaces to the caller of <see cref="IEmailDispatcher.DispatchAsync"/> as an exception or
    /// a slow response; it only shows up in the worker's logs. This is the default because a
    /// caller that forgets to ask for it should still get the safe behaviour.
    /// </summary>
    Background,

    /// <summary>
    /// The send happens inline, awaited by the caller. A failed send propagates as an exception
    /// and the send's latency is part of the caller's response time. Opt into this only when a
    /// host genuinely needs delivery to be confirmed, or its failure reported, synchronously.
    /// </summary>
    Inline
}
