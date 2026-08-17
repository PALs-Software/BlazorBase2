using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using BlazorBase.CRUD.Validation;
using Xunit;

namespace BlazorBase.CRUD.Test.Validation;

public class BaseValidationServiceTests
{
    [Fact]
    public async Task ValidateAsync_ValidModelWithoutCustomValidators_ReturnsNoErrors()
    {
        var service = new BaseValidationService<TestProduct>([]);

        var results = await service.ValidateAsync(new TestProduct { Name = "Valid" });

        Assert.Empty(results);
    }

    [Fact]
    public async Task ValidateAsync_MissingRequiredField_ReturnsDataAnnotationError()
    {
        var service = new BaseValidationService<TestProduct>([]);

        var results = await service.ValidateAsync(new TestProduct { Name = string.Empty });

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(TestProduct.Name)));
    }

    [Fact]
    public async Task ValidateAsync_ValueExceedingMaxLength_ReturnsDataAnnotationError()
    {
        var service = new BaseValidationService<TestProduct>([]);

        var results = await service.ValidateAsync(new TestProduct { Name = new string('x', 201) });

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(TestProduct.Name)));
    }

    [Fact]
    public async Task ValidateAsync_CustomValidator_ContributesItsErrors()
    {
        var service = new BaseValidationService<TestProduct>([new AlwaysInvalidProductValidator()]);

        var results = await service.ValidateAsync(new TestProduct { Name = "Valid" });

        Assert.Contains(results, r => r.ErrorMessage == AlwaysInvalidProductValidator.Message);
    }

    [Fact]
    public async Task ValidateAsync_AggregatesDataAnnotationAndCustomErrors()
    {
        var service = new BaseValidationService<TestProduct>([new AlwaysInvalidProductValidator()]);

        var results = await service.ValidateAsync(new TestProduct { Name = string.Empty });

        Assert.Contains(results, r => r.ErrorMessage == AlwaysInvalidProductValidator.Message);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(TestProduct.Name)));
    }
}
