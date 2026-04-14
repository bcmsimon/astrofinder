namespace AstroFinder.App.Services;

public sealed partial class FolderWatcherCaptureConfidenceService
{
    private partial bool GetIsRunning() => false;

    private partial void StartWatchingCore(string folderPath, string fileFilter)
    {
        throw new PlatformNotSupportedException("FileSystemWatcher is not available on iOS. Use manual import or explicit file processing.");
    }

    private partial void StopWatchingCore()
    {
    }
}