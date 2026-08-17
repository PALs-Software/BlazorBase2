using System.Security.Claims;
using BlazorBase.Files.Models;
using BlazorBase.Files.Server.Services;
using BlazorBase.Files.Server.Test.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlazorBase.Files.Server.Test.Services;

public sealed class OwnerScopedFileAccessAuthorizerTests : IDisposable
{
    private readonly SqliteConnection Connection;
    private readonly TestFilesDbContext DbContext;
    private readonly OwnerScopedFileAccessAuthorizer Authorizer;
    private readonly Guid OwnedFileId = Guid.NewGuid();
    private readonly Guid UnscopedFileId = Guid.NewGuid();

    public OwnerScopedFileAccessAuthorizerTests()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        var options = new DbContextOptionsBuilder<TestFilesDbContext>()
            .UseSqlite(Connection)
            .Options;

        DbContext = new TestFilesDbContext(options);
        DbContext.Database.EnsureCreated();

        DbContext.Files.Add(new BaseFile
        {
            Id = OwnedFileId,
            FileName = "owned-file",
            Extension = ".txt",
            ContentType = "text/plain",
            OwnerScopeKey = "user-1",
        });
        DbContext.Files.Add(new BaseFile
        {
            Id = UnscopedFileId,
            FileName = "unscoped-file",
            Extension = ".txt",
            ContentType = "text/plain",
            OwnerScopeKey = null,
        });
        DbContext.SaveChanges();

        Authorizer = new OwnerScopedFileAccessAuthorizer(DbContext);
    }

    private static ClaimsPrincipal PrincipalFor(string userId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test"));

    private static ClaimsPrincipal AnonymousPrincipal() =>
        new(new ClaimsIdentity());

    [Fact]
    public async Task CanAccessAsync_ReturnsTrue_ForOwningUser()
    {
        var result = await Authorizer.CanAccessAsync(OwnedFileId, PrincipalFor("user-1"), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task CanAccessAsync_ReturnsFalse_ForNonOwningUser()
    {
        var result = await Authorizer.CanAccessAsync(OwnedFileId, PrincipalFor("user-2"), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task CanAccessAsync_ReturnsFalse_ForAnonymousPrincipal()
    {
        var result = await Authorizer.CanAccessAsync(OwnedFileId, AnonymousPrincipal(), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task CanAccessAsync_ReturnsFalse_ForNonExistentFile()
    {
        var result = await Authorizer.CanAccessAsync(Guid.NewGuid(), PrincipalFor("user-1"), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task CanAccessAsync_ReturnsFalse_ForUnscopedFile()
    {
        var result = await Authorizer.CanAccessAsync(UnscopedFileId, PrincipalFor("user-1"), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task CanAssignScopeAsync_ReturnsTrue_ForNullScope()
    {
        var result = await Authorizer.CanAssignScopeAsync(null, PrincipalFor("user-1"), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task CanAssignScopeAsync_ReturnsTrue_ForOwnScope()
    {
        var result = await Authorizer.CanAssignScopeAsync("user-1", PrincipalFor("user-1"), CancellationToken.None);

        Assert.True(result);
    }

    [Fact]
    public async Task CanAssignScopeAsync_ReturnsFalse_ForForeignScope()
    {
        var result = await Authorizer.CanAssignScopeAsync("user-2", PrincipalFor("user-1"), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public async Task CanAssignScopeAsync_ReturnsFalse_ForAnonymousPrincipal()
    {
        var result = await Authorizer.CanAssignScopeAsync(null, AnonymousPrincipal(), CancellationToken.None);

        Assert.False(result);
    }

    [Fact]
    public void ResolveDefaultOwnerScope_ReturnsCallerId_ForAuthenticatedPrincipal()
    {
        var result = Authorizer.ResolveDefaultOwnerScope(PrincipalFor("user-1"));

        Assert.Equal("user-1", result);
    }

    [Fact]
    public void ResolveDefaultOwnerScope_ReturnsNull_ForAnonymousPrincipal()
    {
        var result = Authorizer.ResolveDefaultOwnerScope(AnonymousPrincipal());

        Assert.Null(result);
    }

    public void Dispose()
    {
        DbContext.Dispose();
        Connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
