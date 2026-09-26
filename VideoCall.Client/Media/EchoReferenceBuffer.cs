namespace VideoCall.Client.Media;

/// ĞÇßÑÉ ãÄŞÊÉ ÏÇÆÑíÉ (Ring Buffer) áÊÎÒíä ÚíäÇÊ ÇáÕæÊ ÇáÕÇÏÑÉ ÍÏíËÇğ (Far-end)¡
/// ÊõÓÊÎÏã ßãÑÌÚ áÎæÇÑÒãíÉ ÅáÛÇÁ ÇáÕÏì áÊÊÈÚ ÇáÕæÊ ÇáÎÇÑÌ ãä ÇáÓãÇÚÇÊ.
public sealed class EchoReferenceBuffer
{
    private readonly object _gate = new(); // Şİá ÃãÇä áÍãÇíÉ ÇáÈíÇäÇÊ ÃËäÇÁ ÇáŞÑÇÁÉ æÇáßÊÇÈÉ ÇáãÊÒÇãäÉ
    private readonly short[] _buffer;
    private int _writePos;
    private int _count;

    public EchoReferenceBuffer(int sampleRate, int milliseconds)
    {
        _buffer = new short[Math.Max(1, sampleRate * milliseconds / 1000)];
    }
    /// <summary>ÅÖÇİÉ ÈÇíÊÇÊ PCM ÇáÕæÊíÉ ÇáÌÏíÏÉ (16-ÈÊ ÃÍÇÏí ÇáŞäÇÉ) Åáì ÇáĞÇßÑÉ ÇáÏÇÆÑíÉ.</summary>
    public void Write(byte[] pcmBytes)
    {
        if (pcmBytes is null || pcmBytes.Length < 2)
            return;
        var sampleCount = pcmBytes.Length / 2;
        lock (_gate)
        {
            for (var i = 0; i < sampleCount; i++)
            {
                var sample = (short)(pcmBytes[i * 2] | (pcmBytes[i * 2 + 1] << 8));
                _buffer[_writePos] = sample;
                _writePos = (_writePos + 1) % _buffer.Length;
                if (_count < _buffer.Length)
                    _count++;
            }
        }
    }

    /// ŞÑÇÁÉ ÃÍÏË ÇáÚíäÇÊ ÇáãØáæÈÉ ÈÊÑÊíÈ Òãäí (ÇáÃŞÏã İÇáÃÍÏË)¡
    /// ãÚ ÅÑÌÇÚ Şíã İÇÑÛÉ (ÕãÊ 0) ááãæÇÖÚ ÇáÊí áíÓ áåÇ ÊÇÑíÎ ÈÚÏ.
    public short[] ReadRecent(int count)
    {
        var result = new short[count];
        lock (_gate)
        {
            var available = Math.Min(count, Math.Min(_count, _buffer.Length));
            if (available <= 0)
                return result;
            var start = (_writePos - available + _buffer.Length) % _buffer.Length;
            for (var i = 0; i < available; i++)
                result[count - available + i] = _buffer[(start + i) % _buffer.Length];
        }
        return result;
    }
}