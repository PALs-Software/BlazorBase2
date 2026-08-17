namespace BlazorBase.CRUD.Components;

/// <summary>
/// Names of the cascading values a dialog supplies to the card it hosts.
/// </summary>
/// <remarks>
/// A dialog can hand a plain <see cref="BaseCard{TModel}"/> everything it knows through parameters,
/// but a custom <c>CardType</c> is instantiated through <c>DynamicComponent</c> and only receives the
/// parameters that card happens to declare. Cascading the values instead lets a custom card stay a
/// thin wrapper: it declares nothing extra, and the inner card still gets the dialog's answer.
/// </remarks>
public static class CardCascadeNames
{
    /// <summary>
    /// Carries whether the hosted model is being created rather than edited.
    /// </summary>
    public const string IsNew = "BlazorBase.CRUD.BaseCard.IsNew";
}
