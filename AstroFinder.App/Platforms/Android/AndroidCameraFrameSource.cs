using Android.Graphics;
using AstroFinder.Domain.AR.Calibration;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Graphics;

namespace AstroFinder.App.Services;

/// <summary>
/// Uses periodic low-resolution camera captures to feed the calibrated AR star detector.
/// This keeps the existing CameraView preview while providing real camera imagery for matching.
/// </summary>
public sealed class AndroidCameraFrameSource : IArFrameSource
{
    private static readonly GrayImageFrame EmptyFrame = new(0, 0, []);
    private readonly object _gate = new();
    private CameraView? _cameraView;
    private CancellationTokenSource? _captureLoopCts;
    private GrayImageFrame _latestFrame = EmptyFrame;
    private bool _isActive;

    public void Attach(CameraView cameraView)
    {
        ArgumentNullException.ThrowIfNull(cameraView);

        lock (_gate)
        {
            if (ReferenceEquals(_cameraView, cameraView))
            {
                return;
            }

            UnsubscribeLocked();
            _cameraView = cameraView;
            _cameraView.MediaCaptured += OnMediaCaptured;
            _cameraView.MediaCaptureFailed += OnMediaCaptureFailed;
        }

        _ = MainThread.InvokeOnMainThreadAsync(() =>
        {
            cameraView.ImageCaptureResolution = new Size(640, 480);
        });

        if (_isActive)
        {
            StartLoop();
        }
    }

    public void Detach()
    {
        StopLoop(clearFrame: true);

        lock (_gate)
        {
            UnsubscribeLocked();
            _cameraView = null;
        }
    }

    public void SetActive(bool isActive)
    {
        _isActive = isActive;

        if (isActive)
        {
            StartLoop();
        }
        else
        {
            StopLoop(clearFrame: true);
        }
    }

    public bool TryGetLatestFrame(out GrayImageFrame frame)
    {
        lock (_gate)
        {
            frame = _latestFrame;
            return frame.Width > 0 && frame.Height > 0;
        }
    }

    private void StartLoop()
    {
        lock (_gate)
        {
            if (_captureLoopCts is not null || _cameraView is null)
            {
                return;
            }

            _captureLoopCts = new CancellationTokenSource();
            var ct = _captureLoopCts.Token;
            _ = Task.Run(() => CaptureLoopAsync(ct), ct);
        }
    }

    private void StopLoop(bool clearFrame)
    {
        CancellationTokenSource? cts;

        lock (_gate)
        {
            cts = _captureLoopCts;
            _captureLoopCts = null;

            if (clearFrame)
            {
                _latestFrame = EmptyFrame;
            }
        }

        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
            cts.Dispose();
        }
        catch
        {
            // Ignore cancellation races during page teardown.
        }
    }

    private async Task CaptureLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            CameraView? cameraView;
            lock (_gate)
            {
                cameraView = _cameraView;
            }

            if (_isActive && cameraView is not null && cameraView.IsAvailable && !cameraView.IsCameraBusy)
            {
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(
                        () => cameraView.CaptureImage(cancellationToken));
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Opportunistic capture only; calibration falls back to sensor AR.
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(700), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void OnMediaCaptured(object? sender, MediaCapturedEventArgs e)
    {
        try
        {
            using var buffer = new MemoryStream();
            e.Media.CopyTo(buffer);
            var data = buffer.ToArray();

            using var bitmap = BitmapFactory.DecodeByteArray(data, 0, data.Length);
            if (bitmap is null)
            {
                return;
            }

            var frame = ConvertToGrayFrame(bitmap);
            lock (_gate)
            {
                _latestFrame = frame;
            }
        }
        catch
        {
            // Ignore malformed or transient capture failures.
        }
    }

    private void OnMediaCaptureFailed(object? sender, MediaCaptureFailedEventArgs e)
    {
        // Capture can fail transiently while preview is warming up; sensor AR remains primary.
    }

    private static GrayImageFrame ConvertToGrayFrame(Bitmap bitmap)
    {
        using var scaledBitmap = bitmap.Width > 960 || bitmap.Height > 960
            ? Bitmap.CreateScaledBitmap(bitmap, 960, Math.Max(1, (int)Math.Round(bitmap.Height * (960.0 / bitmap.Width))), filter: true)
            : null;

        var source = scaledBitmap ?? bitmap;
        var width = source.Width;
        var height = source.Height;
        var pixels = new int[width * height];
        source.GetPixels(pixels, 0, width, 0, 0, width, height);

        var grayPixels = new byte[pixels.Length];
        for (var i = 0; i < pixels.Length; i++)
        {
            var color = pixels[i];
            var r = (color >> 16) & 0xFF;
            var g = (color >> 8) & 0xFF;
            var b = color & 0xFF;
            grayPixels[i] = (byte)((77 * r + 150 * g + 29 * b) >> 8);
        }

        return new GrayImageFrame(width, height, grayPixels);
    }

    private void UnsubscribeLocked()
    {
        if (_cameraView is null)
        {
            return;
        }

        _cameraView.MediaCaptured -= OnMediaCaptured;
        _cameraView.MediaCaptureFailed -= OnMediaCaptureFailed;
    }
}