namespace BlazorBase.User.Models;

/// <summary>
/// Marks a string property as the write-only password field of a user model. A registered
/// <c>UserPasswordInput</c> custom property input opts in for any property carrying this attribute
/// (or, by convention, named <c>Password</c>), rendering a password box whose blank value keeps the
/// existing password and whose non-blank value sets or resets it.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class UserPasswordInputAttribute : Attribute;
