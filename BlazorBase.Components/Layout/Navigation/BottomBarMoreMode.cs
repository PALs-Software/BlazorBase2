namespace BlazorBase.Components.Layout.Navigation;

/// <summary>What the "More" tab of the phone bottom bar of <see cref="BaseLayout"/> reveals.</summary>
public enum BottomBarMoreMode
{
    /// <summary>The "More" tab opens the full navigation drawer, the same list the header button opens.</summary>
    Navigation,

    /// <summary>
    /// The "More" tab opens the bottom sheet of <see cref="BaseBottomNavigation"/>, which lists only the entries that
    /// did not become a tab. Without such entries the bar shows no "More" tab.
    /// </summary>
    Sheet,
}
