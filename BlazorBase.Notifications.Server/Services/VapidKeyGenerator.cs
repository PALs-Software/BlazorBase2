using WebPush;

namespace BlazorBase.Notifications.Server.Services;

/// <summary>
/// Generates a VAPID key pair for <see cref="Configuration.WebPushOptions.PublicKey"/> and
/// <see cref="Configuration.WebPushOptions.PrivateKey"/>. Intended for one-off use while setting up
/// a host — run it once, store the result (for example in the host's gitignored
/// <c>appsettings.Development.json</c> or a secret store), and reuse the same pair for every
/// message afterwards; generating a new pair invalidates every subscription created under the old
/// one.
/// </summary>
public static class VapidKeyGenerator
{
    /// <summary>Creates a new, random VAPID key pair.</summary>
    public static (string PublicKey, string PrivateKey) Generate()
    {
        var keys = VapidHelper.GenerateVapidKeys();
        return (keys.PublicKey, keys.PrivateKey);
    }
}
