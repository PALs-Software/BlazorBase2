using BlazorBase.CRUD.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Localization;

public class LocalizerResolverTests
{
    private abstract class AuditLike
    {
        public DateTime CreatedOn { get; set; }
    }

    private sealed class Thing : AuditLike
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class DictionaryLocalizer(Dictionary<string, string> values) : IStringLocalizer
    {
        private readonly Dictionary<string, string> Values = values;

        public LocalizedString this[string name] =>
            Values.TryGetValue(name, out var value)
                ? new LocalizedString(name, value, resourceNotFound: false)
                : new LocalizedString(name, name, resourceNotFound: true);

        public LocalizedString this[string name, params object[] arguments] => this[name];

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            Values.Select(pair => new LocalizedString(pair.Key, pair.Value, resourceNotFound: false));
    }

    private static IServiceProvider ServicesWith(IStringLocalizerFactory factory)
    {
        var services = new ServiceCollection();
        services.AddSingleton(factory);
        return services.BuildServiceProvider();
    }

    private static IServiceProvider TypeChainServices()
    {
        var thingLocalizer = new DictionaryLocalizer(new() { ["Name"] = "Bezeichnung" });
        var auditLocalizer = new DictionaryLocalizer(new() { ["CreatedOn"] = "Erstellt am" });

        var factory = Substitute.For<IStringLocalizerFactory>();
        factory.Create(typeof(Thing)).Returns(thingLocalizer);
        factory.Create(typeof(AuditLike)).Returns(auditLocalizer);

        return ServicesWith(factory);
    }

    [Fact]
    public void ResolveProperty_ResolvesOwnProperty_FromModelLocalizer()
    {
        var localizer = LocalizerResolver.ResolveProperty(TypeChainServices(), param: null, typeof(Thing));

        Assert.Equal("Bezeichnung", localizer["Name"].Value);
    }

    [Fact]
    public void ResolveProperty_FallsBackToBaseType_ForInheritedProperty()
    {
        var localizer = LocalizerResolver.ResolveProperty(TypeChainServices(), param: null, typeof(Thing));

        var result = localizer["CreatedOn"];

        Assert.False(result.ResourceNotFound);
        Assert.Equal("Erstellt am", result.Value);
    }

    [Fact]
    public void ResolveProperty_ParamLocalizer_TakesPrecedence()
    {
        var param = new DictionaryLocalizer(new() { ["Name"] = "Override" });

        var localizer = LocalizerResolver.ResolveProperty(TypeChainServices(), param, typeof(Thing));

        Assert.Equal("Override", localizer["Name"].Value);
    }

    [Fact]
    public void ResolveProperty_UnknownKey_ReportsResourceNotFound()
    {
        var localizer = LocalizerResolver.ResolveProperty(TypeChainServices(), param: null, typeof(Thing));

        Assert.True(localizer["DoesNotExist"].ResourceNotFound);
    }

    [Fact]
    public void ResolveOptional_ReturnsValue_WhenFound()
    {
        var localizer = new DictionaryLocalizer(new() { ["Name_Tooltip"] = "Der Anzeigename" });

        Assert.Equal("Der Anzeigename", LocalizerResolver.ResolveOptional(localizer, "Name_Tooltip"));
    }

    [Fact]
    public void ResolveOptional_ReturnsNull_WhenMissing()
    {
        var localizer = new DictionaryLocalizer(new());

        Assert.Null(LocalizerResolver.ResolveOptional(localizer, "Name_Tooltip"));
    }
}
