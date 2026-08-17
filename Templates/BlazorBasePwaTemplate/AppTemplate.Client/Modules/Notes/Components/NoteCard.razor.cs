using AppTemplate.Shared.Modules.Notes.Entities;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace AppTemplate.Client.Modules.Notes.Components;

/// <summary>
/// Declarative detail card for a <see cref="Note"/>, linked to <see cref="Pages.NotesPage"/> via
/// the BaseList <c>CardType</c> seam — the pattern to copy for the first real module's card.
/// </summary>
public partial class NoteCard : ComponentBase
{
    [Parameter]
    public Note Model { get; set; } = new();

    [Parameter]
    public IBaseDataProvider<Note>? DataProvider { get; set; }

    [Parameter]
    public IStringLocalizer? Localizer { get; set; }

    [Parameter]
    public BaseCardConfiguration<Note>? Configuration { get; set; }

    [Parameter]
    public EventCallback<Note> OnAfterSave { get; set; }

    [Parameter]
    public EventCallback OnCancelled { get; set; }
}
