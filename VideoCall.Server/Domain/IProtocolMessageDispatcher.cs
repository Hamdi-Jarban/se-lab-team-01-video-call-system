using VideoCall.Shared.Messages;

namespace VideoCall.Server.Domain;

/// <summary>
/// íÍæøá ÇáÑÓÇÆá ÇáæÇÑÏÉ ãä ÇáÚãíá Åáì ÇáÚãáíÇÊ ÇáãäÇÓÈÉ ÏÇÎá ÇáÊØÈíŞ¡
/// ãËá ÊÓÌíá ÇáÏÎæá æÅÏÇÑÉ ÇáãßÇáãÇÊ æÇáÛÑİ.
/// </summary>
public interface IProtocolMessageDispatcher
{
    /// <summary>
    /// íÚÇáÌ ÑÓÇáÉ æÇÑÏÉ ãä ÌáÓÉ ÇáÚãíá.
    /// </summary>
    /// <param name="session">ÌáÓÉ ÇáÚãíá ÇáãÑÓáÉ ááÑÓÇáÉ.</param>
    /// <param name="message">ÇáÑÓÇáÉ ÇáæÇÑÏÉ.</param>
    /// <param name="ct">ÑãÒ ÅáÛÇÁ ÇáÚãáíÉ.</param>
    Task DispatchAsync(
        IClientHandler session,
        Message message,
        CancellationToken ct);
}
