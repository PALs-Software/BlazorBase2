using BlazorBase.Components.Routing;
using Xunit;

namespace BlazorBase.Components.Test.Routing;

/// <summary>
/// The reader carries the deep link that decides which item a list opens on, so the cases that matter
/// are the ones a hand-written URL actually produces - a missing value, a fragment, an encoded id.
/// </summary>
public class QueryStringReaderTests
{
    [Theory]
    [InlineData("/notes?id=42", "id", "42")]
    [InlineData("https://example.test/notes?id=42", "id", "42")]
    [InlineData("/notes?page=2&id=42", "id", "42")]
    [InlineData("/notes?id=42&id=7", "id", "42")]
    [InlineData("/notes?ID=42", "id", "42")]
    [InlineData("/notes?id=42#section", "id", "42")]
    [InlineData("/notes?id=a%20b%26c", "id", "a b&c")]
    [InlineData("/notes?id=", "id", "")]
    [InlineData("/notes?id", "id", "")]
    public void ReadsTheValue(string uri, string parameterName, string expected)
    {
        Assert.Equal(expected, QueryStringReader.TryReadValue(uri, parameterName));
    }

    [Theory]
    [InlineData("/notes", "id")]
    [InlineData("/notes?", "id")]
    [InlineData("/notes?page=2", "id")]
    [InlineData("/notes#id=42", "id")]
    [InlineData("", "id")]
    [InlineData("/notes?id=42", "")]
    public void ReturnsNull_WhenThereIsNothingToRead(string uri, string parameterName)
    {
        Assert.Null(QueryStringReader.TryReadValue(uri, parameterName));
    }
}
