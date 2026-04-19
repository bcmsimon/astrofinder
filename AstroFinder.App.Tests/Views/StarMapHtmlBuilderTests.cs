using AstroFinder.App.Views;

namespace AstroFinder.App.Tests.Views;

public class StarMapHtmlBuilderTests
{
    [Fact]
    public void BuildHtml_UsesResponsiveViewportSizing()
    {
        var html = StarMapHtmlBuilder.BuildHtml(CreateData());

        Assert.Contains("width=device-width", html);
        Assert.Contains("max-width: 1400px", html);
        Assert.Contains("width: 100%", html);
        Assert.Contains("height: auto", html);
        Assert.DoesNotContain("content=\"width=1400", html);
    }

    [Fact]
    public void BuildHtml_WhenObserverOrientationEnabled_RendersNorthPointerInsteadOfFixedCardinalLabels()
    {
        var html = StarMapHtmlBuilder.BuildHtml(CreateData(
            useObserverOrientation: true,
            displayRotationDegrees: 37.5,
            isTargetAboveHorizon: true));

        Assert.Contains("north-pointer", html);
        Assert.Contains(">N</text>", html);
        Assert.DoesNotContain("North is this way", html);
        Assert.DoesNotContain(">E</text>", html);
        Assert.DoesNotContain(">W</text>", html);
    }

    [Fact]
    public void BuildHtml_WhenTargetBelowHorizon_LabelsThatState()
    {
        var html = StarMapHtmlBuilder.BuildHtml(CreateData(
            useObserverOrientation: true,
            displayRotationDegrees: -14.0,
            isTargetAboveHorizon: false));

        Assert.Contains("Target below horizon", html);
    }

    private static StarMapData CreateData(
        bool useObserverOrientation = false,
        double displayRotationDegrees = 0.0,
        bool isTargetAboveHorizon = true) => new()
    {
        Title = "Orion",
        Target = new StarMapPoint(5.6, -5.4, 4.1, "M42"),
        AsterismName = "Orion Belt",
        AsterismStars =
        [
            new StarMapPoint(5.5, -1.2, 1.7, "Alnitak"),
            new StarMapPoint(5.6, -1.2, 1.7, "Alnilam"),
            new StarMapPoint(5.7, -1.2, 2.2, "Mintaka")
        ],
        AsterismSegments =
        [
            (0, 1),
            (1, 2)
        ],
        HopSteps =
        [
            new StarMapPoint(5.5, -1.2, 1.7, "Alnitak"),
            new StarMapPoint(5.55, -3.0, 2.8, "Hop 1")
        ],
        BackgroundStars =
        [
            new StarMapPoint(5.45, -2.5, 4.8, null),
            new StarMapPoint(5.75, -4.4, 5.1, null)
        ],
        MapFillStars =
        [
            new StarMapPoint(5.48, -3.1, 6.8, null)
        ],
        UseObserverOrientation = useObserverOrientation,
        DisplayRotationDegrees = displayRotationDegrees,
        IsTargetAboveHorizon = isTargetAboveHorizon
    };
}
