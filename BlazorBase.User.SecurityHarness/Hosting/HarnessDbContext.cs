using BlazorBase.User.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.User.SecurityHarness.Hosting;

/// <summary>Concrete identity <see cref="BaseUserDbContext{TUser}"/> backing each profile's own SQLite database.</summary>
public sealed class HarnessDbContext(DbContextOptions options) : BaseUserDbContext<HarnessUser>(options);
