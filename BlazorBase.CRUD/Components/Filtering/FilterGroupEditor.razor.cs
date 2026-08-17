using BlazorBase.CRUD.Filtering;
using BlazorBase.CRUD.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace BlazorBase.CRUD.Components.Filtering;

public partial class FilterGroupEditor
{
    [Parameter, EditorRequired]
    public FilterGroupModel Group { get; set; } = default!;

    [Parameter, EditorRequired]
    public IReadOnlyList<FilterFieldMetadata> Fields { get; set; } = [];

    [Parameter, EditorRequired]
    public IStringLocalizer Localizer { get; set; } = default!;

    [Parameter]
    public bool IsRoot { get; set; }

    [Parameter]
    public EventCallback OnRemove { get; set; }

    private string LogicLabel =>
        (Group.Logic == FilterLogic.And ? Localizer["FilterLogicAnd"] : Localizer["FilterLogicOr"]).Value;

    private string LogicClass => Group.Logic == FilterLogic.And ? "and" : "or";

    private void ToggleLogic() =>
        Group.Logic = Group.Logic == FilterLogic.And ? FilterLogic.Or : FilterLogic.And;

    private void AddCondition() => Group.Conditions.Add(new FilterConditionModel());

    private void AddGroup() => Group.Groups.Add(new FilterGroupModel());

    private void RemoveCondition(FilterConditionModel condition) => Group.Conditions.Remove(condition);

    private void RemoveGroup(FilterGroupModel group) => Group.Groups.Remove(group);
}
