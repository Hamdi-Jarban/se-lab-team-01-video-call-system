using OpenCvSharp;

namespace VideoCall.Client.Media;

// ÎÏãÉ ÇáÊŞÇØ ÇáİíÏíæ æãÚÇáÌÊå ÈÇÓÊÎÏÇã ãßÊÈÉ OpenCvSharp ÈÔßá Âãä æÛíÑ ãÊÒÇãä
public sealed class VideoCaptureService : IAsyncDisposable, IDisposable
{
    private const int TargetFps = 15; // ãÚÏá ÇáÅØÇÑÇÊ ÇáãÓÊåÏİ İí ÇáËÇäíÉ
    private const int JpegQuality = 60; // ÌæÏÉ ÖÛØ ÅØÇÑÇÊ JPEG ÇáãäŞæáÉ ÚÈÑ ÇáÔÈßÉ
    private readonly object _gate = new(); // Şİá ÃãÇä áÍãÇíÉ ÇáãæÇÑÏ ÇáãÔÊÑßÉ ÖÏ ÇáÊÏÇÎá Èíä ÇáÎíæØ
    private VideoCapture? _capture;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private int _disposed;

    public bool IsCameraOn { get; private set; } // ãÄÔÑ ÍÇáÉ ÊÔÛíá ÇáßÇãíÑÇ ÍÇáíÇğ
    public event Action<byte[], byte[]>? FrameCaptured; // ÍÏË íØáŞ ÚäÏ ÇáÊŞÇØ ÅØÇÑ ÌÏíÏ (íÚíÏ ãÕİæİÉ ÇáÈÇíÊÇÊ ÇáãÔİÑÉ æÇáãÚÇíäÉ)
    public event Action<Exception>? CaptureError; // ÍÏË íØáŞ ÚäÏ ÍÏæË ÎØÃ ÇÓÊËäÇÆí ÃËäÇÁ ÇáÊŞÇØ ÇáİíÏíæ

    public void Start()
    {
        // ÇáÊÃßÏ ãä Ãä ÇáÎÏãÉ áã íÊã ÇáÊÎáÕ ãäåÇ (Disposed) ãÓÈŞÇğ
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        lock (_gate)
        {
            if (_capture is not null)
                return;

            // İÊÍ ÇáßÇãíÑÇ ÇáÇİÊÑÇÖíÉ ááÌåÇÒ (ÇáİåÑÓ 0)
            var capture = new VideoCapture(0);
            if (!capture.IsOpened())
            {
                capture.Dispose();
                throw new InvalidOperationException("CAMERA_UNAVAILABLE");
            }
            _capture = capture;
            _cts = new CancellationTokenSource();
            IsCameraOn = true;

            // ÈÏÁ ÍáŞÉ ÇáÊŞÇØ ÇáÅØÇÑÇÊ ÇáÎáİíÉ ÈÔßá ÛíÑ ãÊÒÇãä
            _loop = CaptureLoopAsync(_cts.Token);
        }
    }

    public void SetCameraOn(bool enabled) => IsCameraOn = enabled;

    // ÍáŞÉ ÇáÊŞÇØ ÇáÅØÇÑÇÊ ÇáãÓÊãÑÉ İí ÇáÎáİíÉ ãÚ ÇáÊÍßã ÈãÚÏá ÇáÅØÇÑÇÊ (FPS)
    private async Task CaptureLoopAsync(CancellationToken ct)
    {
        using var frame = new Mat();
        var delay = TimeSpan.FromMilliseconds(1000.0 / TargetFps);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                VideoCapture? capture;
                lock (_gate)
                    capture = _capture;
                if (capture is null)
                    break;

                // ÅĞÇ Êã ÅíŞÇİ ÇáßÇãíÑÇ ãÄŞÊÇğ¡ íÊã ÇáÇäÊÙÇÑ ŞáíáÇğ æÊÎØí ÇáÅØÇÑ ÇáÍÇáí áÊæİíÑ ÇáãæÇÑÏ
                if (!IsCameraOn)
                {
                    await Task.Delay(100, ct).ConfigureAwait(false);
                    continue;
                }

                // ŞÑÇÁÉ ÇáÅØÇÑ ãä ÇáßÇãíÑÇ æÇáÊÍŞŞ ãä ÕÍÊå æÚÏã İÑÇÛå
                if (capture.Read(frame) && !frame.Empty())
                {
                    // ÊÔİíÑ ÇáÅØÇÑ ÈÕíÛÉ JPEG ááÅÑÓÇá ÇáÔÈßí ÇáãÖÛæØ
                    Cv2.ImEncode(".jpg", frame, out var encoded,
                        new ImageEncodingParam(ImwriteFlags.JpegQuality, JpegQuality));

                    // ÊÔİíÑ ÇáÅØÇÑ ÈÕíÛÉ BMP áÚÑÖ ãÚÇíäÉ ãÍáíÉ ÓÑíÚÉ æÏŞíŞÉ
                    Cv2.ImEncode(".bmp", frame, out var preview);

                    // ÅØáÇŞ ÇáÍÏË áÊãÑíÑ ÇáÈíÇäÇÊ ÇáãÚÇáÌÉ ááØÈŞÇÊ ÇáÃÎÑì
                    FrameCaptured?.Invoke(encoded, preview);
                }
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex) { CaptureError?.Invoke(ex); }
    }

    // ÅíŞÇİ ÚãáíÉ ÇáÊŞÇØ ÇáİíÏíæ ÈÃãÇä æÊİÑíÛ ÇáãæÇÑÏ ÇáãÑÊÈØÉ ÈÇáßÇãíÑÇ
    public async Task StopAsync()
    {
        CancellationTokenSource? cts;
        Task? loop;
        VideoCapture? capture;
        lock (_gate)
        {
            cts = _cts;
            loop = _loop;
            capture = _capture;
            _cts = null;
            _loop = null;
            _capture = null;
            IsCameraOn = false;
        }
        cts?.Cancel();
        if (loop is not null)
        {
            try
            { await loop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        capture?.Release();
        capture?.Dispose();
        cts?.Dispose();
    }

    // ÇáÊÎáÕ ÛíÑ ÇáãÊÒÇãä ÇáãæÌå ááãæÇÑÏ ÛíÑ ÇáãÏÇÑÉ
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        await StopAsync().ConfigureAwait(false);
    }

    // ÇáÊÎáÕ ÇáãÊÒÇãä (ÇáÊæÇİŞíÉ ÇáÊŞáíÏíÉ) ÈÇáÊÎáÕ ÛíÑ ÇáãÊÒÇãä
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
}