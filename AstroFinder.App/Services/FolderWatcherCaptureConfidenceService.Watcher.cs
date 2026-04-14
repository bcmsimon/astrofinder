using System.Runtime.Versioning;

namespace AstroFinder.App.Services;

public sealed partial class FolderWatcherCaptureConfidenceService
{
    private FileSystemWatcher? _watcher;

    private partial bool GetIsRunning() => _watcher?.EnableRaisingEvents == true;

    private partial void StartWatchingCore(string folderPath, string fileFilter)
    {
        var watcher = new FileSystemWatcher(folderPath, fileFilter)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size,
            EnableRaisingEvents = true,
        };

        watcher.Created += OnFileChanged;
        watcher.Changed += OnFileChanged;
        watcher.Renamed += OnFileRenamed;

        _watcher = watcher;
    }

    private partial void StopWatchingCore()
    {
        if (_watcher is null)
        {
            return;
        }

        _watcher.EnableRaisingEvents = false;
        _watcher.Created -= OnFileChanged;
        _watcher.Changed -= OnFileChanged;
        _watcher.Renamed -= OnFileRenamed;
        _watcher.Dispose();
        _watcher = null;
    }

    [SupportedOSPlatform("android")]
    [SupportedOSPlatform("maccatalyst")]
    [SupportedOSPlatform("windows")]
    private async void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        _ = await ProcessMetricsFileAsync(e.FullPath).ConfigureAwait(false);
    }

    [SupportedOSPlatform("android")]
    [SupportedOSPlatform("maccatalyst")]
    [SupportedOSPlatform("windows")]
    private async void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        _ = await ProcessMetricsFileAsync(e.FullPath).ConfigureAwait(false);
    }
}