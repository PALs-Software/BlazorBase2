using BlazorBase.CRUD.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.CRUD.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor that automatically populates <see cref="AuditModel"/> fields.
/// Data interceptor events are fired by <see cref="DbContextBaseDataProvider{TEntity, TModel}"/> only.
/// </summary>
public class BaseSaveChangesInterceptor(IServiceProvider serviceProvider) : SaveChangesInterceptor, IBaseDbInterceptor
{
    #region Injects

    private readonly IServiceProvider ServiceProvider = serviceProvider;

    #endregion

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            PopulateAuditFields(eventData.Context);

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void PopulateAuditFields(DbContext dbContext)
    {
        dbContext.ChangeTracker.DetectChanges();

        var auditUserProvider = ServiceProvider.GetService<IAuditUserProvider>();
        var userId = auditUserProvider?.GetCurrentUserId();
        var now = DateTime.UtcNow;

        foreach (var entry in dbContext.ChangeTracker.Entries())
        {
            if (entry.Entity is not AuditModel audit)
                continue;

            switch (entry.State)
            {
                case EntityState.Added:
                    audit.CreatedOn = now;
                    audit.CreatedBy = userId;
                    audit.ModifiedOn = null;
                    audit.ModifiedBy = null;
                    break;

                case EntityState.Modified:
                    audit.ModifiedOn = now;
                    audit.ModifiedBy = userId;

                    var createdOn = entry.Property(nameof(AuditModel.CreatedOn));
                    createdOn.CurrentValue = createdOn.OriginalValue;
                    createdOn.IsModified = false;

                    var createdBy = entry.Property(nameof(AuditModel.CreatedBy));
                    createdBy.CurrentValue = createdBy.OriginalValue;
                    createdBy.IsModified = false;
                    break;
            }
        }
    }
}
