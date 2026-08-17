using AppTemplate.Server.Data;
using AppTemplate.Shared.Modules.Notes.Entities;
using BlazorBase.CRUD.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppTemplate.Tests.Data;

public class AppTemplateDbContextNoteTests
{
    private static AppTemplateDbContext CreateContext(string databaseName)
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var interceptor = new BaseSaveChangesInterceptor(serviceProvider);

        var options = new DbContextOptionsBuilder<AppTemplateDbContext>()
            .UseInMemoryDatabase(databaseName)
            .AddInterceptors(interceptor)
            .Options;

        return new AppTemplateDbContext(options);
    }

    [Fact]
    public async Task AddingANote_PersistsTitleAndContent_AndStampsCreatedOn()
    {
        var databaseName = Guid.NewGuid().ToString();

        await using (var writeContext = CreateContext(databaseName))
        {
            writeContext.Notes.Add(new Note { Title = "Shopping list", Content = "Milk, eggs, bread" });
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = CreateContext(databaseName);
        var stored = await readContext.Notes.SingleAsync(n => n.Title == "Shopping list");

        Assert.Equal("Milk, eggs, bread", stored.Content);
        Assert.NotEqual(default, stored.CreatedOn);
    }
}
