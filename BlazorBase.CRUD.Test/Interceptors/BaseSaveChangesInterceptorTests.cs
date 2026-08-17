using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Interceptors;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.CRUD.Test.Interceptors;

public class BaseSaveChangesInterceptorTests
{
    [Fact]
    public async Task AddedAuditEntity_GetsCreatedFieldsPopulated()
    {
        using var db = CreateDatabase("user-1");
        var id = Guid.NewGuid();

        await using (var ctx = db.CreateContext())
        {
            ctx.Orders.Add(new TestOrder { Id = id, CustomerName = "A", Total = 1 });
            await ctx.SaveChangesAsync();
        }

        await using var verify = db.CreateContext();
        var order = await verify.Orders.FindAsync(id);
        Assert.NotNull(order);
        Assert.Equal("user-1", order!.CreatedBy);
        Assert.NotEqual(default, order.CreatedOn);
        Assert.Null(order.ModifiedOn);
    }

    [Fact]
    public async Task ModifiedAuditEntity_GetsModifiedFieldsPopulated()
    {
        using var db = CreateDatabase("user-1");
        var id = Guid.NewGuid();

        await using (var ctx = db.CreateContext())
        {
            ctx.Orders.Add(new TestOrder { Id = id, CustomerName = "A", Total = 1 });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = db.CreateContext())
        {
            var order = await ctx.Orders.FindAsync(id);
            order!.CustomerName = "B";
            await ctx.SaveChangesAsync();
        }

        await using var verify = db.CreateContext();
        var updated = await verify.Orders.FindAsync(id);
        Assert.NotNull(updated!.ModifiedOn);
        Assert.Equal("user-1", updated.ModifiedBy);
    }

    [Fact]
    public async Task WithoutAuditUserProvider_LeavesUserNullButSetsTimestamp()
    {
        using var db = CreateDatabase(userId: null);
        var id = Guid.NewGuid();

        await using (var ctx = db.CreateContext())
        {
            ctx.Orders.Add(new TestOrder { Id = id, CustomerName = "A", Total = 1 });
            await ctx.SaveChangesAsync();
        }

        await using var verify = db.CreateContext();
        var order = await verify.Orders.FindAsync(id);
        Assert.Null(order!.CreatedBy);
        Assert.NotEqual(default, order.CreatedOn);
    }

    [Fact]
    public async Task NonAuditEntity_IsLeftUntouched()
    {
        using var db = CreateDatabase("user-1");
        var id = Guid.NewGuid();

        await using (var ctx = db.CreateContext())
        {
            ctx.Products.Add(new TestProduct { Id = id, Name = "P" });
            await ctx.SaveChangesAsync();
        }

        await using var verify = db.CreateContext();
        Assert.NotNull(await verify.Products.FindAsync(id));
    }

    [Fact]
    public async Task Modify_CannotTamperCreatedFields()
    {
        var userProvider = new MutableAuditUserProvider("creator");
        using var db = CreateDatabase(userProvider);
        var id = Guid.NewGuid();

        await using (var ctx = db.CreateContext())
        {
            ctx.Orders.Add(new TestOrder { Id = id, CustomerName = "A", Total = 1 });
            await ctx.SaveChangesAsync();
        }

        DateTime originalCreatedOn;
        await using (var readCtx = db.CreateContext())
        {
            var order = await readCtx.Orders.FindAsync(id);
            originalCreatedOn = order!.CreatedOn;
        }

        userProvider.UserId = "editor";

        await using (var ctx = db.CreateContext())
        {
            var order = await ctx.Orders.FindAsync(id);
            order!.CustomerName = "B";
            order.CreatedBy = "attacker";
            order.CreatedOn = DateTime.UtcNow.AddYears(-5);
            await ctx.SaveChangesAsync();
        }

        await using var verify = db.CreateContext();
        var updated = await verify.Orders.FindAsync(id);
        Assert.NotNull(updated);
        Assert.Equal("creator", updated!.CreatedBy);
        Assert.Equal(originalCreatedOn, updated.CreatedOn);
        Assert.Equal("editor", updated.ModifiedBy);
        Assert.NotNull(updated.ModifiedOn);
    }

    [Fact]
    public async Task Add_ClearsClientSuppliedModifiedFields()
    {
        using var db = CreateDatabase("user-1");
        var id = Guid.NewGuid();

        await using (var ctx = db.CreateContext())
        {
            ctx.Orders.Add(new TestOrder
            {
                Id = id,
                CustomerName = "A",
                Total = 1,
                ModifiedOn = DateTime.UtcNow.AddDays(-1),
                ModifiedBy = "attacker"
            });
            await ctx.SaveChangesAsync();
        }

        await using var verify = db.CreateContext();
        var order = await verify.Orders.FindAsync(id);
        Assert.NotNull(order);
        Assert.Null(order!.ModifiedOn);
        Assert.Null(order.ModifiedBy);
        Assert.Equal("user-1", order.CreatedBy);
        Assert.NotEqual(default, order.CreatedOn);
    }

    private static SqliteInMemoryDatabase CreateDatabase(string? userId)
    {
        var services = new ServiceCollection();

        if (userId is not null)
            services.AddSingleton<IAuditUserProvider>(new StubAuditUserProvider(userId));

        var serviceProvider = services.BuildServiceProvider();
        var interceptor = new BaseSaveChangesInterceptor(serviceProvider);

        return new SqliteInMemoryDatabase(interceptor);
    }

    private static SqliteInMemoryDatabase CreateDatabase(IAuditUserProvider auditUserProvider)
    {
        var services = new ServiceCollection();
        services.AddSingleton(auditUserProvider);

        var serviceProvider = services.BuildServiceProvider();
        var interceptor = new BaseSaveChangesInterceptor(serviceProvider);

        return new SqliteInMemoryDatabase(interceptor);
    }

    private sealed class MutableAuditUserProvider(string? userId) : IAuditUserProvider
    {
        public string? UserId { get; set; } = userId;

        public string? GetCurrentUserId()
        {
            return UserId;
        }
    }
}
