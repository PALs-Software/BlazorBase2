using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace AppTemplate.Client.Layout;

public partial class AppBrand(IStringLocalizer<AppBrand> localizer) : ComponentBase
{
    #region Injects

    private readonly IStringLocalizer<AppBrand> Localizer = localizer;

    #endregion
}
