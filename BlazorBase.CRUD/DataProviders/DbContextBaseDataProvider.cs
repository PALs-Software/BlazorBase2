using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Events;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Navigation;
using BlazorBase.CRUD.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BlazorBase.CRUD.DataProviders;

/// <summary>
/// Data provider backed by EF Core DbContext with dynamic field projection,
/// PATCH-only updates with optimistic concurrency, and interceptor pipeline support.
/// </summary>
public class DbContextBaseDataProvider<TEntity>(
    DbContext dbContext,
    IEnumerable<IBaseDataInterceptor<TEntity>> interceptors,
    Func<IQueryable<TEntity>, IQueryable<TEntity>>? queryCustomizer = null
) : IBaseDataProvider<TEntity>
    where TEntity : class
{
    #region Injects
    private readonly DbContext DbContext = dbContext;
    private readonly IEnumerable<IBaseDataInterceptor<TEntity>> Interceptors = interceptors;
    private readonly Func<IQueryable<TEntity>, IQueryable<TEntity>>? QueryCustomizer = queryCustomizer;
    #endregion

    private static readonly ConcurrentDictionary<string, LambdaExpression> ProjectionCache = new();

    public async Task<BaseQueryResult<TEntity>> GetListAsync(BaseQuery query, Expression<Func<TEntity, bool>>? scopeFilter = null, CancellationToken cancellationToken = default)
    {
        var interceptedQuery = query;
        foreach (var interceptor in Interceptors)
            interceptedQuery = await interceptor.OnBeforeQueryAsync(interceptedQuery, cancellationToken);

        IQueryable<TEntity> dbQuery = DbContext.Set<TEntity>();

        if (QueryCustomizer is not null)
            dbQuery = QueryCustomizer(dbQuery);

        if (scopeFilter is not null)
            dbQuery = dbQuery.Where(scopeFilter);

        dbQuery = ApplyFilters(dbQuery, interceptedQuery.Filters);
        var totalCount = await dbQuery.CountAsync(cancellationToken);

        dbQuery = ApplySorts(dbQuery, interceptedQuery.Sorts);
        var effectiveTake = Math.Min(interceptedQuery.Take, BaseQueryLimits.MaxPageSize);
        dbQuery = dbQuery.Skip(interceptedQuery.Skip).Take(effectiveTake);

        if (interceptedQuery.Select is { Count: > 0 })
        {
            var entityType = DbContext.Model.FindEntityType(typeof(TEntity));
            var projection = GetOrCreateProjection(interceptedQuery.Select, interceptedQuery.NavigationFilters, entityType);
            var projected = dbQuery.AsNoTracking().Select((Expression<Func<TEntity, TEntity>>)projection);
            var items = await projected.ToListAsync(cancellationToken);
            return new BaseQueryResult<TEntity> { Items = items, TotalCount = totalCount };
        }

        var entities = await dbQuery.AsNoTracking().ToListAsync(cancellationToken);
        return new BaseQueryResult<TEntity> { Items = entities, TotalCount = totalCount };
    }

    public async Task<TEntity?> GetByIdAsync(object id, IEnumerable<string>? select = null, Expression<Func<TEntity, bool>>? scopeFilter = null, CancellationToken cancellationToken = default)
    {
        var selectList = select?.ToList();
        var keyPredicate = BuildKeyPredicate(id);

        if (selectList is { Count: > 0 })
        {
            IQueryable<TEntity> selectQuery = DbContext.Set<TEntity>();

            if (QueryCustomizer is not null)
                selectQuery = QueryCustomizer(selectQuery);

            if (scopeFilter is not null)
                selectQuery = selectQuery.Where(scopeFilter);

            var entityType = DbContext.Model.FindEntityType(typeof(TEntity));
            var projection = GetOrCreateProjection(selectList, navigationFilters: null, entityType);
            return await selectQuery.AsNoTracking().Where(keyPredicate).Select((Expression<Func<TEntity, TEntity>>)projection).FirstOrDefaultAsync(cancellationToken);
        }

        IQueryable<TEntity> dbQuery = DbContext.Set<TEntity>();

        if (QueryCustomizer is not null)
            dbQuery = QueryCustomizer(dbQuery);

        if (scopeFilter is not null)
            dbQuery = dbQuery.Where(scopeFilter);

        dbQuery = IncludeCollectionNavigations(dbQuery);

        return await dbQuery.Where(keyPredicate).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Eagerly loads every collection navigation of <typeparamref name="TEntity"/>.
    /// </summary>
    /// <remarks>
    /// Without this, a caller asking for a whole entity - no <c>select</c>, meaning "give me all of
    /// it" - got its scalar columns and empty collections, because neither <c>FindAsync</c> nor a
    /// plain <c>FirstOrDefault</c> loads navigations and BlazorBase deliberately runs without lazy
    /// loading. The visible symptom was a <c>ListPartField</c> on a <c>BaseCard</c> rendering
    /// "No items." for a record that demonstrably had children: <c>BaseList</c> opens the edit
    /// dialog through <c>GetByIdAsync(id)</c> with no select, so the very component whose purpose is
    /// editing children never received any. Nothing failed - the list was simply, silently empty.
    ///
    /// Only the no-select path does this. A caller that passes a select list has said exactly what
    /// it wants and gets the projection, and <see cref="GetListAsync"/> is left alone on purpose:
    /// pulling every child row for a 50-row page would be a performance trap, and a list has no
    /// list-part fields to fill.
    ///
    /// These are single-query includes, so an entity with several collections fetches their
    /// cartesian product. That is bounded - one entity, and the collections behind an editable
    /// list part are small by construction - and the alternative, <c>AsSplitQuery</c>, lives in
    /// <c>Microsoft.EntityFrameworkCore.Relational</c>, which this assembly deliberately does not
    /// reference so it stays provider-agnostic. An entity with several large collections should
    /// pass an explicit select and take only what it needs.
    /// </remarks>
    private IQueryable<TEntity> IncludeCollectionNavigations(IQueryable<TEntity> dbQuery)
    {
        var entityType = DbContext.Model.FindEntityType(typeof(TEntity));

        if (entityType is null)
            return dbQuery;

        var collectionNavigations = entityType.GetNavigations()
            .Where(navigation => navigation.IsCollection)
            .Select(navigation => navigation.Name)
            .ToList();

        if (collectionNavigations.Count == 0)
            return dbQuery;

        foreach (var navigationName in collectionNavigations)
            dbQuery = dbQuery.Include(navigationName);

        return dbQuery;
    }

    public async Task<int> GetCountAsync(Expression<Func<TEntity, bool>>? scopeFilter = null, CancellationToken cancellationToken = default)
    {
        IQueryable<TEntity> dbQuery = DbContext.Set<TEntity>();

        if (QueryCustomizer is not null)
            dbQuery = QueryCustomizer(dbQuery);

        if (scopeFilter is not null)
            dbQuery = dbQuery.Where(scopeFilter);

        return await dbQuery.CountAsync(cancellationToken);
    }

    public async Task<TEntity> CreateAsync(TEntity model, CancellationToken cancellationToken = default)
    {
        var interceptedModel = model;
        foreach (var interceptor in Interceptors)
            interceptedModel = await interceptor.OnBeforeCreateAsync(interceptedModel, cancellationToken);

        DbContext.Set<TEntity>().Add(interceptedModel);
        await DbContext.SaveChangesAsync(cancellationToken);

        foreach (var interceptor in Interceptors)
            await interceptor.OnAfterCreateAsync(interceptedModel, cancellationToken);

        return interceptedModel;
    }

    public async Task<TEntity> PatchAsync(object id, Dictionary<string, object?> changedFields, string? concurrencyStamp = null, CancellationToken cancellationToken = default)
    {
        var interceptedFields = changedFields;
        foreach (var interceptor in Interceptors)
            interceptedFields = await interceptor.OnBeforePatchAsync(id, interceptedFields, cancellationToken);

        var entity = await DbContext.Set<TEntity>().FindAsync([id], cancellationToken)
            ?? throw new InvalidOperationException($"Entity of type {typeof(TEntity).Name} with id '{id}' not found.");

        if (concurrencyStamp is not null)
            VerifyConcurrencyStamp(entity, concurrencyStamp);

        ApplyPatchFields(entity, interceptedFields);

        try
        {
            await DbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(
                $"The entity of type {typeof(TEntity).Name} with id '{id}' was modified by another user. Please reload and try again.");
        }

        foreach (var interceptor in Interceptors)
            await interceptor.OnAfterPatchAsync(entity, cancellationToken);

        return entity;
    }

    public async Task DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<TEntity>().FindAsync([id], cancellationToken)
            ?? throw new InvalidOperationException($"Entity of type {typeof(TEntity).Name} with id '{id}' not found.");

        foreach (var interceptor in Interceptors)
            await interceptor.OnBeforeDeleteAsync(id, entity, cancellationToken);

        DbContext.Set<TEntity>().Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        foreach (var interceptor in Interceptors)
            await interceptor.OnAfterDeleteAsync(id, cancellationToken);
    }

    #region Dynamic Projection

    private static LambdaExpression GetOrCreateProjection(List<string> selectFields, List<NavigationFilter>? navigationFilters, IEntityType? entityType)
    {
        // Collection navigations must be materialized per call so the projection honors the
        // navigation filters; only the (filter-independent) scalar/reference shape is cached.
        if (!ContainsCollectionNavigation(selectFields, entityType))
        {
            var key = string.Join(',', selectFields.Order(StringComparer.OrdinalIgnoreCase));
            return ProjectionCache.GetOrAdd(key, _ => BuildProjectionExpression(selectFields, navigationFilters: null, entityType));
        }

        return BuildProjectionExpression(selectFields, navigationFilters, entityType);
    }

    private static bool ContainsCollectionNavigation(List<string> selectFields, IEntityType? entityType)
    {
        if (entityType is null)
            return false;

        foreach (var fieldName in selectFields)
        {
            var rootName = fieldName.Contains('.') ? fieldName[..fieldName.IndexOf('.')] : fieldName;

            if (entityType.FindNavigation(rootName) is { IsCollection: true })
                return true;
        }

        return false;
    }

    private static LambdaExpression BuildProjectionExpression(List<string> selectFields, List<NavigationFilter>? navigationFilters, IEntityType? entityType)
    {
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var bindings = new List<MemberBinding>();
        var boundProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var navigationFilterMap = navigationFilters?.ToDictionary(filter => filter.NavigationName, StringComparer.OrdinalIgnoreCase);

        foreach (var fieldName in selectFields)
        {
            // For dotted paths like "ActivityType.Name", bind the navigation root "ActivityType"
            var rootName = fieldName.Contains('.') ? fieldName[..fieldName.IndexOf('.')] : fieldName;

            if (!boundProperties.Add(rootName))
                continue;

            var property = typeof(TEntity).GetProperty(rootName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

            if (property is null || CrudAccessResolver.IsAlwaysExcluded(typeof(TEntity), property.Name))
                continue;

            if (entityType?.FindNavigation(property.Name) is { IsCollection: true })
            {
                var collection = BuildCollectionProjection(parameter, property, navigationFilterMap);

                if (collection is not null)
                    bindings.Add(Expression.Bind(property, collection));

                continue;
            }

            bindings.Add(Expression.Bind(property, Expression.Property(parameter, property)));
        }

        var body = Expression.MemberInit(Expression.New(typeof(TEntity)), bindings);
        return Expression.Lambda<Func<TEntity, TEntity>>(body, parameter);
    }

    /// <summary>
    /// Materializes a collection navigation inside the entity projection as
    /// <c>e.Navigation.Where(...).OrderBy(...).ToList()</c>. Binding the raw navigation does not
    /// populate the collection under a Select projection, so it is rebuilt explicitly, optionally
    /// applying the supplied navigation filter.
    /// </summary>
    private static Expression? BuildCollectionProjection(ParameterExpression parameter, PropertyInfo property, Dictionary<string, NavigationFilter>? navigationFilterMap)
    {
        var elementType = property.PropertyType.IsGenericType
            ? property.PropertyType.GetGenericArguments()[0]
            : property.PropertyType.GetElementType();

        if (elementType is null)
            return null;

        Expression collection = Expression.Property(parameter, property);

        if (navigationFilterMap is not null && navigationFilterMap.TryGetValue(property.Name, out var navigationFilter))
        {
            collection = ApplyCollectionWhere(collection, elementType, navigationFilter);
            collection = ApplyCollectionOrder(collection, elementType, navigationFilter);
        }

        var toListMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.ToList))!.MakeGenericMethod(elementType);
        return Expression.Call(toListMethod, collection);
    }

    private static Expression ApplyCollectionWhere(Expression collection, Type elementType, NavigationFilter navigationFilter)
    {
        if (navigationFilter.Filters.Count == 0)
            return collection;

        var elementParameter = Expression.Parameter(elementType, "x");
        Expression? predicate = null;

        foreach (var filter in navigationFilter.Filters)
        {
            var filterExpression = BuildFilterTree(elementParameter, filter);

            if (filterExpression is null)
                continue;

            predicate = predicate is null ? filterExpression : Expression.AndAlso(predicate, filterExpression);
        }

        if (predicate is null)
            return collection;

        var whereMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == nameof(Enumerable.Where) && m.GetParameters().Length == 2
                && m.GetParameters()[1].ParameterType.GetGenericArguments().Length == 2)
            .MakeGenericMethod(elementType);

        return Expression.Call(whereMethod, collection, Expression.Lambda(predicate, elementParameter));
    }

    private static Expression ApplyCollectionOrder(Expression collection, Type elementType, NavigationFilter navigationFilter)
    {
        var isFirst = true;

        foreach (var sort in navigationFilter.Sorts)
        {
            var elementParameter = Expression.Parameter(elementType, "x");
            var sortProperty = BuildPropertyExpression(elementParameter, sort.PropertyName);

            if (sortProperty is null)
                continue;

            var methodName = isFirst
                ? sort.Direction == SortDirection.Descending ? "OrderByDescending" : "OrderBy"
                : sort.Direction == SortDirection.Descending ? "ThenByDescending" : "ThenBy";

            var orderMethod = typeof(Enumerable).GetMethods()
                .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(elementType, sortProperty.Type);

            collection = Expression.Call(orderMethod, collection, Expression.Lambda(sortProperty, elementParameter));
            isFirst = false;
        }

        return collection;
    }

    #endregion

    #region Concurrency

    private static void VerifyConcurrencyStamp(TEntity entity, string concurrencyStamp)
    {
        if (entity is AuditModel auditEntity)
        {
            if (!DateTime.TryParse(concurrencyStamp, out var stampDate))
                throw new ConcurrencyConflictException(
                    "Invalid concurrency stamp format. Please reload and try again.");

            if (auditEntity.ModifiedOn.HasValue && auditEntity.ModifiedOn.Value != stampDate)
                throw new ConcurrencyConflictException(
                    $"The entity was modified at {auditEntity.ModifiedOn.Value:O} but the concurrency stamp references {stampDate:O}. Please reload and try again.");

            if (!auditEntity.ModifiedOn.HasValue)
                throw new ConcurrencyConflictException(
                    "Concurrency conflict: entity has no modification timestamp. Please reload and try again.");

            return;
        }

        var concurrencyProperty = typeof(TEntity).GetProperties()
            .FirstOrDefault(p => p.GetCustomAttribute<ConcurrencyCheckAttribute>() is not null
                              || p.GetCustomAttribute<TimestampAttribute>() is not null);

        if (concurrencyProperty is null)
            return;

        var currentValue = concurrencyProperty.GetValue(entity)?.ToString();

        if (currentValue is not null && currentValue != concurrencyStamp)
            throw new ConcurrencyConflictException(
                $"Concurrency conflict on {typeof(TEntity).Name}. Please reload and try again.");
    }

    #endregion

    #region Patch Helpers

    private static void ApplyPatchFields(TEntity entity, Dictionary<string, object?> changedFields)
    {
        foreach (var (fieldName, value) in changedFields)
        {
            var property = typeof(TEntity).GetProperty(fieldName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

            if (property is null || !property.CanWrite)
                continue;

            if (CrudAccessResolver.IsAlwaysExcluded(typeof(TEntity), property.Name))
                continue;

            if (NavigationPropertyResolver.GetNavigationProperty(typeof(TEntity), property.Name) is not null)
                continue;

            if (typeof(AuditModel).GetProperty(property.Name, BindingFlags.Public | BindingFlags.Instance) is not null)
                continue;

            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            if (value is null)
            {
                property.SetValue(entity, null);
                continue;
            }

            if (value is JsonElement jsonElement)
            {
                var deserialized = JsonSerializer.Deserialize(jsonElement.GetRawText(), property.PropertyType);
                property.SetValue(entity, deserialized);
                continue;
            }

            try
            {
                var converted = Convert.ChangeType(value, targetType);
                property.SetValue(entity, converted);
            }
            catch
            {
                property.SetValue(entity, value);
            }
        }
    }

    private static PropertyInfo GetKeyProperty(DbContext dbContext)
    {
        var entityType = dbContext.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"Entity type {typeof(TEntity).Name} is not registered in the DbContext.");

        var key = entityType.FindPrimaryKey()
            ?? throw new InvalidOperationException($"Entity type {typeof(TEntity).Name} has no primary key.");

        return key.Properties[0].PropertyInfo
            ?? throw new InvalidOperationException($"Primary key of {typeof(TEntity).Name} has no backing property.");
    }

    private static object ConvertId(object id, Type targetType)
    {
        if (id.GetType() == targetType)
            return id;

        if (targetType == typeof(Guid) && id is string guidStr)
            return Guid.Parse(guidStr);

        return Convert.ChangeType(id, targetType);
    }

    private Expression<Func<TEntity, bool>> BuildKeyPredicate(object id)
    {
        var keyProperty = GetKeyProperty(DbContext);
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var keyAccess = Expression.Property(parameter, keyProperty);
        var idConstant = Expression.Constant(ConvertId(id, keyProperty.PropertyType));
        var equality = Expression.Equal(keyAccess, idConstant);
        return Expression.Lambda<Func<TEntity, bool>>(equality, parameter);
    }

    #endregion

    #region Query Building

    private static IQueryable<TEntity> ApplyFilters(IQueryable<TEntity> query, List<FilterDescriptor> filters)
    {
        foreach (var filter in filters)
        {
            var parameter = Expression.Parameter(typeof(TEntity), "e");
            var predicate = BuildFilterTree(parameter, filter);

            if (predicate is null)
                continue;

            var lambda = Expression.Lambda<Func<TEntity, bool>>(predicate, parameter);
            query = query.Where(lambda);
        }

        return query;
    }

    private static Expression? BuildFilterTree(ParameterExpression parameter, FilterDescriptor filter)
    {
        if (!filter.IsGroup)
        {
            if (string.IsNullOrEmpty(filter.PropertyName))
                return null;

            var property = BuildPropertyExpression(parameter, filter.PropertyName);

            if (property is null)
                return null;

            return BuildFilterExpression(parameter, property, filter);
        }

        Expression? combined = null;
        foreach (var child in filter.Filters!)
        {
            var childExpr = BuildFilterTree(parameter, child);

            if (childExpr is null)
                continue;

            if (combined is null)
                combined = childExpr;
            else
                combined = filter.Logic == FilterLogic.Or
                    ? Expression.OrElse(combined, childExpr)
                    : Expression.AndAlso(combined, childExpr);
        }

        return combined;
    }

    private static IQueryable<TEntity> ApplySorts(IQueryable<TEntity> query, List<SortDescriptor> sorts)
    {
        if (sorts.Count == 0)
            return query;

        IOrderedQueryable<TEntity>? orderedQuery = null;

        foreach (var sort in sorts)
        {
            var parameter = Expression.Parameter(typeof(TEntity), "e");
            var property = BuildPropertyExpression(parameter, sort.PropertyName);

            if (property is null)
                continue;

            var lambda = Expression.Lambda(property, parameter);

            var methodName = orderedQuery is null
                ? sort.Direction == SortDirection.Descending ? "OrderByDescending" : "OrderBy"
                : sort.Direction == SortDirection.Descending ? "ThenByDescending" : "ThenBy";

            var method = typeof(Queryable).GetMethods()
                .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(TEntity), property.Type);

            orderedQuery = (IOrderedQueryable<TEntity>)method.Invoke(null, [orderedQuery ?? query, lambda])!;
        }

        return orderedQuery ?? query;
    }

    private static MemberExpression? BuildPropertyExpression(ParameterExpression parameter, string propertyPath)
    {
        Expression current = parameter;
        foreach (var part in propertyPath.Split('.'))
        {
            var propertyInfo = current.Type.GetProperty(part, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

            if (propertyInfo is null)
                return null;

            current = Expression.Property(current, propertyInfo);
        }

        return current as MemberExpression;
    }

    private static Expression? BuildFilterExpression(ParameterExpression parameter, MemberExpression property, FilterDescriptor filter)
    {
        if (filter.Operator == FilterOperator.IsNull)
            return Expression.Equal(property, Expression.Constant(null));

        if (filter.Operator == FilterOperator.IsNotNull)
            return Expression.NotEqual(property, Expression.Constant(null));

        if (filter.Value is null)
            return null;

        var propertyType = Nullable.GetUnderlyingType(property.Type) ?? property.Type;

        if (filter.Operator == FilterOperator.In)
            return BuildInExpression(property, propertyType, filter.Value);

        object convertedValue;
        try
        {
            var coerced = Querying.FilterValueCoercion.Coerce(filter.Value, propertyType);

            if (coerced is null)
                return null;

            convertedValue = coerced;
        }
        catch
        {
            return null;
        }

        var constant = Expression.Constant(convertedValue, property.Type);

        return filter.Operator switch
        {
            FilterOperator.Equals => Expression.Equal(property, constant),
            FilterOperator.NotEquals => Expression.NotEqual(property, constant),
            FilterOperator.GreaterThan => Expression.GreaterThan(property, constant),
            FilterOperator.LessThan => Expression.LessThan(property, constant),
            FilterOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(property, constant),
            FilterOperator.LessThanOrEqual => Expression.LessThanOrEqual(property, constant),
            FilterOperator.Contains => BuildStringMethodCall(property, "Contains", constant),
            FilterOperator.StartsWith => BuildStringMethodCall(property, "StartsWith", constant),
            FilterOperator.EndsWith => BuildStringMethodCall(property, "EndsWith", constant),
            _ => null
        };
    }

    private static Expression? BuildInExpression(MemberExpression property, Type elementType, object rawValue)
    {
        try
        {
            var typedList = Querying.FilterValueCoercion.CoerceCollection(rawValue, elementType);

            if (typedList is null || typedList.Count == 0)
                return null;

            var containsMethod = typeof(Enumerable)
                .GetMethods()
                .First(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2)
                .MakeGenericMethod(elementType);

            var collectionConstant = Expression.Constant(typedList, typeof(IEnumerable<>).MakeGenericType(elementType));

            Expression propertyArg = property.Type == elementType
                ? property
                : Expression.Convert(property, elementType);

            return Expression.Call(containsMethod, collectionConstant, propertyArg);
        }
        catch
        {
            return null;
        }
    }

    private static Expression BuildStringMethodCall(MemberExpression property, string methodName, ConstantExpression value)
    {
        var method = typeof(string).GetMethod(methodName, [typeof(string)])!;
        return Expression.Call(property, method, value);
    }

    #endregion
}
