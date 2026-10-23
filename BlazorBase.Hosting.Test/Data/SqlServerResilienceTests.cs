using BlazorBase.Hosting.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlazorBase.Hosting.Test.Data;

public sealed class SqlServerResilienceTests
{
    private const string ConnectionString = "Server=localhost;Database=BlazorBaseTest;Trusted_Connection=True;TrustServerCertificate=True";

    [Fact]
    public void WithRetry_UsesARetryingExecutionStrategy()
    {
        using var context = CreateContext(enableRetry: true);

        Assert.True(context.Database.CreateExecutionStrategy().RetriesOnFailure);
    }

    [Fact]
    public void WithoutRetry_KeepsTheDefaultExecutionStrategy()
    {
        using var context = CreateContext(enableRetry: false);

        Assert.False(context.Database.CreateExecutionStrategy().RetriesOnFailure);
    }

    [Fact]
    public void Configure_CanAdjustTheSqlServerOptions()
    {
        var invoked = false;
        var builder = new DbContextOptionsBuilder<HostingTestDbContext>()
            .UseBlazorBaseSqlServer(ConnectionString, enableRetry: true, _ => invoked = true);

        Assert.True(invoked);
        Assert.NotNull(builder);
    }

    [Fact]
    public void ConnectionString_IsRequired()
    {
        var builder = new DbContextOptionsBuilder<HostingTestDbContext>();

        Assert.Throws<ArgumentException>(() => builder.UseBlazorBaseSqlServer(" ", enableRetry: true));
    }

    private static HostingTestDbContext CreateContext(bool enableRetry)
    {
        var options = new DbContextOptionsBuilder<HostingTestDbContext>();
        options.UseBlazorBaseSqlServer(ConnectionString, enableRetry);
        return new HostingTestDbContext(options.Options);
    }
}
