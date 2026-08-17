using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace AppTemplate.Client.Pages;

public partial class NotFoundPage(IStringLocalizer<NotFoundPage> localizer) : ComponentBase
{
    #region Injects
    private readonly IStringLocalizer<NotFoundPage> Localizer = localizer;
    #endregion
}
