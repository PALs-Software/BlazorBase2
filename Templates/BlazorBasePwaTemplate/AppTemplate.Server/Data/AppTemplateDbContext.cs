using AppTemplate.Server.Entities;
using AppTemplate.Shared.Modules.Notes.Entities;
using BlazorBase.User.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace AppTemplate.Server.Data;

public class AppTemplateDbContext(DbContextOptions<AppTemplateDbContext> options)
    : BaseUserDbContext<AppUser>(options)
{
    public DbSet<Note> Notes => Set<Note>();
}
