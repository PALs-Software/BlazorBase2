using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AppTemplate.Server.Data;

/// <summary>
/// Lets the EF Core tools construct the context without booting the web host, so
/// design-time commands never touch the startup migration or need a reachable database.
/// </summary>
public class AppTemplateDbContextFactory : IDesignTimeDbContextFactory<AppTemplateDbContext>
{
    public AppTemplateDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<AppTemplateDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=localhost;Database=AppTemplate;Trusted_Connection=True;TrustServerCertificate=True";

        var optionsBuilder = new DbContextOptionsBuilder<AppTemplateDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new AppTemplateDbContext(optionsBuilder.Options);
    }
}
