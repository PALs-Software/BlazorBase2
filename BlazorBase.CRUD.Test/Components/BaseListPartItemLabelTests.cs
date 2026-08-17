using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// What a list-part row actually says. This rendered <c>item.ToString()</c>, so an EF entity - which
/// almost never overrides it - showed the end user its fully qualified CLR type name, the same
/// string for every row of the list.
/// </summary>
public class BaseListPartItemLabelTests : BunitTestContextBase
{
    [Fact]
    public void A_row_reads_as_its_display_keys_in_order()
    {
        var cut = RenderListPart(
        [
            new TestPrinting { Id = Guid.NewGuid(), Code = "LOB-005", SetName = "Legend of Blue Eyes" }
        ]);

        var label = cut.Find(".base-listpart-item-content").TextContent.Trim();

        Assert.Contains("LOB-005", label);
        Assert.Contains("Legend of Blue Eyes", label);
        Assert.True(
            label.IndexOf("LOB-005", StringComparison.Ordinal) < label.IndexOf("Legend of Blue Eyes", StringComparison.Ordinal),
            $"Display keys are out of Order: '{label}'.");
    }

    [Fact]
    public void A_row_never_shows_the_clr_type_name()
    {
        var cut = RenderListPart([new TestAnonymousChild { Amount = 3 }]);

        var label = cut.Find(".base-listpart-item-content").TextContent.Trim();

        Assert.DoesNotContain(typeof(TestAnonymousChild).FullName!, label);
        Assert.DoesNotContain("BlazorBase.CRUD.Test", label);
    }

    /// <summary>
    /// With nothing to name it by, the row gets a localized placeholder rather than a blank or a
    /// type name - a freshly added row before the user has typed anything.
    /// </summary>
    [Fact]
    public void A_row_with_nothing_to_show_gets_the_localized_placeholder()
    {
        var cut = RenderListPart([new TestAnonymousChild()]);

        var framework = Services.GetRequiredService<IStringLocalizerFactory>().Create(typeof(BlazorBaseCrudResources));
        var label = cut.Find(".base-listpart-item-content").TextContent.Trim();

        Assert.Equal(framework["UnnamedListItem"].Value, label);
    }

    /// <summary>
    /// A type that declares display keys has said how it wants to be identified. When they are all
    /// empty on one row, the primary key is not an answer — a bare database id names nothing and
    /// only leaks a key, and it sits in the list right next to properly named siblings.
    /// </summary>
    [Fact]
    public void A_row_whose_declared_display_keys_are_all_empty_gets_the_placeholder_not_its_key()
    {
        var cut = RenderListPart([new TestPrinting { Id = Guid.NewGuid(), Code = null, SetName = null }]);

        var framework = Services.GetRequiredService<IStringLocalizerFactory>().Create(typeof(BlazorBaseCrudResources));
        var label = cut.Find(".base-listpart-item-content").TextContent.Trim();

        Assert.Equal(framework["UnnamedListItem"].Value, label);
    }

    [Fact]
    public void A_row_still_honours_an_overridden_ToString()
    {
        var cut = RenderListPart([new TestSpokenNote { Id = Guid.NewGuid(), Body = "remember this" }]);

        var label = cut.Find(".base-listpart-item-content").TextContent.Trim();

        Assert.Equal("Note: remember this", label);
    }

    /// <summary>
    /// No display-key attribute, but a conventional <c>Name</c> - the precedence
    /// <c>DisplayKeyResolver</c> documents, applied here as it already is in the card header.
    /// </summary>
    [Fact]
    public void A_row_falls_back_to_a_conventional_name_property()
    {
        var cut = RenderListPart([new TestCategory { Id = Guid.NewGuid(), Name = "Spellcaster" }]);

        Assert.Equal("Spellcaster", cut.Find(".base-listpart-item-content").TextContent.Trim());
    }

    [Fact]
    public void Every_row_reads_differently_when_the_items_differ()
    {
        var cut = RenderListPart(
        [
            new TestPrinting { Id = Guid.NewGuid(), Code = "LOB-005", SetName = "Legend of Blue Eyes" },
            new TestPrinting { Id = Guid.NewGuid(), Code = "YSYR-EN001", SetName = "Starter Deck" }
        ]);

        var labels = cut.FindAll(".base-listpart-item-content").Select(element => element.TextContent.Trim()).ToList();

        Assert.Equal(2, labels.Count);
        Assert.Equal(2, labels.Distinct().Count());
    }

    private IRenderedComponent<BaseListPart<TModel>> RenderListPart<TModel>(List<TModel> items)
        where TModel : class, new()
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");

        Services.AddLocalization();

        return Render<BaseListPart<TModel>>(parameters => parameters
            .Add(part => part.Items, items)
            .Add(part => part.EditMode, ListPartEditMode.Dialog));
    }
}
