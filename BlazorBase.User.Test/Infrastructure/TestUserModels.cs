using BlazorBase.User.Models;

namespace BlazorBase.User.Test.Infrastructure;

/// <summary>Distinct user model subclass used by the password-input tests so its property metadata
/// tokens do not share custom-input resolution cache entries with other test models.</summary>
public sealed class PasswordTestUser : UserModel;

/// <summary>Distinct user model subclass used by the role-input tests.</summary>
public sealed class RoleTestUser : UserModel;

/// <summary>Distinct user model subclass used by the full-card render test.</summary>
public sealed class CardTestUser : UserModel;

/// <summary>App-style extension proving the framework model is subclassable with extra properties.</summary>
public sealed class ExtendedTestUser : UserModel
{
    public string Department { get; set; } = string.Empty;
}
