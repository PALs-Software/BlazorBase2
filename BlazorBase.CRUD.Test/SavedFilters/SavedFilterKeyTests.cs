using BlazorBase.CRUD.SavedFilters;
using Xunit;

namespace BlazorBase.CRUD.Test.SavedFilters;

public sealed class SavedFilterKeyTests
{
    private const string Base = "https://localhost:7941/";

    private static string Key(string uri, string baseUri = Base, string? explicitKey = null) =>
        SavedFilterKey.Resolve(explicitKey, typeof(SavedFilterKeyTests), uri, baseUri);

    private static string Prefix => typeof(SavedFilterKeyTests).FullName + "@";

    [Fact]
    public void Resolve_CombinesModelFullNameAndRoute()
    {
        Assert.Equal(Prefix + "/users", Key(Base + "Users"));
    }

    [Fact]
    public void Resolve_DifferentPagesOverTheSameModelGetDifferentKeys()
    {
        Assert.NotEqual(Key(Base + "Users"), Key(Base + "EnrolledUsers"));
    }

    [Theory]
    [InlineData("Users?page=2&q=a", "/users")]
    [InlineData("Users#section", "/users")]
    [InlineData("Users?deeplink=5#x", "/users")]
    public void Resolve_IgnoresQueryStringAndFragment(string relative, string route)
    {
        Assert.Equal(Prefix + route, Key(Base + relative));
    }

    [Theory]
    [InlineData("Users", "/users")]
    [InlineData("USERS", "/users")]
    [InlineData("users/", "/users")]
    [InlineData("Users///", "/users")]
    [InlineData("Admin/Budget-Boosts/", "/admin/budget-boosts")]
    public void Resolve_LowerCasesAndTrimsSlashes(string relative, string route)
    {
        Assert.Equal(Prefix + route, Key(Base + relative));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("?x=1")]
    public void Resolve_RootRouteIsSlash(string relative)
    {
        Assert.Equal(Prefix + "/", Key(Base + relative));
    }

    [Fact]
    public void Resolve_ExplicitKeyWinsUnchanged()
    {
        Assert.Equal("Products-Admin", Key(Base + "Users", explicitKey: "Products-Admin"));
    }

    [Theory]
    [InlineData("https://localhost/app/", "https://localhost/app/Users?x=1")]
    [InlineData("https://localhost/app", "https://localhost/app/users")]
    [InlineData("https://localhost/", "https://localhost/users")]
    public void Resolve_BasePathIsNotPartOfTheRoute(string baseUri, string uri)
    {
        Assert.Equal(Prefix + "/users", Key(uri, baseUri));
    }

    [Fact]
    public void Resolve_BaseHrefRootPageIsSlash()
    {
        Assert.Equal(Prefix + "/", Key("https://localhost/app/", "https://localhost/app/"));
        Assert.Equal(Prefix + "/", Key("https://localhost/app", "https://localhost/app/"));
    }

    [Fact]
    public void Resolve_PathThatOnlyStartsWithTheBasePathKeepsItsPrefix()
    {
        Assert.Equal(Prefix + "/application/users", Key("https://localhost/application/users", "https://localhost/app/"));
    }

    [Fact]
    public void Resolve_MissingUriFallsBackToRoot()
    {
        Assert.Equal(Prefix + "/", SavedFilterKey.Resolve(null, typeof(SavedFilterKeyTests), null, null));
    }

    [Fact]
    public void Resolve_RelativeUriIsReadAsPath()
    {
        Assert.Equal(Prefix + "/users", SavedFilterKey.Resolve(null, typeof(SavedFilterKeyTests), "/Users?x=1", null));
    }

    [Fact]
    public void Resolve_LongRouteIsShortenedDeterministicallyWithinTheLimit()
    {
        var route = String.Join('/', Enumerable.Repeat("segment", 80));

        var first = Key(Base + route + "/a");
        var second = Key(Base + route + "/a");
        var other = Key(Base + route + "/b");

        Assert.True(first.Length <= ListFilterValidator.MaxListKeyLength);
        Assert.StartsWith(Prefix + "/segment", first);
        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void Resolve_VeryLongModelNameIsShortenedAndKeepsTheRoute()
    {
        var model = typeof(Dictionary<,>).MakeGenericType(typeof(Dictionary<string, List<Dictionary<string, List<int>>>>), typeof(Dictionary<string, Dictionary<string, Dictionary<string, List<SavedFilterKeyTests>>>>));

        var key = SavedFilterKey.Resolve(null, model, Base + "Users", Base);

        Assert.True(model.FullName!.Length > 200);
        Assert.True(key.Length <= ListFilterValidator.MaxListKeyLength);
        Assert.EndsWith("@/users", key);
    }

    [Fact]
    public void Resolve_ResultIsAcceptedByTheValidator()
    {
        var key = Key(Base + new string('x', 1000));

        Assert.Equal(key, ListFilterValidator.ValidateListKey(key));
    }
}
