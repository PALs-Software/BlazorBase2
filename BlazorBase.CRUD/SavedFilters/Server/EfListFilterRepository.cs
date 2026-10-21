using Microsoft.EntityFrameworkCore;

namespace BlazorBase.CRUD.SavedFilters.Server;

/// <summary>
/// EF Core implementation of <see cref="IListFilterRepository"/> over <see cref="SavedListFilter"/>. Every query is
/// restricted to the given user, so another user's rows are neither visible nor reachable by id.
/// </summary>
public sealed class EfListFilterRepository<TContext>(IDbContextFactory<TContext> contextFactory) : IListFilterRepository
    where TContext : DbContext
{
    #region Injects

    private readonly IDbContextFactory<TContext> ContextFactory = contextFactory;

    #endregion

    public async Task<IReadOnlyList<SavedListFilterInfo>> GetAsync(string userId, string listKey, CancellationToken cancellationToken = default)
    {
        var key = ListFilterValidator.ValidateListKey(listKey);
        await using var context = await ContextFactory.CreateDbContextAsync(cancellationToken);

        var rows = await context.Set<SavedListFilter>()
            .AsNoTracking()
            .Where(filter => filter.UserId == userId && filter.ListKey == key)
            .OrderBy(filter => filter.Name)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(ToInfo)];
    }

    public async Task<SavedListFilterInfo> SaveAsync(string userId, string listKey, SavedListFilterInput input, CancellationToken cancellationToken = default)
    {
        var key = ListFilterValidator.ValidateListKey(listKey);
        var name = ListFilterValidator.NormalizeName(input.Name);
        ListFilterValidator.ValidateFilter(input.Filter);

        await using var context = await ContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = context.Set<SavedListFilter>();

        var ownRows = await rows
            .Where(filter => filter.UserId == userId && filter.ListKey == key)
            .ToListAsync(cancellationToken);

        var sameName = ownRows.FirstOrDefault(filter => String.Equals(filter.Name, name, StringComparison.OrdinalIgnoreCase));
        var target = FindTarget(ownRows, input, sameName);

        if (target is null)
        {
            if (ownRows.Count >= ListFilterValidator.MaxFiltersPerList)
                throw new ListFilterValidationException($"At most {ListFilterValidator.MaxFiltersPerList} filters can be saved per list.");

            var now = DateTime.UtcNow;
            target = new SavedListFilter { Id = Guid.NewGuid(), UserId = userId, ListKey = key, CreatedOn = now };
            rows.Add(target);
            ownRows.Add(target);
        }

        target.Name = name;
        target.FilterJson = ListFilterSerializer.Serialize(input.Filter);
        target.ModifiedOn = DateTime.UtcNow;

        if (input.SetAsDefault)
            MarkDefault(ownRows, target.Id);

        await context.SaveChangesAsync(cancellationToken);
        return ToInfo(target);
    }

    public async Task<bool> DeleteAsync(string userId, string listKey, Guid id, CancellationToken cancellationToken = default)
    {
        var key = ListFilterValidator.ValidateListKey(listKey);
        await using var context = await ContextFactory.CreateDbContextAsync(cancellationToken);

        var ownRows = await context.Set<SavedListFilter>()
            .Where(filter => filter.Id == id && filter.UserId == userId && filter.ListKey == key)
            .ToListAsync(cancellationToken);

        if (ownRows.Count == 0)
            return false;

        context.Set<SavedListFilter>().RemoveRange(ownRows);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SetDefaultAsync(string userId, string listKey, Guid? id, CancellationToken cancellationToken = default)
    {
        var key = ListFilterValidator.ValidateListKey(listKey);
        await using var context = await ContextFactory.CreateDbContextAsync(cancellationToken);

        var ownRows = await context.Set<SavedListFilter>()
            .Where(filter => filter.UserId == userId && filter.ListKey == key)
            .ToListAsync(cancellationToken);

        if (id is { } wanted && ownRows.All(filter => filter.Id != wanted))
            return false;

        MarkDefault(ownRows, id);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static SavedListFilter? FindTarget(List<SavedListFilter> ownRows, SavedListFilterInput input, SavedListFilter? sameName)
    {
        if (input.Id is not { } id)
            return sameName;

        var byId = ownRows.FirstOrDefault(filter => filter.Id == id)
            ?? throw new KeyNotFoundException("The saved filter no longer exists.");

        if (sameName is not null && sameName.Id != byId.Id)
            throw new ListFilterNameConflictException(sameName.Name);

        return byId;
    }

    private static void MarkDefault(List<SavedListFilter> ownRows, Guid? id)
    {
        foreach (var filter in ownRows)
            filter.IsDefault = filter.Id == id;
    }

    private static SavedListFilterInfo ToInfo(SavedListFilter row) => new()
    {
        Id = row.Id,
        Name = row.Name,
        Filter = ListFilterSerializer.Deserialize(row.FilterJson),
        IsDefault = row.IsDefault,
        ModifiedOn = DateTime.SpecifyKind(row.ModifiedOn, DateTimeKind.Utc)
    };
}
