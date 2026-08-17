using System.Linq.Expressions;
using System.Security.Claims;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Models;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Data;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.Lifecycle;
using BlazorBase.User.Server.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace BlazorBase.User.Server.Services;

public class UserDataProvider<TUser>(
    UserManager<TUser> userManager,
    IHttpContextAccessor httpContextAccessor,
    BaseUserDbContext<TUser> dbContext,
    IEnumerable<IUserDeletionHandler>? deletionHandlers = null,
    IStringLocalizer<BlazorBaseUserServerResources>? localizer = null) : IBaseDataProvider<UserModel>
    where TUser : BaseUser, new()
{
    #region Injects
    private readonly UserManager<TUser> UserManager = userManager;
    private readonly IHttpContextAccessor HttpContextAccessor = httpContextAccessor;
    private readonly BaseUserDbContext<TUser> DbContext = dbContext;
    private readonly IEnumerable<IUserDeletionHandler> DeletionHandlers = deletionHandlers ?? [];
    private readonly IStringLocalizer<BlazorBaseUserServerResources>? Localizer = localizer;
    #endregion

    /// <summary>
    /// Resolves a user-facing message, falling back to the key when the host has not called
    /// <c>AddLocalization()</c> — a missing resource must not turn a validation error into a crash.
    /// </summary>
    private string Text(string key, params object[] arguments)
        => Localizer is null ? key : Localizer[key, arguments].Value;

    public async Task<BaseQueryResult<UserModel>> GetListAsync(BaseQuery query, Expression<Func<UserModel, bool>>? scopeFilter = null, CancellationToken cancellationToken = default)
    {
        if (scopeFilter is not null)
            throw new NotSupportedException("Row-level scope filtering is not supported by the Identity-backed UserDataProvider.");

        IQueryable<TUser> dbQuery = UserManager.Users;

        foreach (var filter in query.Filters)
        {
            dbQuery = filter.PropertyName switch
            {
                nameof(UserModel.DisplayName) => ApplyStringFilter(dbQuery, u => u.DisplayName, filter),
                nameof(UserModel.Email) => ApplyStringFilter(dbQuery, u => u.Email!, filter),
                nameof(UserModel.IsActive) => filter.Operator == FilterOperator.Equals && filter.Value is bool isActive
                    ? dbQuery.Where(u => u.IsActive == isActive)
                    : dbQuery,
                _ => dbQuery,
            };
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        IOrderedQueryable<TUser>? ordered = null;
        foreach (var sort in query.Sorts)
        {
            ordered = (sort.PropertyName, sort.Direction) switch
            {
                (nameof(UserModel.DisplayName), SortDirection.Ascending) => (ordered ?? dbQuery).OrderBy(u => u.DisplayName),
                (nameof(UserModel.DisplayName), _) => (ordered ?? dbQuery).OrderByDescending(u => u.DisplayName),
                (nameof(UserModel.Email), SortDirection.Ascending) => (ordered ?? dbQuery).OrderBy(u => u.Email),
                (nameof(UserModel.Email), _) => (ordered ?? dbQuery).OrderByDescending(u => u.Email),
                (nameof(UserModel.CreatedAt), SortDirection.Ascending) => (ordered ?? dbQuery).OrderBy(u => u.CreatedAt),
                (nameof(UserModel.CreatedAt), _) => (ordered ?? dbQuery).OrderByDescending(u => u.CreatedAt),
                (nameof(UserModel.IsActive), SortDirection.Ascending) => (ordered ?? dbQuery).OrderBy(u => u.IsActive),
                (nameof(UserModel.IsActive), _) => (ordered ?? dbQuery).OrderByDescending(u => u.IsActive),
                _ => ordered,
            };
        }

        var finalQuery = ordered ?? dbQuery.OrderBy(u => u.DisplayName);
        var users = await finalQuery.Skip(query.Skip).Take(query.Take).ToListAsync(cancellationToken);

        var items = new List<UserModel>(users.Count);
        foreach (var user in users)
            items.Add(await MapToModelAsync(user));

        return new BaseQueryResult<UserModel> { Items = items, TotalCount = totalCount };
    }

    public async Task<UserModel?> GetByIdAsync(object id, IEnumerable<string>? select = null, Expression<Func<UserModel, bool>>? scopeFilter = null, CancellationToken cancellationToken = default)
    {
        if (scopeFilter is not null)
            throw new NotSupportedException("Row-level scope filtering is not supported by the Identity-backed UserDataProvider.");

        var user = await UserManager.FindByIdAsync(id.ToString()!);
        return user is null ? null : await MapToModelAsync(user);
    }

    public async Task<int> GetCountAsync(Expression<Func<UserModel, bool>>? scopeFilter = null, CancellationToken cancellationToken = default)
    {
        if (scopeFilter is not null)
            throw new NotSupportedException("Row-level scope filtering is not supported by the Identity-backed UserDataProvider.");

        return await UserManager.Users.CountAsync(cancellationToken);
    }

    /// <summary>
    /// Creates the account and assigns its role.
    /// </summary>
    /// <remarks>
    /// Both preconditions are checked <em>before</em> the account is written. Assigning a role that
    /// does not exist throws inside Identity, which used to surface as a 500 after the user row had
    /// already been committed — leaving an account with no role behind and no way to tell from the
    /// error what went wrong. A role can be missing in practice: the names offered to the
    /// administrator come from the app's role provider, while the rows are seeded elsewhere.
    /// </remarks>
    public async Task<UserModel> CreateAsync(UserModel model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.Password))
            throw new BaseValidationException(Text("PasswordRequired"));

        if (string.IsNullOrWhiteSpace(model.Role))
            throw new BaseValidationException(Text("RoleRequired"));

        var normalizedRole = UserManager.KeyNormalizer.NormalizeName(model.Role);

        if (!await DbContext.Roles.AnyAsync(role => role.NormalizedName == normalizedRole, cancellationToken))
            throw new BaseValidationException(Text("RoleUnknown", model.Role));

        var user = new TUser
        {
            UserName = model.Email,
            Email = model.Email,
            DisplayName = model.DisplayName,
            IsActive = model.IsActive,
        };

        var result = await UserManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
            throw new BaseValidationException(string.Join(" ", result.Errors.Select(error => error.Description)));

        await UserManager.AddToRoleAsync(user, model.Role);
        return await MapToModelAsync(user);
    }

    public async Task<UserModel> PatchAsync(object id, Dictionary<string, object?> changedFields, string? concurrencyStamp = null, CancellationToken cancellationToken = default)
    {
        var user = await UserManager.FindByIdAsync(id.ToString()!)
            ?? throw new InvalidOperationException($"User '{id}' not found.");

        foreach (var (key, value) in changedFields)
        {
            switch (key)
            {
                case nameof(UserModel.DisplayName):
                    user.DisplayName = value?.ToString() ?? string.Empty;
                    break;
                case nameof(UserModel.Email):
                    user.Email = value?.ToString() ?? string.Empty;
                    user.UserName = user.Email;
                    break;
                case nameof(UserModel.IsActive):
                    if (value is bool active)
                        user.IsActive = active;
                    break;
                case nameof(UserModel.Role):
                    var newRole = value?.ToString() ?? "User";
                    var currentRoles = await UserManager.GetRolesAsync(user);
                    if (!currentRoles.Contains(newRole))
                    {
                        await UserManager.RemoveFromRolesAsync(user, currentRoles);
                        await UserManager.AddToRoleAsync(user, newRole);
                    }
                    break;
                case nameof(UserModel.Password):
                    var password = value?.ToString();
                    if (!string.IsNullOrEmpty(password))
                    {
                        var token = await UserManager.GeneratePasswordResetTokenAsync(user);
                        var passResult = await UserManager.ResetPasswordAsync(user, token, password);
                        if (!passResult.Succeeded)
                            throw new InvalidOperationException(string.Join("; ", passResult.Errors.Select(e => e.Description)));

                        await DbContext.RefreshTokens
                            .Where(r => r.UserId == user.Id && !r.IsRevoked)
                            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.IsRevoked, true), cancellationToken);
                    }
                    break;
            }
        }

        var updateResult = await UserManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            throw new InvalidOperationException(string.Join("; ", updateResult.Errors.Select(e => e.Description)));

        return await MapToModelAsync(user);
    }

    public async Task DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        var currentUserId = HttpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id.ToString() == currentUserId)
            throw new InvalidOperationException("Cannot delete your own account.");

        var user = await UserManager.FindByIdAsync(id.ToString()!)
            ?? throw new InvalidOperationException($"User '{id}' not found.");

        var result = await UserManager.DeleteAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        foreach (var handler in DeletionHandlers)
            await handler.OnUserDeletedAsync(user.Id, cancellationToken);
    }

    private async Task<UserModel> MapToModelAsync(TUser user)
    {
        var roles = await UserManager.GetRolesAsync(user);
        return new UserModel
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            DisplayName = user.DisplayName,
            Role = roles.FirstOrDefault() ?? "User",
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
        };
    }

    private static IQueryable<TUser> ApplyStringFilter(IQueryable<TUser> query, Expression<Func<TUser, string>> selector, FilterDescriptor filter)
    {
        var value = filter.Value?.ToString() ?? string.Empty;
        return filter.Operator switch
        {
            FilterOperator.Contains => query.Where(Expression.Lambda<Func<TUser, bool>>(
                Expression.Call(
                    typeof(DbFunctionsExtensions),
                    nameof(DbFunctionsExtensions.Like),
                    null,
                    Expression.Property(null, typeof(EF), nameof(EF.Functions)),
                    selector.Body,
                    Expression.Constant($"%{value}%")),
                selector.Parameters)),
            FilterOperator.Equals => query.Where(Expression.Lambda<Func<TUser, bool>>(
                Expression.Equal(selector.Body, Expression.Constant(value)),
                selector.Parameters)),
            _ => query,
        };
    }
}
