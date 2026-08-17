using System.Collections.Generic;

namespace BlazorBase.CRUD.Security;

/// <summary>
/// A parsed, normalized rule from a <see cref="Attributes.CrudAccessAttribute"/>:
/// a set of roles and the rights those roles receive.
/// </summary>
/// <param name="Roles">Normalized role names — trimmed, case-preserved. Contains "*" when the attribute used the wildcard.</param>
/// <param name="IsWildcard">True when any of the roles is "*".</param>
/// <param name="Rights">Parsed RIMD flags.</param>
public sealed record CrudAccessRule(IReadOnlyList<string> Roles, bool IsWildcard, CrudRights Rights);
