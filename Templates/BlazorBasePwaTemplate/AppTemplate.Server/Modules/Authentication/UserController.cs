using AppTemplate.Server.Entities;
using BlazorBase.User.Server.Controllers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AppTemplate.Server.Modules.Authentication;

/// <summary>
/// Concrete user endpoint so ASP.NET Core discovers the abstract base implementation.
/// </summary>
[ApiController]
public class UserController(UserManager<AppUser> users)
    : UserControllerBase<AppUser>(users);
