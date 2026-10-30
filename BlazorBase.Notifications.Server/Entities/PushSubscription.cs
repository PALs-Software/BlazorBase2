using System.ComponentModel.DataAnnotations;
using BlazorBase.CRUD.Core;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.Notifications.Server.Entities;

/// <summary>
/// One browser Push API subscription registered by a signed-in user on one device. The host owns
/// the <c>DbSet&lt;PushSubscription&gt;</c> and migration; see
/// <see cref="Data.PushSubscriptionEntityConfiguration.ApplyPushSubscriptionConfiguration"/>.
/// </summary>
[Index(nameof(Endpoint), IsUnique = true)]
[Index(nameof(UserId))]
public class PushSubscription : AuditModel
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>Identifier of the user this subscription delivers push notifications to.</summary>
    public Guid UserId { get; set; }

    /// <summary>The push service URL that uniquely identifies this subscription.</summary>
    [Required]
    [MaxLength(1000)]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>The subscription's P-256 Diffie-Hellman public key, base64url-encoded.</summary>
    [Required]
    [MaxLength(200)]
    public string P256dh { get; set; } = string.Empty;

    /// <summary>The subscription's authentication secret, base64url-encoded.</summary>
    [Required]
    [MaxLength(200)]
    public string Auth { get; set; } = string.Empty;

    /// <summary>Free-text identifying the browser/device, shown in the user's own settings list.</summary>
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// Whether native push delivery is currently switched on for this device. <see cref="Services.IPushNotifier"/>
    /// only sends to subscriptions where this is <see langword="true"/>.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
