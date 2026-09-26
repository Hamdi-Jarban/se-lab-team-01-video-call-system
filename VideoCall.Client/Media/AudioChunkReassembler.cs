using VideoCall.Shared.Networking;

namespace VideoCall.Client.Media;

// İÆÉ ãÓÄæáÉ Úä ÊÌãíÚ æÊÑßíÈ ÃÌÒÇÁ ÇáÍÒã ÇáÕæÊíÉ (Audio Fragments) ÇáŞÇÏãÉ ÚÈÑ ÔÈßÉ UDP áÅäÊÇÌ ãŞØÚ ÕæÊí ßÇãá
public sealed class AudioChunkReassembler
{
    private readonly object _gate = new(); // Şİá ÃãÇä áÍãÇíÉ ÇáÈíÇäÇÊ ÖÏ ÇáÊÏÇÎá Èíä ÇáÎíæØ ÃËäÇÁ ÇáÊÌãíÚ
    private uint? _sequence;
    private byte[]?[] _fragments = Array.Empty<byte[]?>();
    private int _received;

    // ŞÈæá ÍÒãÉ ÇáæÓÇÆØ¡ ÇáÊÍŞŞ ãä ÕÍÊåÇ¡ æÅÚÇÏÉ ÊÌãíÚ ÇáÃÌÒÇÁ ÚäÏ ÇßÊãÇáåÇ áÅÑÌÇÚ ãÕİæİÉ PCM ÇáÕæÊíÉ
    public byte[]? Accept(MediaPacket packet)
    {
        // ÇáÊÍŞŞ ãä Ãä ÇáÍÒãÉ ÊÎÕ ÇáÕæÊ æáíÓÊ İÇÑÛÉ ÇáÃÌÒÇÁ
        if (packet.MediaType != MediaType.Audio || packet.FragmentCount == 0)
            return null;
        lock (_gate)
        {
            // ÅĞÇ ÊÛíÑ ÑŞã ÇáÊÓáÓá Ãæ ÚÏÏ ÇáÃÌÒÇÁ¡ íÊã ÅÚÇÏÉ ÊåíÆÉ ÇáãÕİæİÉ áÊÊÈÚ ÇáãŞØÚ ÇáÕæÊí ÇáÌÏíÏ
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
            if (++_received != _fragments.Length)
                return null;

            // ÍÓÇÈ ÇáÍÌã ÇáÅÌãÇáí æÏãÌ ÇáÃÌÒÇÁ ÈÏÇÎá ãÕİæİÉ ÈÇíÊÇÊ æÇÍÏÉ ãÊÕáÉ
            var result = new byte[_fragments.Sum(x => x?.Length ?? 0)];
            var offset = 0;
            foreach (var fragment in _fragments)
            {
                if (fragment is null)
                    return null;
                Buffer.BlockCopy(fragment, 0, result, offset, fragment.Length);
                offset += fragment.Length;
            }

            // ÅÚÇÏÉ ÖÈØ ÇáĞÇßÑÉ ÇáãÄŞÊÉ æÅÑÌÇÚ ÇáãŞØÚ ÇáÕæÊí ÇáßÇãá ÇáÌÇåÒ ááÊÔÛíá
            _fragments = Array.Empty<byte[]?>();
            _received = 0;
            return result;
        }
    }
}