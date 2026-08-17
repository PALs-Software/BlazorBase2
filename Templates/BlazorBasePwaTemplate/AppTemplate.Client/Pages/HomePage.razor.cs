using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace AppTemplate.Client.Pages;

public partial class HomePage(IStringLocalizer<HomePage> localizer) : ComponentBase
{
    #region Injects
    private readonly IStringLocalizer<HomePage> Localizer = localizer;
    #endregion
}
