namespace BlazorBase.CRUD.Test.Infrastructure.TestEntities;

/// <summary>
/// A child entity that overrides <see cref="object.ToString"/> and declares no display key - a
/// deliberate statement about how it should read, which the list part must keep honouring.
/// </summary>
public class TestSpokenNote
{
    public Guid Id { get; set; }

    public string Body { get; set; } = string.Empty;

    public override string ToString() => $"Note: {Body}";
}
