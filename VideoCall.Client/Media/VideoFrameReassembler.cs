using VideoCall.Shared.Networking;

namespace VideoCall.Client.Media;

// İÆÉ ãÓÄæáÉ Úä ÊÌãíÚ æÊÑßíÈ ÃÌÒÇÁ ÅØÇÑÇÊ ÇáİíÏíæ (Fragments) ÇáŞÇÏãÉ ÚÈÑ ÍÒã UDP áÅäÊÇÌ ÅØÇÑ ßÇãá
public sealed class VideoFrameReassembler
{
    private readonly object _gate = new(); // Şİá ÃãÇä áÍãÇíÉ ÇáÈíÇäÇÊ ÃËäÇÁ ÇáÊÌãíÚ ÇáãÊÒÇãä
    private uint? _sequence;
    private byte[]?[] _fragments = Array.Empty<byte[]?>();
    private int _received;

    // ÇÓÊŞÈÇá ÍÒã ÇáæÓÇÆØ æÇáÊÍŞŞ ãäåÇ æÅÚÇÏÉ ÏãÌ ÇáÃÌÒÇÁ ÚäÏ ÇßÊãÇáåÇ
    public byte[]? Accept(MediaPacket packet)
    {
        // ÇáÊÍŞŞ ãä Ãä ÇáÍÒãÉ ÊÎÕ ÇáİíÏíæ æáíÓÊ İÇÑÛÉ ÇáÃÌÒÇÁ
        if (packet.MediaType != MediaType.Video || packet.FragmentCount == 0)
            return null;
        lock (_gate)
        {
            // ÅĞÇ ÊÛíÑ ÑŞã ÇáÊÓáÓá (Sequence) Ãæ ÚÏÏ ÇáÃÌÒÇÁ¡ íÊã ÅÚÇÏÉ ÊåíÆÉ ÇáãÕİæİÉ áÊÊÈÚ ÇáÅØÇÑ ÇáÌÏíÏ
            if (_sequence != packet.SequenceNumber || _fragments.Length != packet.FragmentCount)
            {
                _sequence = packet.SequenceNumber;
                _fragments = new byte[packet.FragmentCount][];
                _received = 0;
            }

            // ÇáÊÍŞŞ ãä ÕÍÉ ÇáİåÑÓ Ãæ ÚÏã ÇÓÊŞÈÇá äİÓ ÇáÌÒÁ ãÓÈŞÇğ áÊÌäÈ ÇáÊßÑÇÑ
            if (packet.FragmentIndex >= _fragments.Length || _fragments[packet.FragmentIndex] is not null)
                return null;

            // ÊÎÒíä ÇáÌÒÁ ÇáÍÇáí æÒíÇÏÉ ÚÏÇÏ ÇáÃÌÒÇÁ ÇáãÓÊáãÉ
            _fragments[packet.FragmentIndex] = packet.Payload;
            _received++;

            // ÅĞÇ áã ÊßÊãá ÌãíÚ ÃÌÒÇÁ ÇáÅØÇÑ ÈÚÏ¡ íÊã ÇáÇäÊÙÇÑ æÅÑÌÇÚ null
            if (_received != _fragments.Length)
                return null;

            // ÍÓÇÈ ÇáÍÌã ÇáÅÌãÇáí ááÅØÇÑ ÇáßÇãá æÏãÌ ÇáÃÌÒÇÁ ÈÏÇÎá ãÕİæİÉ ÈÇíÊÇÊ æÇÍÏÉ ãÊÕáÉ
            var total = _fragments.Sum(x => x?.Length ?? 0);
            var frame = new byte[total];
            var offset = 0;
            foreach (var fragment in _fragments)
            {
                if (fragment is null)
                    return null;
                Buffer.BlockCopy(fragment, 0, frame, offset, fragment.Length);
                offset += fragment.Length;
            }

            // ÅÚÇÏÉ ÖÈØ ÇáĞÇßÑÉ ÇáãÄŞÊÉ æÅÑÌÇÚ ÇáÅØÇÑ ÇáãõÚÇÏ ÊÌãíÚå æÌÇåÒíÊå ááÚÑÖ
            _fragments = Array.Empty<byte[]?>();
            _received = 0;
            return frame;
        }
    }
}