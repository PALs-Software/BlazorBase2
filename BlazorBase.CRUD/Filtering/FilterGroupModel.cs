using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.Filtering;

/// <summary>
/// Editable view-model for a filter group (a logical AND/OR over its conditions and nested groups)
/// while the user builds a filter in the panel.
/// </summary>
public sealed class FilterGroupModel
{
    public FilterLogic Logic { get; set; } = FilterLogic.And;

    public List<FilterConditionModel> Conditions { get; set; } = [];

    public List<FilterGroupModel> Groups { get; set; } = [];

    public FilterGroupModel Clone() => new()
    {
        Logic = Logic,
        Conditions = [.. Conditions.Select(c => c.Clone())],
        Groups = [.. Groups.Select(g => g.Clone())]
    };
}
