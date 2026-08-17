using AppTemplate.Server.Data;
using AppTemplate.Server.Entities;
using AppTemplate.Server.Modules.Authentication.Services;
using AppTemplate.Shared.Modules.Authentication;
using AppTemplate.Shared.Modules.Notes.Entities;
using BlazorBase.CRUD.Extensions;
using BlazorBase.DataProtection;
using BlazorBase.User.Server.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
    builder.Configuration.AddJsonFile("/app/appsettings/appsettings.json");

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

builder.Services.AddBaseDbContext<AppTemplateDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddBlazorBaseDataProtection(dataProtection => dataProtection.SetApplicationName("AppTemplate"));

builder.Services.AddBlazorBaseUserServer<AppUser, AppTemplateDbContext>(builder.Configuration);

builder.Services.AddBlazorBaseDevelopmentAuthentication<AppUser>(builder.Configuration, builder.Environment);

builder.Services.AddBlazorBaseCrud<HttpContextAuditUserProvider>();
builder.Services.AddBlazorBaseCrudServer<AppTemplateDbContext>(typeof(Note).Assembly);

builder.Services.AddLocalization();
builder.Services.AddControllers();

var app = builder.Build();

app.Services.UseBlazorBaseDataProtection();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapStaticAssets();

app.MapControllers();
app.MapBlazorBaseCrudEndpoints(typeof(Note).Assembly);
app.MapBlazorBaseUserAdminEndpoints();
app.MapFallbackToFile("index.html");

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppTemplateDbContext>();
    await dbContext.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    foreach (var roleName in RoleConstants.All)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
            await roleManager.CreateAsync(new IdentityRole(roleName));
    }
}

app.Run();
