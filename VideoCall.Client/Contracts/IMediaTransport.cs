using VideoCall.Shared.Networking;

namespace VideoCall.Client.Contracts;

/// æÇÌåÉ (Interface) ãÓÄæáÉ Úä äŞá ÈíÇäÇÊ ÇáÕæÊ æÇáİíÏíæ ÚÈÑ ÈÑæÊæßæá UDP ÃËäÇÁ ÇáãßÇáãÉ.
/// íÊã ÊäİíĞåÇ áÇÍŞÇğ ÚÈÑ ÇáßáÇÓ <see cref="Services.UdpMediaClient"/>.
/// ãáÇÍÙÉ ãÚãÇÑíÉ: áã íÊã ÇÓÊÎÏÇã äãØ (Factory Pattern) åäÇ áÃä ÅäÔÇÁ ÇáÇÊÕÇá íÊØáÈ 
/// ŞíãÇğ ãÊÛíÑÉ æŞÊ ÇáÊÔÛíá (ãËá ÑãÒ ÇáÌáÓÉ æÇÓã ÇáãÓÊÎÏã)¡ æÊÌäÈÇğ ááÊÚŞíÏ ÛíÑ ÇáãÈÑÑ.
public interface IMediaTransport : IDisposable
{
    // ÃÍÏÇË (Events) ÊõØáŞ ÚäÏ ÇÓÊŞÈÇá ÈíÇäÇÊ ÇáÕæÊ¡ ÇáİíÏíæ¡ Ãæ ÚäÏ ÍÏæË ÎØÃ İí ÇáÔÈßÉ
    event Action<MediaPacket>? AudioPacketReceived;
    event Action<MediaPacket>? VideoPacketReceived;
    event Action<Exception>? TransportError;

    // ÈÏÁ ÊÔÛíá ÎÏãÉ ÇáäŞá
    void Start();

    // ÅÑÓÇá ÈíÇäÇÊ ÇáÕæÊ (PCM) ÈÔßá ÛíÑ ãÊÒÇãä
    Task SendAudioAsync(byte[] pcmChunk, CancellationToken ct = default);

    // ÅÑÓÇá ÅØÇÑÇÊ ÇáİíÏíæ ÇáãÔİÑÉ ÈÔßá ÛíÑ ãÊÒÇãä
    Task SendVideoFrameAsync(byte[] encodedFrame, CancellationToken ct = default);
}