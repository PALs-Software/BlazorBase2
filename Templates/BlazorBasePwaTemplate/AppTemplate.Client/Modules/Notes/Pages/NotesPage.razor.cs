using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace AppTemplate.Client.Modules.Notes.Pages;

public partial class NotesPage(IStringLocalizer<NotesPage> localizer) : ComponentBase
{
    #region Injects
    private readonly IStringLocalizer<NotesPage> Localizer = localizer;
    #endregion
}
