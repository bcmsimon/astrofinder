using System.Text.Json;
using AstroFinder.Domain.CaptureConfidence;
using AstroFinder.Engine.CaptureConfidence;

namespace AstroFinder.App.Services;

/// <summary>
/// Watches a folder for metrics files and feeds each parsed snapshot into
/// <see cref="CaptureConfidenceMonitor"/>.
/// </summary>
public sealed partial class FolderWatcherCaptureConfidenceService : IDisposable
{
    private readonly CaptureConfidenceMonitor _monitor;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _processingGate = new(1, 1);
    private readonly Dictionary<string, DateTimeOffset> _recentlyProcessed = new(StringComparer.OrdinalIgnoreCase);

    public FolderWatcherCaptureConfidenceService()
        : this(new CaptureConfidenceMonitor())
    {
    }

    public FolderWatcherCaptureConfidenceService(CaptureConfidenceMonitor monitor)
    {
        _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };
    }

    public bool IsRunning => GetIsRunning();

    public string? WatchedFolderPath { get; private set; }

    public event EventHandler<FrameQualitySnapshot>? SnapshotReceived;
    public event EventHandler<CaptureConfidenceAssessment>? AssessmentProduced;

    /// <summary>
    /// Starts watching <paramref name="folderPath"/> for metrics files.
    /// </summary>
    public void StartWatching(string folderPath, string fileFilter = "*.quality.json")
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ArgumentException("Folder path is required.", nameof(folderPath));
        }

        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Folder '{folderPath}' does not exist.");
        }

        StopWatching();

        StartWatchingCore(folderPath, fileFilter);
        WatchedFolderPath = folderPath;
    }

    /// <summary>
    /// Stops the active watcher and clears watcher-bound state.
    /// </summary>
    public void StopWatching()
    {
        StopWatchingCore();
        WatchedFolderPath = null;
        _recentlyProcessed.Clear();
    }

    /// <summary>
    /// Parses a metrics file and returns the resulting capture-confidence assessment,
    /// or <c>null</c> if the file is unreadable or invalid.
    /// </summary>
    public async Task<CaptureConfidenceAssessment?> ProcessMetricsFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        if (ShouldSkipAsDuplicate(filePath, TimeSpan.FromMilliseconds(350)))
        {
            return null;
        }

        await _processingGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshot = await TryReadSnapshotAsync(filePath, cancellationToken).ConfigureAwait(false);
            if (snapshot is null)
            {
                return null;
            }

            SnapshotReceived?.Invoke(this, snapshot);

            var assessment = _monitor.AddSnapshot(snapshot);
            AssessmentProduced?.Invoke(this, assessment);
            return assessment;
        }
        finally
        {
            _processingGate.Release();
        }
    }

    private bool ShouldSkipAsDuplicate(string filePath, TimeSpan minGap)
    {
        var now = DateTimeOffset.UtcNow;
        if (_recentlyProcessed.TryGetValue(filePath, out var lastSeen)
            && (now - lastSeen) < minGap)
        {
            return true;
        }

        _recentlyProcessed[filePath] = now;
        return false;
    }

    private async Task<FrameQualitySnapshot?> TryReadSnapshotAsync(string filePath, CancellationToken cancellationToken)
    {
        const int maxAttempts = 5;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var dto = await JsonSerializer.DeserializeAsync<FrameQualitySnapshotDto>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false);
                return dto?.ToSnapshot(filePath);
            }
            catch (IOException) when (attempt < maxAttempts - 1)
            {
                await Task.Delay(80, cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                return null;
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts - 1)
            {
                await Task.Delay(80, cancellationToken).ConfigureAwait(false);
            }
        }

        return null;
    }

    public void Dispose()
    {
        StopWatching();
        _processingGate.Dispose();
    }

    private partial bool GetIsRunning();

    private partial void StartWatchingCore(string folderPath, string fileFilter);

    private partial void StopWatchingCore();

    private sealed class FrameQualitySnapshotDto
    {
        public DateTimeOffset? CapturedAtUtc { get; init; }
        public int? StarCount { get; init; }
        public double? MedianHfrPixels { get; init; }
        public double? MedianFwhmPixels { get; init; }
        public double? MedianEccentricity { get; init; }
        public double? BackgroundLevel { get; init; }
        public double? FieldMatchConfidence { get; init; }
        public string? SourceId { get; init; }

        public FrameQualitySnapshot ToSnapshot(string filePath) => new()
        {
            CapturedAtUtc = CapturedAtUtc ?? File.GetLastWriteTimeUtc(filePath),
            StarCount = StarCount,
            MedianHfrPixels = MedianHfrPixels,
            MedianFwhmPixels = MedianFwhmPixels,
            MedianEccentricity = MedianEccentricity,
            BackgroundLevel = BackgroundLevel,
            FieldMatchConfidence = FieldMatchConfidence,
            SourceId = SourceId ?? Path.GetFileNameWithoutExtension(filePath),
        };
    }
}
