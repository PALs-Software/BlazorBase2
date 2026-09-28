using BlazorBase.User.Server.Entities;

namespace BlazorBase.User.SecurityHarness.Hosting;

/// <summary>
/// Concrete Identity user used to drive every harness host profile. Public because it appears in
/// <see cref="HarnessAuthController"/>'s constructor, which ASP.NET Core's controller discovery
/// requires to be public.
/// </summary>
public sealed class HarnessUser : BaseUser;
