using BlazorBase.Mailing.Services;
using BlazorBase.User.Server.Configuration;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.Invitations.Configuration;
using BlazorBase.User.Server.Invitations.Mail;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlazorBase.User.Server.Invitations;

/// <summary>Registers the opt-in invitation flow.</summary>
public static class BlazorBaseUserInvitationServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="UserInvitationService{TUser}"/>, <see cref="UserInvitationOptions"/> bound from the
    /// <see cref="UserInvitationOptions.SectionName"/> section (validated at startup), and the
    /// <see cref="IUserInvitationEmailSender"/>: <see cref="MailingUserInvitationEmailSender"/> when
    /// <c>AddBlazorBaseMailing</c> is registered (in either call order), otherwise
    /// <see cref="NoOpUserInvitationEmailSender"/>. Requires <c>AddBlazorBaseUserServer</c>. Map the API with
    /// <c>app.MapBlazorBaseUserInvitationEndpoints&lt;TUser&gt;()</c>; the host adds a migration for the
    /// <see cref="UserInvitation"/> table.
    /// </summary>
    /// <param name="configureOptions">Applied after the configuration section, for values computed in code.</param>
    public static IServiceCollection AddBlazorBaseUserInvitations<TUser>(
        this IServiceCollection services,
        Action<UserInvitationOptions>? configureOptions = null)
        where TUser : BaseUser, new()
    {
        var optionsBuilder = services.AddOptions<UserInvitationOptions>().BindConfiguration(UserInvitationOptions.SectionName);

        if (configureOptions is not null)
            optionsBuilder.Configure(configureOptions);

        optionsBuilder
            .Validate(options => options.LifetimeDays >= 1, $"{UserInvitationOptions.SectionName}:LifetimeDays must be at least 1.")
            .ValidateOnStart();

        services.AddOptions<AccountManagementOptions>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped(CreateEmailSender);
        services.TryAddScoped<UserInvitationService<TUser>>();

        return services;
    }

    /// <summary>
    /// Registers an <see cref="IUserInvitationHandler{TUser}"/>. Handlers run in registration order; call once
    /// per handler type.
    /// </summary>
    public static IServiceCollection AddUserInvitationHandler<TUser, THandler>(this IServiceCollection services)
        where TUser : BaseUser
        where THandler : class, IUserInvitationHandler<TUser>
    {
        services.AddScoped<IUserInvitationHandler<TUser>, THandler>();
        return services;
    }

    /// <summary>Decided per scope rather than at registration, so the call order relative to <c>AddBlazorBaseMailing</c> does not matter.</summary>
    private static IUserInvitationEmailSender CreateEmailSender(IServiceProvider serviceProvider)
    {
        var mailingIsRegistered = serviceProvider.GetService<IEmailDispatcher>() is not null
            && serviceProvider.GetService<IAccountEmailBuilder>() is not null;

        return mailingIsRegistered
            ? ActivatorUtilities.CreateInstance<MailingUserInvitationEmailSender>(serviceProvider)
            : ActivatorUtilities.CreateInstance<NoOpUserInvitationEmailSender>(serviceProvider);
    }
}
