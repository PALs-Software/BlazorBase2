using AppTemplate.Client.Modules.Notes.Pages;
using AppTemplate.Shared.Modules.Notes.Entities;
using AppTemplate.Tests.Infrastructure;
using Bunit;
using Bunit.TestDoubles;
using BlazorBase.CRUD.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace AppTemplate.Tests.Modules.Notes;

public class NotesPageTests : BunitContext
{
    public NotesPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
        Services.AddLocalization();
        Services.AddSingleton<IBaseDataProvider<Note>>(new StubNoteDataProvider());

        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("test-user");
    }

    [Fact]
    public void Render_WithNoNotes_ShowsEmptyText()
    {
        var component = Render<NotesPage>();

        var localizer = Services.GetRequiredService<IStringLocalizer<NotesPage>>();
        Assert.Contains(localizer["EmptyText"].Value, component.Markup);
    }
}
