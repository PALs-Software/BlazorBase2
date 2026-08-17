using System.ComponentModel.DataAnnotations;

namespace BlazorBase.CRUD.Validation;

/// <summary>
/// Extensibility interface for custom validators. Register via DI to add custom validation logic.
/// </summary>
public interface IBaseValidator<TModel> where TModel : class
{
    Task<IEnumerable<ValidationResult>> ValidateAsync(TModel model, CancellationToken cancellationToken = default);
}
