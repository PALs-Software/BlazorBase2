using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace BlazorBase.CRUD.Generators;

public sealed class LocationInfo(string filePath, TextSpan textSpan, LinePositionSpan lineSpan) : IEquatable<LocationInfo>
{
    public string FilePath { get; } = filePath;
    public TextSpan TextSpan { get; } = textSpan;
    public LinePositionSpan LineSpan { get; } = lineSpan;

    public static LocationInfo? CreateFrom(Location location)
    {
        if (location.SourceTree is null)
            return null;

        return new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
    }

    public Location ToLocation()
        => Location.Create(FilePath, TextSpan, LineSpan);

    public bool Equals(LocationInfo? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return FilePath == other.FilePath && TextSpan.Equals(other.TextSpan) && LineSpan.Equals(other.LineSpan);
    }

    public override bool Equals(object? obj)
        => Equals(obj as LocationInfo);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = FilePath.GetHashCode();
            hashCode = (hashCode * 397) ^ TextSpan.GetHashCode();
            hashCode = (hashCode * 397) ^ LineSpan.GetHashCode();
            return hashCode;
        }
    }
}
