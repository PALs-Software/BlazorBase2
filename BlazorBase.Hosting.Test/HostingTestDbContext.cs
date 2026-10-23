using Microsoft.EntityFrameworkCore;

namespace BlazorBase.Hosting.Test;

public sealed class HostingTestDbContext(DbContextOptions<HostingTestDbContext> options) : DbContext(options);
