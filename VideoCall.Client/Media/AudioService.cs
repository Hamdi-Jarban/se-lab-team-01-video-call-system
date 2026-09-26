using NAudio.Wave;

namespace VideoCall.Client.Media;

// ÎÏãÉ ÇáÊŞÇØ ÇáÕæÊ ÚÈÑ ÇáãíßÑæİæä ãÚ ÅãßÇäíÉ ÊØÈíŞ ÅáÛÇÁ ÇáÕÏì ÇáÕæÊí (AEC)
public sealed class AudioCaptureService : IDisposable
{
    public static readonly WaveFormat Format = new(16000, 16, 1); // ÊäÓíŞ ÇáÕæÊ ÇáŞíÇÓí (16 ßíáæåÑÊÒ¡ 16 ÈÊ¡ ŞäÇÉ ÃÍÇÏíÉ)
    private readonly EchoReferenceBuffer? _echoReference;
    private readonly AcousticEchoCanceller? _aec;
    private WaveInEvent? _input;
    private int _disposed;

    public bool IsMuted { get; set; } // ãÄÔÑ ßÊã ÇáÕæÊ ÇáÍÇáí
    public event Action<byte[]>? ChunkCaptured; // ÍÏË íØáŞ ÚäÏ ÇáÊŞÇØ ÍÒãÉ ÕæÊíÉ ÌÇåÒÉ ááÅÑÓÇá

    /// <param name="echoReference">
    /// ãÑÌÚ ÇáĞÇßÑÉ ÇáãÄŞÊÉ ãä ÎÏãÉ ÊÔÛíá ÇáÕæÊ (AudioPlaybackService)
    /// ÇáãÓÊÎÏã İí äİÓ ÇáãßÇáãÉ áÊØÈíŞ ÎæÇÑÒãíÉ ÅáÛÇÁ ÇáÕÏì.
    /// </param>
    public AudioCaptureService(EchoReferenceBuffer? echoReference = null)
    {
        _echoReference = echoReference;
        if (echoReference is not null)
            _aec = new AcousticEchoCanceller();
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
        if (_input is not null)
            return;

        _input = new WaveInEvent
        {
            WaveFormat = Format,
            BufferMilliseconds = 40
        };
        _input.DataAvailable += OnDataAvailable;
        _input.StartRecording();
    }

    // ãÚÇáÌÉ ÇáÈíÇäÇÊ ÇáÕæÊíÉ İæÑ ÊæİÑåÇ ãä ÇáãíßÑæİæä æÅáÛÇÁ ÇáÕÏì Åä æÌÏ
    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (IsMuted || e.BytesRecorded <= 0)
            return;
        var chunk = new byte[e.BytesRecorded];
        Buffer.BlockCopy(e.Buffer, 0, chunk, 0, e.BytesRecorded);

        // ÊØÈíŞ ÎæÇÑÒãíÉ ÅáÛÇÁ ÇáÕÏì ÇáÕæÊí (AEC) ÅĞÇ ßÇäÊ ãİÚáÉ æãÊæİÑÉ
        if (_aec is not null && _echoReference is not null)
            chunk = CancelEcho(chunk);

        ChunkCaptured?.Invoke(chunk);
    }

    // ÎæÇÑÒãíÉ ÅÒÇáÉ ÕÏì ÇáÕæÊ ÇáÕÇÏÑ ãä ÇáÓãÇÚÇÊ æÇáãõáÊŞØ ÚÈÑ ÇáãíßÑæİæä
    private byte[] CancelEcho(byte[] pcmBytes)
    {
        var sampleCount = pcmBytes.Length / 2;
        if (sampleCount == 0)
            return pcmBytes;

        var micSamples = new short[sampleCount];
        for (var i = 0; i < sampleCount; i++)
            micSamples[i] = (short)(pcmBytes[i * 2] | (pcmBytes[i * 2 + 1] << 8));

        var history = _echoReference!.ReadRecent(sampleCount + _aec!.FilterLength - 1);
        var cleaned = _aec.Process(micSamples, history);

        var result = new byte[pcmBytes.Length];
        for (var i = 0; i < sampleCount; i++)
        {
            result[i * 2] = (byte)(cleaned[i] & 0xFF);
            result[i * 2 + 1] = (byte)((cleaned[i] >> 8) & 0xFF);
        }
        return result;
    }

    public void Stop()
    {
        var input = Interlocked.Exchange(ref _input, null);
        if (input is null)
            return;
        input.DataAvailable -= OnDataAvailable;
        try
        { input.StopRecording(); }
        catch { }
        input.Dispose();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        Stop();
    }
}

// ÎÏãÉ ÊÔÛíá ÇáÕæÊ ÇáŞÇÏã ãä ÇáØÑİ ÇáÂÎÑ ÚÈÑ ÇáÓãÇÚÇÊ ãÚ ãÒÇãäÉ ãÑÌÚ ÇáÕÏì
public sealed class AudioPlaybackService : IDisposable
{
    private readonly WaveOutEvent _output = new();
    private readonly BufferedWaveProvider _buffer;
    private int _disposed;

    /// ÓÌá ÊÇÑíÎí ãÊÌÏÏ ááÕæÊ ÇáĞí íÊã ÊÔÛíáå ÚÈÑ ÇáÓãÇÚÇÊ ÍÇáíÇğ¡
    /// æíõãÑÑ áÎÏãÉ ÇáÇáÊŞÇØ (AudioCaptureService) áÅÒÇáÉ ÇáÕÏì.
    public EchoReferenceBuffer EchoReference { get; } = new(sampleRate: 16000, milliseconds: 500);

    public AudioPlaybackService()
    {
        _buffer = new BufferedWaveProvider(AudioCaptureService.Format)
        {
            BufferDuration = TimeSpan.FromMilliseconds(800),
            DiscardOnBufferOverflow = true
        };
        _output.Init(_buffer);
        _output.Play();
    }

    // ÅÖÇİÉ ÍÒãÉ ÕæÊíÉ ÌÏíÏÉ Åáì ØÇÈæÑ ÇáÊÔÛíá æÌÏæáÉ ßÊÇÈÊåÇ İí ãÑÌÚ ÇáÕÏì
    public void Enqueue(byte[] pcm)
    {
        if (Volatile.Read(ref _disposed) != 0 || pcm is null || pcm.Length == 0)
            return;

        // ÍÓÇÈ ÇáÊÃÎíÑ ÇáÒãäí ŞÈá ÎÑæÌ ÇáÕæÊ İÚáíÇğ ãä ÇáÓãÇÚÇÊ
        var playbackDelay = _buffer.BufferedDuration;

        _buffer.AddSamples(pcm, 0, pcm.Length);

        // ÌÏæáÉ ßÊÇÈÉ ÇáÈíÇäÇÊ İí ãÑÌÚ ÇáÕÏì ãÊÒÇãäÉ ÊãÇãÇğ ãÚ áÍÙÉ ÎÑæÌ ÇáÕæÊ ãä ÇáÓãÇÚÉ
        ScheduleEchoReferenceWrite(pcm, playbackDelay);
    }

    private void ScheduleEchoReferenceWrite(byte[] pcm, TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero)
        {
            EchoReference.Write(pcm);
            return;
        }

        _ = Task.Delay(delay).ContinueWith(_ =>
        {
            if (Volatile.Read(ref _disposed) == 0)
                EchoReference.Write(pcm);
        }, TaskScheduler.Default);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _output.Stop();
        _output.Dispose();
    }
}