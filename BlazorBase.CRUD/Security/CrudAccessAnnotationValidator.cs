using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using BlazorBase.CRUD.Attributes;
using Microsoft.Extensions.Hosting;

namespace BlazorBase.CRUD.Security;

/// <summary>
/// Verifies at startup that every entity decorated with <see cref="BaseCrudAttribute"/> also
/// carries at least one class-level <see cref="CrudAccessAttribute"/>. Throws
/// <see cref="InvalidOperationException"/> naming the unannotated entities when validation fails.
/// </summary>
/// <remarks>
/// Pairs with <see cref="CrudAccessResolver.StrictMode"/> but is independent of it: strict mode
/// changes what an unannotated entity grants at runtime (nothing, instead of everything); this
/// validator instead refuses to let the application start at all when one is found. A host may
/// enable either, both, or neither — see
/// <see cref="Extensions.BaseServiceCollectionExtensions.AddCrudAccessStrictMode"/>.
/// </remarks>
public sealed class CrudAccessAnnotationValidator(Assembly entityAssembly) : IHostedService
{
    private readonly Assembly EntityAssembly = entityAssembly;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var unannotated = FindUnannotatedEntities(EntityAssembly.GetTypes());

        if (unannotated.Length > 0)
            throw new InvalidOperationException(
                $"[BaseCrud] entities without a class-level [CrudAccess]: {string.Join(", ", unannotated)}. " +
                $"Add [CrudAccess] to declare who may reach the generated endpoints.");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Exposed internally so tests can validate a hand-picked type list without depending
    /// on every other [BaseCrud] entity that happens to share an assembly.</summary>
    internal static string[] FindUnannotatedEntities(IEnumerable<Type> candidateTypes)
    {
        return candidateTypes
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && t.GetCustomAttribute<BaseCrudAttribute>() is not null
                && CrudAccessResolver.GetClassRules(t).Count == 0)
            .Select(t => t.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
    }
}
