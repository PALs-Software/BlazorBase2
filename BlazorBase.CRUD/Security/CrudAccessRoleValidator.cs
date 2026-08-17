using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BlazorBase.CRUD.Attributes;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BlazorBase.CRUD.Security;

/// <summary>
/// Verifies at startup that every role referenced by <see cref="CrudAccessAttribute"/>
/// on a <see cref="BaseCrudAttribute"/>-marked entity actually exists in the Identity
/// role store. Throws <see cref="InvalidOperationException"/> with the unknown role names
/// when validation fails.
/// </summary>
/// <remarks>
/// Operates per assembly. Register one instance per assembly that contains entities
/// via <see cref="Extensions.BaseServiceCollectionExtensions"/>.
/// </remarks>
public sealed class CrudAccessRoleValidator<TRole>(
    IServiceProvider serviceProvider,
    Assembly entityAssembly
) : IHostedService where TRole : class
{
    private readonly IServiceProvider ServiceProvider = serviceProvider;
    private readonly Assembly EntityAssembly = entityAssembly;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var requiredRoles = CollectReferencedRoles(EntityAssembly);

        if (requiredRoles.Count == 0)
            return;

        using var scope = ServiceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetService<RoleManager<TRole>>();

        if (roleManager is null)
            throw new InvalidOperationException(
                $"CrudAccessRoleValidator<{typeof(TRole).Name}> requires RoleManager<{typeof(TRole).Name}> to be registered.");

        var missing = new List<string>();

        foreach (var role in requiredRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                missing.Add(role);
        }

        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"[CrudAccess] references unknown roles: {string.Join(", ", missing.OrderBy(r => r))}. " +
                $"Either create the roles in Identity or correct the attribute values.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static HashSet<string> CollectReferencedRoles(Assembly assembly)
    {
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var entityTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && t.GetCustomAttribute<BaseCrudAttribute>() is not null);

        foreach (var entityType in entityTypes)
            foreach (var role in CrudAccessResolver.CollectDeclaredRoles(entityType))
                roles.Add(role);

        return roles;
    }
}
