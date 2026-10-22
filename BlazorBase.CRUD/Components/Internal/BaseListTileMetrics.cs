using BlazorBase.CRUD.Configuration;

namespace BlazorBase.CRUD.Components.Internal;

/// <summary>
/// The height of a tile of the smartphone list, worked out from what the tile shows.
/// </summary>
/// <remarks>
/// The tiles are virtualized, and ASP.NET's virtualizer places the tiles it does not render by the average height of the
/// ones it does: with tiles of different heights that average moves whenever a different set is on screen, and with
/// tens of thousands of tiles above the visible ones a change of a single pixel moves the content by thousands. Every
/// tile therefore has the same height, which is the sum of what the plan can put into it, and the stylesheet clamps the
/// parts (one line for the subtitle, the badges and each value, two for the title) to the room they were given. The values
/// below mirror BaseListCardView.razor.css: they are in rem, as the text is.
/// </remarks>
internal static class BaseListTileMetrics
{
    public const double BorderPixels = 2;
    public const double GapPixels = 8;
    public const double RootFontSizePixels = 16;
    public const double CustomBodyHeightRem = 7.375;

    private const double BodyPaddingRem = 1.5;
    private const double PartGapRem = 0.375;
    private const double TitleRem = 2.6;
    private const double SubtitleRem = 1.1375;
    private const double BadgeRowRem = 1.5;
    private const double FieldRem = 2.15625;
    private const double ActionRowRem = 3.5;
    private const double MoreButtonRem = 2.875;
    private const int FieldColumns = 2;

    /// <summary>
    /// The height of a tile for the parts of <paramref name="plan"/>, without its border, in rem.
    /// </summary>
    public static double Resolve<TModel>(MobileCardPlan<TModel> plan, bool hasActions)
    {
        var body = BodyPaddingRem + TitleRem;

        if (plan.Subtitle is not null)
            body += PartGapRem + SubtitleRem;

        if (plan.Badges.Count > 0)
            body += PartGapRem + BadgeRowRem;

        if (plan.Fields.Count > 0)
        {
            var rows = (plan.Fields.Count + FieldColumns - 1) / FieldColumns;
            body += PartGapRem + rows * FieldRem + (rows - 1) * PartGapRem;
        }

        return Math.Max(body, MoreButtonRem) + (hasActions ? ActionRowRem : 0);
    }

    /// <summary>
    /// The room a tile takes in the list in pixels: its height, its border and the gap below it.
    /// </summary>
    public static double ToSlotPixels(double tileHeightRem) => tileHeightRem * RootFontSizePixels + BorderPixels + GapPixels;
}
