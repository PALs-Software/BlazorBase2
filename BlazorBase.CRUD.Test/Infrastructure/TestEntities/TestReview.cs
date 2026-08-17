namespace BlazorBase.CRUD.Test.Infrastructure.TestEntities;

public class TestReview
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string Author { get; set; } = string.Empty;

    public int Rating { get; set; }

    public bool IsApproved { get; set; }

    public int DisplayOrder { get; set; }
}
