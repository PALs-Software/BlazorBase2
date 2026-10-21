using System.ComponentModel.DataAnnotations;
using BlazorBase.CRUD.Components;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// A column without an explicit width gets the floor its data needs: an e-mail address room for its whole text, a
/// yes/no column hardly any, so the equal split no longer cuts every address to the same prefix.
/// </summary>
public class BaseListColumnWidthTests
{
    [Theory]
    [InlineData(nameof(Row.IsActive), "6rem")]
    [InlineData(nameof(Row.Amount), "7rem")]
    [InlineData(nameof(Row.Count), "7rem")]
    [InlineData(nameof(Row.CreatedOn), "11rem")]
    [InlineData(nameof(Row.Kind), "11rem")]
    [InlineData(nameof(Row.UserPrincipalName), "16rem")]
    [InlineData(nameof(Row.Contact), "16rem")]
    [InlineData(nameof(Row.DisplayName), "12rem")]
    [InlineData(nameof(Row.Note), "16rem")]
    public void Floor_MatchesTheDataTheColumnHolds(string property, string expectedFloor)
    {
        var track = BaseListColumnWidth.Resolve(typeof(Row).GetProperty(property)!, "Title");

        Assert.StartsWith($"minmax(max({expectedFloor},", track);
    }

    [Fact]
    public void PlainText_KeepsTheOverridableFloor()
    {
        var track = BaseListColumnWidth.Resolve(typeof(Row).GetProperty(nameof(Row.Department))!, "Department");

        Assert.Contains("--base-list-column-min-width", track);
    }

    [Fact]
    public void EmailColumn_IsWiderThanAPlainTextColumn()
    {
        var email = BaseListColumnWidth.Resolve(typeof(Row).GetProperty(nameof(Row.UserPrincipalName))!, "Title");
        var plain = BaseListColumnWidth.Resolve(typeof(Row).GetProperty(nameof(Row.Department))!, "Title");

        Assert.NotEqual(email, plain);
    }

    [Fact]
    public void LongHeaderTitle_RaisesTheFloor_SoItFitsOnTwoLinesWithoutBeingCut()
    {
        var shortTitle = BaseListColumnWidth.Resolve(typeof(Row).GetProperty(nameof(Row.Count))!, "Count");
        var longTitle = BaseListColumnWidth.Resolve(typeof(Row).GetProperty(nameof(Row.Count))!, "Number of sessions created");

        Assert.Contains("calc(5ch + 3.5rem)", shortTitle);
        Assert.Contains("calc(14ch + 3.5rem)", longTitle);
    }

    public enum Kinds
    {
        One
    }

    public class Row
    {
        public bool IsActive { get; set; }

        public decimal Amount { get; set; }

        public int? Count { get; set; }

        public DateTime CreatedOn { get; set; }

        public Kinds Kind { get; set; }

        public string UserPrincipalName { get; set; } = string.Empty;

        [EmailAddress]
        public string Contact { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        [StringLength(500)]
        public string Note { get; set; } = string.Empty;

        public string Department { get; set; } = string.Empty;
    }
}
