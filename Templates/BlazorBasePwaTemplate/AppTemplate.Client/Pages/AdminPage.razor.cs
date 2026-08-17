using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace AppTemplate.Client.Pages;

public partial class AdminPage(IStringLocalizer<AdminPage> localizer) : ComponentBase
{
    #region Injects
    private readonly IStringLocalizer<AdminPage> Localizer = localizer;
    #endregion
}
