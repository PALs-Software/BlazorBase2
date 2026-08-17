using System.ComponentModel.DataAnnotations;

namespace BlazorBase.CRUD.Validation;

/// <summary>
/// Runs DataAnnotation validation and all registered IBaseValidator implementations.
/// </summary>
public class BaseValidationService<TModel>(
    IEnumerable<IBaseValidator<TModel>> customValidators
) where TModel : class
{
    #region Injects

    private readonly IEnumerable<IBaseValidator<TModel>> CustomValidators = customValidators;

    #endregion

    public async Task<List<ValidationResult>> ValidateAsync(TModel model, CancellationToken cancellationToken = default)
    {
        var results = new List<ValidationResult>();

        var context = new ValidationContext(model);
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);

        foreach (var validator in CustomValidators)
        {
            var customResults = await validator.ValidateAsync(model, cancellationToken);
            results.AddRange(customResults);
        }

        return results;
    }
}
