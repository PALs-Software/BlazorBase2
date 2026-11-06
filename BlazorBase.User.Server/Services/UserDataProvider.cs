using System.Linq.Expressions;
using System.Security.Claims;
using System.Text.Json;
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

/// <summary>
/// The Identity-backed <see cref="IBaseDataProvider{TModel}"/> of the framework user model: the admin user list and
/// its edits over <see cref="UserManager{TUser}"/>.
/// </summary>
/// <typeparam name="TUser">The application user entity.</typeparam>
/// <typeparam name="TModel">
/// The user model the list shows: <see cref="UserModel"/> or a model of its own that implements <see cref="IBaseUser"/>.
/// Override <see cref="MapToModelAsync"/> to fill extra columns and <see cref="ApplySort"/> to sort by them.
/// </typeparam>
public class UserDataProvider<TUser, TModel>(
    UserManager<TUser> userManager,
    IHttpContextAccessor httpContextAccessor,
    BaseUserDbContext<TUser> dbContext,
    IEnumerable<IUserDeletionHandler>? deletionHandlers = null,
    IStringLocalizer<BlazorBaseUserServerResources>? localizer = null) : IBaseDataProvider<TModel>
    where TUser : BaseUser, new()
    where TModel : class, IBaseUser, new()
{
    #region Injects
    protected UserManager<TUser> UserManager { get; } = userManager;
    private readonly IHttpContextAccessor HttpContextAccessor = httpContextAccessor;
    protected BaseUserDbContext<TUser> DbContext { get; } = dbContext;
    private readonly IEnumerable<IUserDeletionHandler> DeletionHandlers = deletionHandlers ?? [];
    private readonly IStringLocalizer<BlazorBaseUserServerResources>? Localizer = localizer;
    #endregion

    /// <summary>
    /// Resolves a user-facing message, falling back to the key when the host has not called
    /// <c>AddLocalization()</c> — a missing resource must not turn a validation error into a crash.
    /// </summary>
    private string Text(string key, params object[] arguments)
        => Localizer is null ? key : Localizer[key, arguments].Value;

    public async Task<BaseQueryResult<TModel>> GetListAsync(BaseQuery query, Expression<Func<TModel, bool>>? scopeFilter = null, CancellationToken cancellationToken = default)
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
            ordered = sort.PropertyName switch
            {
                nameof(UserModel.DisplayName) => OrderByOrThenBy(dbQuery, ordered, u => u.DisplayName, sort.Direction),
                nameof(UserModel.Email) => OrderByOrThenBy(dbQuery, ordered, u => u.Email, sort.Direction),
                nameof(UserModel.CreatedAt) => OrderByOrThenBy(dbQuery, ordered, u => u.CreatedAt, sort.Direction),
                nameof(UserModel.IsActive) => OrderByOrThenBy(dbQuery, ordered, u => u.IsActive, sort.Direction),
                _ => ApplySort(dbQuery, ordered, sort),
            };
        }

        var finalQuery = ordered ?? dbQuery.OrderBy(u => u.DisplayName);
        var users = await finalQuery.Skip(query.Skip).Take(query.Take).ToListAsync(cancellationToken);

        var items = new List<TModel>(users.Count);
        foreach (var user in users)
            items.Add(await MapToModelAsync(user));

        return new BaseQueryResult<TModel> { Items = items, TotalCount = totalCount };
    }

    public async Task<TModel?> GetByIdAsync(object id, IEnumerable<string>? select = null, Expression<Func<TModel, bool>>? scopeFilter = null, CancellationToken cancellationToken = default)
    {
        if (scopeFilter is not null)
            throw new NotSupportedException("Row-level scope filtering is not supported by the Identity-backed UserDataProvider.");

        var user = await UserManager.FindByIdAsync(id.ToString()!);
        return user is null ? null : await MapToModelAsync(user);
    }

    public async Task<int> GetCountAsync(Expression<Func<TModel, bool>>? scopeFilter = null, CancellationToken cancellationToken = default)
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
    public async Task<TModel> CreateAsync(TModel model, CancellationToken cancellationToken = default)
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

    public virtual async Task<TModel> PatchAsync(object id, Dictionary<string, object?> changedFields, string? concurrencyStamp = null, CancellationToken cancellationToken = default)
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
                    if (TryReadBoolean(value) is { } active)
                    {
                        var isDeactivating = user.IsActive && !active;
                        user.IsActive = active;

                        if (isDeactivating)
                            await UserManager.UpdateSecurityStampAsync(user);
                    }
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

                        // Kills every outstanding bearer token and cookie session for the account,
                        // not merely the ones a revocation table would have tracked.
                        await UserManager.UpdateSecurityStampAsync(user);

                        // The stamp rotation above already makes every outstanding refresh token
                        // unusable; revoking the rows too keeps the table from holding dead ones.
                        await DbContext.RefreshTokens
                            .Where(token => token.UserId == user.Id && !token.IsRevoked)
                            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.IsRevoked, true), cancellationToken);
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

    /// <summary>Maps an account to the model the list shows. Override to fill the extra columns of a model.</summary>
    protected virtual async Task<TModel> MapToModelAsync(TUser user)
    {
        var roles = await UserManager.GetRolesAsync(user);
        var model = new TModel
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            DisplayName = user.DisplayName,
            Role = roles.FirstOrDefault() ?? "User",
            IsActive = user.IsActive,
        };

        if (model is UserModel userModel)
            userModel.CreatedAt = user.CreatedAt;

        return model;
    }

    /// <summary>
    /// Orders the accounts by a column the provider does not know. Return <paramref name="ordered"/> unchanged to ignore
    /// the sort. Otherwise return <c>ordered.ThenBy…</c> when <paramref name="ordered"/> is not <see langword="null"/>,
    /// else <c>source.OrderBy…</c>.
    /// </summary>
    protected virtual IOrderedQueryable<TUser>? ApplySort(IQueryable<TUser> source, IOrderedQueryable<TUser>? ordered, SortDescriptor sort)
        => ordered;

    /// <summary>
    /// Starts the ordering with <c>OrderBy</c> or, when <paramref name="ordered"/> is set, extends it with <c>ThenBy</c>.
    /// </summary>
    private static IOrderedQueryable<TUser> OrderByOrThenBy<TKey>(IQueryable<TUser> source, IOrderedQueryable<TUser>? ordered, Expression<Func<TUser, TKey>> keySelector, SortDirection direction)
    {
        if (ordered is null)
            return direction == SortDirection.Ascending ? source.OrderBy(keySelector) : source.OrderByDescending(keySelector);

        return direction == SortDirection.Ascending ? ordered.ThenBy(keySelector) : ordered.ThenByDescending(keySelector);
    }

    /// <summary>
    /// Reads a boolean from a patched value, which arrives as a <see cref="JsonElement"/> when the patch came over HTTP
    /// and as a CLR value when a caller patches in process.
    /// </summary>
    private static bool? TryReadBoolean(object? value) => value switch
    {
        bool flag => flag,
        JsonElement { ValueKind: JsonValueKind.True } => true,
        JsonElement { ValueKind: JsonValueKind.False } => false,
        JsonElement { ValueKind: JsonValueKind.String } element when bool.TryParse(element.GetString(), out var parsed) => parsed,
        string text when bool.TryParse(text, out var parsed) => parsed,
        _ => null,
    };

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

/// <summary>The data provider of the framework <see cref="UserModel"/>.</summary>
public class UserDataProvider<TUser>(
    UserManager<TUser> userManager,
    IHttpContextAccessor httpContextAccessor,
    BaseUserDbContext<TUser> dbContext,
    IEnumerable<IUserDeletionHandler>? deletionHandlers = null,
    IStringLocalizer<BlazorBaseUserServerResources>? localizer = null)
    : UserDataProvider<TUser, UserModel>(userManager, httpContextAccessor, dbContext, deletionHandlers, localizer)
    where TUser : BaseUser, new();
