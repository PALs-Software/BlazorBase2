using System.Reflection;
using BlazorBase.User.Pages;
using Microsoft.Extensions.Localization;

namespace AppTemplate.Client;

public partial class App(IStringLocalizer<App> localizer)
{
    #region Injects
    private readonly IStringLocalizer<App> Localizer = localizer;
    #endregion

    private static readonly Assembly[] AdditionalAssemblies = [typeof(Login).Assembly];
}
