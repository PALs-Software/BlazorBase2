using System;

namespace BlazorBase.CRUD.Attributes;

/// <summary>
/// Marks an entity class for automatic CRUD registration (endpoints, DataProvider, DI).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class BaseCrudAttribute(string route) : Attribute
{
    public string Route { get; } = route;
}
