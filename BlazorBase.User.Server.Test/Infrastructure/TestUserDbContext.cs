using BlazorBase.User.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.User.Server.Test.Infrastructure;

/// <summary>Concrete identity DbContext over <see cref="TestUser"/> for the server-side tests.</summary>
public sealed class TestUserDbContext(DbContextOptions options) : BaseUserDbContext<TestUser>(options);
