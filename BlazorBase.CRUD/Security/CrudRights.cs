using System;

namespace BlazorBase.CRUD.Security;

/// <summary>
/// Granular CRUD rights expressed as flags. Parsed from the rights string
/// of <see cref="Attributes.CrudAccessAttribute"/>:
/// R = Read, I = Insert, M = Modify, D = Delete.
/// </summary>
[Flags]
public enum CrudRights
{
    None = 0,
    Read = 1 << 0,
    Insert = 1 << 1,
    Modify = 1 << 2,
    Delete = 1 << 3,
    All = Read | Insert | Modify | Delete
}
