namespace BlazorBase.CRUD.Models;

/// <summary>
/// Request body for PATCH endpoint — contains only the changed fields and an optional concurrency stamp.
/// </summary>
public class PatchModel
{
    public Dictionary<string, object?> ChangedFields { get; set; } = [];

    public string? ConcurrencyStamp { get; set; }
}
