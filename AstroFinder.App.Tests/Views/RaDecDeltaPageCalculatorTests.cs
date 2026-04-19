using AstroFinder.App.Views;

namespace AstroFinder.App.Tests.Views;

public class RaDecDeltaPageCalculatorTests
{
    [Fact]
    public void CalculateTargetRaGraduation_WrapsPastTwelveHours()
    {
        var targetGraduation = RaDecDeltaPage.CalculateTargetRaGraduation(
            currentGraduation: 11.8,
            deltaRaDegrees: -18.0);

        Assert.Equal(1.0, targetGraduation, 8);
    }

    [Fact]
    public void CalculateTargetRaGraduation_WrapsNegativeValuesBackIntoDialRange()
    {
        var targetGraduation = RaDecDeltaPage.CalculateTargetRaGraduation(
            currentGraduation: 0.3,
            deltaRaDegrees: 24.0);

        Assert.Equal(10.7, targetGraduation, 8);
    }

    [Fact]
    public void CalculateRaDialDeltaHours_MatchesHourAngleMove()
    {
        var dialDeltaHours = RaDecDeltaPage.CalculateRaDialDeltaHours(deltaRaDegrees: -17.0);

        Assert.Equal(17.0 / 15.0, dialDeltaHours, 8);
    }

    [Theory]
    [InlineData("4.2", 4.2)]
    [InlineData("4,2", 4.2)]
    public void TryParseGraduation_AcceptsCommonDecimalFormats(string text, double expected)
    {
        var parsed = RaDecDeltaPage.TryParseGraduation(text, out var value);

        Assert.True(parsed);
        Assert.Equal(expected, value, 8);
    }
}