using System.Globalization;
using System.Linq.Expressions;
using BlazorBase.CRUD.Attributes;
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// A list used to print the stored UTC wall time of a timestamp while the notification bell and the user's browser
/// used local time, so the same moment read two hours apart. A UTC instant is shown in local time; a date-only value
/// is left exactly as stored.
/// </summary>
[Collection(CustomPropertyResolutionCollection.Name)]
public class BaseListDateTimeDisplayTests : BunitTestContextBase
{
    private static readonly DateTime Stamp = new(2026, 10, 20, 18, 31, 27);

    [Fact]
    public void UnspecifiedAuditTimestamp_IsShownInLocalTime()
    {
        var property = typeof(TestOrder).GetProperty(nameof(TestOrder.CreatedOn))!;

        var shown = BaseListDateTimeDisplay.ToDisplayValue(property, Stamp);

        Assert.Equal(DateTime.SpecifyKind(Stamp, DateTimeKind.Utc).ToLocalTime(), shown);
    }

    [Fact]
    public void UtcKind_IsShownInLocalTime_ForAnyProperty()
    {
        var property = typeof(Appointment).GetProperty(nameof(Appointment.Plain))!;
        var utc = DateTime.SpecifyKind(Stamp, DateTimeKind.Utc);

        Assert.Equal(utc.ToLocalTime(), BaseListDateTimeDisplay.ToDisplayValue(property, utc));
    }

    [Fact]
    public void UnspecifiedValueOfAPlainProperty_IsShownAsStored()
    {
        var property = typeof(Appointment).GetProperty(nameof(Appointment.Plain))!;

        Assert.Equal(Stamp, BaseListDateTimeDisplay.ToDisplayValue(property, Stamp));
    }

    [Fact]
    public void UnspecifiedValueMarkedUtc_IsShownInLocalTime()
    {
        var property = typeof(Appointment).GetProperty(nameof(Appointment.PlacedOn))!;

        Assert.Equal(DateTime.SpecifyKind(Stamp, DateTimeKind.Utc).ToLocalTime(), BaseListDateTimeDisplay.ToDisplayValue(property, Stamp));
    }

    [Fact]
    public void LocalKind_IsShownAsIs()
    {
        var property = typeof(TestOrder).GetProperty(nameof(TestOrder.CreatedOn))!;
        var local = DateTime.SpecifyKind(Stamp, DateTimeKind.Local);

        Assert.Equal(local, BaseListDateTimeDisplay.ToDisplayValue(property, local));
    }

    [Fact]
    public void DateTimeOffset_IsShownInLocalTime()
    {
        var property = typeof(Appointment).GetProperty(nameof(Appointment.Offset))!;
        var offset = new DateTimeOffset(Stamp, TimeSpan.Zero);

        Assert.Equal(offset.ToLocalTime(), BaseListDateTimeDisplay.ToDisplayValue(property, offset));
    }

    [Fact]
    public void Format_WithoutExplicitFormat_ShowsDateAndShortTime_OrOnlyTheDateAtMidnight()
    {
        var culture = CultureInfo.GetCultureInfo("de-DE");

        Assert.Equal("20.10.2026 18:31", BaseListDateTimeDisplay.Format(Stamp, null, culture));
        Assert.Equal("01.10.2026", BaseListDateTimeDisplay.Format(new DateTime(2026, 10, 1), null, culture));
        Assert.Equal("2026-10-20", BaseListDateTimeDisplay.Format(Stamp, "yyyy-MM-dd", culture));
    }

    [Fact]
    public void ListCell_ShowsTheAuditTimestampInLocalTime_WithItsFullTextAsTooltip()
    {
        var order = new TestOrder { Id = Guid.NewGuid(), CustomerName = "Alpha", CreatedOn = Stamp };
        var cut = RenderList(order);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("tr.base-list-row:not([row-type]) td")));

        var local = DateTime.SpecifyKind(Stamp, DateTimeKind.Utc).ToLocalTime();
        var expected = BaseListDateTimeDisplay.Format(local, null, CultureInfo.CurrentCulture);
        var cell = cut.FindAll("tr.base-list-row:not([row-type]) td")[0];
        Assert.Equal(expected, cell.TextContent.Trim());
        Assert.Equal(expected, cell.GetAttribute("title"));
    }

    private IRenderedComponent<BaseList<TestOrder>> RenderList(TestOrder order)
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");
        Services.AddLocalization();

        var provider = Substitute.For<IBaseDataProvider<TestOrder>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestOrder, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new BaseQueryResult<TestOrder> { Items = [order], TotalCount = 1 });
        Services.AddSingleton(provider);

        RenderFragment columns = builder =>
        {
            builder.OpenComponent<BaseColumn<TestOrder>>(0);
            builder.AddComponentParameter(1, nameof(BaseColumn<TestOrder>.Property), (Expression<Func<TestOrder, object?>>)(item => item.CreatedOn));
            builder.CloseComponent();
        };

        return Render<BaseList<TestOrder>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.ChildContent, columns));
    }

    public class Appointment
    {
        public DateTime Plain { get; set; }

        [UtcDateTime]
        public DateTime PlacedOn { get; set; }

        public DateTimeOffset Offset { get; set; }
    }
}
