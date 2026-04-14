using AstroFinder.App.Services;
using AstroFinder.Domain.CaptureConfidence;
using AstroFinder.Engine.CaptureConfidence;

namespace AstroFinder.App.Tests.Services;

public sealed class FolderWatcherCaptureConfidenceServiceTests
{
    [Fact]
    public async Task ProcessMetricsFileAsync_ValidSequence_ProducesProblemAssessment()
    {
        using var temp = new TempDirectory();
        using var service = new FolderWatcherCaptureConfidenceService(
            new CaptureConfidenceMonitor(baselineFrameCount: 3, historyLimit: 8));

        await WriteMetricsFileAsync(temp.Path, "f1.quality.json", 100, 2.00, 0.18, 1200, 0.95, 0);
        await WriteMetricsFileAsync(temp.Path, "f2.quality.json", 98, 2.02, 0.19, 1190, 0.94, 1);
        await WriteMetricsFileAsync(temp.Path, "f3.quality.json", 101, 1.98, 0.18, 1210, 0.96, 2);
        await WriteMetricsFileAsync(temp.Path, "f4.quality.json", 43, 2.72, 0.40, 1710, 0.35, 3);

        var r1 = await service.ProcessMetricsFileAsync(Path.Combine(temp.Path, "f1.quality.json"));
        var r2 = await service.ProcessMetricsFileAsync(Path.Combine(temp.Path, "f2.quality.json"));
        var r3 = await service.ProcessMetricsFileAsync(Path.Combine(temp.Path, "f3.quality.json"));
        var r4 = await service.ProcessMetricsFileAsync(Path.Combine(temp.Path, "f4.quality.json"));

        Assert.NotNull(r1);
        Assert.NotNull(r2);
        Assert.NotNull(r3);
        Assert.NotNull(r4);

        Assert.Equal(CaptureConfidenceState.InsufficientData, r1!.State);
        Assert.Equal(CaptureConfidenceState.InsufficientData, r2!.State);
        Assert.Equal(CaptureConfidenceState.InsufficientData, r3!.State);

        Assert.Equal(CaptureConfidenceState.ProblemDetected, r4!.State);
        Assert.Contains(r4.Findings, f => f.IssueKind == CaptureConfidenceIssueKind.FieldMatchConfidenceLow);
        Assert.Contains(r4.Findings, f => f.IssueKind == CaptureConfidenceIssueKind.StarCountDrop);
    }

    [Fact]
    public async Task ProcessMetricsFileAsync_InvalidJson_ReturnsNull()
    {
        using var temp = new TempDirectory();
        using var service = new FolderWatcherCaptureConfidenceService();

        var path = Path.Combine(temp.Path, "broken.quality.json");
        await File.WriteAllTextAsync(path, "{ this is not valid json");

        var result = await service.ProcessMetricsFileAsync(path);

        Assert.Null(result);
    }

    [Fact]
    public async Task StartWatching_WhenFileAppears_RaisesAssessmentProduced()
    {
        using var temp = new TempDirectory();
        using var service = new FolderWatcherCaptureConfidenceService(
            new CaptureConfidenceMonitor(baselineFrameCount: 2, historyLimit: 6));

        var seenStates = new List<CaptureConfidenceState>();
        var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        service.AssessmentProduced += (_, assessment) =>
        {
            lock (seenStates)
            {
                seenStates.Add(assessment.State);
            }

            if (seenStates.Count >= 3)
            {
                signal.TrySetResult(true);
            }
        };

        service.StartWatching(temp.Path);

        await WriteMetricsFileAsync(temp.Path, "w1.quality.json", 100, 2.0, 0.18, 1200, 0.95, 0);
        await WriteMetricsFileAsync(temp.Path, "w2.quality.json", 99, 2.0, 0.19, 1205, 0.95, 1);
        await WriteMetricsFileAsync(temp.Path, "w3.quality.json", 48, 2.7, 0.39, 1700, 0.33, 2);

        var completed = await Task.WhenAny(signal.Task, Task.Delay(TimeSpan.FromSeconds(5))) == signal.Task;
        Assert.True(completed, "Expected watcher to publish assessments within timeout.");

        lock (seenStates)
        {
            Assert.Equal(CaptureConfidenceState.InsufficientData, seenStates[0]);
            Assert.Equal(CaptureConfidenceState.InsufficientData, seenStates[1]);
            Assert.Equal(CaptureConfidenceState.ProblemDetected, seenStates[2]);
        }
    }

    private static async Task WriteMetricsFileAsync(
        string folder,
        string fileName,
        int starCount,
        double hfr,
        double eccentricity,
        double background,
        double fieldMatch,
        int minuteOffset)
    {
        var capturedAt = new DateTimeOffset(2026, 4, 13, 21, minuteOffset, 0, TimeSpan.Zero);
        var json = $$"""
        {
          "capturedAtUtc": "{{capturedAt:O}}",
          "starCount": {{starCount}},
          "medianHfrPixels": {{hfr.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
          "medianEccentricity": {{eccentricity.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
          "backgroundLevel": {{background.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
          "fieldMatchConfidence": {{fieldMatch.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
          "sourceId": "folder-watcher"
        }
        """;

        await File.WriteAllTextAsync(Path.Combine(folder, fileName), json);
        await Task.Delay(120);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "astrofinder-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup failures in test teardown.
            }
        }
    }
}
