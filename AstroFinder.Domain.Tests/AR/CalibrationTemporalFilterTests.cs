using AstroFinder.Domain.AR.Calibration;

namespace AstroFinder.Domain.Tests.AR;

public class CalibrationTemporalFilterTests
{
    [Fact]
    public void TemporalFilter_SmoothsJitter_WithoutLargeLag()
    {
        var filter = new TemporalCalibrationFilter();

        var t1 = new SimilarityTransform2D(1.0, 0.0, 0.0, 0.0);
        var t2 = new SimilarityTransform2D(1.0, 0.0, 10.0, -5.0);

        _ = filter.Update(t1, 0.8);
        var after2 = filter.Update(t2, 0.8);

        // Smoothing should move toward target but not jump fully in one step.
        Assert.True(after2.Tx > 1.0 && after2.Tx < 10.0);
        Assert.True(after2.Ty < -0.5 && after2.Ty > -5.0);
    }

    [Fact]
    public void TemporalFilter_DecaysTowardIdentity_WhenNoMeasurements()
    {
        var filter = new TemporalCalibrationFilter();
        _ = filter.Update(new SimilarityTransform2D(1.02, 0.04, 14.0, -9.0), 0.9);

        var before = filter.GetBlendedTransform();
        for (var i = 0; i < 12; i++)
        {
            filter.DecayWithoutMeasurement();
        }

        var after = filter.GetBlendedTransform();

        Assert.True(Math.Abs(after.Tx) < Math.Abs(before.Tx));
        Assert.True(Math.Abs(after.Ty) < Math.Abs(before.Ty));
        Assert.True(Math.Abs(after.Scale - 1.0) < Math.Abs(before.Scale - 1.0));
    }
}
