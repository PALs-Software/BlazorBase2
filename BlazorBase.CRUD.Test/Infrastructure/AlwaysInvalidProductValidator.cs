using System.ComponentModel.DataAnnotations;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using BlazorBase.CRUD.Validation;

namespace BlazorBase.CRUD.Test.Infrastructure;

/// <summary>
/// Custom validator that always reports a single failure, used to verify that
/// <see cref="BaseValidationService{TModel}"/> aggregates custom validator results.
/// </summary>
public sealed class AlwaysInvalidProductValidator : IBaseValidator<TestProduct>
{
    public const string Message = "Custom validator rejected the product.";

    public Task<IEnumerable<ValidationResult>> ValidateAsync(TestProduct model, CancellationToken cancellationToken = default)
    {
        IEnumerable<ValidationResult> results = [new ValidationResult(Message, [nameof(TestProduct.Name)])];
        return Task.FromResult(results);
    }
}
