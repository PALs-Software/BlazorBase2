using Microsoft.AspNetCore.Components;

namespace BlazorBase.CRUD.Configuration;

/// <summary>
/// How a list shows an item as a tile on a narrow screen, built with <see cref="MobileCardBuilder{TModel}"/>.
/// </summary>
/// <remarks>
/// Properties named here take their formatting and template from the list column of the same property; a
/// property that has no column is read directly from the item. A list that names nothing falls back to the roles
/// set on its columns and on the entity, and finally to a choice by data type.
/// </remarks>
public class MobileCardConfiguration<TModel>
{
    /// <summary>The properties the tile shows and the part each plays.</summary>
    public List<MobileFieldConfig<TModel>> Fields { get; set; } = [];

    /// <summary>
    /// The most label and value pairs on the tile. Null leaves explicitly declared fields unlimited and limits the
    /// automatic choice to <see cref="MobileCardPlanResolver.DefaultMaxFields"/>.
    /// </summary>
    public int? MaxFields { get; set; }

    /// <summary>
    /// Replaces the title, subtitle, badges and fields with custom markup. The tile keeps its frame: tapping it,
    /// the more button, the selection mark and the action row.
    /// </summary>
    public RenderFragment<TModel>? Template { get; set; }

    /// <summary>An action row rendered at the bottom of the tile, next to the columns with the role Actions.</summary>
    public RenderFragment<TModel>? Actions { get; set; }
}
