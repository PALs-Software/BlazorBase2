using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace BlazorBase.CRUD.Test.Configuration;

/// <summary>
/// Which columns the tile of a list shows, and the part each plays: the automatic choice by data type, the roles
/// set on columns and on the entity, and the explicit tile configuration.
/// </summary>
public class MobileCardPlanResolverTests
{
    [Fact]
    public void Automatic_TakesTheFirstTextAsTitleAndTheSecondAsSubtitle()
    {
        var plan = Resolve(list => list
            .Column(e => e.Id)
            .Column(e => e.Name)
            .Column(e => e.Count)
            .Column(e => e.Email));

        Assert.Equal("Name", plan.Title?.PropertyName);
        Assert.Equal("Email", plan.Subtitle?.PropertyName);
        Assert.Equal(["Count"], Names(plan.Fields));
    }

    [Fact]
    public void Automatic_NeverShowsRawIdentifiers()
    {
        var plan = Resolve(list => list
            .Column(e => e.Id)
            .Column(e => e.TeamId)
            .Column(e => e.CustomerId)
            .Column(e => e.Name));

        Assert.Equal("Name", plan.Title?.PropertyName);
        Assert.Empty(plan.Fields);
        Assert.Empty(plan.Badges);
    }

    [Fact]
    public void Automatic_KeepsAnIdentifierColumnThatHasATemplate()
    {
        var plan = Resolve(list => list
            .Column(e => e.Name)
            .Column(e => e.TeamId));
        Assert.Empty(plan.Fields);

        var templated = Resolve(list => list
            .Column(e => e.Name)
            .Column(e => e.TeamId), column => column.Template = item => builder => { });

        Assert.Equal(["TeamId"], Names(templated.Fields));
    }

    [Fact]
    public void Automatic_TurnsAtMostTwoBooleansAndEnumsIntoBadges()
    {
        var plan = Resolve(list => list
            .Column(e => e.Name)
            .Column(e => e.IsActive)
            .Column(e => e.Kind)
            .Column(e => e.Count));

        Assert.Equal(["IsActive", "Kind"], Names(plan.Badges));
        Assert.Equal(["Count"], Names(plan.Fields));
    }

    [Fact]
    public void Automatic_RanksTextBeforeDatesBeforeNumbers_AndStopsAtFourFields()
    {
        var plan = Resolve(list => list
            .Column(e => e.Name)
            .Column(e => e.Count)
            .Column(e => e.Amount)
            .Column(e => e.CreatedOn)
            .Column(e => e.Email)
            .Column(e => e.Notes)
            .Column(e => e.LastSeenOn));

        Assert.Equal("Name", plan.Title?.PropertyName);
        Assert.Equal("Email", plan.Subtitle?.PropertyName);
        Assert.Equal(["Notes", "CreatedOn", "LastSeenOn", "Count"], Names(plan.Fields));
    }

    [Fact]
    public void Automatic_SkipsBinaryDataCollectionsAndUntemplatedObjects()
    {
        var plan = Resolve(list => list
            .Column(e => e.Name)
            .Column(e => e.Payload)
            .Column(e => e.Tags)
            .Column(e => e.Category));

        Assert.Empty(plan.Fields);
    }

    [Fact]
    public void Automatic_SkipsAColumnMarkedHidden()
    {
        var plan = Resolve(list => list
            .Column(e => e.Name)
            .Column(e => e.Email, column => column.Mobile(MobileRole.Hidden))
            .Column(e => e.Count));

        Assert.Null(plan.Subtitle);
        Assert.Equal(["Count"], Names(plan.Fields));
    }

    [Fact]
    public void Automatic_TreatsAnUnsortableTemplatedIdColumnAsTheActionRow()
    {
        var plan = Resolve(list => list
            .Column(e => e.Id, column => column.Sortable(false))
            .Column(e => e.Name), column =>
            {
                if (column.PropertyName == "Id")
                    column.Template = item => builder => { };
            });

        Assert.Equal(["Id"], Names(plan.ActionColumns));
        Assert.Equal("Name", plan.Title?.PropertyName);
        Assert.Empty(plan.Fields);
    }

    [Fact]
    public void Automatic_WithoutAnyColumn_HasNoTitle()
    {
        var plan = MobileCardPlanResolver.Resolve<MobileSampleEntity>([], null);

        Assert.Null(plan.Title);
        Assert.Empty(plan.Fields);
    }

    [Fact]
    public void ColumnRoles_ShowOnlyTheDeclaredColumns()
    {
        var plan = Resolve(list => list
            .Column(e => e.Name, column => column.Mobile(MobileRole.Title))
            .Column(e => e.Email)
            .Column(e => e.Count, column => column.Mobile(MobileRole.Field))
            .Column(e => e.IsActive, column => column.Mobile(MobileRole.Badge))
            .Column(e => e.Notes));

        Assert.Equal("Name", plan.Title?.PropertyName);
        Assert.Null(plan.Subtitle);
        Assert.Equal(["Count"], Names(plan.Fields));
        Assert.Equal(["IsActive"], Names(plan.Badges));
    }

    [Fact]
    public void ColumnRoles_AreOrderedByPriorityThenByColumnOrder()
    {
        var plan = Resolve(list => list
            .Column(e => e.Name, column => column.Mobile(MobileRole.Title))
            .Column(e => e.Count, column => column.Mobile(MobileRole.Field))
            .Column(e => e.Amount, column => column.Mobile(MobileRole.Field, 5))
            .Column(e => e.CreatedOn, column => column.Mobile(MobileRole.Field, 1)));

        Assert.Equal(["CreatedOn", "Amount", "Count"], Names(plan.Fields));
    }

    [Fact]
    public void ColumnRoles_WithoutATitle_FallBackToTheFirstUndecidedTextColumn()
    {
        var plan = Resolve(list => list
            .Column(e => e.Count, column => column.Mobile(MobileRole.Field))
            .Column(e => e.Email)
            .Column(e => e.Name));

        Assert.Equal("Email", plan.Title?.PropertyName);
        Assert.Equal(["Count"], Names(plan.Fields));
    }

    [Fact]
    public void ColumnRoles_AnActionsColumnDoesNotSwitchOffTheAutomaticChoice()
    {
        var plan = Resolve(list => list
            .Column(e => e.Name, column => column.Mobile(MobileRole.Actions))
            .Column(e => e.Email)
            .Column(e => e.Count), column =>
            {
                if (column.MobileRole == MobileRole.Actions)
                    column.Template = item => builder => { };
            });

        Assert.Equal(["Name"], Names(plan.ActionColumns));
        Assert.Equal("Email", plan.Title?.PropertyName);
        Assert.Equal(["Count"], Names(plan.Fields));
    }

    [Fact]
    public void ExtraTitlesAndSubtitles_BecomeFields()
    {
        var plan = Resolve(list => list
            .Column(e => e.Name, column => column.Mobile(MobileRole.Title, 1))
            .Column(e => e.Email, column => column.Mobile(MobileRole.Title, 2)));

        Assert.Equal("Name", plan.Title?.PropertyName);
        Assert.Equal(["Email"], Names(plan.Fields));
    }

    [Fact]
    public void Attribute_DeclaresRolesAndOrderOnTheEntity()
    {
        var columns = new BaseListBuilder<MobileAnnotatedEntity>()
            .Column(e => e.Id)
            .Column(e => e.Code)
            .Column(e => e.Name)
            .Column(e => e.CreatedOn)
            .Column(e => e.Amount)
            .Column(e => e.IsActive)
            .Column(e => e.Secret)
            .Build()
            .Columns;

        var plan = MobileCardPlanResolver.Resolve(columns, null);

        Assert.Equal("Name", plan.Title?.PropertyName);
        Assert.Equal(["Amount", "CreatedOn"], Names(plan.Fields));
        Assert.Equal(["IsActive"], Names(plan.Badges));
    }

    [Fact]
    public void ColumnRole_TakesPrecedenceOverTheAttribute()
    {
        var columns = new BaseListBuilder<MobileAnnotatedEntity>()
            .Column(e => e.Name, column => column.Mobile(MobileRole.Hidden))
            .Column(e => e.Code, column => column.Mobile(MobileRole.Title))
            .Column(e => e.Amount)
            .Build()
            .Columns;

        var plan = MobileCardPlanResolver.Resolve(columns, null);

        Assert.Equal("Code", plan.Title?.PropertyName);
        Assert.Equal(["Amount"], Names(plan.Fields));
    }

    [Fact]
    public void Configuration_TakesPrecedenceOverColumnRoles_AndKeepsTheDeclarationOrder()
    {
        var configuration = new MobileCardBuilder<MobileSampleEntity>()
            .Title(e => e.Email)
            .Subtitle(e => e.Name)
            .Field(e => e.CreatedOn)
            .Field(e => e.Count, "Anzahl")
            .Badge(e => e.IsActive)
            .Build();

        var plan = Resolve(list => list
            .Column(e => e.Name, column => column.Mobile(MobileRole.Title))
            .Column(e => e.Email)
            .Column(e => e.Count)
            .Column(e => e.CreatedOn)
            .Column(e => e.IsActive), configuration: configuration);

        Assert.Equal("Email", plan.Title?.PropertyName);
        Assert.Equal("Name", plan.Subtitle?.PropertyName);
        Assert.Equal(["CreatedOn", "Count"], Names(plan.Fields));
        Assert.Equal(["IsActive"], Names(plan.Badges));
        Assert.Equal("Anzahl", plan.LabelFor(plan.Fields[1]));
        Assert.Null(plan.LabelFor(plan.Fields[0]));
    }

    [Fact]
    public void Configuration_ReadsAPropertyTheListHasNoColumnFor()
    {
        var configuration = new MobileCardBuilder<MobileSampleEntity>()
            .Title(e => e.Name)
            .Field(e => e.Amount)
            .Build();

        var plan = Resolve(list => list.Column(e => e.Name), configuration: configuration);

        Assert.Equal(["Amount"], Names(plan.Fields));
        Assert.Equal(["Amount"], Names(plan.ExtraColumns));
    }

    [Fact]
    public void Configuration_DoesNotReadAPropertyTheUserMayNotSee()
    {
        var configuration = new MobileCardBuilder<MobileSampleEntity>()
            .Title(e => e.Name)
            .Field(e => e.Amount)
            .Build();

        var columns = new BaseListBuilder<MobileSampleEntity>().Column(e => e.Name).Build().Columns;
        var plan = MobileCardPlanResolver.Resolve(columns, configuration, column => column.PropertyName != "Amount");

        Assert.Empty(plan.Fields);
        Assert.Empty(plan.ExtraColumns);
    }

    [Fact]
    public void Configuration_Hide_RemovesAColumnTheAutomaticChoiceWouldTake()
    {
        var configuration = new MobileCardBuilder<MobileSampleEntity>()
            .Hide(e => e.Email)
            .Build();

        var plan = Resolve(list => list
            .Column(e => e.Name)
            .Column(e => e.Email)
            .Column(e => e.Count), configuration: configuration);

        Assert.Equal("Name", plan.Title?.PropertyName);
        Assert.Null(plan.Subtitle);
        Assert.Equal(["Count"], Names(plan.Fields));
    }

    [Fact]
    public void MaxFields_CapsAnExplicitDeclarationAndTheAutomaticChoice()
    {
        var explicitConfiguration = new MobileCardBuilder<MobileSampleEntity>()
            .Title(e => e.Name)
            .Field(e => e.Count)
            .Field(e => e.Amount)
            .Field(e => e.CreatedOn)
            .MaxFields(2)
            .Build();

        var explicitPlan = Resolve(list => list
            .Column(e => e.Name).Column(e => e.Count).Column(e => e.Amount).Column(e => e.CreatedOn), configuration: explicitConfiguration);
        Assert.Equal(["Count", "Amount"], Names(explicitPlan.Fields));

        var automaticPlan = Resolve(list => list
            .Column(e => e.Name).Column(e => e.Count).Column(e => e.Amount).Column(e => e.CreatedOn), configuration: new MobileCardConfiguration<MobileSampleEntity> { MaxFields = 1 });
        Assert.Single(automaticPlan.Fields);
    }

    [Fact]
    public void Explicit_WithoutAMaxFields_IsNotCapped()
    {
        var configuration = new MobileCardBuilder<MobileSampleEntity>()
            .Title(e => e.Name)
            .Field(e => e.Count).Field(e => e.Amount).Field(e => e.CreatedOn).Field(e => e.LastSeenOn).Field(e => e.Notes).Field(e => e.Email)
            .Build();

        var plan = Resolve(list => list
            .Column(e => e.Name).Column(e => e.Count).Column(e => e.Amount).Column(e => e.CreatedOn).Column(e => e.LastSeenOn).Column(e => e.Notes).Column(e => e.Email), configuration: configuration);

        Assert.Equal(6, plan.Fields.Count);
    }

    [Theory]
    [InlineData(nameof(MobileSampleEntity.Name), MobileValueKind.Text)]
    [InlineData(nameof(MobileSampleEntity.IsActive), MobileValueKind.Flag)]
    [InlineData(nameof(MobileSampleEntity.Kind), MobileValueKind.Enum)]
    [InlineData(nameof(MobileSampleEntity.CreatedOn), MobileValueKind.Date)]
    [InlineData(nameof(MobileSampleEntity.LastSeenOn), MobileValueKind.Date)]
    [InlineData(nameof(MobileSampleEntity.Amount), MobileValueKind.Number)]
    [InlineData(nameof(MobileSampleEntity.Id), MobileValueKind.Excluded)]
    [InlineData(nameof(MobileSampleEntity.CustomerId), MobileValueKind.Excluded)]
    [InlineData(nameof(MobileSampleEntity.Payload), MobileValueKind.Excluded)]
    public void Classify_NamesTheKindOfValue(string property, MobileValueKind expected)
    {
        var column = new BaseListBuilder<MobileSampleEntity>().Column(ColumnOf(property)).Build().Columns[0];

        Assert.Equal(expected, MobileCardPlanResolver.Classify(column));
    }

    private static System.Linq.Expressions.Expression<Func<MobileSampleEntity, object?>> ColumnOf(string property)
    {
        var parameter = System.Linq.Expressions.Expression.Parameter(typeof(MobileSampleEntity), "e");
        var member = System.Linq.Expressions.Expression.Convert(System.Linq.Expressions.Expression.Property(parameter, property), typeof(object));

        return System.Linq.Expressions.Expression.Lambda<Func<MobileSampleEntity, object?>>(member, parameter);
    }

    private static MobileCardPlan<MobileSampleEntity> Resolve(
        Action<BaseListBuilder<MobileSampleEntity>> configure,
        Action<PropertyColumnConfig<MobileSampleEntity>>? decorate = null,
        MobileCardConfiguration<MobileSampleEntity>? configuration = null)
    {
        var builder = new BaseListBuilder<MobileSampleEntity>();
        configure(builder);
        var columns = builder.Build().Columns;

        if (decorate is not null)
            foreach (var column in columns)
                decorate(column);

        return MobileCardPlanResolver.Resolve(columns, configuration);
    }

    private static List<string> Names<T>(IEnumerable<PropertyColumnConfig<T>> columns)
        => [.. columns.Select(column => column.PropertyName)];
}
