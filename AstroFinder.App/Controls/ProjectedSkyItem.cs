using AstroApps.Equipment.Profiles.Models;

namespace AstroFinder.App.Controls;

/// <summary>
/// A catalog object with its projected screen position for the current frame.
/// Screen position is normalised: (0,0) = top-left, (1,1) = bottom-right.
/// </summary>
public sealed class ProjectedSkyItem
{
    public string Label { get; init; } = string.Empty;
    public double Magnitude { get; init; }
    public bool IsStar { get; init; }
    public bool IsHighlighted { get; init; }
    public System.Drawing.PointF ScreenPosition { get; init; }
}
