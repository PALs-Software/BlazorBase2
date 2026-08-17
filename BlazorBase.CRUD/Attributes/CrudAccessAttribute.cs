using System;

namespace BlazorBase.CRUD.Attributes;

/// <summary>
/// Declares CRUD access rights for one or more roles, either at class level
/// (controls which operations the endpoint allows) or at property level
/// (controls which fields are readable/writable).
/// </summary>
/// <remarks>
/// <para>
/// Multiple attributes may be combined on the same target — effective rights
/// for a user with multiple roles are the union of all matching attributes
/// on the same hierarchy level.
/// </para>
/// <para>
/// The <see cref="Roles"/> string accepts a comma-separated list (e.g. "Admin, User")
/// or the wildcard "*" to match every user, including unauthenticated requests.
/// </para>
/// <para>
/// The <see cref="Rights"/> string contains the characters R (Read), I (Insert),
/// M (Modify) and D (Delete) in any order, without separators. An empty string
/// grants no rights. Any character outside RIMD is rejected with
/// <see cref="ArgumentException"/> when the attribute is parsed by
/// <see cref="Security.CrudAccessResolver"/>.
/// </para>
/// <para>
/// The hierarchy is Class > Property > View — each level can only further
/// restrict the rights granted above, never expand them.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
public sealed class CrudAccessAttribute(string roles, string rights) : Attribute
{
    public string Roles { get; } = roles;

    public string Rights { get; } = rights;
}
