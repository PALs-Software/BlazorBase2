using System.Security.Claims;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Filtering;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Xunit;
using BlazorBase.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.CRUD.Test.Filtering;

public class FilterFieldResolverTests
{
    private static List<FilterFieldMetadata> Resolve(FilterConfiguration<TestProduct> configuration, ClaimsPrincipal? user = null)
        => FilterFieldResolver.Resolve(configuration, user, EmptyLocalizer.Instance);

    [Fact]
    public void Auto_NoUser_IncludesScalarFields_ExcludesNavigations()
    {
        var names = Resolve(new FilterConfiguration<TestProduct>()).Select(f => f.PropertyName).ToList();

        Assert.Contains("Name", names);
        Assert.Contains("Price", names);
        Assert.Contains("Kind", names);
        Assert.DoesNotContain("Category", names);
        Assert.DoesNotContain("Reviews", names);
    }

    [Fact]
    public void Auto_HidesTheTechnicalFields_PrimaryKeyAndForeignKey()
    {
        var names = Resolve(new FilterConfiguration<TestProduct>()).Select(f => f.PropertyName).ToList();

        Assert.DoesNotContain("Id", names);
        Assert.DoesNotContain("CategoryId", names);
    }

    [Fact]
    public void Auto_HidesAnAttributedForeignKeyAndTheKeyAttribute()
    {
        var names = FilterFieldResolver.Resolve(new FilterConfiguration<KeyedLink>(), null, EmptyLocalizer.Instance)
            .Select(f => f.PropertyName)
            .ToList();

        Assert.Equal(["Label"], names);
    }

    [Fact]
    public void Include_StillOffersATechnicalFieldTheConfigurationNamesExplicitly()
    {
        var configuration = new FilterConfigBuilder<TestProduct>().Include(p => p.CategoryId).Build();

        Assert.Equal(["CategoryId"], Resolve(configuration).Select(f => f.PropertyName));
    }

    [Fact]
    public void EnumField_WithServices_OffersTheLocalizedMemberText()
    {
        var services = new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider();
        var localizer = new KeyedLocalizer(new Dictionary<string, string> { ["Origin_ManualEdit"] = "Manuelle Bearbeitung" });

        var origin = FilterFieldResolver.Resolve(new FilterConfiguration<ChangeRecord>(), null, localizer, services)
            .Single(f => f.PropertyName == "Origin");

        Assert.Equal(["Initial", "AppliedSuggestion", "ManualEdit"], origin.EnumNames);
        Assert.Equal("Manuelle Bearbeitung", origin.EnumLabel("ManualEdit"));
        Assert.Equal("Applied suggestion", origin.EnumLabel("AppliedSuggestion"));
    }

    [Fact]
    public void EnumField_WithoutServices_FallsBackToTheMemberName()
    {
        var origin = FilterFieldResolver.Resolve(new FilterConfiguration<ChangeRecord>(), null, EmptyLocalizer.Instance)
            .Single(f => f.PropertyName == "Origin");

        Assert.Equal("AppliedSuggestion", origin.EnumLabel("AppliedSuggestion"));
    }

    public enum ChangeOrigin
    {
        Initial,
        AppliedSuggestion,
        ManualEdit
    }

    public class ChangeRecord
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public User? User { get; set; }

        public ChangeOrigin Origin { get; set; }
    }

    public class User
    {
        public Guid Id { get; set; }
    }

    public class KeyedLink
    {
        [System.ComponentModel.DataAnnotations.Key]
        public Guid LinkKey { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("Target")]
        public Guid TargetReference { get; set; }

        public User? Target { get; set; }

        public string Label { get; set; } = string.Empty;
    }

    private sealed class KeyedLocalizer(Dictionary<string, string> values) : Microsoft.Extensions.Localization.IStringLocalizer
    {
        public Microsoft.Extensions.Localization.LocalizedString this[string name] =>
            values.TryGetValue(name, out var value) ? new(name, value) : new(name, name, resourceNotFound: true);

        public Microsoft.Extensions.Localization.LocalizedString this[string name, params object[] arguments] => this[name];

        public IEnumerable<Microsoft.Extensions.Localization.LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    [Fact]
    public void Auto_AdminUser_ExcludesUnreadableField()
    {
        var names = Resolve(new FilterConfiguration<TestProduct>(), TestPrincipals.WithRoles("Admin"))
            .Select(f => f.PropertyName)
            .ToList();

        Assert.DoesNotContain("InternalNotes", names);
        Assert.Contains("CostPrice", names);
    }

    [Fact]
    public void EnumField_CarriesEnumNames()
    {
        var kind = Resolve(new FilterConfiguration<TestProduct>()).Single(f => f.PropertyName == "Kind");

        Assert.Equal(FilterFieldKind.Enum, kind.Kind);
        Assert.Equal(["Physical", "Digital", "Service"], kind.EnumNames);
    }

    [Fact]
    public void Include_ReturnsOnlyListedFields()
    {
        var configuration = new FilterConfigBuilder<TestProduct>().Include(p => p.Name).Build();

        Assert.Equal(["Name"], Resolve(configuration).Select(f => f.PropertyName));
    }

    [Fact]
    public void Exclude_RemovesListedFields()
    {
        var configuration = new FilterConfigBuilder<TestProduct>().Exclude(p => p.Price).Build();
        var names = Resolve(configuration).Select(f => f.PropertyName).ToList();

        Assert.DoesNotContain("Price", names);
        Assert.Contains("Name", names);
    }

    [Fact]
    public void Disabled_ReturnsEmpty()
    {
        var configuration = new FilterConfigBuilder<TestProduct>().Disable().Build();

        Assert.Empty(Resolve(configuration));
    }

    [Fact]
    public void Override_AppliesLabelAndOperators()
    {
        var configuration = new FilterConfigBuilder<TestProduct>()
            .Field(p => p.Name, f => f.Label("Bezeichnung").Operators(FilterOperator.Equals))
            .Build();

        var name = Resolve(configuration).Single(f => f.PropertyName == "Name");

        Assert.Equal("Bezeichnung", name.Label);
        Assert.Equal([FilterOperator.Equals], name.Operators);
    }

    [Fact]
    public void MixingIncludeAndExclude_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new FilterConfigBuilder<TestProduct>().Include(p => p.Name).Exclude(p => p.Price));
    }
}
