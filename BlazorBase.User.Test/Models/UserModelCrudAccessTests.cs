using System.Reflection;
using System.Security.Claims;
using BlazorBase.CRUD.Attributes;
using BlazorBase.CRUD.Security;
using BlazorBase.User.Models;
using Xunit;

namespace BlazorBase.User.Test.Models;

/// <summary>
/// Defense-in-depth proof for USRV-06: <see cref="UserModel"/> carries a class-level
/// <see cref="CrudAccessAttribute"/> restricting it to the Admin role, so a generic assembly scan
/// cannot expose the user model role-lessly even if a host mis-wires the admin-endpoint mapping.
/// </summary>
public class UserModelCrudAccessTests
{
    [Fact]
    public void UserModel_HasClassLevelCrudAccessAttribute_RestrictingToAdmin()
    {
        var attribute = typeof(UserModel).GetCustomAttribute<CrudAccessAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("Admin", attribute!.Roles);
        Assert.Equal("RIMD", attribute.Rights);
    }

    [Fact]
    public void UserModel_ClassLevelCrudAccess_GrantsNoRightsToNonAdminPrincipal()
    {
        var nonAdmin = CreatePrincipal("User");

        var rights = CrudAccessResolver.EvaluateClass(typeof(UserModel), nonAdmin);

        Assert.Equal(CrudRights.None, rights);
    }

    [Fact]
    public void UserModel_ClassLevelCrudAccess_GrantsNoRightsToAnonymousPrincipal()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        var rights = CrudAccessResolver.EvaluateClass(typeof(UserModel), anonymous);

        Assert.Equal(CrudRights.None, rights);
    }

    [Fact]
    public void UserModel_ClassLevelCrudAccess_GrantsAllRightsToAdminPrincipal()
    {
        var admin = CreatePrincipal("Admin");

        var rights = CrudAccessResolver.EvaluateClass(typeof(UserModel), admin);

        Assert.Equal(CrudRights.All, rights);
    }

    private static ClaimsPrincipal CreatePrincipal(params string[] roles)
    {
        var claims = roles.Select(role => new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "TestAuth"));
    }
}
