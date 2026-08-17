using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Localization;

namespace BlazorBase.CRUD.Test.Infrastructure.Cards;

/// <summary>
/// Stands in for a host-written custom card wired to a list through the <c>CardType</c> seam —
/// the shape <c>UserCard</c> and the PWA template's <c>NoteCard</c> both have.
/// </summary>
/// <remarks>
/// It deliberately declares only the parameters a dialog passes through
/// <c>DynamicComponent</c> and forwards them unchanged. Anything the inner
/// <see cref="BaseCard{TModel}"/> needs beyond those has to reach it some other way.
/// </remarks>
public sealed class WrappingNoteCard : ComponentBase
{
    [Parameter]
    public TestStringKeyedNote Model { get; set; } = new();

    [Parameter]
    public IBaseDataProvider<TestStringKeyedNote>? DataProvider { get; set; }

    [Parameter]
    public IStringLocalizer? Localizer { get; set; }

    [Parameter]
    public BaseCardConfiguration<TestStringKeyedNote>? Configuration { get; set; }

    [Parameter]
    public EventCallback<TestStringKeyedNote> OnAfterSave { get; set; }

    [Parameter]
    public EventCallback OnCancelled { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<BaseCard<TestStringKeyedNote>>(0);
        builder.AddComponentParameter(1, nameof(BaseCard<TestStringKeyedNote>.Model), Model);
        builder.AddComponentParameter(2, nameof(BaseCard<TestStringKeyedNote>.DataProvider), DataProvider);
        builder.AddComponentParameter(3, nameof(BaseCard<TestStringKeyedNote>.Localizer), Localizer);
        builder.AddComponentParameter(4, nameof(BaseCard<TestStringKeyedNote>.Configuration), Configuration);
        builder.AddComponentParameter(5, nameof(BaseCard<TestStringKeyedNote>.OnAfterSave), OnAfterSave);
        builder.AddComponentParameter(6, nameof(BaseCard<TestStringKeyedNote>.OnCancelled), OnCancelled);
        builder.CloseComponent();
    }
}
