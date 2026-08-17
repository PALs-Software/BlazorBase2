namespace BlazorBase.User.Models;

/// <summary>
/// Marks a string property as the role field of a user model. A registered <c>UserRoleInput</c>
/// custom property input opts in for any property carrying this attribute (or, by convention, named
/// <c>Role</c>), rendering a select over the role names supplied by the app-registered
/// <c>IUserRoleProvider</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class UserRoleInputAttribute : Attribute;
