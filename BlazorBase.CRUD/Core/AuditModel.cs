using System.ComponentModel.DataAnnotations;

namespace BlazorBase.CRUD.Core;

public abstract class AuditModel
{
    public DateTime CreatedOn { get; set; }

    public string? CreatedBy { get; set; }

    [ConcurrencyCheck]
    public DateTime? ModifiedOn { get; set; }

    public string? ModifiedBy { get; set; }
}
