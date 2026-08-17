using System.ComponentModel.DataAnnotations;

namespace BlazorBase.CRUD.Test.Infrastructure.TestEntities;

public class TestCategory
{
    public Guid Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}
