using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BlazorBase.Speech.Server.Test.Infrastructure;

/// <summary>Real resource-backed localizers, so the tests also prove the .resx files are embedded.</summary>
public static class Localizers
{
    public static IStringLocalizer<T> For<T>()
        => new StringLocalizer<T>(new ResourceManagerStringLocalizerFactory(
            Options.Create(new LocalizationOptions()),
            NullLoggerFactory.Instance));
}
